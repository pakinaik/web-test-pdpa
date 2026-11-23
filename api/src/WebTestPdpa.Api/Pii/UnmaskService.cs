using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace WebTestPdpa.Api.Pii;

/// <summary>🔄 AL6: แทน token ด้วยค่าจริงจาก vault ของ conversation นั้นเท่านั้น</summary>
public sealed class UnmaskService
{
    private readonly IVaultStore _vault;
    private readonly VaultCrypto _crypto;
    private readonly ILogger<UnmaskService>? _logger;

    public UnmaskService(IVaultStore vault, VaultCrypto crypto, ILogger<UnmaskService>? logger = null)
    {
        _vault = vault;
        _crypto = crypto;
        _logger = logger;
    }

    public async Task<string> UnmaskAsync(Guid conversationId, string maskedText, CancellationToken ct)
    {
        var map = await LoadMapAsync(conversationId, ct);
        return Apply(maskedText, map);
    }

    /// <summary>🔄 unmask หลายข้อความโดยโหลด vault ครั้งเดียว</summary>
    public async Task<IReadOnlyList<string>> UnmaskManyAsync(Guid conversationId, IReadOnlyList<string> maskedTexts, CancellationToken ct)
    {
        var map = await LoadMapAsync(conversationId, ct);
        return maskedTexts.Select(t => Apply(t, map)).ToList();
    }

    private async Task<Dictionary<(PiiEntityType, int), string>> LoadMapAsync(Guid conversationId, CancellationToken ct)
    {
        // 📦 โหลด vault ของ conversation ครั้งเดียว แล้ว decrypt
        var map = new Dictionary<(PiiEntityType, int), string>();
        foreach (var e in await _vault.LoadAllAsync(conversationId, ct))
        {
            try
            {
                map[(e.Type, e.TokenIndex)] = _crypto.Decrypt(e.ValueCipher, conversationId, e.Token);
            }
            catch (CryptographicException)
            {
                // ⚠️ decrypt ไม่ได้ -> คง token ไว้ ไม่ throw ทั้ง request (log เฉพาะ id)
                _logger?.LogWarning("vault decrypt failed id={VaultId}", e.Id);
            }
        }
        return map;
    }

    private static string Apply(string text, Dictionary<(PiiEntityType, int), string> map)
    {
        return PiiRegex.Token.Replace(text, m =>
        {
            // 🔄 type หลายคำที่ LLM เปลี่ยนตัวคั่น (เช่น "blood type") -> ขีดล่างก่อน parse
            var wire = PiiRegex.TypeSeparator.Replace(m.Groups[1].Value, "_");
            if (PiiEntityTypes.TryParse(wire, out var type)
                && int.TryParse(m.Groups[2].Value, out var n)
                && map.TryGetValue((type, n), out var value))
            {
                return value;
            }
            // 🔍 token ไม่อยู่ใน vault ของ conversation นี้ -> คงไว้ตามเดิม
            return m.Value;
        });
    }
}
