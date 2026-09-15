using System.Security.Claims;
using System.Text;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RideService.Domain;
using RideX.Contracts.Events;
using Serilog;

// ── EF Core DbContext ─────────────────────────────────────────────────────────
namespace RideService.Infrastructure;

public class RideDbContext(DbContextOptions<RideDbContext> options) : DbContext(options)
{
    public DbSet<Ride> Rides => Set<Ride>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Ride>(r =>
        {
            r.HasKey(x => x.Id);
            r.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            r.Property(x => x.Preference).HasMaxLength(50);
            r.Property(x => x.Fare).HasPrecision(10, 2);
            r.HasIndex(x => x.UserId);
            r.HasIndex(x => x.DriverId);
            r.HasIndex(x => x.Status);
        });
    }
}

// ── Repository ────────────────────────────────────────────────────────────────
public class RideRepository(RideDbContext db)
{
    public Task<Ride?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Rides.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<Ride>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => db.Rides.Where(r => r.UserId == userId)
                   .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Ride ride, CancellationToken ct = default)
    { await db.Rides.AddAsync(ride, ct); await db.SaveChangesAsync(ct); }

    public async Task UpdateAsync(Ride ride, CancellationToken ct = default)
    { db.Rides.Update(ride); await db.SaveChangesAsync(ct); }
}

// ── CQRS Commands ─────────────────────────────────────────────────────────────
namespace RideService.Application;

public record CreateRideCommand(Guid UserId, double PickupLat, double PickupLon,
    double DestLat, double DestLon, string Preference = "Economy") : IRequest<Guid>;

public class CreateRideHandler(RideRepository repo, IPublishEndpoint bus)
    : IRequestHandler<CreateRideCommand, Guid>
{
    public async Task<Guid> Handle(CreateRideCommand cmd, CancellationToken ct)
    {
        var ride = Ride.Create(cmd.UserId, cmd.PickupLat, cmd.PickupLon,
                               cmd.DestLat, cmd.DestLon, cmd.Preference);
        await repo.AddAsync(ride, ct);
        await bus.Publish(new RideRequestedMessage(ride.Id, ride.UserId,
            ride.PickupLat, ride.PickupLon, ride.DestLat, ride.DestLon,
            ride.Preference, ride.CreatedAt), ct);
        return ride.Id;
    }
}

public record CompleteRideCommand(Guid RideId) : IRequest<decimal>;

public class CompleteRideHandler(RideRepository repo, IPublishEndpoint bus)
    : IRequestHandler<CompleteRideCommand, decimal>
{
    private const decimal BaseFare      = 2.50m;
    private const decimal RatePerKm     = 1.20m;

    public async Task<decimal> Handle(CompleteRideCommand cmd, CancellationToken ct)
    {
        var ride = await repo.GetByIdAsync(cmd.RideId, ct)
                   ?? throw new KeyNotFoundException($"Ride {cmd.RideId} not found.");

        var distKm = Haversine(ride.PickupLat, ride.PickupLon, ride.DestLat, ride.DestLon);
        var fare   = Math.Round(BaseFare + (decimal)distKm * RatePerKm, 2);
        ride.Complete(fare);
        await repo.UpdateAsync(ride, ct);
        await bus.Publish(new RideCompletedMessage(ride.Id, ride.UserId,
            ride.DriverId!.Value, fare, ride.CompletedAt!.Value), ct);
        return fare;
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

// ── MassTransit Consumer ──────────────────────────────────────────────────────
public class RideAcceptedConsumer(RideRepository repo, ILogger<RideAcceptedConsumer> log)
    : IConsumer<RideAcceptedMessage>
{
    public async Task Consume(ConsumeContext<RideAcceptedMessage> ctx)
    {
        var ride = await repo.GetByIdAsync(ctx.Message.RideId);
        if (ride is null) return;
        ride.AcceptByDriver(ctx.Message.DriverId);
        await repo.UpdateAsync(ride);
        log.LogInformation("Ride {RideId} accepted by driver {DriverId}", ride.Id, ctx.Message.DriverId);
    }
}

// ── Controller ────────────────────────────────────────────────────────────────
namespace RideService.API;

[ApiController, Route("api/rides"), Authorize]
public class RidesController(IMediator mediator, RideRepository repo) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Create([FromBody] CreateRideCommand cmd, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var id = await mediator.Send(cmd with { UserId = userId }, ct);
        return Created($"/api/rides/{id}", new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var ride = await repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException();
        return Ok(ride);
    }

    [HttpGet("my-rides")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> MyRides(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await repo.GetByUserIdAsync(userId, ct));
    }

    [HttpPut("{id:guid}/start")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var ride = await repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException();
        ride.Start();
        await repo.UpdateAsync(ride, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/complete")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
        => Ok(new { fare = await mediator.Send(new CompleteRideCommand(id), ct) });

    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var ride = await repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException();
        ride.Cancel();
        await repo.UpdateAsync(ride, ct);
        return NoContent();
    }
}

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<RideService.Infrastructure.RideDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<RideRepository>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateRideHandler).Assembly));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RideAcceptedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });
        cfg.ReceiveEndpoint("ride-service-queue", e =>
        {
            e.ConfigureConsumer<RideAcceptedConsumer>(ctx);
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
    });
});

var jwtCfg = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwtCfg["Issuer"], ValidAudience = jwtCfg["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtCfg["Key"]!))
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddPolicy("Angular", p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<RideService.Infrastructure.RideDbContext>().Database.MigrateAsync();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors("Angular");
app.UseSwagger(); app.UseSwaggerUI();
app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

// Inline middleware (reused pattern)
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            log.LogError(ex, ex.Message);
            var (s, t) = ex switch
            {
                KeyNotFoundException        => (404, "Not found"),
                UnauthorizedAccessException => (401, "Unauthorised"),
                InvalidOperationException   => (409, "Conflict"),
                _ => (500, "Internal error")
            };
            ctx.Response.StatusCode  = s;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = s, Title = t, Detail = ex.Message });
        }
    }
}
