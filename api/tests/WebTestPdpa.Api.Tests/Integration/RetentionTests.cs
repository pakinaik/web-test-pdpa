using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using WebTestPdpa.Api.Background;

namespace WebTestPdpa.Api.Tests.Integration;

[Collection("pg")]
[Trait("Category", "Integration")]
public class RetentionTests
{
    private readonly PostgresFixture _pg;
    public RetentionTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task RunOnce_DeletesOldMessagesVault_ThenAuditLater_KeepsRecentVault()
    {
        await using var f = new ApiFactory(await _pg.CreateDatabaseAsync());
        var c = await f.LoginEmployeeAsync("emp_ret");
        // 💾 conversation A: ถูกใช้งานล่าสุด; conversation B: เก่า
        var rA = await (await c.PostAsJsonAsync("/api/chat/send", new { text = "เบอร์ 081-234-5678" })).JsonAsync();
        await c.PostAsJsonAsync("/api/chat/send", new { text = "เบอร์ 089-999-1234" });
        var convA = rA.GetProperty("conversationId").GetGuid();
        var now = DateTimeOffset.UtcNow;

        // 🔄 ทำให้ conversation A "ยังใหม่" ที่เวลาจำลอง (+20 วัน) ทั้ง vault และ conversation
        await f.WithDb(async db =>
        {
            await db.PiiVault.Where(v => v.ConversationId == convA).ExecuteUpdateAsync(s => s.SetProperty(v => v.LastUsedAt, now.AddDays(20)));
            await db.Conversations.Where(x => x.Id == convA).ExecuteUpdateAsync(s => s.SetProperty(v => v.UpdatedAt, now.AddDays(20)));
            return 0;
        });

        var svc = f.Services.GetRequiredService<RetentionService>();
        await svc.RunOnceAsync(now.AddDays(31), default);
        var (msgs, vaultA, vaultAll, convs, audits) = await f.WithDb(async db => (
            await db.Messages.CountAsync(),
            await db.PiiVault.CountAsync(v => v.ConversationId == convA),
            await db.PiiVault.CountAsync(),
            await db.Conversations.CountAsync(),
            await db.AuditLogs.CountAsync()));
        Assert.Equal(0, msgs);          // 🔍 messages เก่าถูกลบ
        Assert.Equal(1, vaultA);        // 🔍 vault ที่ last_used ใหม่อยู่
        Assert.Equal(1, vaultAll);      // 🔍 vault ของ conversation อื่นที่เก่าถูกลบ
        Assert.Equal(1, convs);         // 🔍 เหลือเฉพาะ A
        Assert.Equal(2, audits);        // 🔍 audit ยังอยู่

        await svc.RunOnceAsync(now.AddDays(181), default);
        Assert.Equal(0, await f.WithDb(db => db.AuditLogs.CountAsync()));
    }

    [Fact]
    public async Task AuditRetentionLessThanRetention_FailsToStart()
    {
        await using var f = new ApiFactory(await _pg.CreateDatabaseAsync(),
            new() { ["RETENTION_DAYS"] = "30", ["AUDIT_RETENTION_DAYS"] = "5" });
        Assert.ThrowsAny<Exception>(() => f.CreateClient());
    }
}
