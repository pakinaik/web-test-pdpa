using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data;

namespace WebTestPdpa.Api.Tests.Integration;

/// <summary>💾 R7b: seed user พนักงาน emp001–emp020 + role HR (รหัสผ่านสุ่มใน test เอง)</summary>
[Collection("pg")]
[Trait("Category", "Integration")]
public class EmployeeUserTests
{
    private readonly PostgresFixture _pg;

    public EmployeeUserTests(PostgresFixture pg) => _pg = pg;

    private static string NewPassword() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    private static ApiFactory Factory(string conn, string? password) =>
        new(conn, password is null ? null : new Dictionary<string, string?> { ["EMPLOYEE_SEED_PASSWORD"] = password });

    private static async Task<System.Text.Json.JsonElement> LoginAsync(ApiFactory f, string user, string pwd)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { userName = user, password = pwd });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return await r.JsonAsync();
    }

    [Fact]
    public async Task WithPassword_AllTwentyCanLogin_RolesAndLinks()
    {
        var pwd = NewPassword();
        await using var f = Factory(await _pg.CreateDatabaseAsync(), pwd);
        for (var i = 1; i <= 20; i++)
        {
            var body = await LoginAsync(f, $"emp{i:000}", pwd);
            var roles = body.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).Order().ToList();
            Assert.Equal(i == 20 ? ["Employee", "HR"] : ["Employee"], roles);
        }

        await f.WithDb(async db =>
        {
            var emps = await db.Employees.AsNoTracking().ToListAsync();
            Assert.All(emps, e => Assert.NotNull(e.UserId));
            Assert.Equal(20, emps.Select(e => e.UserId).Distinct().Count());
            // 🔍 username ของ user ที่ผูก = employee_code
            var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.UserName);
            Assert.All(emps, e => Assert.Equal(e.EmployeeCode, users[e.UserId!.Value]));
            return 0;
        });
    }

    [Fact]
    public async Task WithoutPassword_NoEmployeeUsers_ButStartsFine()
    {
        await using var f = Factory(await _pg.CreateDatabaseAsync(), null);
        await f.WithDb(async db =>
        {
            Assert.Equal(0, await db.Users.CountAsync(u => u.UserName!.StartsWith("emp0")));
            Assert.Equal(20, await db.Employees.CountAsync());
            Assert.All(await db.Employees.AsNoTracking().ToListAsync(), e => Assert.Null(e.UserId));
            return 0;
        });
    }

    [Fact]
    public async Task ShortPassword_FailsStart_ErrorNamesKeyNotValue()
    {
        const string shortPwd = "Ab1!xyz";
        await using var f = Factory(await _pg.CreateDatabaseAsync(), shortPwd);
        var ex = Assert.ThrowsAny<Exception>(() => f.Services);
        var text = ex.ToString();
        Assert.Contains("EMPLOYEE_SEED_PASSWORD", text);
        Assert.DoesNotContain(shortPwd, text);
    }

    [Fact]
    public async Task KnownNames_ContainAllTwentyEmployeesThaiAndEnglish()
    {
        var pwd = NewPassword();
        await using var f = Factory(await _pg.CreateDatabaseAsync(), pwd);
        var c = f.CreateClient();
        (await c.PostAsJsonAsync("/api/auth/login", new { userName = "emp001", password = pwd })).EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync("/api/chat/send", new { text = "สวัสดี ทดสอบระบบ" })).EnsureSuccessStatusCode();

        var seen = f.Pii.SeenKnownNames.Last();
        var emps = await f.WithDb(db => db.Employees.AsNoTracking().ToListAsync());
        foreach (var e in emps)
        {
            Assert.Contains($"{e.FirstNameTh} {e.LastNameTh}", seen);
            Assert.Contains($"{e.FirstNameEn} {e.LastNameEn}", seen);
        }
    }

    [Fact]
    public async Task SeedAgain_NoDuplicates_AndPasswordChangeDoesNotOverwriteExistingUsers()
    {
        var conn = await _pg.CreateDatabaseAsync();
        var pwdA = NewPassword();
        await using (var f1 = Factory(conn, pwdA))
        {
            await LoginAsync(f1, "emp003", pwdA);
            await DbInitializer.InitializeAsync(f1.Services);
            Assert.Equal(20, await f1.WithDb(db => db.Users.CountAsync(u => u.UserName!.StartsWith("emp0"))));
        }

        // 🔄 start ใหม่ด้วยรหัสผ่านใหม่ — user เดิมต้องใช้รหัสผ่านเดิมต่อ ไม่ถูกทับ และไม่สร้างซ้ำ
        var pwdB = NewPassword();
        await using var f2 = Factory(conn, pwdB);
        await LoginAsync(f2, "emp003", pwdA);
        var bad = await f2.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = "emp003", password = pwdB });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal(20, await f2.WithDb(db => db.Users.CountAsync(u => u.UserName!.StartsWith("emp0"))));
    }
}
