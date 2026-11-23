using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebTestPdpa.Api.Llm;
using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Tests.Fakes;

namespace WebTestPdpa.Api.Tests.Integration;

/// <summary>🚀 host ทดสอบ: Postgres จริง + fake pii + mock LLM — ค่า secret สุ่มใน test เอง</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _conn;
    private readonly Dictionary<string, string?> _extra;
    private readonly Action<IServiceCollection>? _overrides;

    public string AdminUser { get; } = "admin_test";
    public string AdminPassword { get; } = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    public FakePiiDetectionClient Pii { get; } = new();

    public ApiFactory(string connectionString, Dictionary<string, string?>? extra = null,
        Action<IServiceCollection>? overrides = null)
    {
        _conn = connectionString;
        _extra = extra ?? new();
        _overrides = overrides;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var d = new Dictionary<string, string?>
            {
                ["DB_CONNECTION"] = _conn,
                ["JWT_SIGNING_KEY"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                ["VAULT_ENCRYPTION_KEY"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["PII_SERVICE_TOKEN"] = "test-token-" + Guid.NewGuid().ToString("N"),
                ["ADMIN_USERNAME"] = AdminUser,
                ["ADMIN_PASSWORD"] = AdminPassword,
                ["LLM_PROVIDER"] = "mock",
                ["RETENTION_INITIAL_DELAY_SECONDS"] = "86400"
            };
            foreach (var kv in _extra) d[kv.Key] = kv.Value;
            cfg.AddInMemoryCollection(d);
        });
        builder.ConfigureServices(s =>
        {
            // 🔄 แทน PII client ด้วย fake
            foreach (var x in s.Where(x => x.ServiceType == typeof(IPiiDetectionClient)).ToList()) s.Remove(x);
            s.AddSingleton<IPiiDetectionClient>(Pii);
            // 🔄 ให้ test แทน service อื่นเพิ่มได้ (เช่น ILlmGateway)
            _overrides?.Invoke(s);
        });
    }

    public MockChatClient Llm => (MockChatClient)Services.GetRequiredService<IChatClient>();
}
