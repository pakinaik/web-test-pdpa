using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Pii;

/// <summary>🔄 R7c: type ใหม่ (D41) — wire round-trip และการแยก detector type</summary>
public class PiiEntityTypeTests
{
    [Fact]
    public void EveryType_RoundTripsThroughWire()
    {
        foreach (var t in Enum.GetValues<PiiEntityType>())
        {
            var wire = PiiEntityTypes.ToWire(t);
            Assert.True(PiiEntityTypes.TryParse(wire, out var back), wire);
            Assert.Equal(t, back);
            // 🔍 case-insensitive
            Assert.True(PiiEntityTypes.TryParse(wire.ToLowerInvariant(), out var lower));
            Assert.Equal(t, lower);
        }
    }

    [Theory]
    [InlineData(PiiEntityType.Person, "PERSON")]
    [InlineData(PiiEntityType.Phone, "PHONE")]
    [InlineData(PiiEntityType.ThaiId, "THAI_ID")]
    [InlineData(PiiEntityType.Gender, "GENDER")]
    [InlineData(PiiEntityType.Email, "EMAIL")]
    [InlineData(PiiEntityType.BankAccount, "BANK_ACCOUNT")]
    [InlineData(PiiEntityType.Custom, "CUSTOM")]
    [InlineData(PiiEntityType.Religion, "RELIGION")]
    [InlineData(PiiEntityType.Ethnicity, "ETHNICITY")]
    [InlineData(PiiEntityType.Health, "HEALTH")]
    [InlineData(PiiEntityType.BloodType, "BLOOD_TYPE")]
    [InlineData(PiiEntityType.Nationality, "NATIONALITY")]
    [InlineData(PiiEntityType.MaritalStatus, "MARITAL_STATUS")]
    [InlineData(PiiEntityType.Address, "ADDRESS")]
    [InlineData(PiiEntityType.DateOfBirth, "DATE_OF_BIRTH")]
    [InlineData(PiiEntityType.EmployeeId, "EMPLOYEE_ID")]
    public void WireNames_AreStable(PiiEntityType type, string wire) => Assert.Equal(wire, PiiEntityTypes.ToWire(type));

    [Fact]
    public void ExistingTypes_KeepNumericValues()
    {
        // ⚠️ type เดิมห้ามเปลี่ยนค่า (default(PiiEntityType) = Person)
        Assert.Equal(0, (int)PiiEntityType.Person);
        Assert.Equal(6, (int)PiiEntityType.Custom);
    }

    [Fact]
    public void DetectorTypes_AreOnlyTheOriginalSixDetectorOnes()
    {
        var detector = Enum.GetValues<PiiEntityType>().Where(PiiEntityTypes.IsDetectorType).ToList();
        Assert.Equal(
            [PiiEntityType.Person, PiiEntityType.Phone, PiiEntityType.ThaiId, PiiEntityType.Gender, PiiEntityType.Email, PiiEntityType.BankAccount],
            detector);
        Assert.False(PiiEntityTypes.IsDetectorType(PiiEntityType.Custom));
        Assert.False(PiiEntityTypes.IsDetectorType(PiiEntityType.Religion));
    }

    [Fact]
    public void Unknown_DoesNotParse()
    {
        Assert.False(PiiEntityTypes.TryParse("ALIEN", out _));
        Assert.False(PiiEntityTypes.TryParse(null, out _));
        Assert.False(PiiEntityTypes.TryParse("", out _));
    }
}
