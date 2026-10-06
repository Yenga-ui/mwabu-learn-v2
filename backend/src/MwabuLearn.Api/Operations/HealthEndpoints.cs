using Microsoft.EntityFrameworkCore;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Api.Operations;

public static class HealthEndpoints
{
    public static void MapOperationalHealth(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
        app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
        app.MapGet("/health/ready", async (MwabuDbContext db, CancellationToken ct) =>
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                if (!await db.Database.CanConnectAsync(timeout.Token) || (await db.Database.GetPendingMigrationsAsync(timeout.Token)).Any())
                    return Results.Json(new { status = "unhealthy" }, statusCode: 503);
                return Results.Ok(new { status = "healthy" });
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return Results.Json(new { status = "unhealthy" }, statusCode: 503);
            }
        }).AllowAnonymous();
    }
}
