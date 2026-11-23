using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace WebTestPdpa.Api.Tests.Llm;

/// <summary>
/// 🔍 R7 / F24: payload R7 = [system(prompt), system(context), ...history, user] — ตรวจว่า adapter Anthropic (SDK 13.12.0)
/// รวม system หลายตัวเข้า field `system` ระดับบนสุดโดยไม่ทิ้ง/ไม่ throw (ไม่เรียก network จริง)
/// </summary>
public class AnthropicSystemMessagesTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            const string json = """{"id":"msg_1","type":"message","role":"assistant","model":"m","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":1,"output_tokens":1}}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task MultipleSystemMessages_AreAllSentInSystemField()
    {
        var handler = new CaptureHandler();
        var client = new Anthropic.AnthropicClient
        {
            ApiKey = "test-key-not-real",
            BaseUrl = "http://anthropic.test",
            HttpClient = new HttpClient(handler),
            MaxRetries = 0
        };
        IChatClient chat = client.AsIChatClient("test-model");

        await chat.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, "SYSTEM-PROMPT-AAA"),
            new ChatMessage(ChatRole.System, "SYSTEM-CONTEXT-BBB"),
            new ChatMessage(ChatRole.User, "USER-HELLO")
        ], new ChatOptions { MaxOutputTokens = 16 });

        Assert.NotNull(handler.Body);
        using var doc = JsonDocument.Parse(handler.Body!);
        var root = doc.RootElement;
        // 🔍 ทั้งสอง system ต้องอยู่ใน field system (string หรือ array ของ block)
        var system = root.GetProperty("system").GetRawText();
        Assert.Contains("SYSTEM-PROMPT-AAA", system);
        Assert.Contains("SYSTEM-CONTEXT-BBB", system);
        // 🔍 messages ต้องมีเฉพาะ user (ไม่มี role system ปนใน messages)
        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        Assert.Single(messages);
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.DoesNotContain("SYSTEM-", messages[0].GetRawText());
    }
}
