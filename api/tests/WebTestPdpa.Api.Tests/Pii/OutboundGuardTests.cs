using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Pii;

public class OutboundGuardTests
{
    private readonly OutboundGuard _g = new();

    [Theory]
    [InlineData("เลข 1101700234568")]
    [InlineData("เลข 1101700234567")]
    [InlineData("เลข ๑๑๐๑๗๐๐๒๓๔๕๖๘")]
    [InlineData("เลข 1-1017-00234-56-8")]
    [InlineData("เลข 1 1017 00234 56 8")]
    [InlineData("เลข 11017\u200b00234568")]
    public void BlocksThaiIdLike(string s)
    {
        var r = _g.Check([s]);
        Assert.False(r.Passed);
        Assert.Contains("THAI_ID_LIKE", r.Reasons);
    }

    [Theory]
    [InlineData("โทร 081-234-5678")]
    [InlineData("โทร ๐๘๑๒๓๔๕๖๗๘")]
    [InlineData("โทร +66 81 234 5678")]
    [InlineData("โทร 02-123-4567")]
    public void BlocksPhone(string s) => Assert.Contains("PHONE_LIKE", _g.Check([s]).Reasons);

    [Theory]
    [InlineData("เบอร์ 081\u00a0234\u00a05678")]
    [InlineData("เบอร์ 081\t234\t5678")]
    [InlineData("เบอร์ 081\n234\n5678")]
    public void BlocksPhoneSeparatedByUnicodeWhitespace(string s) =>
        Assert.Contains("PHONE_LIKE", _g.Check([s]).Reasons);

    [Theory]
    [InlineData("เลข 1\u00a01017\u00a000234\u00a056\u00a08")]
    [InlineData("เลข 1\t1017\t00234\t56\t8")]
    [InlineData("เลข 1\n1017\n00234\n56\n8")]
    public void BlocksThaiIdSeparatedByUnicodeWhitespace(string s) =>
        Assert.Contains("THAI_ID_LIKE", _g.Check([s]).Reasons);

    [Fact]
    public void BlocksEmail() => Assert.Contains("EMAIL_LIKE", _g.Check(["mail a.b@example.com"]).Reasons);

    [Fact]
    public void PassesTokensOnly() =>
        Assert.True(_g.Check(["[PERSON_1] เบอร์ [PHONE_1] [EMAIL_2] [THAI_ID_1]"]).Passed);

    [Fact]
    public void DoesNotBlockMoneyOrDates() =>
        Assert.True(_g.Check(["หักเงิน 25,000 บาท วันที่ 12/03/2567 ยอด 1,250.50"]).Passed);

    [Fact]
    public void ChecksEveryContent()
    {
        var r = _g.Check(["ปกติ", "เบอร์ 0812345678"]);
        Assert.False(r.Passed);
    }

    [Theory]
    [InlineData("เบอร์ 081\u001c234\u001c5678")]
    [InlineData("เบอร์ 081\u001f234\u001d5678")]
    public void BlocksPhoneSeparatedByControlSeparators(string s) =>
        Assert.Contains("PHONE_LIKE", _g.Check([s]).Reasons);

    [Theory]
    [InlineData("เลข 1\u001c1017\u001c00234\u001c56\u001c8")]
    public void BlocksThaiIdSeparatedByControlSeparators(string s) =>
        Assert.Contains("THAI_ID_LIKE", _g.Check([s]).Reasons);

    [Theory]
    [InlineData("tel 081\r\n234\r\n5678")]
    [InlineData("tel 081  234  5678")]
    [InlineData("tel 081 - 234 - 5678")]
    public void BlocksPhoneWithMultiCharSeparators(string s) =>
        Assert.Contains("PHONE_LIKE", _g.Check([s]).Reasons);

    [Theory]
    [InlineData("id 1\r\n1017\r\n00234\r\n56\r\n8")]
    [InlineData("id 1  1017  00234  56  8")]
    [InlineData("id 1 - 1017 - 00234 - 56 - 8")]
    public void BlocksThaiIdWithMultiCharSeparators(string s) =>
        Assert.Contains("THAI_ID_LIKE", _g.Check([s]).Reasons);

    // ⚠️ m-r3-2: ตัวคั่น {0,3} ทำให้ข้อความตัวเลขบางรูปแบบถูก guard block (fail-closed ไม่รั่ว) — ล็อกพฤติกรรมไว้ตาม README
    [Theory]
    [InlineData("ยอด 1234567 - 891011 บาท")]
    [InlineData("รายการ 1. 2. 3. 4. 5. 6. 7. 8. 9. 10. 11.")]
    public void KnownLimitation_NumericSequencesLookingLikeThaiIdAreBlocked(string s)
    {
        var r = _g.Check([s]);
        Assert.False(r.Passed);
        Assert.Contains("THAI_ID_LIKE", r.Reasons);
    }
}
