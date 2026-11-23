using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Integration;

/// <summary>⚠️ R7c (F23): แถว vault ที่ type ไม่รู้จักต้องไม่ถูก unmask เป็น type อื่น (เดิมตกเป็น Person เงียบๆ)</summary>
[Collection("pg")]
[Trait("Category", "Integration")]
public class VaultUnknownTypeTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private ApiFactory _f = null!;

    public VaultUnknownTypeTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync() => _f = new ApiFactory(await _pg.CreateDatabaseAsync());
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task UnknownTypeRow_IsSkipped_NotUnmaskedAsPerson()
    {
        using var scope = _f.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var crypto = sp.GetRequiredService<VaultCrypto>();
        var adminId = (await db.Users.AsNoTracking().FirstAsync(u => u.UserName == _f.AdminUser)).Id;

        var conv = new Conversation { UserId = adminId, Title = "t", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Conversations.Add(conv);
        // 💾 แถวที่ entity_type ไม่อยู่ใน enum (เช่น มาจากเวอร์ชันอื่น) + แถว PERSON ปกติ index 2
        db.PiiVault.Add(new PiiVaultEntry
        {
            ConversationId = conv.Id, EntityType = "ALIEN", TokenIndex = 1, Token = "[ALIEN_1]",
            ValueHash = [1], ValueCipher = crypto.Encrypt("ค่าลับของ-alien", conv.Id, "[ALIEN_1]"),
            CreatedAt = DateTimeOffset.UtcNow, LastUsedAt = DateTimeOffset.UtcNow
        });
        db.PiiVault.Add(new PiiVaultEntry
        {
            ConversationId = conv.Id, EntityType = "PERSON", TokenIndex = 2, Token = "[PERSON_2]",
            ValueHash = [2], ValueCipher = crypto.Encrypt("ชื่อทดสอบ", conv.Id, "[PERSON_2]"),
            CreatedAt = DateTimeOffset.UtcNow, LastUsedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var store = sp.GetRequiredService<IVaultStore>();
        var all = await store.LoadAllAsync(conv.Id, default);
        var only = Assert.Single(all);
        Assert.Equal(PiiEntityType.Person, only.Type);

        var unmask = sp.GetRequiredService<UnmaskService>();
        var text = await unmask.UnmaskAsync(conv.Id, "[PERSON_1] [PERSON_2] [ALIEN_1]", default);
        Assert.Equal("[PERSON_1] ชื่อทดสอบ [ALIEN_1]", text);
    }
}
