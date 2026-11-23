using Microsoft.Extensions.AI;
using WebTestPdpa.Api.Employees;

namespace WebTestPdpa.Api.Llm;

/// <summary>🚀 mock provider — ไม่เรียก network, echo ข้อความ masked ที่ได้รับ (ใช้ทดสอบว่าไม่มี PII รั่ว)</summary>
public sealed class MockChatClient : IChatClient
{
    private volatile IReadOnlyList<ChatMessage> _lastMessages = [];

    /// <summary>📦 messages ที่ได้รับล่าสุด — ให้ test ตรวจว่าไม่มี PII</summary>
    public IReadOnlyList<ChatMessage> LastMessages => _lastMessages;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        _lastMessages = list;
        var lastUser = list.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var text = "[MOCK] ได้รับข้อความ: " + lastUser + "\n[MOCK] history=" + list.Count;

        // 🔄 R7 (D44): มี system ตัวที่ 2 ที่ขึ้นต้นด้วย marker ของ context พนักงาน -> ต่อท้าย echo (masked) ให้ e2e ตรวจได้
        // ⚠️ ไม่มี context = ข้อความเดิมเป๊ะ
        if (list.Count > 1 && list[1].Role == ChatRole.System
            && (list[1].Text ?? "").StartsWith(EmployeeContextBuilder.Marker, StringComparison.Ordinal))
        {
            text += "\n[MOCK] context=" + list[1].Text;
        }
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("mock ไม่รองรับ streaming ใน MVP");

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        // 🧹 ไม่มี resource ต้อง dispose
    }
}
