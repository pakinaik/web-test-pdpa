using System.Net;
using WebTestPdpa.Api.Options;
using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Tests.Fakes;

namespace WebTestPdpa.Api.Tests.Pii;

/// <summary>🔍 R7c: unmask / guard / pii client / span priority กับ type ใหม่ (D41)</summary>
public class R7TypeBehaviorTests
{
    private readonly InMemoryVaultStore _vault = new();
    private readonly VaultCrypto _crypto = TestKeys.NewCrypto();

    private void Put(Guid conv, PiiEntityType type, int index, string value)
    {
        var token = $"[{PiiEntityTypes.ToWire(type)}_{index}]";
        _vault.Entries.Add(new VaultEntry
        {
            ConversationId = conv, Type = type, TokenIndex = index, Token = token,
            ValueHash = [], ValueCipher = _crypto.Encrypt(value, conv, token)
        });
    }

    [Fact]
    public async Task UnmasksNewTypes_AndDistortedForms()
    {
        var conv = Guid.NewGuid();
        Put(conv, PiiEntityType.Religion, 1, "ศาสนาทดสอบ");
        Put(conv, PiiEntityType.DateOfBirth, 2, "1990-01-02");
        Put(conv, PiiEntityType.BloodType, 1, "AB");
        Put(conv, PiiEntityType.MaritalStatus, 1, "สถานภาพทดสอบ");
        Put(conv, PiiEntityType.EmployeeId, 1, "emp900");
        var u = new UnmaskService(_vault, _crypto);

        Assert.Equal("ศาสนาทดสอบ", await u.UnmaskAsync(conv, "[RELIGION_1]", default));
        Assert.Equal("1990-01-02", await u.UnmaskAsync(conv, "[DATE_OF_BIRTH_2]", default));
        // 🔄 รูปเพี้ยนของ type หลายคำ
        Assert.Equal("AB", await u.UnmaskAsync(conv, "[blood type 1]", default));
        Assert.Equal("AB", await u.UnmaskAsync(conv, "[ BLOOD-TYPE-1 ]", default));
        Assert.Equal("สถานภาพทดสอบ", await u.UnmaskAsync(conv, "[marital status_1]", default));
        Assert.Equal("emp900", await u.UnmaskAsync(conv, "[employee id 1]", default));
        // 🔍 ไม่มีใน vault -> คงไว้
        Assert.Equal("[HEALTH_3]", await u.UnmaskAsync(conv, "[HEALTH_3]", default));
    }

    [Fact]
    public async Task TypeMustMatch_NotUnmaskedAsOtherType()
    {
        var conv = Guid.NewGuid();
        Put(conv, PiiEntityType.Religion, 1, "ศาสนาทดสอบ");
        var u = new UnmaskService(_vault, _crypto);
        Assert.Equal("[ETHNICITY_1]", await u.UnmaskAsync(conv, "[ETHNICITY_1]", default));
        Assert.Equal("[PERSON_1]", await u.UnmaskAsync(conv, "[PERSON_1]", default));
    }

    [Theory]
    [InlineData("[RELIGION_1] [ETHNICITY_2] [HEALTH_1] [BLOOD_TYPE_1] [NATIONALITY_1]")]
    [InlineData("[MARITAL_STATUS_1] [ADDRESS_1] [DATE_OF_BIRTH_1] [EMPLOYEE_ID_12]")]
    [InlineData("เกิด [DATE_OF_BIRTH_1] บ้าน [ADDRESS_2] รหัส [EMPLOYEE_ID_1]")]
    public void Guard_StripsNewTokensBeforeScan(string s) => Assert.True(new OutboundGuard().Check([s]).Passed);

    [Fact]
    public void Guard_StillBlocksRealPiiNextToNewTokens()
    {
        var r = new OutboundGuard().Check(["[RELIGION_1] โทร 081-234-5678"]);
        Assert.False(r.Passed);
        Assert.Contains("PHONE_LIKE", r.Reasons);
    }

    [Theory]
    [InlineData("RELIGION")]
    [InlineData("HEALTH")]
    [InlineData("BLOOD_TYPE")]
    [InlineData("EMPLOYEE_ID")]
    [InlineData("CUSTOM")]
    public async Task PiiServiceReturningNonDetectorType_IsFailClosed(string type)
    {
        var h = FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            "{\"entities\":[{\"type\":\"" + type + "\",\"start\":0,\"end\":1}],\"ner_used\":false}");
        var c = new PiiDetectionClient(new HttpClient(h) { BaseAddress = new Uri("http://pii.test/") },
            Microsoft.Extensions.Options.Options.Create(new PiiOptions { ServiceToken = "test-token", TimeoutSeconds = 5 }));
        var ex = await Assert.ThrowsAsync<PiiUnavailableException>(() => c.DetectAsync("x", [], default));
        Assert.Equal("UNKNOWN_TYPE", ex.ReasonCode);
    }

    [Fact]
    public void SpanResolver_NewTypeBeatsCustom_WhenSameLength()
    {
        var merged = SpanResolver.Merge(
        [
            new MaskSpan(PiiEntityType.Custom, 0, 5, "custom"),
            new MaskSpan(PiiEntityType.Religion, 0, 5, "employee_data")
        ], 10);
        var m = Assert.Single(merged);
        Assert.Equal(PiiEntityType.Religion, m.Type);
    }

    [Fact]
    public void Canonicalizer_NewTypes_UseGenericRule()
    {
        // ⚠️ ค่าเดียวกันต่างตัวพิมพ์/ช่องว่าง = canonical เดียวกัน (token เดียวกัน)
        Assert.Equal(
            ValueCanonicalizer.Canonical(PiiEntityType.Address, "99/1  ถนน  สมมติ"),
            ValueCanonicalizer.Canonical(PiiEntityType.Address, " 99/1 ถนน สมมติ "));
        Assert.Equal("1990-05-12", ValueCanonicalizer.Canonical(PiiEntityType.DateOfBirth, "1990-05-12"));
        Assert.Equal("emp001", ValueCanonicalizer.Canonical(PiiEntityType.EmployeeId, "EMP001"));
    }
}
