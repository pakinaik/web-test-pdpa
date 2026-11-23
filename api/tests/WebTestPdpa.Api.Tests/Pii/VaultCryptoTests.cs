using System.Security.Cryptography;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Pii;

public class VaultCryptoTests
{
    private readonly Guid _conv = Guid.NewGuid();

    [Fact]
    public void RoundTrip()
    {
        var c = TestKeys.NewCrypto();
        var data = c.Encrypt("081-234-5678", _conv, "[PHONE_1]");
        Assert.Equal("081-234-5678", c.Decrypt(data, _conv, "[PHONE_1]"));
    }

    [Fact]
    public void NonceDiffersEachTime()
    {
        var c = TestKeys.NewCrypto();
        var a = c.Encrypt("x", _conv, "[PHONE_1]");
        var b = c.Encrypt("x", _conv, "[PHONE_1]");
        Assert.NotEqual(a.AsSpan(0, 12).ToArray(), b.AsSpan(0, 12).ToArray());
    }

    [Fact]
    public void TamperedByteThrows()
    {
        var c = TestKeys.NewCrypto();
        var data = c.Encrypt("secret", _conv, "[PHONE_1]");
        data[14] ^= 0x01;
        Assert.ThrowsAny<CryptographicException>(() => c.Decrypt(data, _conv, "[PHONE_1]"));
    }

    [Fact]
    public void DifferentAadThrows()
    {
        var c = TestKeys.NewCrypto();
        var data = c.Encrypt("secret", _conv, "[PHONE_1]");
        Assert.ThrowsAny<CryptographicException>(() => c.Decrypt(data, _conv, "[PHONE_2]"));
        Assert.ThrowsAny<CryptographicException>(() => c.Decrypt(data, Guid.NewGuid(), "[PHONE_1]"));
    }

    [Fact]
    public void WrongKeyLengthThrows() => Assert.Throws<ArgumentException>(() => new VaultCrypto(new byte[16]));

    [Fact]
    public void InvalidBase64Throws()
    {
        Assert.Throws<ArgumentException>(() => VaultCrypto.FromBase64("not base64!!"));
        Assert.Throws<ArgumentException>(() => VaultCrypto.FromBase64(""));
    }

    [Fact]
    public void HmacDeterministicAndConversationScoped()
    {
        var c = TestKeys.NewCrypto();
        var h1 = c.ComputeValueHash(_conv, PiiEntityType.Phone, "0812345678");
        Assert.Equal(h1, c.ComputeValueHash(_conv, PiiEntityType.Phone, "0812345678"));
        Assert.NotEqual(h1, c.ComputeValueHash(Guid.NewGuid(), PiiEntityType.Phone, "0812345678"));
        Assert.NotEqual(h1, c.ComputeValueHash(_conv, PiiEntityType.BankAccount, "0812345678"));
        Assert.Equal(32, h1.Length);
    }

    [Fact]
    public void CiphertextDoesNotContainPlaintext()
    {
        var c = TestKeys.NewCrypto();
        var data = c.Encrypt("081-234-5678", _conv, "[PHONE_1]");
        Assert.DoesNotContain("081-234-5678", System.Text.Encoding.UTF8.GetString(data));
    }
}
