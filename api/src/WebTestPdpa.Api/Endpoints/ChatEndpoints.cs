using System.Security.Claims;
using WebTestPdpa.Api.Chat;
using WebTestPdpa.Api.Contracts;

namespace WebTestPdpa.Api.Endpoints;

public static class ChatEndpoints
{
    // 🔄 แปลง ChatException -> ProblemDetails + code
    private static IResult Map(ChatException ex) => ApiErrors.Problem(ex.Status, ex.Code, extra: ex.Extra);

    // 🔍 R7d (D40): role จาก JWT claim เท่านั้น (ใช้ตัดสินสิทธิ์ดูข้อมูลพนักงานฝั่ง server)
    private static List<string> Roles(ClaimsPrincipal user) => user.FindAll("role").Select(c => c.Value).ToList();

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/chat").RequireAuthorization().RequireRateLimiting("chat");

        g.MapPost("/preview", async (ChatRequest req, ClaimsPrincipal user, ChatService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.PreviewAsync(ConversationEndpoints.UserId(user), req, ct, Roles(user))); }
            catch (ChatException ex) { return Map(ex); }
        });

        g.MapPost("/send", async (ChatRequest req, ClaimsPrincipal user, ChatService svc, CancellationToken ct) =>
        {
            try
            {
                var name = user.FindFirstValue("name") ?? "";
                return Results.Ok(await svc.SendAsync(ConversationEndpoints.UserId(user), name, req, ct, Roles(user)));
            }
            catch (ChatException ex) { return Map(ex); }
        });
    }
}
