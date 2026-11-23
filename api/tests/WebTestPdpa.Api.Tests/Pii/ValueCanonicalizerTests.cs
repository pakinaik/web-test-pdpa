using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Pii;

public class ValueCanonicalizerTests
{
    [Theory]
    [InlineData("081-234-5678", "0812345678")]
    [InlineData("+66 81 234 5678", "0812345678")]
    [InlineData("๐๘๑-๒๓๔-๕๖๗๘", "0812345678")]
    public void Phone(string v, string e) => Assert.Equal(e, ValueCanonicalizer.Canonical(PiiEntityType.Phone, v));

    [Fact]
    public void Email() =>
        Assert.Equal("a@b.com", ValueCanonicalizer.Canonical(PiiEntityType.Email, "A (at) B (dot) com"));

    [Fact]
    public void PersonStripsTitle() =>
        Assert.Equal("john  smith".Replace("  ", " "), ValueCanonicalizer.Canonical(PiiEntityType.Person, "Mr. John  Smith"));
}
