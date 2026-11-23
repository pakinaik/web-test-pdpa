namespace WebTestPdpa.Api.Options;

/// <summary>🚀 ค่าจาก env (อ่านผ่าน IConfiguration แบบ flat key)</summary>
public sealed class DbOptions
{
    public string Connection { get; set; } = "";
}

public sealed class JwtOptions
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "webtestpdpa";
    public string Audience { get; set; } = "webtestpdpa-web";
    public int ExpiresMinutes { get; set; } = 60;
    public bool CookieSecure { get; set; }
}

public sealed class VaultOptions
{
    public string EncryptionKey { get; set; } = "";
}

public sealed class AdminSeedOptions
{
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class RetentionOptions
{
    public int Days { get; set; } = 30;
    public int AuditDays { get; set; } = 180;
    public int IntervalMinutes { get; set; } = 60;
    /// <summary>🚀 หน่วงรอบแรกหลัง start (วินาที)</summary>
    public int InitialDelaySeconds { get; set; } = 60;
}

public sealed class ChatLimitOptions
{
    public int RateLimitPerMinute { get; set; } = 20;
    public int HistoryLimit { get; set; } = 10;
}

/// <summary>🚀 R7b (D36): รหัสผ่านร่วมของ user พนักงาน emp001–emp020 — ว่าง = ข้าม seed user</summary>
public sealed class EmployeeSeedOptions
{
    public string Password { get; set; } = "";
}
