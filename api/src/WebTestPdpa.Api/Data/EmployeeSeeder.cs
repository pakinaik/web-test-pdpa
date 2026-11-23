using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data.Entities;

namespace WebTestPdpa.Api.Data;

/// <summary>
/// 🚀 R7 (D35): seed master + พนักงาน (mock) จาก SeedData/*.json ตอน start — idempotent, ห้าม HasData
/// ⚠️ log เฉพาะจำนวนแถว — ห้าม log ค่าใดๆ ของพนักงาน; error ใช้ข้อความคงที่ + code ของ master เท่านั้น
/// </summary>
public static class EmployeeSeeder
{
    /// <summary>🔍 ช่วงเงินได้รายเดือนคงที่ 24 เดือน 2024-11..2026-10 (D35 / Q8) — deterministic ให้ test อ้างอิงได้</summary>
    public static readonly DateOnly IncomeStartMonth = new(2024, 11, 1);
    public const int IncomeMonthCount = 24;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // 🔄 รูปแบบไฟล์ seed (ดู findings F19)
    private sealed record PositionSeed(string Code, string NameTh, string NameEn, short Level, string DepartmentTh);
    private sealed record LeaveTypeSeed(string Code, string NameTh, string NameEn, short DaysPerYear, bool IsPaid);
    private sealed record IncomeTypeSeed(string Code, string NameTh, string NameEn, string Category);
    private sealed record ContactSeed(string Name, string Relationship, string Phone);
    private sealed record SalarySeed(DateOnly EffectiveDate, decimal BaseSalary, string PositionCode);
    private sealed record RecurringSeed(string Type, decimal Amount);
    private sealed record PeriodicSeed(string Type, int[] Months, decimal Amount);
    private sealed record IncomeProfileSeed(
        List<RecurringSeed>? Recurring, List<PeriodicSeed>? Irregular, List<PeriodicSeed>? Special);
    private sealed record LeaveSeed(string EmployeeCode, string LeaveTypeCode, DateOnly StartDate, DateOnly EndDate,
        decimal Days, string Status);

    private sealed record EmployeeSeed(
        string EmployeeCode, string[]? Roles,
        string TitleTh, string FirstNameTh, string LastNameTh, string TitleEn, string FirstNameEn, string LastNameEn,
        string NationalId, string TaxId, string SocialSecurityNo, string Phone, string Email, string Gender,
        DateOnly DateOfBirth, string Address, string Nationality, string MaritalStatus, string Religion,
        string BloodType, string ChronicDisease, string Ethnicity, string BankName, string BankAccountNo,
        string PositionCode, DateOnly HireDate,
        List<ContactSeed>? EmergencyContacts, List<SalarySeed>? SalaryHistory, IncomeProfileSeed? IncomeProfile);

    // 📦 อ่านไฟล์ seed (ไฟล์หาย = build ผิดพลาด → fail ด้วยข้อความคงที่)
    private static async Task<List<T>> ReadAsync<T>(string file, CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData", file);
        if (!File.Exists(path)) throw new InvalidOperationException("seed employee ไม่สำเร็จ: ไม่พบไฟล์ " + file);
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<T>>(fs, JsonOpts, ct)
               ?? throw new InvalidOperationException("seed employee ไม่สำเร็จ: ไฟล์ว่าง " + file);
    }

    private static InvalidOperationException Fail(string what) => new("seed employee ไม่สำเร็จ: " + what);

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        // 💾 master: seed ต่อตารางเมื่อตารางว่าง
        if (!await db.Positions.AnyAsync(ct))
        {
            foreach (var p in await ReadAsync<PositionSeed>("positions.json", ct))
            {
                db.Positions.Add(new Position { Code = p.Code, NameTh = p.NameTh, NameEn = p.NameEn, Level = p.Level, DepartmentTh = p.DepartmentTh });
            }
        }
        if (!await db.LeaveTypes.AnyAsync(ct))
        {
            foreach (var l in await ReadAsync<LeaveTypeSeed>("leave_types.json", ct))
            {
                db.LeaveTypes.Add(new LeaveType { Code = l.Code, NameTh = l.NameTh, NameEn = l.NameEn, DaysPerYear = l.DaysPerYear, IsPaid = l.IsPaid });
            }
        }
        if (!await db.IncomeTypes.AnyAsync(ct))
        {
            foreach (var i in await ReadAsync<IncomeTypeSeed>("income_types.json", ct))
            {
                db.IncomeTypes.Add(new IncomeType { Code = i.Code, NameTh = i.NameTh, NameEn = i.NameEn, Category = i.Category });
            }
        }
        await db.SaveChangesAsync(ct);

        // 💾 พนักงาน + ตารางลูกทั้งหมด ใน transaction เดียว (เมื่อ employees ว่าง)
        if (await db.Employees.AnyAsync(ct)) return;

        var positions = await db.Positions.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, ct);
        var incomeTypes = await db.IncomeTypes.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, ct);
        var leaveTypes = await db.LeaveTypes.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        int Pos(string code) => positions.TryGetValue(code, out var id) ? id : throw Fail("position " + code);
        int Inc(string code) => incomeTypes.TryGetValue(code, out var id) ? id : throw Fail("income_type " + code);

        var seeds = await ReadAsync<EmployeeSeed>("employees.json", ct);
        var leaves = await ReadAsync<LeaveSeed>("employee_leaves.json", ct);
        var entities = new Dictionary<string, Employee>(StringComparer.Ordinal);

        foreach (var s in seeds)
        {
            var emp = new Employee
            {
                EmployeeCode = s.EmployeeCode,
                TitleTh = s.TitleTh, FirstNameTh = s.FirstNameTh, LastNameTh = s.LastNameTh,
                TitleEn = s.TitleEn, FirstNameEn = s.FirstNameEn, LastNameEn = s.LastNameEn,
                NationalId = s.NationalId, TaxId = s.TaxId, SocialSecurityNo = s.SocialSecurityNo,
                Phone = s.Phone, Email = s.Email, Gender = s.Gender, DateOfBirth = s.DateOfBirth,
                Address = s.Address, Nationality = s.Nationality, MaritalStatus = s.MaritalStatus,
                Religion = s.Religion, BloodType = s.BloodType, ChronicDisease = s.ChronicDisease,
                Ethnicity = s.Ethnicity, BankName = s.BankName, BankAccountNo = s.BankAccountNo,
                PositionId = Pos(s.PositionCode), HireDate = s.HireDate, IsActive = true
            };
            foreach (var c in s.EmergencyContacts ?? [])
            {
                emp.EmergencyContacts.Add(new EmployeeEmergencyContact { Name = c.Name, Relationship = c.Relationship, Phone = c.Phone });
            }
            var salaries = (s.SalaryHistory ?? []).OrderBy(x => x.EffectiveDate).ToList();
            foreach (var h in salaries)
            {
                emp.SalaryHistory.Add(new EmployeeSalaryHistory { EffectiveDate = h.EffectiveDate, BaseSalary = h.BaseSalary, PositionId = Pos(h.PositionCode) });
            }
            ExpandIncomes(emp, s.IncomeProfile, salaries.Select(x => (x.EffectiveDate, x.BaseSalary)).ToList(), Inc);
            if (!entities.TryAdd(s.EmployeeCode, emp)) throw Fail("employee ซ้ำ");
        }

        foreach (var l in leaves)
        {
            if (!entities.TryGetValue(l.EmployeeCode, out var emp)) throw Fail("leave อ้างพนักงานที่ไม่มี " + l.EmployeeCode);
            if (!leaveTypes.TryGetValue(l.LeaveTypeCode, out var ltId)) throw Fail("leave_type " + l.LeaveTypeCode);
            emp.Leaves.Add(new EmployeeLeave { LeaveTypeId = ltId, StartDate = l.StartDate, EndDate = l.EndDate, Days = l.Days, Status = l.Status });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Employees.AddRange(entities.Values);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // ⚠️ log เฉพาะจำนวน (ห้ามมีค่าของพนักงาน)
        logger.LogInformation("employee seed employees={E} incomeRows={I} leaves={L}",
            entities.Count, entities.Values.Sum(e => e.MonthlyIncomes.Count), entities.Values.Sum(e => e.Leaves.Count));
    }

    /// <summary>
    /// 💾 R7b: สร้าง user พนักงาน (username = employee_code) แล้วผูก employees.user_id — idempotent
    /// ⚠️ password ว่าง = ข้าม; user ที่มีอยู่แล้วไม่ถูกเปลี่ยนรหัสผ่าน/role; error ใช้ code เท่านั้น (ไม่มีรหัสผ่าน/ชื่อ)
    /// </summary>
    public static async Task SeedUsersAsync(AppDbContext db, UserManager<ApplicationUser> users, string password,
        ILogger logger, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password))
        {
            logger.LogWarning("EMPLOYEE_SEED_PASSWORD ไม่ได้ตั้งค่า — ข้าม seed user พนักงาน");
            return;
        }

        // 📦 role เพิ่มเติมรายคนจาก employees.json (default emp020 = HR)
        var extraRoles = (await ReadAsync<EmployeeSeed>("employees.json", ct))
            .ToDictionary(x => x.EmployeeCode, x => x.Roles ?? [], StringComparer.Ordinal);

        var created = 0;
        var linked = 0;
        var linkedExisting = 0;
        foreach (var emp in await db.Employees.OrderBy(x => x.EmployeeCode).ToListAsync(ct))
        {
            var user = await users.FindByNameAsync(emp.EmployeeCode);
            var existed = user is not null;
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = emp.EmployeeCode,
                    FirstNameTh = emp.FirstNameTh,
                    LastNameTh = emp.LastNameTh,
                    FirstNameEn = emp.FirstNameEn,
                    LastNameEn = emp.LastNameEn,
                    IsActive = true
                };
                var res = await users.CreateAsync(user, password);
                if (!res.Succeeded)
                {
                    // ⚠️ เฉพาะ error code (ห้ามใส่รหัสผ่าน/ชื่อ)
                    throw Fail("user " + emp.EmployeeCode + " " + string.Join(",", res.Errors.Select(e => e.Code)));
                }
                var roles = new List<string> { "Employee" };
                roles.AddRange((extraRoles.GetValueOrDefault(emp.EmployeeCode) ?? []).Where(r => r != "Employee"));
                var addRes = await users.AddToRolesAsync(user, roles);
                if (!addRes.Succeeded)
                {
                    throw Fail("role " + emp.EmployeeCode + " " + string.Join(",", addRes.Errors.Select(e => e.Code)));
                }
                created++;
            }

            // 🔄 ผูก user กับพนักงานถ้ายังไม่ผูก
            if (emp.UserId is null)
            {
                emp.UserId = user.Id;
                linked++;
                if (existed) linkedExisting++;
            }
        }
        await db.SaveChangesAsync(ct);

        // ⚠️ log เฉพาะจำนวน
        logger.LogInformation("employee user seed created={Created} linked={Linked}", created, linked);

        // ⚠️ ผูกกับ user ชื่อ empNNN ที่มีอยู่ก่อนแล้ว (ไม่ได้สร้างโดย seeder) — แจ้งเป็นจำนวนอย่างเดียว
        if (linkedExisting > 0)
        {
            logger.LogWarning("employee user seed linked pre-existing users count={Count}", linkedExisting);
        }
    }

    // 🔄 ขยาย incomeProfile -> employee_monthly_incomes 24 เดือน (SALARY = ประวัติเงินเดือนที่มีผล ณ เดือนนั้น)
    private static void ExpandIncomes(Employee emp, IncomeProfileSeed? profile,
        IReadOnlyList<(DateOnly Effective, decimal Salary)> salaries, Func<string, int> incomeTypeId)
    {
        var salaryTypeId = incomeTypeId("SALARY");
        var used = new HashSet<(int, DateOnly)>();

        void Add(int typeId, DateOnly month, decimal amount)
        {
            // 🔍 unique (employee, type, month) — ซ้ำใน profile = ข้าม ไม่ให้ชน unique index
            if (used.Add((typeId, month)))
            {
                emp.MonthlyIncomes.Add(new EmployeeMonthlyIncome { IncomeTypeId = typeId, PayMonth = month, Amount = decimal.Round(amount, 2) });
            }
        }

        for (var k = 0; k < IncomeMonthCount; k++)
        {
            var month = IncomeStartMonth.AddMonths(k);

            // 🔍 SALARY: แถวล่าสุดที่มีผลก่อนหรือตรงวันที่ 1 ของเดือน (ไม่มี = seed ผิด)
            var eff = salaries.Where(x => x.Effective <= month).ToList();
            if (eff.Count == 0) throw Fail("salary_history ไม่ครอบคลุมเดือนเริ่มต้น " + emp.EmployeeCode);
            Add(salaryTypeId, month, eff[^1].Salary);

            foreach (var r in profile?.Recurring ?? []) Add(incomeTypeId(r.Type), month, r.Amount);
            foreach (var p in (profile?.Irregular ?? []).Concat(profile?.Special ?? []))
            {
                if (p.Months.Contains(month.Month)) Add(incomeTypeId(p.Type), month, p.Amount);
            }
        }
    }
}
