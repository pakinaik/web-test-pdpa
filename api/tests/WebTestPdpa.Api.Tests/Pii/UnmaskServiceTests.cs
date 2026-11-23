using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Tests.Fakes;

namespace WebTestPdpa.Api.Tests.Pii;

public class UnmaskServiceTests
{
    private readonly InMemoryVaultStore _vault = new();
    private readonly FakePiiDetectionClient _pii = new();
    private readonly VaultCrypto _crypto = TestKeys.NewCrypto();

    private (MaskingService, UnmaskService) Svc() => (new MaskingService(_pii, _vault, _crypto), new UnmaskService(_vault, _crypto));

    [Fact]
    public async Task RoundTrip()
    {
        var (m, u) = Svc();
        var conv = Guid.NewGuid();
        var text = "สมชาย ใจดี โทร 081-234-5678 mail somchai@example.com";
        var r = await m.MaskAsync(conv, text, [], ["สมชาย ใจดี"], true, default);
        Assert.Equal(text, await u.UnmaskAsync(conv, r.MaskedText, default));
    }

    [Theory]
    [InlineData("[PHONE 1]")]
    [InlineData("[phone_1]")]
    [InlineData("[ PHONE-1 ]")]
    public async Task TolerantTokenForms(string token)
    {
        var (m, u) = Svc();
        var conv = Guid.NewGuid();
        await m.MaskAsync(conv, "081-234-5678", [], [], true, default);
        Assert.Equal("081-234-5678", await u.UnmaskAsync(conv, token, default));
    }

    [Fact]
    public async Task UnknownTokenKept()
    {
        var (_, u) = Svc();
        Assert.Equal("[PHONE_9]", await u.UnmaskAsync(Guid.NewGuid(), "[PHONE_9]", default));
    }

    [Fact]
    public async Task OtherConversationTokenNotUnmasked()
    {
        var (m, u) = Svc();
        await m.MaskAsync(Guid.NewGuid(), "081-234-5678", [], [], true, default);
        Assert.Equal("[PHONE_1]", await u.UnmaskAsync(Guid.NewGuid(), "[PHONE_1]", default));
    }

    [Fact]
    public async Task DecryptFailureKeepsToken()
    {
        var (m, u) = Svc();
        var conv = Guid.NewGuid();
        await m.MaskAsync(conv, "081-234-5678", [], [], true, default);
        _vault.Entries[0].ValueCipher[15] ^= 1;
        Assert.Equal("[PHONE_1]", await u.UnmaskAsync(conv, "[PHONE_1]", default));
    }
}
