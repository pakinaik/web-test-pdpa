using System.Security.Cryptography;
using System.Text;

namespace WebTestPdpa.Api.Reports;

/// <summary>💾 R8: AES-GCM เข้ารหัสไฟล์รายงาน — key derive จาก VAULT_ENCRYPTION_KEY ด้วย HKDF (info แยกจาก vault)</summary>
public sealed class ReportCrypto
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public ReportCrypto(byte[] masterKey)
    {
        // 🔍 master key ต้องยาว 32 bytes เท่านั้น
        if (masterKey is null || masterKey.Length != 32)
        {
            throw new ArgumentException("VAULT_ENCRYPTION_KEY ต้องเป็น base64 ของ 32 bytes");
        }
        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, null, Encoding.UTF8.GetBytes("webtestpdpa-report-enc"));
    }

    /// <summary>🔄 แปลง base64 จาก env (ผิดรูปแบบ -> throw โดยไม่แสดงค่า)</summary>
    public static ReportCrypto FromBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new ArgumentException("VAULT_ENCRYPTION_KEY ไม่ได้ตั้งค่า");
        }
        try
        {
            return new ReportCrypto(Convert.FromBase64String(base64));
        }
        catch (FormatException)
        {
            throw new ArgumentException("VAULT_ENCRYPTION_KEY ไม่ใช่ base64 ที่ถูกต้อง");
        }
    }

    private static byte[] Aad(Guid reportId, Guid userId) => Encoding.UTF8.GetBytes($"{reportId:D}|{userId:D}");

    /// <summary>💾 ผลลัพธ์ = nonce(12) ‖ ciphertext ‖ tag(16)</summary>
    public byte[] Encrypt(byte[] plain, Guid reportId, Guid userId)
    {
        var result = new byte[NonceSize + plain.Length + TagSize];
        var nonce = result.AsSpan(0, NonceSize);
        // ⚠️ nonce สุ่มใหม่ทุกครั้งด้วย CSPRNG
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, result.AsSpan(NonceSize, plain.Length), result.AsSpan(NonceSize + plain.Length, TagSize), Aad(reportId, userId));
        return result;
    }

    /// <exception cref="CryptographicException">⚠️ ข้อมูลถูกแก้/AAD ไม่ตรง/key ผิด</exception>
    public byte[] Decrypt(byte[] data, Guid reportId, Guid userId)
    {
        if (data.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("ciphertext สั้นเกินไป");
        }
        var len = data.Length - NonceSize - TagSize;
        var plain = new byte[len];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize, len), data.AsSpan(NonceSize + len, TagSize), plain, Aad(reportId, userId));
        return plain;
    }
}
