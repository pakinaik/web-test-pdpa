namespace WebTestPdpa.Api.Data.Entities;

/// <summary>💾 R8: ไฟล์ PDF รายงานที่สร้างจากคำตอบ LLM — ⚠️ เก็บเฉพาะแบบเข้ารหัส (AES-GCM) และมีวันหมดอายุ</summary>
public class ReportFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    /// <summary>🔍 assistant message ที่เป็นที่มาของรายงาน</summary>
    public Guid MessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>⚠️ nonce(12) ‖ ciphertext ‖ tag(16) — AAD = reportId|userId</summary>
    public byte[] Cipher { get; set; } = [];
    public int SizeBytes { get; set; }
    public int DownloadCount { get; set; }
    public DateTimeOffset? LastDownloadedAt { get; set; }
}
