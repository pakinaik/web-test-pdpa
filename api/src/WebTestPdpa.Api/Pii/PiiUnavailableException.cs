namespace WebTestPdpa.Api.Pii;

/// <summary>⚠️ PII service ล่ม/ผลผิดปกติ — ต้องหยุดไม่ส่ง LLM (fail-closed, D12)</summary>
public sealed class PiiUnavailableException : Exception
{
    public string ReasonCode { get; }

    public PiiUnavailableException(string reasonCode)
        : base("PII service unavailable: " + reasonCode)
    {
        ReasonCode = reasonCode;
    }

    public PiiUnavailableException(string reasonCode, Exception inner)
        : base("PII service unavailable: " + reasonCode, inner)
    {
        ReasonCode = reasonCode;
    }
}
