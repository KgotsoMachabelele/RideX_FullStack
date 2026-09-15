using System.Security.Claims;
using System.Text;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RideX.Contracts.Events;
using Serilog;

// ── Domain ────────────────────────────────────────────────────────────────────
public class Driver
{
    public Guid     Id            { get; private set; }
    public string   Email         { get; private set; } = string.Empty;
    public string   PasswordHash  { get; private set; } = string.Empty;
    public string   FirstName     { get; private set; } = string.Empty;
    public string   LastName      { get; private set; } = string.Empty;
    public string   LicensePlate  { get; private set; } = string.Empty;
    public string   VehicleModel  { get; private set; } = string.Empty;
    public bool     IsAvailable   { get; private set; }
    public bool     IsOnline      { get; private set; }
    public double?  CurrentLat    { get; private set; }
    public double?  CurrentLon    { get; private set; }
    public decimal  Rating        { get; private set; } = 5.0m;
    public int      TotalRides    { get; private set; }
    public decimal  TotalEarnings { get; private set; }
    public DateTime CreatedAt     { get; private set; }

    private Driver() { }

    public static Driver Create(string email, string hash,
        string firstName, string lastName, string plate, string model) => new()
    {
        Id           = Guid.NewGuid(),
        Email        = email.ToLowerInvariant(),
        PasswordHash = hash,
        FirstName    = firstName, LastName = lastName,
        LicensePlate = plate, VehicleModel = model,
        CreatedAt    = DateTime.UtcNow
    };

    public void GoOnline()  { IsOnline = true;  IsAvailable = true;  }
    public void GoOffline() { IsOnline = false; IsAvailable = false; }
    public void UpdateLocation(double lat, double lon) { CurrentLat = lat; CurrentLon = lon; }
    public void AssignToRide()  => IsAvailable = false;
    public void FreeFromRide()  => IsAvailable = IsOnline;

    public void RecordCompletedRide(decimal earnings)
    { TotalRides++; TotalEarnings += earnings; }
}

// ── EF Core ───────────────────────────────────────────────────────────────────
public class DriverDbContext(DbContextOptions<DriverDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Driver>(d =>
        {
            d.HasKey(x => x.Id);
            d.Property(x => x.Email).HasMaxLength(256).IsRequired();
            d.HasIndex(x => x.Email).IsUnique();
            d.Property(x => x.LicensePlate).HasMaxLength(20).IsRequired();
            d.HasIndex(x => x.LicensePlate).IsUnique();
            d.Property(x => x.Rating).HasPrecision(3, 2);
            d.Property(x => x.TotalEarnings).HasPrecision(12, 2);
            d.HasIndex(x => new { x.IsAvailable, x.IsOnline });
        });
    }
}

// ── Repository ────────────────────────────────────────────────────────────────
public class DriverRepository(DriverDbContext db)
{
    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Drivers.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Driver?> GetByEmailAsync(string email, CancellationToken ct = default)
        => db.Drivers.FirstOrDefaultAsync(d => d.Email == email.ToLowerInvariant(), ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => db.Drivers.AnyAsync(d => d.Email == email.ToLowerInvariant(), ct);

    public Task<List<Driver>> GetAvailableAsync(CancellationToken ct = default)
        => db.Drivers.Where(d => d.IsAvailable && d.IsOnline).ToListAsync(ct);

    public async Task AddAsync(Driver d, CancellationToken ct = default)
    { await db.Drivers.AddAsync(d, ct); await db.SaveChangesAsync(ct); }

    public async Task UpdateAsync(Driver d, CancellationToken ct = default)
    { db.Drivers.Update(d); await db.SaveChangesAsync(ct); }
}

// ── Password hasher ───────────────────────────────────────────────────────────
public interface IPasswordHasher { string Hash(string pw); bool Verify(string pw, string hash); }
public class BcryptHasher : IPasswordHasher
{
    public string Hash(string pw)               => BCrypt.Net.BCrypt.HashPassword(pw, 12);
    public bool   Verify(string pw, string hash)=> BCrypt.Net.BCrypt.Verify(pw, hash);
}

// ── Commands ──────────────────────────────────────────────────────────────────
public record RegisterDriverCommand(string Email, string Password,
    string FirstName, string LastName, string LicensePlate, string VehicleModel) : IRequest<Guid>;

public class RegisterDriverHandler(DriverRepository repo, IPasswordHasher hasher)
    : IRequestHandler<RegisterDriverCommand, Guid>
{
    public async Task<Guid> Handle(RegisterDriverCommand cmd, CancellationToken ct)
    {
        if (await repo.ExistsByEmailAsync(cmd.Email, ct))
            throw new InvalidOperationException("Email already registered.");
        var driver = Driver.Create(cmd.Email, hasher.Hash(cmd.Password),
            cmd.FirstName, cmd.LastName, cmd.LicensePlate, cmd.VehicleModel);
        await repo.AddAsync(driver, ct);
        return driver.Id;
    }
}

public record UpdateLocationCommand(Guid DriverId, double Lat, double Lon) : IRequest;

public class UpdateLocationHandler(DriverRepository repo)
    : IRequestHandler<UpdateLocationCommand>
{
    public async Task Handle(UpdateLocationCommand cmd, CancellationToken ct)
    {
        var driver = await repo.GetByIdAsync(cmd.DriverId, ct) ?? throw new KeyNotFoundException();
        driver.UpdateLocation(cmd.Lat, cmd.Lon);
        await repo.UpdateAsync(driver, ct);
    }
}

// ── MassTransit Consumers ─────────────────────────────────────────────────────
public class RideRequestedConsumer(DriverRepository repo, IPublishEndpoint bus,
    ILogger<RideRequestedConsumer> log) : IConsumer<RideRequestedMessage>
{
    public async Task Consume(ConsumeContext<RideRequestedMessage> ctx)
    {
        var msg       = ctx.Message;
        var available = await repo.GetAvailableAsync();

        var best = available
            .Where(d => d.CurrentLat.HasValue && d.CurrentLon.HasValue)
            .OrderBy(d => Haversine(d.CurrentLat!.Value, d.CurrentLon!.Value, msg.PickupLat, msg.PickupLon))
            .ThenByDescending(d => d.Rating)
            .FirstOrDefault();

        if (best is null) { log.LogWarning("No driver available for ride {RideId}", msg.RideId); return; }

        best.AssignToRide();
        await repo.UpdateAsync(best);
        await bus.Publish(new RideAcceptedMessage(msg.RideId, best.Id, msg.PickupLat, msg.PickupLon));
        log.LogInformation("Driver {DriverId} assigned to ride {RideId}", best.Id, msg.RideId);
    }

    private static double Haversine(double la1, double lo1, double la2, double lo2)
    {
        const double R = 6371;
        var dLat = (la2 - la1) * Math.PI / 180;
        var dLon = (lo2 - lo1) * Math.PI / 180;
        var a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public class RideCompletedConsumer(DriverRepository repo) : IConsumer<RideCompletedMessage>
{
    public async Task Consume(ConsumeContext<RideCompletedMessage> ctx)
    {
        var driver = await repo.GetByIdAsync(ctx.Message.DriverId);
        if (driver is null) return;
        driver.FreeFromRide();
        driver.RecordCompletedRide(ctx.Message.Fare);
        await repo.UpdateAsync(driver);
    }
}

// ── Controller ────────────────────────────────────────────────────────────────
[ApiController, Route("api/drivers")]
public class DriversController(IMediator mediator, DriverRepository repo, IPasswordHasher hasher,
    IConfiguration cfg) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDriverCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Created($"/api/drivers/{id}", new { id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var driver = await repo.GetByEmailAsync(req.Email, ct)
                     ?? throw new UnauthorizedAccessException("Invalid credentials.");
        if (!hasher.Verify(req.Password, driver.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(cfg["Jwt:ExpiryMinutes"]!));
        var claims = new[]
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, driver.Id.ToString()),
            new System.Security.Claims.Claim(ClaimTypes.Email, driver.Email),
            new System.Security.Claims.Claim(ClaimTypes.Role, "Driver"),
            new System.Security.Claims.Claim("firstName", driver.FirstName),
        };
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"], audience: cfg["Jwt:Audience"],
            claims: claims, expires: expiry, signingCredentials: creds);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { driverId = driver.Id, token = jwt, expiresAt = expiry });
    }

    [HttpPut("go-online")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GoOnline(CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException();
        driver.GoOnline();
        await repo.UpdateAsync(driver, ct);
        return NoContent();
    }

    [HttpPut("go-offline")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GoOffline(CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException();
        driver.GoOffline();
        await repo.UpdateAsync(driver, ct);
        return NoContent();
    }

    [HttpPut("location")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationCommand cmd, CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await mediator.Send(cmd with { DriverId = id }, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetProfile(Guid id, CancellationToken ct)
    {
        var d = await repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException();
        return Ok(new { d.Id, d.FirstName, d.LastName, d.LicensePlate,
            d.VehicleModel, d.Rating, d.TotalRides, d.TotalEarnings, d.IsOnline, d.IsAvailable });
    }
}

public record LoginRequest(string Email, string Password);

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<DriverDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<DriverRepository>();
builder.Services.AddScoped<IPasswordHasher, BcryptHasher>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RegisterDriverHandler).Assembly));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RideRequestedConsumer>();
    x.AddConsumer<RideCompletedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });
        cfg.ReceiveEndpoint("driver-service-queue", e =>
        {
            e.ConfigureConsumer<RideRequestedConsumer>(ctx);
            e.ConfigureConsumer<RideCompletedConsumer>(ctx);
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
builder.Services.AddHealthChecks().AddNpgsql(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Database.MigrateAsync();

app.UseSerilogRequestLogging();
app.UseCors("Angular");
app.UseSwagger(); app.UseSwaggerUI();
app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
