using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data.Entities;

namespace WebTestPdpa.Api.Tests.Integration;

/// <summary>🚀 R7 test support: factory ที่ seed user พนักงาน (รหัสผ่านสุ่มใน test เอง) + login</summary>
public sealed class EmployeeEnv : IAsyncDisposable
{
    public ApiFactory Factory { get; }
    public string Password { get; }

    private EmployeeEnv(ApiFactory f, string pwd)
    {
        Factory = f;
        Password = pwd;
    }

    public static async Task<EmployeeEnv> CreateAsync(PostgresFixture pg, Dictionary<string, string?>? extra = null)
    {
        var pwd = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var cfg = new Dictionary<string, string?>(extra ?? []) { ["EMPLOYEE_SEED_PASSWORD"] = pwd };
        var f = new ApiFactory(await pg.CreateDatabaseAsync(), cfg);
        _ = f.Services;
        return new EmployeeEnv(f, pwd);
    }

    public async Task<HttpClient> LoginAsync(string userName)
    {
        var c = Factory.CreateClient();
        (await c.PostAsJsonAsync("/api/auth/login", new { userName, password = Password })).EnsureSuccessStatusCode();
        return c;
    }

    public Task<Employee> EmployeeAsync(string code) =>
        Factory.WithDb(db => db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == code));

    public async ValueTask DisposeAsync() => await Factory.DisposeAsync();

    /// <summary>🔍 field ค่าคำสั้น (เพศ/สัญชาติ/สถานภาพ/กรุ๊ปเลือด/ศาสนา/เชื้อชาติ) ต้องเป็น token ใน slot ของ label — ไม่ใช่ค่าจริง</summary>
    public static void AssertShortFieldSlotsAreTokens(string contextText)
    {
        foreach (var (label, type) in new[]
        {
            ("เพศ", "GENDER"), ("สัญชาติ", "NATIONALITY"), ("สถานภาพ", "MARITAL_STATUS"),
            ("ศาสนา", "RELIGION"), ("กรุ๊ปเลือด", "BLOOD_TYPE"), ("เชื้อชาติ", "ETHNICITY")
        })
        {
            if (!contextText.Contains(label + ": ", StringComparison.Ordinal)) continue;
            Assert.Matches(label + @": \[" + type + @"_\d+\]", contextText);
        }
    }

    /// <summary>🔍 ค่าที่ต้อง mask ทั้งหมดของพนักงาน (D43) — ใช้ตรวจว่าไม่รั่วใน payload/audit/messages</summary>
    public static IEnumerable<string> SensitiveValues(Employee e, IEnumerable<EmployeeEmergencyContact>? contacts = null)
    {
        var list = new List<string>
        {
            e.FirstNameTh, e.LastNameTh, e.FirstNameEn, e.LastNameEn,
            $"{e.FirstNameTh} {e.LastNameTh}", $"{e.FirstNameEn} {e.LastNameEn}",
            e.NationalId, e.Phone, e.Email, e.Address, e.BankAccountNo,
            e.DateOfBirth.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            e.Religion, e.Ethnicity
        };
        if (e.ChronicDisease != "ไม่มี") list.Add(e.ChronicDisease);
        foreach (var c in contacts ?? []) { list.Add(c.Name); list.Add(c.Phone); }
        // ⚠️ "ไทย" เป็นคำทั่วไปที่ปรากฏใน system prompt ("ภาษาไทย") — ไม่ใช้เป็นตัวตรวจรั่ว
        return list.Where(v => v.Length >= 3 && v != "ไทย");
    }
}
