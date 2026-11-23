using Microsoft.Extensions.AI;
using WebTestPdpa.Api.Llm;
using WebTestPdpa.Api.Options;

namespace WebTestPdpa.Api.Tests.Llm;

public class LlmTests
{
    [Fact]
    public async Task MockEchoesLastUserMessage()
    {
        var c = new MockChatClient();
        var r = await c.GetResponseAsync([new ChatMessage(ChatRole.System, "sys"), new ChatMessage(ChatRole.User, "[PERSON_1] โดนหัก")]);
        Assert.Contains("[PERSON_1] โดนหัก", r.Text);
        Assert.Equal(2, c.LastMessages.Count);
    }

    [Fact]
    public async Task MockAppendsContextLineWhenSecondSystemHasMarker()
    {
        // 🔍 R7 (D44): system ตัวที่ 2 ขึ้นต้นด้วย marker -> ต่อท้ายบรรทัด [MOCK] context=
        var c = new MockChatClient();
        var ctx = WebTestPdpa.Api.Employees.EmployeeContextBuilder.Marker + "\n[PERSON_1] ศาสนา [RELIGION_1]";
        var r = await c.GetResponseAsync([
            new ChatMessage(ChatRole.System, "sys"),
            new ChatMessage(ChatRole.System, ctx),
            new ChatMessage(ChatRole.User, "ขอดูศาสนา")]);
        Assert.Contains("\n[MOCK] context=" + ctx, r.Text);
    }

    [Fact]
    public async Task MockWithoutContextKeepsOriginalAnswerExactly()
    {
        // ⚠️ ไม่มี context (หรือ system ตัวที่ 2 ไม่มี marker) = คำตอบเดิมเป๊ะ
        var c = new MockChatClient();
        var r = await c.GetResponseAsync([new ChatMessage(ChatRole.System, "sys"), new ChatMessage(ChatRole.User, "สวัสดี")]);
        Assert.Equal("[MOCK] ได้รับข้อความ: สวัสดี\n[MOCK] history=2", r.Text);

        var r2 = await c.GetResponseAsync([
            new ChatMessage(ChatRole.System, "sys"),
            new ChatMessage(ChatRole.System, "ไม่ใช่ marker"),
            new ChatMessage(ChatRole.User, "สวัสดี")]);
        Assert.DoesNotContain("[MOCK] context=", r2.Text);
        Assert.Equal("[MOCK] ได้รับข้อความ: สวัสดี\n[MOCK] history=3", r2.Text);
    }

    [Fact]
    public void MockStreamingNotSupported() =>
        Assert.Throws<NotSupportedException>(() => new MockChatClient().GetStreamingResponseAsync([]));

    [Fact]
    public void UnknownProviderThrows() =>
        Assert.Throws<InvalidOperationException>(() => LlmClientFactory.Create(new LlmOptions { Provider = "alien" }));

    [Fact]
    public void DeepseekWithoutKeyThrowsNamingEnv()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => LlmClientFactory.Create(new LlmOptions { Provider = "deepseek" }));
        Assert.Contains("LLM_API_KEY", ex.Message);
    }

    [Fact]
    public void MockNeedsNoKey() => Assert.IsType<MockChatClient>(LlmClientFactory.Create(new LlmOptions { Provider = "mock" }));

    [Fact]
    public void OpenAiAndClaudeRequireModel()
    {
        Assert.Throws<InvalidOperationException>(() => LlmClientFactory.Create(new LlmOptions { Provider = "openai", ApiKey = "fake-key" }));
        Assert.Throws<InvalidOperationException>(() => LlmClientFactory.Create(new LlmOptions { Provider = "claude", ApiKey = "fake-key" }));
    }

    [Theory]
    [InlineData("deepseek", null)]
    [InlineData("openai", "gpt-test")]
    [InlineData("claude", "claude-test")]
    [InlineData("ollama", null)]
    public void FactoryCreatesClients(string provider, string? model)
    {
        using var c = LlmClientFactory.Create(new LlmOptions { Provider = provider, Model = model, ApiKey = "fake-key-for-test" });
        Assert.NotNull(c);
    }

    [Fact]
    public void ResolveDefaults()
    {
        var r = LlmClientFactory.Resolve(new LlmOptions { Provider = "deepseek", ApiKey = "fake" });
        Assert.Equal("deepseek-chat", r.Model);
        Assert.Equal("https://api.deepseek.com", r.BaseUrl);
    }

    [Fact]
    public async Task GatewayWrapsErrors()
    {
        var gw = new LlmGateway(new ThrowingClient(), Microsoft.Extensions.Options.Options.Create(new LlmOptions { Provider = "mock" }));
        await Assert.ThrowsAsync<LlmException>(() => gw.CompleteAsync([new LlmMessage("user", "x")], default));
    }

    private sealed class ThrowingClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
