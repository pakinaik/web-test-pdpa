namespace WebTestPdpa.Api.Pii;

/// <summary>📤 client เรียก PII service (detection เท่านั้น) — fail-closed</summary>
public interface IPiiDetectionClient
{
    /// <exception cref="PiiUnavailableException">⚠️ เมื่อ service ใช้ไม่ได้หรือผลไม่ถูกต้อง</exception>
    Task<PiiDetectionResult> DetectAsync(string text, IReadOnlyList<string> knownNames, CancellationToken ct);

    /// <summary>📦 สถานะ service สำหรับ /api/health (null = เรียกไม่ได้)</summary>
    Task<PiiHealth?> GetHealthAsync(CancellationToken ct);
}

public sealed record PiiHealth(bool NerEnabled);
