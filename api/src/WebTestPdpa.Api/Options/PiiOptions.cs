namespace WebTestPdpa.Api.Options;

/// <summary>🚀 ตั้งค่าการเรียก PII service (จาก env PII_*)</summary>
public sealed class PiiOptions
{
    public string BaseUrl { get; set; } = "http://pii:8000";
    public string ServiceToken { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 5;
}
