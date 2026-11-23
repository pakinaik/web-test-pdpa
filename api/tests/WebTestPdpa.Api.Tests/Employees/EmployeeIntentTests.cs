using WebTestPdpa.Api.Employees;

namespace WebTestPdpa.Api.Tests.Employees;

/// <summary>🔍 R7d: keyword intent (D39) — ไม่ตรง = ไม่แนบ context (flow ร้องเรียนเดิม)</summary>
public class EmployeeIntentTests
{
    [Theory]
    [InlineData("ขอดูศาสนาและโรคประจำตัวของฉัน", "sensitive")]
    [InlineData("กรุ๊ปเลือดของฉันคืออะไร", "sensitive")]
    [InlineData("what is my religion", "sensitive")]
    [InlineData("ขอดูข้อมูลสุขภาพของฉัน", "sensitive")]
    [InlineData("ขอข้อมูลส่วนตัวหน่อย", "profile")]
    [InlineData("วันเกิดและที่อยู่ของฉัน", "profile")]
    [InlineData("เบอร์โทรของฉัน", "profile")]
    [InlineData("เลขบัญชีธนาคารของฉัน", "finance")]
    [InlineData("เลขประกันสังคมและภาษี", "finance")]
    [InlineData("ผู้ติดต่อฉุกเฉินของฉันคือใคร", "emergency")]
    [InlineData("ตำแหน่งและระดับของฉัน", "position")]
    [InlineData("เงินเดือนปัจจุบันเท่าไหร่", "salary")]
    [InlineData("ขอดูโบนัสปีที่แล้ว", "income")]
    [InlineData("OT เดือนนี้เท่าไหร่", "income")]
    [InlineData("my overtime last month", "income")]
    [InlineData("ลาพักร้อนไปกี่วัน", "leave")]
    [InlineData("วันลาคงเหลือ", "leave")]
    public void Matches_ExpectedSection(string text, string section) =>
        Assert.Contains(section, EmployeeIntent.Analyze(text).Sections);

    [Theory]
    [InlineData("หัวหน้างานพูดจาไม่สุภาพกับฉัน")]
    [InlineData("ฉันเชื่อว่าระบบเวลาทำงานผิดพลาด")]
    [InlineData("ตลาดปลาใกล้ที่ทำงานเสียงดัง")]
    [InlineData("มีคนเอาของของฉันไป")]
    [InlineData("I want to report a noisy neighbour")]
    [InlineData("hot weather")]
    [InlineData("ถูกหัวหน้าล่วงละเมิดทางเพศในแผนก")]
    [InlineData("เครียดจนสุขภาพแย่เพราะหัวหน้า")]
    public void ComplaintText_HasNoIntent(string text) => Assert.False(EmployeeIntent.Analyze(text).HasIntent);

    [Theory]
    [InlineData("เงินเดือนปี 2026", 2026)]
    [InlineData("รายได้ปี ๒๕๖๘ ของฉัน", 2025)]
    [InlineData("OT ปี พ.ศ. 2567", 2024)]
    [InlineData("โบนัส 2024", 2024)]
    public void YearFilter_BuddhistAndGregorian(string text, int year) => Assert.Equal(year, EmployeeIntent.Analyze(text).Year);

    [Theory]
    [InlineData("เงินเดือนปี 2023")]
    [InlineData("เงินเดือน 20260")]
    [InlineData("เงินเดือน 12026")]
    public void YearOutOfRange_IsIgnored(string text) => Assert.Null(EmployeeIntent.Analyze(text).Year);

    [Theory]
    [InlineData("เงินเดือนของฉัน", true)]
    [InlineData("ขอเบอร์ของผมหน่อย", true)]
    [InlineData("show my salary", true)]
    [InlineData("เงินเดือน emp002", false)]
    public void SelfReference(string text, bool expected) => Assert.Equal(expected, EmployeeIntent.Analyze(text).SelfReference);

    [Fact]
    public void ThaiDigits_AreNormalized_AndSectionOrderIsStable()
    {
        var r = EmployeeIntent.Analyze("ลาพักร้อน เงินเดือน ศาสนา ปี ๒๐๒๖");
        Assert.Equal(2026, r.Year);
        Assert.Equal(["sensitive", "salary", "leave"], r.Sections);
    }
}
