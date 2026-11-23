using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Admin;
using WebTestPdpa.Api.Chat;
using WebTestPdpa.Api.Contracts;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Endpoints;

public static class AdminEndpoints
{
    private static JsonElement? Json(string? s) =>
        string.IsNullOrEmpty(s) ? null : JsonSerializer.Deserialize<JsonElement>(s);

    private static object ToItem(AuditLog a) => new
    {
        id = a.Id, createdAt = a.CreatedAt, userName = a.UserName, conversationId = a.ConversationId,
        // 🔄 R8: ชนิดเหตุการณ์ (chat_send / report_create / report_download)
        action = a.Action,
        status = a.Status, provider = a.Provider, model = a.Model, maskCounts = Json(a.MaskCounts),
        maskedPayload = a.MaskedPayload, historyMessageCount = a.HistoryMessageCount, nerEnabled = a.NerEnabled,
        guardReasons = Json(a.GuardReasons), errorCode = a.ErrorCode, latencyMs = a.LatencyMs
    };

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // ⚠️ ทั้งกลุ่มเฉพาะ Admin
        var g = app.MapGroup("/api/admin").RequireAuthorization("Admin");

        g.MapGet("/users", async (UserManager<ApplicationUser> users) =>
        {
            var list = await users.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
            var result = new List<object>();
            foreach (var u in list)
            {
                var roles = await users.GetRolesAsync(u);
                result.Add(new
                {
                    id = u.Id, userName = u.UserName, displayName = AuthEndpoints.DisplayName(u), roles, isActive = u.IsActive,
                    firstNameTh = u.FirstNameTh, lastNameTh = u.LastNameTh, firstNameEn = u.FirstNameEn, lastNameEn = u.LastNameEn
                });
            }
            return Results.Ok(result);
        });

        g.MapPost("/users", async (CreateUserRequest req, UserManager<ApplicationUser> users, KnownNamesProvider names, TimeProvider time) =>
        {
            var role = req.Role ?? "Employee";
            if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrEmpty(req.Password) || !DbInitializer.Roles.Contains(role))
                return ApiErrors.Problem(StatusCodes.Status400BadRequest, "VALIDATION_ERROR");
            if (await users.FindByNameAsync(req.UserName) is not null)
                return ApiErrors.Problem(StatusCodes.Status409Conflict, "DUPLICATE_USER");

            var u = new ApplicationUser
            {
                UserName = req.UserName.Trim(),
                FirstNameTh = req.FirstNameTh, LastNameTh = req.LastNameTh,
                FirstNameEn = req.FirstNameEn, LastNameEn = req.LastNameEn,
                IsActive = true, CreatedAt = time.GetUtcNow()
            };
            var res = await users.CreateAsync(u, req.Password);
            if (!res.Succeeded)
            {
                // ⚠️ ส่งเฉพาะรหัส error ของ policy (ไม่สะท้อนรหัสผ่าน)
                return ApiErrors.Problem(StatusCodes.Status400BadRequest, "PASSWORD_POLICY",
                    extra: new Dictionary<string, object?> { ["errors"] = res.Errors.Select(e => e.Code).ToArray() });
            }
            await users.AddToRoleAsync(u, role);
            // 🧹 ชื่อใหม่ต้องเข้า known_names ทันที
            names.Invalidate();
            return Results.Created($"/api/admin/users/{u.Id}", new { id = u.Id, userName = u.UserName, roles = new[] { role } });
        });

        g.MapGet("/audit-logs", async (AppDbContext db, int? page, int? pageSize, Guid? userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct) =>
        {
            var p = Math.Max(page ?? 1, 1);
            var size = Math.Clamp(pageSize ?? 50, 1, 200);
            var q = db.AuditLogs.AsNoTracking().AsQueryable();
            if (userId is not null) q = q.Where(a => a.UserId == userId);
            if (from is not null) q = q.Where(a => a.CreatedAt >= from);
            if (to is not null) q = q.Where(a => a.CreatedAt <= to);
            var total = await q.CountAsync(ct);
            var rows = await q.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
            return Results.Ok(new { items = rows.Select(ToItem).ToList(), total });
        });

        g.MapGet("/audit-logs/{id:long}", async (long id, AppDbContext db, CancellationToken ct) =>
        {
            var a = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null) return ApiErrors.Problem(StatusCodes.Status404NotFound, "NOT_FOUND");
            var payload = a.OutboundPayload is null ? null : AuditPayload.Parse(a.OutboundPayload).Select(m => new { role = m.Role, content = m.Content }).ToList();
            var item = JsonSerializer.SerializeToElement(ToItem(a));
            var dict = item.EnumerateObject().ToDictionary(x => x.Name, x => (object?)x.Value);
            dict["outboundPayload"] = payload;
            dict["payloadSha256"] = a.PayloadSha256;
            // 🔍 R7 (D45): ใครถูกแนบข้อมูลเป็น context (employees.id + section เท่านั้น ไม่มีชื่อ/ค่า) — เฉพาะ detail
            dict["contextEmployeeIds"] = Json(a.ContextEmployeeIds);
            dict["contextSections"] = Json(a.ContextSections);
            return Results.Ok(dict);
        });

        g.MapGet("/pii-tests", async (AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.PiiTestCases.AsNoTracking().OrderBy(c => c.CaseKey).ToListAsync(ct);
            return Results.Ok(rows.Select(c => new
            {
                id = c.Id, caseKey = c.CaseKey, lang = c.Lang, category = c.Category, text = c.Text,
                knownNames = Json(c.KnownNames), expected = Json(c.Expected), note = c.Note
            }));
        });

        g.MapPost("/pii-tests/run", async (PiiBenchService bench, CancellationToken ct) =>
        {
            try { return Results.Ok(await bench.RunAsync(ct)); }
            catch (PiiUnavailableException) { return ApiErrors.Problem(StatusCodes.Status503ServiceUnavailable, "PII_UNAVAILABLE"); }
        });
    }
}
