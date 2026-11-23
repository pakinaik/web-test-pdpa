namespace WebTestPdpa.Api.Chat;

/// <summary>⚠️ error ที่ map เป็น HTTP + code (ไม่มีข้อความผู้ใช้ใน message)</summary>
public sealed class ChatException : Exception
{
    public int Status { get; }
    public string Code { get; }
    public IDictionary<string, object?>? Extra { get; }

    public ChatException(int status, string code, IDictionary<string, object?>? extra = null) : base(code)
    {
        Status = status;
        Code = code;
        Extra = extra;
    }
}
