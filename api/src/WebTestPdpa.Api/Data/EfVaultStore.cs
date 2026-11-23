using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Data;

/// <summary>💾 vault store บน EF Core (ใช้ DbContext เดียวกับ request เพื่ออยู่ใน transaction เดียวกัน)</summary>
public sealed class EfVaultStore : IVaultStore
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public EfVaultStore(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <exception cref="InvalidOperationException">⚠️ type ใน DB ไม่รู้จัก — ห้ามแปลงเป็น Person เงียบๆ (จะ unmask ผิด type)</exception>
    private static VaultEntry ToModel(PiiVaultEntry e)
    {
        if (!PiiEntityTypes.TryParse(e.EntityType, out var type))
        {
            throw new InvalidOperationException("UNKNOWN_VAULT_TYPE");
        }
        return new VaultEntry
        {
            Id = e.Id,
            ConversationId = e.ConversationId,
            Type = type,
            TokenIndex = e.TokenIndex,
            Token = e.Token,
            ValueHash = e.ValueHash,
            ValueCipher = e.ValueCipher,
            LastUsedAt = e.LastUsedAt
        };
    }

    public async Task<VaultEntry?> FindAsync(Guid conversationId, PiiEntityType type, byte[] valueHash, CancellationToken ct)
    {
        var wire = PiiEntityTypes.ToWire(type);
        var e = await _db.PiiVault.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.EntityType == wire && x.ValueHash == valueHash, ct);
        return e is null ? null : ToModel(e);
    }

    public async Task<IReadOnlyDictionary<PiiEntityType, int>> GetMaxIndexesAsync(Guid conversationId, CancellationToken ct)
    {
        var rows = await _db.PiiVault.AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .GroupBy(x => x.EntityType)
            .Select(g => new { g.Key, Max = g.Max(x => x.TokenIndex) })
            .ToListAsync(ct);
        var dict = new Dictionary<PiiEntityType, int>();
        foreach (var r in rows)
        {
            if (PiiEntityTypes.TryParse(r.Key, out var t)) dict[t] = r.Max;
        }
        return dict;
    }

    public async Task AddAsync(VaultEntry entry, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        _db.PiiVault.Add(new PiiVaultEntry
        {
            Id = entry.Id,
            ConversationId = entry.ConversationId,
            EntityType = PiiEntityTypes.ToWire(entry.Type),
            TokenIndex = entry.TokenIndex,
            Token = entry.Token,
            ValueHash = entry.ValueHash,
            ValueCipher = entry.ValueCipher,
            CreatedAt = now,
            LastUsedAt = now
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task TouchAsync(Guid entryId, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        await _db.PiiVault.Where(x => x.Id == entryId).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastUsedAt, now), ct);
    }

    public async Task<IReadOnlyList<VaultEntry>> LoadAllAsync(Guid conversationId, CancellationToken ct)
    {
        var rows = await _db.PiiVault.AsNoTracking().Where(x => x.ConversationId == conversationId).ToListAsync(ct);
        var result = new List<VaultEntry>(rows.Count);
        foreach (var row in rows)
        {
            // ⚠️ แถวที่ type ไม่รู้จักถูกข้าม -> token นั้นคงไว้ตามเดิมตอน unmask (ไม่ล้มทั้ง request)
            if (!PiiEntityTypes.TryParse(row.EntityType, out _)) continue;
            result.Add(ToModel(row));
        }
        return result;
    }
}
