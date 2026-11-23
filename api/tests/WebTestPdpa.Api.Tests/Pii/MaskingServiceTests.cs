using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Tests.Fakes;

namespace WebTestPdpa.Api.Tests.Pii;

public class MaskingServiceTests
{
    private readonly InMemoryVaultStore _vault = new();
    private readonly FakePiiDetectionClient _pii = new();
    private readonly VaultCrypto _crypto = TestKeys.NewCrypto();
    private readonly MaskingService _svc;

    public MaskingServiceTests() => _svc = new MaskingService(_pii, _vault, _crypto);

    private Task<MaskResult> Mask(Guid conv, string text, string[]? custom = null, string[]? names = null, bool persist = true) =>
        _svc.MaskAsync(conv, text, custom ?? [], names ?? [], persist, CancellationToken.None);

    [Fact]
    public async Task TokenConsistentAcrossMessages()
    {
        var conv = Guid.NewGuid();
        var a = await Mask(conv, "โทร 081-234-5678 ครับ");
        // 🔍 รูปแบบต่างกัน (canonical เดียวกัน) ต้องได้ token เดิม
        var b = await Mask(conv, "เบอร์ 0812345678");
        Assert.Contains("[PHONE_1]", a.MaskedText);
        Assert.Contains("[PHONE_1]", b.MaskedText);
        Assert.Single(_vault.Entries);
    }

    [Fact]
    public async Task DifferentValueGetsNextIndex()
    {
        var conv = Guid.NewGuid();
        await Mask(conv, "081-234-5678");
        var r = await Mask(conv, "089-999-1234");
        Assert.Equal("[PHONE_2]", r.MaskedText);
    }

    [Fact]
    public async Task DifferentConversationRestartsAtOne()
    {
        await Mask(Guid.NewGuid(), "081-234-5678");
        var r = await Mask(Guid.NewGuid(), "089-999-1234");
        Assert.Equal("[PHONE_1]", r.MaskedText);
    }

    [Fact]
    public async Task CustomValueMasksEveryOccurrence()
    {
        var r = await Mask(Guid.NewGuid(), "โครงการ Alpha และ alpha อีกครั้ง", custom: ["alpha"]);
        Assert.Equal("โครงการ [CUSTOM_1] และ [CUSTOM_1] อีกครั้ง", r.MaskedText);
        Assert.Equal(2, r.MaskCounts["CUSTOM"]);
    }

    [Fact]
    public async Task OverlapResolvedAndNoOriginalLeft()
    {
        _pii.Override = _ => [new(PiiEntityType.Phone, 4, 16, "regex"), new(PiiEntityType.Person, 0, 10, "x")];
        var text = "abcd081-234-5678 end";
        var r = await Mask(Guid.NewGuid(), text);
        Assert.DoesNotContain("081-234-5678", r.MaskedText);
    }

    [Fact]
    public async Task MaskedTextHasNoOriginalValues()
    {
        var text = "สมชาย ใจดี โทร 081-234-5678 mail somchai@example.com บัตร 1101700234568";
        var r = await Mask(Guid.NewGuid(), text, names: ["สมชาย ใจดี"]);
        foreach (var v in new[] { "สมชาย", "081-234-5678", "somchai@example.com", "1101700234568" })
            Assert.DoesNotContain(v, r.MaskedText);
        Assert.True(new OutboundGuard().Check([r.MaskedText]).Passed);
    }

    [Fact]
    public async Task PreviewDoesNotWriteVault()
    {
        var r = await Mask(Guid.NewGuid(), "081-234-5678", persist: false);
        Assert.Equal("[PHONE_1]", r.MaskedText);
        Assert.Empty(_vault.Entries);
    }

    [Fact]
    public async Task PreviewUsesExistingTokenAndNextForNew()
    {
        var conv = Guid.NewGuid();
        await Mask(conv, "081-234-5678");
        var r = await Mask(conv, "081-234-5678 และ 089-999-1234", persist: false);
        Assert.Equal("[PHONE_1] และ [PHONE_2]", r.MaskedText);
        Assert.Single(_vault.Entries);
    }

    [Fact]
    public async Task NullConversationDoesNotTouchVault()
    {
        var r = await _svc.MaskAsync(null, "081-234-5678", [], [], false, CancellationToken.None);
        Assert.Equal("[PHONE_1]", r.MaskedText);
        Assert.Empty(_vault.Entries);
    }

    [Fact]
    public async Task PiiUnavailablePropagates()
    {
        _pii.Throw = true;
        await Assert.ThrowsAsync<PiiUnavailableException>(() => Mask(Guid.NewGuid(), "x"));
    }

    [Fact]
    public async Task InvalidOffsetFromPiiIsFailClosed()
    {
        _pii.Override = _ => [new(PiiEntityType.Phone, 0, 999, "x")];
        await Assert.ThrowsAsync<PiiUnavailableException>(() => Mask(Guid.NewGuid(), "short"));
    }

    [Fact]
    public async Task VaultStoresEncryptedFirstSeenValue()
    {
        var conv = Guid.NewGuid();
        await Mask(conv, "081-234-5678");
        await Mask(conv, "0812345678");
        var e = Assert.Single(_vault.Entries);
        Assert.Equal("081-234-5678", _crypto.Decrypt(e.ValueCipher, conv, e.Token));
    }

    [Fact]
    public async Task ExpandedUnionSpanDoesNotShareTokenWithPlainPhone()
    {
        // ⚠️ N1: union (PHONE + CUSTOM) ต้องไม่ใช้ canonical digits-only ร่วมกับเบอร์ล้วน
        _pii.Override = _ => [new(PiiEntityType.Phone, 4, 16, "regex")];
        var conv = Guid.NewGuid();
        var a = await Mask(conv, "โทร 081-234-5678");
        var b = await Mask(conv, "โทร 081-234-5678 สมศรี", custom: ["5678 สมศรี"]);
        Assert.Equal("โทร [PHONE_1]", a.MaskedText);
        Assert.Equal("โทร [PHONE_2]", b.MaskedText);
        Assert.Equal(2, _vault.Entries.Count);
    }

    [Fact]
    public async Task ExpandedUnionSpanFirstThenPlainPhoneGetDifferentTokens()
    {
        // ⚠️ N1: สลับลำดับ — union มาก่อน แล้วเบอร์ล้วนต้องได้ token ใหม่ และ unmask ไม่ปนกัน
        _pii.Override = _ => [new(PiiEntityType.Phone, 4, 16, "regex")];
        var conv = Guid.NewGuid();
        var a = await Mask(conv, "โทร 081-234-5678 สมศรี", custom: ["5678 สมศรี"]);
        var b = await Mask(conv, "โทร 081-234-5678");
        Assert.Equal("โทร [PHONE_1]", a.MaskedText);
        Assert.Equal("โทร [PHONE_2]", b.MaskedText);
        // 🔍 vault เก็บค่า first-seen ของแต่ละ token แยกกัน
        var first = _vault.Entries.Single(e => e.Token == "[PHONE_1]");
        Assert.Equal("081-234-5678 สมศรี", _crypto.Decrypt(first.ValueCipher, conv, first.Token));
    }

    [Fact]
    public async Task ExpandedUnionSpanSameTextReusesToken()
    {
        // 🔍 N1: union ซ้ำข้อความเดิมยังต้อง consistent (token เดิม)
        _pii.Override = _ => [new(PiiEntityType.Phone, 4, 16, "regex")];
        var conv = Guid.NewGuid();
        var a = await Mask(conv, "โทร 081-234-5678 สมศรี", custom: ["5678 สมศรี"]);
        var b = await Mask(conv, "โทร 081-234-5678  สมศรี", custom: ["5678  สมศรี"]);
        Assert.Equal("โทร [PHONE_1]", a.MaskedText);
        Assert.Equal("โทร [PHONE_1]", b.MaskedText);
    }
}
