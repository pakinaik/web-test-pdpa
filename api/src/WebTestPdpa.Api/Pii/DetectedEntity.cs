namespace WebTestPdpa.Api.Pii;

/// <summary>📦 entity ที่ PII service ตรวจพบ (offset = UTF-16 บนข้อความต้นฉบับ, end exclusive)</summary>
public sealed record DetectedEntity(PiiEntityType Type, int Start, int End, string Source);

/// <summary>📦 ผลจาก PII service</summary>
public sealed record PiiDetectionResult(IReadOnlyList<DetectedEntity> Entities, bool NerUsed);
