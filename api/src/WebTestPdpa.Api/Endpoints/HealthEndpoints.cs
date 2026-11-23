using Microsoft.Extensions.Caching.Memory;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // 📦 สถานะ pii + nerEnabled (cache 30 วิ) — frontend ใช้แสดง banner D24
        app.MapGet("/api/health", async (IPiiDetectionClient pii, IMemoryCache cache, CancellationToken ct) =>
        {
            var result = await cache.GetOrCreateAsync("health", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                var h = await pii.GetHealthAsync(ct);
                return new { status = "ok", pii = h is null ? "down" : "up", nerEnabled = h?.NerEnabled ?? true };
            });
            return Results.Ok(result);
        }).AllowAnonymous();
    }
}
