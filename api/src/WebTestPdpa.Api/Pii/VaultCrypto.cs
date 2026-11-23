using System.Security.Cryptography;
using System.Text;

namespace WebTestPdpa.Api.Pii;

/// <summary>💾 AL9: AES-GCM เข้ารหัสค่าใน vault + HMAC-SHA256 สำหรับ lookup — key derive จาก master ด้วย HKDF</summary>
public sealed class VaultCrypto
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _encKey;
    private readonly byte[] _hmacKey;

    public VaultCrypto(byte[] masterKey)
    {
        // 🔍 master key ต้องยาว 32 bytes เท่านั้น
        if (masterKey is null || masterKey.Length != 32)
        {
            throw new ArgumentException("VAULT_ENCRYPTION_KEY ต้องเป็น base64 ของ 32 bytes");
        }
        _encKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, null, Encoding.UTF8.GetBytes("webtestpdpa-vault-enc"));
        _hmacKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, null, Encoding.UTF8.GetBytes("webtestpdpa-vault-hmac"));
    }

    /// <summary>🔄 แปลง base64 จาก env เป็น VaultCrypto (ผิดรูปแบบ -> throw โดยไม่แสดงค่า)</summary>
    public static VaultCrypto FromBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new ArgumentException("VAULT_ENCRYPTION_KEY ไม่ได้ตั้งค่า");
        }
        try
        {
            return new VaultCrypto(Convert.FromBase64String(base64));
        }
        catch (FormatException)
        {
            throw new ArgumentException("VAULT_ENCRYPTION_KEY ไม่ใช่ base64 ที่ถูกต้อง");
        }
    }

    private static byte[] Aad(Guid conversationId, string token) =>
        Encoding.UTF8.GetBytes($"{conversationId:D}|{token}");

    /// <summary>💾 ผลลัพธ์ = nonce(12) ‖ ciphertext ‖ tag(16)</summary>
    public byte[] Encrypt(string plaintext, Guid conversationId, string token)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var result = new byte[NonceSize + plain.Length + TagSize];
        var nonce = result.AsSpan(0, NonceSize);
        // ⚠️ nonce สุ่มใหม่ทุกครั้งด้วย CSPRNG
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_encKey, TagSize);
        aes.Encrypt(nonce, plain, result.AsSpan(NonceSize, plain.Length), result.AsSpan(NonceSize + plain.Length, TagSize), Aad(conversationId, token));
        return result;
    }

    /// <exception cref="CryptographicException">⚠️ ข้อมูลถูกแก้/AAD ไม่ตรง/key ผิด</exception>
    public string Decrypt(byte[] data, Guid conversationId, string token)
    {
        if (data is null || data.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("vault data สั้นเกินไป");
        }
        var cipherLen = data.Length - NonceSize - TagSize;
        var plain = new byte[cipherLen];
        using var aes = new AesGcm(_encKey, TagSize);
        aes.Decrypt(
            data.AsSpan(0, NonceSize),
            data.AsSpan(NonceSize, cipherLen),
            data.AsSpan(NonceSize + cipherLen, TagSize),
            plain,
            Aad(conversationId, token));
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>🔍 D4: HMAC(conversationId|TYPE|canonical) — consistent ต่อ conversation โดยไม่ต้อง decrypt</summary>
    public byte[] ComputeValueHash(Guid conversationId, PiiEntityType type, string canonicalValue)
    {
        var input = Encoding.UTF8.GetBytes($"{conversationId:D}|{PiiEntityTypes.ToWire(type)}|{canonicalValue}");
        return HMACSHA256.HashData(_hmacKey, input);
    }
}
