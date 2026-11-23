namespace WebTestPdpa.Api.Contracts;

public sealed record ChatRequest(Guid? ConversationId, string? Text, List<string>? CustomValues);

public sealed record PreviewEntity(string Type, int Start, int End, string Token, string Source);

public sealed record GuardInfo(bool Passed, IReadOnlyList<string> Reasons);

public sealed record PreviewResponse(
    string MaskedText,
    IReadOnlyList<PreviewEntity> Entities,
    IReadOnlyDictionary<string, int> MaskCounts,
    GuardInfo Guard);

public sealed record MessageDto(
    Guid Id,
    string Role,
    string Content,
    string MaskedContent,
    IReadOnlyDictionary<string, int> MaskCounts,
    DateTimeOffset CreatedAt,
    // 🔄 R8: id รายงาน PDF ที่ดาวน์โหลดได้ (null = ไม่มี/หมดอายุ) — เพิ่มท้ายเพื่อ backward compatible
    Guid? ReportId = null);

public sealed record SendResponse(
    Guid ConversationId,
    MessageDto UserMessage,
    MessageDto AssistantMessage,
    string Provider,
    string Model);

public sealed record ConversationDto(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
