namespace WebTestPdpa.Api.Data.Entities;

/// <summary>⚠️ audit: ไม่มีคอลัมน์เก็บ raw text / ค่าจริง (payload เป็นฉบับ masked เท่านั้น)</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = "";
    public Guid? ConversationId { get; set; }
    public Guid? MessageId { get; set; }
    public string Action { get; set; } = "chat_send";
    /// <summary>🔍 sent / blocked_guard / blocked_pii_unavailable / llm_error</summary>
    public string Status { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string MaskCounts { get; set; } = "{}";
    public string? MaskedPayload { get; set; }
    /// <summary>💾 jsonb: payload ทั้งก้อนที่ส่ง LLM จริง (D23)</summary>
    public string? OutboundPayload { get; set; }
    public string? PayloadSha256 { get; set; }
    public int HistoryMessageCount { get; set; }
    public bool NerEnabled { get; set; }
    public string? GuardReasons { get; set; }
    public string? ErrorCode { get; set; }
    public int? LatencyMs { get; set; }
    /// <summary>💾 R7 (D45): jsonb เช่น [3,7] = employees.id ที่ถูกแนบเป็น context (ไม่เก็บชื่อ/ค่า) — null = ไม่มี context</summary>
    public string? ContextEmployeeIds { get; set; }
    /// <summary>💾 R7 (D45): jsonb เช่น ["sensitive","income"] — null = ไม่มี context</summary>
    public string? ContextSections { get; set; }
}
