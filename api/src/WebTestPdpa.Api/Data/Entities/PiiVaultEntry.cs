namespace WebTestPdpa.Api.Data.Entities;

/// <summary>💾 vault: token ↔ ค่าจริง (เข้ารหัส AES-GCM)</summary>
public class PiiVaultEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string EntityType { get; set; } = "";
    public int TokenIndex { get; set; }
    public string Token { get; set; } = "";
    public byte[] ValueHash { get; set; } = [];
    public byte[] ValueCipher { get; set; } = [];
    public short KeyVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
}
