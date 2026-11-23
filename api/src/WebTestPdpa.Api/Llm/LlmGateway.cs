using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using WebTestPdpa.Api.Options;

namespace WebTestPdpa.Api.Llm;

public sealed record LlmMessage(string Role, string Content);

public sealed record LlmResult(string Text, string Provider, string Model, int LatencyMs);

/// <summary>⚠️ provider error / timeout</summary>
public sealed class LlmException : Exception
{
    public LlmException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface ILlmGateway
{
    string Provider { get; }
    string Model { get; }
    Task<LlmResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, CancellationToken ct);
}

/// <summary>📤 เรียก LLM ผ่าน IChatClient (non-streaming) พร้อม timeout</summary>
public sealed class LlmGateway : ILlmGateway
{
    private readonly IChatClient _client;
    private readonly LlmOptions _options;

    public LlmGateway(IChatClient client, IOptions<LlmOptions> options)
    {
        _client = client;
        _options = LlmClientFactory.Resolve(options.Value);
    }

    public string Provider => _options.Provider;
    public string Model => _options.Model ?? "";

    public async Task<LlmResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, CancellationToken ct)
    {
        var chat = messages.Select(m => new ChatMessage(new ChatRole(m.Role), m.Content)).ToList();
        var opts = new ChatOptions
        {
            MaxOutputTokens = _options.MaxOutputTokens,
            Temperature = _options.Temperature
        };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var resp = await _client.GetResponseAsync(chat, opts, cts.Token);
            return new LlmResult(resp.Text, Provider, Model, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmException("LLM_TIMEOUT");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ⚠️ ไม่เก็บ message ของ exception (อาจมีเนื้อหา) — เก็บแค่ชนิด
            throw new LlmException("LLM_ERROR:" + ex.GetType().Name, ex);
        }
    }
}
