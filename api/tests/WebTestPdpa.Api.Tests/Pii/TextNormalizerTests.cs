using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Pii;

public class TextNormalizerTests
{
    [Fact]
    public void ThaiAndFullWidthDigits() =>
        Assert.Equal("081-234-5678 0123456789", TextNormalizer.Normalize("๐๘๑-๒๓๔-๕๖๗๘ ０１２３４５６７８９"));

    [Fact]
    public void ZeroWidthRemoved() =>
        Assert.Equal("0812345678", TextNormalizer.Normalize("08\u200b1\u200c23\u200d45\u206067\uFEFF8"));

    [Fact]
    public void WhitespaceVariantsBecomeSpaceOneToOne() =>
        Assert.Equal("081 234 567 8", TextNormalizer.Normalize("081\u00a0234\t567\n8"));

    [Fact]
    public void ThaiVowelsUntouched() => Assert.Equal("ทำงาน", TextNormalizer.Normalize("ทำงาน"));

    [Fact]
    public void ControlSeparators1CTo1FBecomeSpaceOneToOne() =>
        Assert.Equal("081 234 567 8 9", TextNormalizer.Normalize("081\u001c234\u001d567\u001e8\u001f9"));
}
