using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Tests.Data;

namespace WebTestPdpa.Api.Tests.Integration;

/// <summary>💾 R7a: seed ข้อมูลพนักงานลง Postgres จริง + idempotent</summary>
[Collection("pg")]
[Trait("Category", "Integration")]
public class EmployeeSeedTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private ApiFactory _f = null!;

    public EmployeeSeedTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _f = new ApiFactory(await _pg.CreateDatabaseAsync());
        // 🚀 บังคับให้ host start (DbInitializer รันตอนสร้าง host)
        _ = _f.Services;
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<Dictionary<string, int>> CountsAsync(AppDbContext db) => new()
    {
        ["positions"] = await db.Positions.CountAsync(),
        ["leave_types"] = await db.LeaveTypes.CountAsync(),
        ["income_types"] = await db.IncomeTypes.CountAsync(),
        ["employees"] = await db.Employees.CountAsync(),
        ["contacts"] = await db.EmployeeEmergencyContacts.CountAsync(),
        ["salary"] = await db.EmployeeSalaryHistory.CountAsync(),
        ["leaves"] = await db.EmployeeLeaves.CountAsync(),
        ["incomes"] = await db.EmployeeMonthlyIncomes.CountAsync(),
        ["users"] = await db.Users.CountAsync(),
    };

    [Fact]
    public async Task Seed_CreatesMasterAndTwentyEmployees()
    {
        await _f.WithDb(async db =>
        {
            Assert.Equal(12, await db.Positions.CountAsync());
            Assert.Equal(6, await db.LeaveTypes.CountAsync());
            Assert.Equal(9, await db.IncomeTypes.CountAsync());
            Assert.Equal(20, await db.Employees.CountAsync());
            Assert.True(await db.EmployeeEmergencyContacts.CountAsync() >= 20);
            // 🔍 ยังไม่มี user พนักงาน (R7b ต้องตั้ง EMPLOYEE_SEED_PASSWORD)
            Assert.All(await db.Employees.AsNoTracking().ToListAsync(), e => Assert.Null(e.UserId));
            return 0;
        });
    }

    [Fact]
    public async Task Income_EveryoneHasSalary24Months_AndCoverageGroups()
    {
        await _f.WithDb(async db =>
        {
            var salaryId = (await db.IncomeTypes.AsNoTracking().FirstAsync(t => t.Code == "SALARY")).Id;
            var cats = await db.IncomeTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Category);
            var rows = await db.EmployeeMonthlyIncomes.AsNoTracking().ToListAsync();
            var byEmp = rows.GroupBy(r => r.EmployeeId).ToList();
            Assert.Equal(20, byEmp.Count);

            foreach (var g in byEmp)
            {
                var months = g.Where(r => r.IncomeTypeId == salaryId).Select(r => r.PayMonth).OrderBy(x => x).ToList();
                Assert.Equal(24, months.Count);
                Assert.Equal(new DateOnly(2024, 11, 1), months.First());
                Assert.Equal(new DateOnly(2026, 10, 1), months.Last());
                Assert.All(months, m => Assert.Equal(1, m.Day));
            }
            var allThree = byEmp.Count(g => g.Select(r => cats[r.IncomeTypeId]).Distinct().Count() == 3);
            var regularOnly = byEmp.Count(g => g.All(r => cats[r.IncomeTypeId] == IncomeType.Regular));
            Assert.True(allThree >= 5, $"ครบ 3 กลุ่ม={allThree}");
            Assert.True(regularOnly >= 5, $"เฉพาะ regular={regularOnly}");
            return 0;
        });
    }

    [Fact]
    public async Task Salary_FollowsEffectiveSalaryHistory()
    {
        // 🔍 เงินเดือนเดือนก่อน/หลังวันที่มีผลต้องตรงประวัติ (emp001: ขึ้นเงินเดือน 2025-04-01)
        await _f.WithDb(async db =>
        {
            var emp = await db.Employees.AsNoTracking().Include(e => e.SalaryHistory).FirstAsync(e => e.EmployeeCode == "emp001");
            var salaryId = (await db.IncomeTypes.AsNoTracking().FirstAsync(t => t.Code == "SALARY")).Id;
            var hist = emp.SalaryHistory.OrderBy(h => h.EffectiveDate).ToList();
            var before = hist.Last(h => h.EffectiveDate <= new DateOnly(2025, 3, 1)).BaseSalary;
            var after = hist.Last(h => h.EffectiveDate <= new DateOnly(2025, 4, 1)).BaseSalary;
            Assert.NotEqual(before, after);
            decimal Amount(DateOnly m) => db.EmployeeMonthlyIncomes.AsNoTracking()
                .Single(r => r.EmployeeId == emp.Id && r.IncomeTypeId == salaryId && r.PayMonth == m).Amount;
            Assert.Equal(before, Amount(new DateOnly(2025, 3, 1)));
            Assert.Equal(after, Amount(new DateOnly(2025, 4, 1)));
            return 0;
        });
    }

    [Fact]
    public async Task Leaves_AtLeast12EmployeesHave_AndSomeDoNot()
    {
        await _f.WithDb(async db =>
        {
            var withLeave = await db.EmployeeLeaves.AsNoTracking().Select(l => l.EmployeeId).Distinct().CountAsync();
            Assert.True(withLeave >= 12);
            Assert.True(withLeave < 20);
            return 0;
        });
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        var before = await _f.WithDb(CountsAsync);
        // 🔄 เรียก initializer ซ้ำ — จำนวนแถวต้องเท่าเดิมทุกตาราง
        await DbInitializer.InitializeAsync(_f.Services);
        var after = await _f.WithDb(CountsAsync);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task SeededNationalIds_PassChecksum()
    {
        // 🔍 ค่าใน DB ต้องตรงกับไฟล์ seed (checksum ถูกทั้ง 20)
        var ids = await _f.WithDb(async db => await db.Employees.AsNoTracking().Select(e => e.NationalId).ToListAsync());
        Assert.Equal(20, ids.Count);
        Assert.All(ids, id => Assert.True(EmployeeSeedDataTests.ChecksumOk(id)));
    }
}
