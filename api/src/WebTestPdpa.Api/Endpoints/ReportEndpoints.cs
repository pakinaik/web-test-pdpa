using System.Security.Claims;
using WebTestPdpa.Api.Contracts;
using WebTestPdpa.Api.Reports;

namespace WebTestPdpa.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports").RequireAuthorization();

        // ⚠️ เจ้าของเท่านั้น + ยังไม่หมดอายุ — อื่นๆ ตอบ 404 เหมือนกันหมด (ไม่เปิดเผยว่ามีอยู่)
        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, HttpContext http, ReportService reports, CancellationToken ct) =>
        {
            var name = user.FindFirstValue("name") ?? "";
            var pdf = await reports.DownloadAsync(id, ConversationEndpoints.UserId(user), name, ct);
            if (pdf is null) return ApiErrors.Problem(StatusCodes.Status404NotFound, "REPORT_NOT_FOUND");

            // ⚠️ ห้าม cache ไฟล์ที่มีข้อมูลส่วนบุคคล
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers.Pragma = "no-cache";
            return Results.File(pdf, "application/pdf", $"report-{id:N}.pdf");
        });
    }
}
