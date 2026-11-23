using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Chat;
using WebTestPdpa.Api.Llm;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Integration;

[Collection("pg")]
[Trait("Category", "Integration")]
public class ChatFlowTests : IAsyncLifetime
{
    private const string Phone = "081-234-5678";
    private const string Email = "somchai@example.com";
    private readonly PostgresFixture _pg;
    private ApiFactory _f = null!;
    private HttpClient _c = null!;

    public ChatFlowTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _f = new ApiFactory(await _pg.CreateDatabaseAsync());
        _c = await _f.LoginEmployeeAsync("emp_chat");
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private Task<HttpResponseMessage> Send(string text, Guid? conv = null, string[]? custom = null, HttpClient? c = null) =>
        (c ?? _c).PostAsJsonAsync("/api/chat/send", new { conversationId = conv, text, customValues = custom ?? [] });

    [Fact]
    public async Task Send_UnmasksResponse_ButStoresOnlyMasked()
    {
        var text = $"สมชาย ใจดี เบอร์ {Phone} อีเมล {Email} โดนหักเงิน";
        var r = await Send(text, custom: ["สมชาย ใจดี"]);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await r.JsonAsync();
        Assert.Contains(Phone, j.GetProperty("assistantMessage").GetProperty("content").GetString());
        Assert.Equal(text, j.GetProperty("userMessage").GetProperty("content").GetString());
        var masked = j.GetProperty("userMessage").GetProperty("maskedContent").GetString()!;
        foreach (var v in new[] { Phone, Email, "สมชาย" }) Assert.DoesNotContain(v, masked);
        Assert.Contains("[PHONE_1]", masked);

        // 🔍 DB ต้องไม่มีค่าจริงทั้งใน messages / audit / vault
        var dump = await _f.WithDb(async db =>
        {
            var m = string.Join("|", await db.Messages.Select(x => x.MaskedContent).ToListAsync());
            var a = await db.AuditLogs.SingleAsync();
            Assert.Equal("sent", a.Status);
            return m + "|" + a.MaskedPayload + "|" + a.OutboundPayload;
        });
        foreach (var v in new[] { Phone, Email, "สมชาย" }) Assert.DoesNotContain(v, dump);
        Assert.Contains("[PHONE_1]", dump);
    }

    [Fact]
    public async Task Audit_OutboundPayloadMatchesWhatLlmReceived_AndHashReproducible()
    {
        _f.Pii.NerEnabled = false;
        Guid conv;
        {
            var r1 = await Send($"ครั้งแรก เบอร์ {Phone}");
            conv = (await r1.JsonAsync()).GetProperty("conversationId").GetGuid();
        }
        await Send("ครั้งที่สอง ไม่มีข้อมูล", conv);
        var received = _f.Llm.LastMessages.Select(m => (m.Role.Value, m.Text)).ToList();

        var audit = await _f.WithDb(db => db.AuditLogs.OrderByDescending(a => a.Id).FirstAsync());
        var stored = AuditPayload.Parse(audit.OutboundPayload!);
        Assert.Equal(received, stored.Select(s => (s.Role, s.Content)).ToList());
        Assert.Equal("system", stored[0].Role);
        Assert.Equal(audit.PayloadSha256, AuditPayload.HashOfStored(audit.OutboundPayload!));
        Assert.DoesNotContain(Phone, audit.OutboundPayload);
        Assert.False(audit.NerEnabled);
        Assert.Equal(2, audit.HistoryMessageCount);
    }

    [Fact]
    public async Task HistorySentToLlmIsMasked_AndTokenConsistent()
    {
        var r1 = await Send($"เบอร์ {Phone}");
        var conv = (await r1.JsonAsync()).GetProperty("conversationId").GetGuid();
        var r2 = await Send("เบอร์เดิม 0812345678", conv);
        Assert.Contains("[PHONE_1]", (await r2.JsonAsync()).GetProperty("userMessage").GetProperty("maskedContent").GetString());
        foreach (var m in _f.Llm.LastMessages) Assert.DoesNotContain("081", m.Text);
        Assert.True(_f.Llm.LastMessages.Count >= 4);
    }

    [Fact]
    public async Task GuardBlock_Returns422_NoMessageNoVault_AuditPayloadsNull_ThenCustomSucceeds()
    {
        const string badId = "1101700234567";
        var r = await Send($"เลขคำร้อง {badId}");
        Assert.Equal((HttpStatusCode)422, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("GUARD_BLOCKED", body);
        Assert.Contains("THAI_ID_LIKE", body);

        var (msgs, vault, audit) = await _f.WithDb(async db =>
            (await db.Messages.CountAsync(), await db.PiiVault.CountAsync(), await db.AuditLogs.SingleAsync()));
        Assert.Equal(0, msgs);
        Assert.Equal(0, vault);
        Assert.Equal("blocked_guard", audit.Status);
        Assert.Null(audit.MaskedPayload);
        Assert.Null(audit.OutboundPayload);
        Assert.Contains("THAI_ID_LIKE", audit.GuardReasons);

        var ok = await Send($"เลขคำร้อง {badId}", custom: [badId]);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task PiiDown_Returns503_LlmNotCalled_NothingSaved()
    {
        _f.Pii.Throw = true;
        var before = _f.Llm.LastMessages.Count;
        var r = await Send($"เบอร์ {Phone}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Contains("PII_UNAVAILABLE", await r.Content.ReadAsStringAsync());
        Assert.Equal(before, _f.Llm.LastMessages.Count);
        var (msgs, convs, audit) = await _f.WithDb(async db =>
            (await db.Messages.CountAsync(), await db.Conversations.CountAsync(), await db.AuditLogs.SingleAsync()));
        Assert.Equal(0, msgs);
        Assert.Equal(0, convs);
        Assert.Equal("blocked_pii_unavailable", audit.Status);
        Assert.Null(audit.OutboundPayload);
    }

    [Fact]
    public async Task Title_DoesNotSplitSurrogatePair()
    {
        // 🔍 Minor 2: อักขระตำแหน่งที่ 39 เป็น high surrogate ของ emoji -> title ต้องไม่ลงท้ายด้วย surrogate เดี่ยว
        var text = new string('ก', 39) + "😀 ต่อท้าย";
        var r = await Send(text);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var title = await _f.WithDb(async db => (await db.Conversations.SingleAsync()).Title);
        Assert.False(char.IsHighSurrogate(title[^1]));
        Assert.Equal(39, title.Length);
    }

    [Fact]
    public async Task OtherUserGets404_OnMessages()
    {
        var r = await Send($"เบอร์ {Phone}");
        var conv = (await r.JsonAsync()).GetProperty("conversationId").GetGuid();
        var other = await _f.LoginEmployeeAsync("emp_other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/conversations/{conv}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("x", conv, c: other)).StatusCode);
        // 🔍 เจ้าของเห็นแบบ unmask
        var mine = await (await _c.GetAsync($"/api/conversations/{conv}/messages")).JsonAsync();
        Assert.Contains(Phone, mine[0].GetProperty("content").GetString());
        var list = await (await other.GetAsync("/api/conversations")).JsonAsync();
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task VaultCipherHasNoPlaintext()
    {
        await Send($"เบอร์ {Phone}");
        var cipher = await _f.WithDb(db => db.PiiVault.Select(v => v.ValueCipher).SingleAsync());
        Assert.False(cipher.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(Phone)) >= 0);
    }

    [Fact]
    public async Task Validation_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(new string('ก', 4001))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("x", custom: Enumerable.Repeat("a", 21).ToArray())).StatusCode);
    }

    [Fact]
    public async Task Preview_WritesNothing_AndShowsEntities()
    {
        var r = await _c.PostAsJsonAsync("/api/chat/preview", new { conversationId = (Guid?)null, text = $"เบอร์ {Phone}", customValues = new[] { "เบอร์" } });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await r.JsonAsync();
        Assert.True(j.GetProperty("guard").GetProperty("passed").GetBoolean());
        Assert.Equal(2, j.GetProperty("entities").GetArrayLength());
        var (v, m, c) = await _f.WithDb(async db => (await db.PiiVault.CountAsync(), await db.Messages.CountAsync(), await db.Conversations.CountAsync()));
        Assert.Equal((0, 0, 0), (v, m, c));
    }

    [Fact]
    public async Task Health_ReportsPii()
    {
        var j = await (await _f.CreateClient().GetAsync("/api/health")).JsonAsync();
        Assert.Equal("up", j.GetProperty("pii").GetString());
    }
}

[Collection("pg")]
[Trait("Category", "Integration")]
public class RateLimitTests
{
    private readonly PostgresFixture _pg;
    public RateLimitTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task ExceedingLimit_Returns429()
    {
        await using var f = new ApiFactory(await _pg.CreateDatabaseAsync(), new() { ["RATE_LIMIT_PER_MINUTE"] = "3" });
        var c = await f.LoginEmployeeAsync("emp_rl");
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/chat/send", new { text = "สวัสดี" })).StatusCode);
        var r = await c.PostAsJsonAsync("/api/chat/send", new { text = "สวัสดี" });
        Assert.Equal((HttpStatusCode)429, r.StatusCode);
        Assert.Contains("RATE_LIMITED", await r.Content.ReadAsStringAsync());
    }
}
