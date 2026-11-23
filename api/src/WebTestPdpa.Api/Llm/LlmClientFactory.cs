using System.ClientModel;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using WebTestPdpa.Api.Options;

namespace WebTestPdpa.Api.Llm;

/// <summary>🚀 สร้าง IChatClient ตาม LLM_PROVIDER</summary>
public static class LlmClientFactory
{
    public const string DeepSeekBaseUrl = "https://api.deepseek.com";

    /// <summary>🔄 ใส่ค่า default ของ model/baseUrl ตาม provider แล้ว validate (บอกชื่อ env ที่ขาด ไม่แสดงค่า)</summary>
    public static LlmOptions Resolve(LlmOptions o)
    {
        var provider = (o.Provider ?? "").Trim().ToLowerInvariant();
        var r = new LlmOptions
        {
            Provider = provider,
            Model = o.Model,
            BaseUrl = o.BaseUrl,
            ApiKey = o.ApiKey,
            TimeoutSeconds = o.TimeoutSeconds,
            MaxOutputTokens = o.MaxOutputTokens,
            Temperature = o.Temperature
        };

        switch (provider)
        {
            case "mock":
                r.Model = string.IsNullOrWhiteSpace(r.Model) ? "mock-echo" : r.Model;
                break;
            case "deepseek":
                // ⚠️ DeepSeek host ในจีน — ใช้กับข้อมูลทดสอบเท่านั้น
                r.Model = string.IsNullOrWhiteSpace(r.Model) ? "deepseek-chat" : r.Model;
                r.BaseUrl = string.IsNullOrWhiteSpace(r.BaseUrl) ? DeepSeekBaseUrl : r.BaseUrl;
                RequireKey(r);
                break;
            case "openai":
                RequireModel(r);
                RequireKey(r);
                break;
            case "claude":
                RequireModel(r);
                RequireKey(r);
                break;
            case "ollama":
                r.Model = string.IsNullOrWhiteSpace(r.Model) ? "qwen2.5:3b" : r.Model;
                r.BaseUrl = string.IsNullOrWhiteSpace(r.BaseUrl) ? "http://ollama:11434" : r.BaseUrl;
                break;
            default:
                throw new InvalidOperationException("LLM_PROVIDER ไม่รู้จัก (รองรับ: mock, deepseek, openai, claude, ollama)");
        }
        return r;
    }

    private static void RequireKey(LlmOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.ApiKey))
            throw new InvalidOperationException("LLM_API_KEY ไม่ได้ตั้งค่า (จำเป็นสำหรับ provider " + o.Provider + ")");
    }

    private static void RequireModel(LlmOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.Model))
            throw new InvalidOperationException("LLM_MODEL ไม่ได้ตั้งค่า (จำเป็นสำหรับ provider " + o.Provider + ")");
    }

    public static IChatClient Create(LlmOptions options)
    {
        var o = Resolve(options);
        switch (o.Provider)
        {
            case "mock":
                return new MockChatClient();
            case "deepseek":
            case "openai":
            {
                // 🚀 OpenAI-compatible (DeepSeek ใช้ endpoint ของตัวเอง)
                var clientOptions = new OpenAIClientOptions();
                if (!string.IsNullOrWhiteSpace(o.BaseUrl))
                {
                    clientOptions.Endpoint = new Uri(o.BaseUrl);
                }
                var client = new OpenAIClient(new ApiKeyCredential(o.ApiKey!), clientOptions);
                return client.GetChatClient(o.Model).AsIChatClient();
            }
            case "claude":
            {
                var client = new Anthropic.AnthropicClient { ApiKey = o.ApiKey! };
                return client.AsIChatClient(o.Model!);
            }
            case "ollama":
                return new OllamaApiClient(new Uri(o.BaseUrl!), o.Model!);
            default:
                throw new InvalidOperationException("LLM_PROVIDER ไม่รู้จัก");
        }
    }
}
