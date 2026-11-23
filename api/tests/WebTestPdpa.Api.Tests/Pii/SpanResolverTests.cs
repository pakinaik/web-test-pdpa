using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Pii;

public class SpanResolverTests
{
    [Fact]
    public void LongerWins()
    {
        var r = SpanResolver.Merge([new(PiiEntityType.Phone, 0, 5, "a"), new(PiiEntityType.Person, 0, 9, "b")], 20);
        Assert.Single(r);
        Assert.Equal(PiiEntityType.Person, r[0].Type);
    }

    [Fact]
    public void PriorityWhenEqualLength()
    {
        var r = SpanResolver.Merge([new(PiiEntityType.Custom, 0, 10, "c"), new(PiiEntityType.ThaiId, 0, 10, "d")], 20);
        Assert.Equal(PiiEntityType.ThaiId, Assert.Single(r).Type);
    }

    [Fact]
    public void BankBeatsPhone()
    {
        var r = SpanResolver.Merge([new(PiiEntityType.Phone, 0, 12, "a"), new(PiiEntityType.BankAccount, 0, 10, "b")], 20);
        // 🔍 D27: union -> ช่วง [0,12] ประเภท BANK_ACCOUNT
        var span = Assert.Single(r);
        Assert.Equal(PiiEntityType.BankAccount, span.Type);
        Assert.Equal((0, 12), (span.Start, span.End));
    }

    [Fact]
    public void TransitiveOverlapCollapsesToOneUnionSpan()
    {
        // 🔍 B1: CUSTOM[9,23] + PERSON[0,13] + PHONE[20,32] ต้องได้ 1 span [0,32]
        var r = SpanResolver.Merge(
            [new(PiiEntityType.Custom, 9, 23, "c"), new(PiiEntityType.Person, 0, 13, "p"), new(PiiEntityType.Phone, 20, 32, "r")], 40);
        var span = Assert.Single(r);
        Assert.Equal((0, 32), (span.Start, span.End));
    }

    [Fact]
    public void MaskedTextHasNoLeakedPartsOfLosingSpans()
    {
        // 🔍 B1: masked text ต้องไม่เหลือ "สมชาย" หรือ "5678"
        const string text = "คุณสมชาย ใจดี เบอร์ 081-234-5678";
        var customStart = text.IndexOf("ใจดี", StringComparison.Ordinal);
        var customEnd = text.IndexOf("081", StringComparison.Ordinal) + 3;
        var phoneStart = text.IndexOf("081", StringComparison.Ordinal);
        var r = SpanResolver.Merge(
            [new(PiiEntityType.Custom, customStart, customEnd, "c"),
             new(PiiEntityType.Person, 0, customStart + "ใจดี".Length, "p"),
             new(PiiEntityType.Phone, phoneStart, text.Length, "r")], text.Length);
        var span = Assert.Single(r);
        var masked = text[..span.Start] + "[X]" + text[span.End..];
        Assert.DoesNotContain("สมชาย", masked);
        Assert.DoesNotContain("5678", masked);
    }

    [Fact]
    public void SortedNonOverlapping()
    {
        var r = SpanResolver.Merge([new(PiiEntityType.Email, 10, 15, "a"), new(PiiEntityType.Phone, 0, 5, "b")], 20);
        Assert.Equal([0, 10], r.Select(s => s.Start));
    }

    [Theory]
    [InlineData(-1, 3, 10)]
    [InlineData(3, 3, 10)]
    [InlineData(0, 11, 10)]
    public void InvalidOffsetThrows(int s, int e, int len) =>
        Assert.Throws<PiiUnavailableException>(() => SpanResolver.Merge([new(PiiEntityType.Phone, s, e, "x")], len));
}
