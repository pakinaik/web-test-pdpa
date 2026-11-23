using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Options;

namespace WebTestPdpa.Api.Data;

/// <summary>🚀 migrate + seed roles / admin / test cases ตอน start</summary>
public static class DbInitializer
{
    public static readonly string[] Roles = ["Admin", "Employee", "HR"];

    public static async Task InitializeAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        using var scope = sp.CreateScope();
        var s = scope.ServiceProvider;
        var db = s.GetRequiredService<AppDbContext>();
        var logger = s.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        // 💾 migrate พร้อม retry (db อาจยังไม่พร้อม)
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < 10 && ex is not OperationCanceledException)
            {
                logger.LogWarning("migrate failed attempt={Attempt} type={Type}", attempt, ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        // 💾 seed roles
        var roleMgr = s.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var r in Roles)
        {
            if (!await roleMgr.RoleExistsAsync(r)) await roleMgr.CreateAsync(new ApplicationRole(r));
        }

        // 💾 seed admin จาก env (ถ้ายังไม่มี)
        var admin = s.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        var userMgr = s.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userMgr.FindByNameAsync(admin.UserName) is null)
        {
            var u = new ApplicationUser { UserName = admin.UserName, Email = null, IsActive = true };
            var res = await userMgr.CreateAsync(u, admin.Password);
            if (!res.Succeeded)
            {
                // ⚠️ log เฉพาะรหัส error (ห้ามใส่รหัสผ่าน)
                throw new InvalidOperationException("seed admin ไม่สำเร็จ: " + string.Join(",", res.Errors.Select(e => e.Code)));
            }
            await userMgr.AddToRoleAsync(u, "Admin");
        }

        // 💾 seed pii_test_cases จาก JSON ถ้าตารางว่าง
        if (!await db.PiiTestCases.AnyAsync(ct))
        {
            var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "pii_test_cases.json");
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
                foreach (var c in doc.RootElement.EnumerateArray())
                {
                    db.PiiTestCases.Add(new PiiTestCase
                    {
                        CaseKey = c.GetProperty("caseKey").GetString() ?? "",
                        Lang = c.GetProperty("lang").GetString() ?? "",
                        Category = c.GetProperty("category").GetString() ?? "",
                        Text = c.GetProperty("text").GetString() ?? "",
                        KnownNames = c.GetProperty("knownNames").GetRawText(),
                        Expected = c.GetProperty("expected").GetRawText(),
                        Note = c.TryGetProperty("note", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null
                    });
                }
                await db.SaveChangesAsync(ct);
            }
        }

        // 💾 R7: seed master + พนักงาน (mock)
        await EmployeeSeeder.SeedAsync(db, logger, ct);

        // 💾 R7b: user พนักงาน emp001–emp020 (รหัสผ่านจาก env — ว่าง = ข้าม)
        var empPassword = s.GetRequiredService<IOptions<EmployeeSeedOptions>>().Value.Password;
        await EmployeeSeeder.SeedUsersAsync(db, userMgr, empPassword, logger, ct);
    }
}
