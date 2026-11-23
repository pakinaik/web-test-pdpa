using System.Text.Json;

namespace WebTestPdpa.Api.Tests.Data;

/// <summary>🔍 R7a: ตรวจไฟล์ seed พนักงาน (unit — อ่าน JSON จาก output dir ไม่ใช้ DB)</summary>
public class EmployeeSeedDataTests
{
    private static JsonElement Load(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData", file);
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    private static List<JsonElement> Employees() => Load("employees.json").EnumerateArray().ToList();

    // 🔍 checksum เลขบัตรประชาชนไทย (F5): (11 - Σ d[i]*(13-i) % 11) % 10
    internal static bool ChecksumOk(string id)
    {
        if (id.Length != 13 || !id.All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (id[i] - '0') * (13 - i);
        return (11 - sum % 11) % 10 == id[12] - '0';
    }

    [Fact]
    public void Has20Employees_WithSequentialCodes()
    {
        var emps = Employees();
        Assert.Equal(20, emps.Count);
        for (var i = 0; i < 20; i++) Assert.Equal($"emp{i + 1:000}", emps[i].GetProperty("employeeCode").GetString());
    }

    [Fact]
    public void NationalIds_ValidChecksum_Unique_AndTaxSsnEqualNationalId()
    {
        var emps = Employees();
        var ids = emps.Select(e => e.GetProperty("nationalId").GetString()!).ToList();
        Assert.All(ids, id => Assert.True(ChecksumOk(id)));
        Assert.Equal(20, ids.Distinct().Count());
        foreach (var e in emps)
        {
            Assert.Equal(e.GetProperty("nationalId").GetString(), e.GetProperty("taxId").GetString());
            Assert.Equal(e.GetProperty("nationalId").GetString(), e.GetProperty("socialSecurityNo").GetString());
        }
    }

    [Fact]
    public void Values_DoNotCollideWithE2eOrDataset()
    {
        // ⚠️ F20: ห้ามชนกับค่าที่ e2e / dataset / test เดิมใช้ (ไม่งั้น e2e ขั้นเดิมจะมี target/over-mask)
        var dataset = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SeedData", "pii_test_cases.json"));
        string[] reserved = ["1101700234568", "081-234-5678", "somchai@example.com", "สมชาย", "สมหญิง", "ใจดี", "รักงาน",
            "วิชัย", "มั่นคง", "John", "Smith"];
        foreach (var e in Employees())
        {
            Assert.DoesNotContain(e.GetProperty("nationalId").GetString()!, reserved);
            foreach (var key in new[] { "firstNameTh", "lastNameTh", "firstNameEn", "lastNameEn", "phone", "email", "nationalId", "bankAccountNo" })
            {
                var v = e.GetProperty(key).GetString()!;
                Assert.False(dataset.Contains(v, StringComparison.OrdinalIgnoreCase), $"{e.GetProperty("employeeCode").GetString()}.{key} ชนกับ dataset");
                Assert.DoesNotContain(reserved, r => v.Contains(r, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var c in e.GetProperty("emergencyContacts").EnumerateArray())
            {
                var first = c.GetProperty("name").GetString()!.Split(' ')[0];
                Assert.False(dataset.Contains(first, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void Emails_EndWithExampleDomain_AndUnique()
    {
        var emails = Employees().Select(e => e.GetProperty("email").GetString()!).ToList();
        Assert.All(emails, m => Assert.EndsWith("@example.co.th", m));
        Assert.Equal(20, emails.Distinct().Count());
    }

    [Fact]
    public void FirstNames_UniqueAndAtLeast3Chars()
    {
        // 🔍 pii จับชื่อต้นเดี่ยวที่ยาว ≥ 3 — และ HR resolver ต้องแยกชื่อต้นได้ (ไม่ซ้ำ)
        var firsts = Employees().Select(e => e.GetProperty("firstNameTh").GetString()!).ToList();
        Assert.Equal(20, firsts.Distinct().Count());
        Assert.All(firsts, f => Assert.True(f.Length >= 3));
    }

    [Fact]
    public void Emp020_IsHr_OthersHaveNoExtraRoles()
    {
        var emps = Employees();
        foreach (var e in emps)
        {
            var roles = e.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToList();
            if (e.GetProperty("employeeCode").GetString() == "emp020") Assert.Equal(["HR"], roles);
            else Assert.Empty(roles);
        }
    }

    [Fact]
    public void EveryEmployee_HasSalaryHistoryBeforeIncomeStart_AndEmergencyContact()
    {
        foreach (var e in Employees())
        {
            var hist = e.GetProperty("salaryHistory").EnumerateArray().Select(x => DateOnly.Parse(x.GetProperty("effectiveDate").GetString()!)).ToList();
            Assert.Contains(hist, d => d < new DateOnly(2024, 11, 1));
            Assert.NotEmpty(e.GetProperty("emergencyContacts").EnumerateArray());
        }
    }

    [Fact]
    public void Master_Files_HaveExpectedCodes()
    {
        var leave = Load("leave_types.json").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("daysPerYear").GetInt32());
        Assert.Equal(30, leave["SICK"]);
        Assert.Equal(6, leave["PERSONAL"]);
        Assert.Equal(10, leave["VACATION"]);
        Assert.Equal(98, leave["MATERNITY"]);
        Assert.Equal(15, leave["ORDINATION"]);
        Assert.Equal(60, leave["MILITARY"]);

        var income = Load("income_types.json").EnumerateArray().ToList();
        Assert.Equal(9, income.Count);
        Assert.Equal(["irregular", "regular", "special"], income.Select(x => x.GetProperty("category").GetString()!).Distinct().Order().ToArray());
    }
}
