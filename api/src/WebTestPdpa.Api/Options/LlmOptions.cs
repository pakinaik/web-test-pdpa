namespace WebTestPdpa.Api.Options;

/// <summary>🚀 ตั้งค่า LLM provider (จาก env LLM_*)</summary>
public sealed class LlmOptions
{
    public string Provider { get; set; } = "mock";
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxOutputTokens { get; set; } = 1024;
    public float Temperature { get; set; } = 0.3f;
}
