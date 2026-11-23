using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Background;
using Microsoft.Extensions.DependencyInjection;
using WebTestPdpa.Api.Reports;

namespace WebTestPdpa.Api.Tests.Integration;

[Collection("pg")]
[Trait("Category", "Integration")]
public class ReportFlowTests : IAsyncLifetime
{
    private const string Phone = "081-234-5678";
    private readonly PostgresFixture _pg;
    private ApiFactory _f = null!;
    private HttpClient _c = null!;

    public ReportFlowTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _f = new ApiFactory(await _pg.CreateDatabaseAsync());
        _c = await _f.LoginEmployeeAsync("emp_report");
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private Task<HttpResponseMessage> Send(string text, HttpClient? c = null) =>
        (c ?? _c).PostAsJsonAsync("/api/chat/send", new { conversationId = (Guid?)null, text, customValues = Array.Empty<string>() });

    [Fact]
    public async Task PdfRequest_CreatesEncryptedReport_OwnerCanDownload()
    {
        var r = await Send($"สรุปเบอร์ {Phone} ทำเป็น pdf ให้หน่อย");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.JsonAsync();
        var reportId = body.GetProperty("assistantMessage").GetProperty("reportId").GetGuid();
        var conv = body.GetProperty("conversationId").GetGuid();

        // 🔍 LLM ได้คำสั่ง PDF (system) และไม่มีค่าจริง
        var mock = _f.Llm;
        Assert.Contains(mock.LastMessages, m => m.Text == ReportIntent.Instruction);
        Assert.DoesNotContain(mock.LastMessages, m => (m.Text ?? "").Contains(Phone));

        // 🔍 DB เก็บแบบเข้ารหัส ไม่มี %PDF / ค่าจริงใน cipher
        var row = await _f.WithDb(db => db.ReportFiles.AsNoTracking().SingleAsync(x => x.Id == reportId));
        Assert.NotEqual("%PDF", Encoding.ASCII.GetString(row.Cipher, 12, 4));
        Assert.True(row.ExpiresAt > row.CreatedAt);

        // 📦 เจ้าของดาวน์โหลดได้ + no-store
        var dl = await _c.GetAsync($"/api/reports/{reportId}");
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
        Assert.Equal("application/pdf", dl.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", dl.Headers.CacheControl?.ToString() ?? "");
        var pdf = await dl.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        // 📦 history ส่ง reportId กลับมา
        var msgs = await (await _c.GetAsync($"/api/conversations/{conv}/messages")).JsonAsync();
        Assert.Equal(reportId, msgs[1].GetProperty("reportId").GetGuid());

        // 💾 audit create + download (ไม่มี payload)
        var audits = await _f.WithDb(db => db.AuditLogs.AsNoTracking()
            .Where(a => a.ConversationId == conv && a.Action.StartsWith("report_")).ToListAsync());
        Assert.Contains(audits, a => a.Action == "report_create");
        Assert.Contains(audits, a => a.Action == "report_download");
        Assert.All(audits, a => Assert.Null(a.OutboundPayload));
        Assert.Equal(1, (await _f.WithDb(db => db.ReportFiles.AsNoTracking().SingleAsync(x => x.Id == reportId))).DownloadCount);
    }

    [Fact]
    public async Task NoPdf_NoReport()
    {
        var body = await (await Send("สวัสดี")).JsonAsync();
        var am = body.GetProperty("assistantMessage");
        Assert.True(!am.TryGetProperty("reportId", out var v) || v.ValueKind == System.Text.Json.JsonValueKind.Null);
        Assert.Equal(0, await _f.WithDb(db => db.ReportFiles.CountAsync()));
    }

    [Fact]
    public async Task OtherUser_404_Expired_404_ThenRetentionDeletes()
    {
        var body = await (await Send("ขอเป็น PDF")).JsonAsync();
        var reportId = body.GetProperty("assistantMessage").GetProperty("reportId").GetGuid();

        var other = await _f.LoginEmployeeAsync("emp_report_other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/reports/{reportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _c.GetAsync($"/api/reports/{Guid.NewGuid()}")).StatusCode);

        // ⚠️ ทำให้หมดอายุ -> 404 และ retention ลบทิ้ง
        await _f.WithDb(db => db.ReportFiles.Where(x => x.Id == reportId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))));
        Assert.Equal(HttpStatusCode.NotFound, (await _c.GetAsync($"/api/reports/{reportId}")).StatusCode);

        await _f.Services.GetRequiredService<RetentionService>().RunOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(0, await _f.WithDb(db => db.ReportFiles.CountAsync()));
    }

    [Fact]
    public async Task Unauthenticated_401()
    {
        var anon = _f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/reports/{Guid.NewGuid()}")).StatusCode);
    }
}
