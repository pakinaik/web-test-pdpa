using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebTestPdpa.Api.Chat;
using WebTestPdpa.Api.Llm;

namespace WebTestPdpa.Api.Tests.Integration;

/// <summary>🔍 M2/Minor 4: payload ออกไปถึง LLM แล้วต้องมี audit เสมอ แม้ request ถูก cancel หรือเกิด exception ที่ไม่คาดไว้</summary>
[Collection("pg")]
[Trait("Category", "Integration")]
public class LlmFailureAuditTests
{
    private const string Phone = "081-234-5678";
    private readonly PostgresFixture _pg;

    public LlmFailureAuditTests(PostgresFixture pg) => _pg = pg;

    /// <summary>🔄 gateway ปลอมที่โยน exception ตามที่กำหนด</summary>
    private sealed class ThrowingGateway(Exception ex) : ILlmGateway
    {
        public string Provider => "mock";
        public string Model => "mock-echo";
        public Task<LlmResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, CancellationToken ct) => throw ex;
    }

    private async Task<AuditSnapshot> SendWithGatewayThrowing(Exception ex)
    {
        await using var f = new ApiFactory(await _pg.CreateDatabaseAsync(),
            overrides: s =>
            {
                foreach (var x in s.Where(x => x.ServiceType == typeof(ILlmGateway)).ToList()) s.Remove(x);
                s.AddSingleton<ILlmGateway>(new ThrowingGateway(ex));
            });
        var c = await f.LoginEmployeeAsync("emp_fail");
        try
        {
            // ⚠️ server อาจตอบ 500 หรือโยน exception ผ่าน TestServer ก็ได้ — สิ่งที่ต้องตรวจคือ audit
            await c.PostAsJsonAsync("/api/chat/send", new { conversationId = (Guid?)null, text = $"เบอร์ {Phone}", customValues = Array.Empty<string>() });
        }
        catch (Exception)
        {
            // 🔍 ไม่สนใจ — ตรวจผลที่ DB
        }
        return await f.WithDb(async db =>
        {
            var a = await db.AuditLogs.SingleAsync();
            return new AuditSnapshot(a.Status, a.ErrorCode, a.OutboundPayload, a.PayloadSha256);
        });
    }

    private sealed record AuditSnapshot(string Status, string? ErrorCode, string? OutboundPayload, string? PayloadSha256);

    [Fact]
    public async Task ClientCancelled_StillWritesLlmErrorAudit_WithPayload()
    {
        var a = await SendWithGatewayThrowing(new OperationCanceledException());
        Assert.Equal("llm_error", a.Status);
        Assert.Equal("CLIENT_CANCELLED", a.ErrorCode);
        Assert.NotNull(a.OutboundPayload);
        Assert.NotNull(a.PayloadSha256);
        Assert.DoesNotContain(Phone, a.OutboundPayload);
    }

    [Fact]
    public async Task UnexpectedException_StillWritesLlmErrorAudit()
    {
        var a = await SendWithGatewayThrowing(new InvalidOperationException("boom"));
        Assert.Equal("llm_error", a.Status);
        Assert.Equal("UNEXPECTED:InvalidOperationException", a.ErrorCode);
        Assert.NotNull(a.OutboundPayload);
    }
}
