using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Contracts;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Reports;

namespace WebTestPdpa.Api.Endpoints;

public static class ConversationEndpoints
{
    public static Guid UserId(ClaimsPrincipal p) => Guid.TryParse(p.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    public static void MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/conversations").RequireAuthorization();

        // 📦 ของตัวเองเท่านั้น
        g.MapGet("", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var list = await db.Conversations.AsNoTracking()
                .Where(c => c.UserId == uid)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new ConversationDto(c.Id, c.Title, c.CreatedAt, c.UpdatedAt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        g.MapPost("", async (ClaimsPrincipal user, AppDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            var now = time.GetUtcNow();
            var c = new Conversation { UserId = UserId(user), Title = "", CreatedAt = now, UpdatedAt = now };
            db.Conversations.Add(c);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/conversations/{c.Id}", new ConversationDto(c.Id, c.Title, c.CreatedAt, c.UpdatedAt));
        });

        // ⚠️ 404 ถ้าไม่ใช่เจ้าของ (ไม่เปิดเผยว่ามีอยู่)
        g.MapGet("/{id:guid}/messages", async (Guid id, ClaimsPrincipal user, AppDbContext db, UnmaskService unmask, ReportService reports, CancellationToken ct) =>
        {
            var uid = UserId(user);
            if (!await db.Conversations.AsNoTracking().AnyAsync(c => c.Id == id && c.UserId == uid, ct))
                return ApiErrors.Problem(StatusCodes.Status404NotFound, "CONVERSATION_NOT_FOUND");

            var msgs = await db.Messages.AsNoTracking().Where(m => m.ConversationId == id).OrderBy(m => m.CreatedAt).ToListAsync(ct);
            var plain = await unmask.UnmaskManyAsync(id, msgs.Select(m => m.MaskedContent).ToList(), ct);
            // 📦 R8: รายงาน PDF ที่ยังไม่หมดอายุของ assistant message
            var reportMap = await reports.ActiveByMessageAsync(uid, msgs.Select(m => m.Id).ToList(), ct);
            var dto = msgs.Select((m, i) => new MessageDto(
                m.Id, m.Role, plain[i], m.MaskedContent,
                JsonSerializer.Deserialize<Dictionary<string, int>>(m.MaskCounts) ?? new(), m.CreatedAt,
                reportMap.TryGetValue(m.Id, out var rid) ? rid : null)).ToList();
            return Results.Ok(dto);
        });
    }
}
