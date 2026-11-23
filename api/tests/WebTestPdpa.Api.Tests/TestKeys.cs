using System.Security.Cryptography;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests;

/// <summary>🚀 key สำหรับ test — สร้างใหม่สุ่มทุกครั้ง ไม่ใช่ค่าจริง</summary>
public static class TestKeys
{
    public static VaultCrypto NewCrypto() => new(RandomNumberGenerator.GetBytes(32));
}
