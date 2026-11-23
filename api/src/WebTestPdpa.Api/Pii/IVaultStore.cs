namespace WebTestPdpa.Api.Pii;

/// <summary>💾 แถวใน vault (ค่าเข้ารหัสแล้ว)</summary>
public sealed class VaultEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public PiiEntityType Type { get; set; }
    public int TokenIndex { get; set; }
    public string Token { get; set; } = "";
    public byte[] ValueHash { get; set; } = [];
    public byte[] ValueCipher { get; set; } = [];
    public DateTimeOffset LastUsedAt { get; set; }
}

/// <summary>💾 ที่เก็บ vault (token ↔ ค่าจริงที่เข้ารหัส)</summary>
public interface IVaultStore
{
    Task<VaultEntry?> FindAsync(Guid conversationId, PiiEntityType type, byte[] valueHash, CancellationToken ct);
    Task<IReadOnlyDictionary<PiiEntityType, int>> GetMaxIndexesAsync(Guid conversationId, CancellationToken ct);
    Task AddAsync(VaultEntry entry, CancellationToken ct);
    Task TouchAsync(Guid entryId, CancellationToken ct);
    Task<IReadOnlyList<VaultEntry>> LoadAllAsync(Guid conversationId, CancellationToken ct);
}
