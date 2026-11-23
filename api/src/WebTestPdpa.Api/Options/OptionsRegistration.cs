using Microsoft.Extensions.Options;
using WebTestPdpa.Api.Llm;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Options;

/// <summary>🚀 bind env -> options + validate ตอน start (error บอกชื่อ key ไม่แสดงค่า)</summary>
public static class OptionsRegistration
{
    private static int Int(IConfiguration c, string key, int def) =>
        int.TryParse(c[key], out var v) ? v : def;

    private static string Str(IConfiguration c, string key, string def = "") =>
        string.IsNullOrWhiteSpace(c[key]) ? def : c[key]!;

    public static IServiceCollection AddAppOptions(this IServiceCollection s)
    {
        s.AddOptions<DbOptions>()
            .Configure<IConfiguration>((o, c) => o.Connection = Str(c, "DB_CONNECTION"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Connection), "DB_CONNECTION ไม่ได้ตั้งค่า")
            .ValidateOnStart();

        s.AddOptions<JwtOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.SigningKey = Str(c, "JWT_SIGNING_KEY");
                o.Issuer = Str(c, "JWT_ISSUER", "webtestpdpa");
                o.Audience = Str(c, "JWT_AUDIENCE", "webtestpdpa-web");
                o.ExpiresMinutes = Int(c, "JWT_EXPIRES_MINUTES", 60);
                o.CookieSecure = string.Equals(c["COOKIE_SECURE"], "true", StringComparison.OrdinalIgnoreCase);
            })
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "JWT_SIGNING_KEY ต้องยาวอย่างน้อย 32 bytes")
            .Validate(o => o.ExpiresMinutes > 0, "JWT_EXPIRES_MINUTES ต้องมากกว่า 0")
            .ValidateOnStart();

        s.AddOptions<VaultOptions>()
            .Configure<IConfiguration>((o, c) => o.EncryptionKey = Str(c, "VAULT_ENCRYPTION_KEY"))
            .Validate(o =>
            {
                try { _ = VaultCrypto.FromBase64(o.EncryptionKey); return true; }
                catch (ArgumentException) { return false; }
            }, "VAULT_ENCRYPTION_KEY ต้องเป็น base64 ของ 32 bytes")
            .ValidateOnStart();

        s.AddOptions<PiiOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.BaseUrl = Str(c, "PII_BASE_URL", "http://pii:8000");
                o.ServiceToken = Str(c, "PII_SERVICE_TOKEN");
                o.TimeoutSeconds = Int(c, "PII_TIMEOUT_SECONDS", 5);
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.ServiceToken), "PII_SERVICE_TOKEN ไม่ได้ตั้งค่า")
            .Validate(o => o.TimeoutSeconds > 0, "PII_TIMEOUT_SECONDS ต้องมากกว่า 0")
            .ValidateOnStart();

        s.AddOptions<LlmOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.Provider = Str(c, "LLM_PROVIDER", "mock");
                o.Model = c["LLM_MODEL"];
                o.BaseUrl = c["LLM_BASE_URL"];
                o.ApiKey = c["LLM_API_KEY"];
                o.TimeoutSeconds = Int(c, "LLM_TIMEOUT_SECONDS", 60);
                o.MaxOutputTokens = Int(c, "LLM_MAX_OUTPUT_TOKENS", 1024);
                o.Temperature = float.TryParse(c["LLM_TEMPERATURE"], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0.3f;
            })
            .Validate(o =>
            {
                try { _ = LlmClientFactory.Resolve(o); return true; }
                catch (InvalidOperationException) { return false; }
            }, "LLM_* ไม่ถูกต้อง (ตรวจ LLM_PROVIDER / LLM_MODEL / LLM_API_KEY)")
            .ValidateOnStart();

        s.AddOptions<AdminSeedOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.UserName = Str(c, "ADMIN_USERNAME");
                o.Password = Str(c, "ADMIN_PASSWORD");
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserName) && !string.IsNullOrWhiteSpace(o.Password),
                "ADMIN_USERNAME / ADMIN_PASSWORD ไม่ได้ตั้งค่า")
            .ValidateOnStart();

        // 🔍 R7b (D36): optional — ว่าง = ข้าม seed user; มีค่าต้องยาว >= 10 (error บอกชื่อ key ไม่แสดงค่า)
        s.AddOptions<EmployeeSeedOptions>()
            .Configure<IConfiguration>((o, c) => o.Password = Str(c, "EMPLOYEE_SEED_PASSWORD"))
            .Validate(o => o.Password.Length == 0 || o.Password.Length >= 10,
                "EMPLOYEE_SEED_PASSWORD ต้องยาวอย่างน้อย 10 ตัวอักษร")
            .ValidateOnStart();

        s.AddOptions<RetentionOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.Days = Int(c, "RETENTION_DAYS", 30);
                o.AuditDays = Int(c, "AUDIT_RETENTION_DAYS", 180);
                o.IntervalMinutes = Int(c, "RETENTION_INTERVAL_MINUTES", 60);
                o.InitialDelaySeconds = Int(c, "RETENTION_INITIAL_DELAY_SECONDS", 60);
            })
            .Validate(o => o.Days > 0, "RETENTION_DAYS ต้องมากกว่า 0")
            .Validate(o => o.AuditDays >= o.Days, "AUDIT_RETENTION_DAYS ต้อง >= RETENTION_DAYS")
            .Validate(o => o.IntervalMinutes > 0, "RETENTION_INTERVAL_MINUTES ต้องมากกว่า 0")
            .ValidateOnStart();

        s.AddOptions<ChatLimitOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.RateLimitPerMinute = Int(c, "RATE_LIMIT_PER_MINUTE", 20);
                o.HistoryLimit = Int(c, "LLM_HISTORY_LIMIT", 10);
            })
            .Validate(o => o.RateLimitPerMinute > 0 && o.HistoryLimit >= 0, "RATE_LIMIT_PER_MINUTE / LLM_HISTORY_LIMIT ไม่ถูกต้อง")
            .ValidateOnStart();

        return s;
    }
}
