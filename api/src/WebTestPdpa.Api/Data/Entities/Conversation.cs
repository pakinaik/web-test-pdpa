namespace WebTestPdpa.Api.Data.Entities;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    /// <summary>💾 40 ตัวอักษรแรกของข้อความ masked (D21)</summary>
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<Message> Messages { get; set; } = [];
}

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    /// <summary>🔍 user / assistant</summary>
    public string Role { get; set; } = "";
    /// <summary>⚠️ เก็บเฉพาะฉบับ masked (D6)</summary>
    public string MaskedContent { get; set; } = "";
    /// <summary>💾 jsonb เช่น {"PERSON":1}</summary>
    public string MaskCounts { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
