using FluentValidation;

namespace UserService.API.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex) { await HandleAsync(ctx, ex); }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        logger.LogError(ex, "Unhandled: {Message}", ex.Message);

        var (status, title) = ex switch
        {
            ValidationException        => (400, "Validation failed"),
            UnauthorizedAccessException=> (401, "Unauthorised"),
            KeyNotFoundException        => (404, "Not found"),
            _                          => (500, "An unexpected error occurred")
        };

        ctx.Response.StatusCode  = status;
        ctx.Response.ContentType = "application/problem+json";

        var detail = ex is ValidationException ve
            ? string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))
            : ex.Message;

        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status   = status,
            Title    = title,
            Detail   = detail,
            Instance = ctx.Request.Path
        });
    }
}
