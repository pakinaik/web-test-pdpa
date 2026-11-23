using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Employees;
using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Tests.Fakes;

namespace WebTestPdpa.Api.Tests.Integration;

/// <summary>🔄 R7d (D38/D43/D46): context สร้างจาก label + token — ไม่มีค่าจริง, guard ผ่าน, ขนาดไม่เกิน cap</summary>
[Collection("pg")]
[Trait("Category", "Integration")]
public class EmployeeContextBuilderTests : IAsyncLifetime
{
    private static readonly string[] AllSections = ["profile", "sensitive", "finance", "emergency", "position", "salary", "income", "leave"];

    private readonly PostgresFixture _pg;
    private EmployeeEnv _env = null!;

    public EmployeeContextBuilderTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync() => _env = await EmployeeEnv.CreateAsync(_pg);
    public async Task DisposeAsync() => await _env.DisposeAsync();

    // 🚀 builder บน DbContext จริง + vault ในหน่วยความจำ (ไม่ใช้ pii จริง)
    private (IServiceScope Scope, EmployeeContextBuilder Builder, MaskingService Masking, InMemoryVaultStore Vault, VaultCrypto Crypto, AppDbContext Db) Create()
    {
        var scope = _env.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vault = new InMemoryVaultStore();
        var crypto = TestKeys.NewCrypto();
        var masking = new MaskingService(new FakePiiDetectionClient(), vault, crypto);
        return (scope, new EmployeeContextBuilder(db, masking), masking, vault, crypto, db);
    }

    private static IntentResult All(int? year = null) => new(AllSections, year, false);

    [Fact]
    public async Task EveryEmployee_AllSections_NoRealValue_GuardPasses_WithinCap()
    {
        var (scope, builder, _, _, _, db) = Create();
        using var _s = scope;
        var emps = await db.Employees.AsNoTracking().OrderBy(e => e.EmployeeCode).ToListAsync();
        var contacts = await db.EmployeeEmergencyContacts.AsNoTracking().ToListAsync();
        var guard = new OutboundGuard();
        Assert.Equal(20, emps.Count);

        foreach (var e in emps)
        {
            var loaded = await builder.LoadAsync([e.Id], All(), default);
            Assert.NotNull(loaded);
            var ctx = await builder.BuildAsync(Guid.NewGuid(), loaded!, persist: true, default);

            Assert.StartsWith(EmployeeContextBuilder.Marker, ctx.MaskedText);
            Assert.True(ctx.MaskedText.Length <= EmployeeContextBuilder.MaxContextChars, $"{e.EmployeeCode} length={ctx.MaskedText.Length}");
            var g = guard.Check([ctx.MaskedText]);
            Assert.True(g.Passed, $"{e.EmployeeCode} guard: {string.Join(",", g.Reasons)}");

            // 🔍 ไม่มีค่าจริงของ field ที่ต้อง mask ปรากฏใน context
            foreach (var v in EmployeeEnv.SensitiveValues(e, contacts.Where(c => c.EmployeeId == e.Id)))
            {
                Assert.False(ctx.MaskedText.Contains(v, StringComparison.OrdinalIgnoreCase), $"{e.EmployeeCode} leaked a masked field (len={v.Length})");
            }
            Assert.DoesNotContain(e.EmployeeCode, ctx.MaskedText);
            EmployeeEnv.AssertShortFieldSlotsAreTokens(ctx.MaskedText);
            Assert.Matches(@"\[PERSON_\d+\]", ctx.MaskedText);
            Assert.Matches(@"\[EMPLOYEE_ID_\d+\]", ctx.MaskedText);
            foreach (var t in new[] { "RELIGION", "ETHNICITY", "HEALTH", "BLOOD_TYPE", "NATIONALITY", "MARITAL_STATUS", "ADDRESS", "DATE_OF_BIRTH", "GENDER", "PHONE", "EMAIL", "THAI_ID", "BANK_ACCOUNT" })
            {
                Assert.Contains($"[{t}_", ctx.MaskedText);
            }
        }
    }

    [Fact]
    public async Task PlainFields_AreReadable_AndMoneyFormatted()
    {
        var (scope, builder, _, _, _, db) = Create();
        using var _s = scope;
        var e = await db.Employees.AsNoTracking().Include(x => x.Position).SingleAsync(x => x.EmployeeCode == "emp001");
        var loaded = await builder.LoadAsync([e.Id], All(2026), default);
        var ctx = await builder.BuildAsync(Guid.NewGuid(), loaded!, true, default);

        Assert.Contains(e.Position!.NameTh, ctx.MaskedText);
        Assert.Contains(e.BankName, ctx.MaskedText);
        Assert.Contains("เดือน 2026-10: เงินเดือน=", ctx.MaskedText);
        Assert.Matches(@"เงินเดือน=\d{1,3}(,\d{3})*\.\d{2}", ctx.MaskedText);
        Assert.Contains("#### การลา ปี 2026", ctx.MaskedText);
        // 🔍 กรองปี: ไม่มีเดือนของปีอื่น
        Assert.DoesNotContain("เดือน 2025-", ctx.MaskedText);
        Assert.DoesNotContain("เดือน 2024-", ctx.MaskedText);
    }

    [Fact]
    public async Task IncomeTotals_ComputedByServer_MatchDb()
    {
        var (scope, builder, _, _, _, db) = Create();
        using var _s = scope;
        var e = await db.Employees.AsNoTracking().SingleAsync(x => x.EmployeeCode == "emp002");
        var rows = await db.EmployeeMonthlyIncomes.AsNoTracking().Where(i => i.EmployeeId == e.Id).ToListAsync();
        var types = await db.IncomeTypes.AsNoTracking().ToDictionaryAsync(t => t.Id);
        static string M(decimal v) => v.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture);

        var loaded = await builder.LoadAsync([e.Id], new IntentResult(["income"], null, false), default);
        var ctx = await builder.BuildAsync(Guid.NewGuid(), loaded!, true, default);
        var totals = ctx.MaskedText.Split("#### ยอดรวมเงินได้ (คำนวณโดยระบบ, บาท)")[1];

        // 🔍 ยอดรวมทั้งสิ้น / ต่อประเภท / ต่อปี ตรงกับผลรวมใน DB
        Assert.Contains($"รวมทั้งสิ้น: {M(rows.Sum(r => r.Amount))}", totals);
        foreach (var g in rows.GroupBy(r => r.IncomeTypeId))
            Assert.Contains($"{types[g.Key].NameTh}={M(g.Sum(r => r.Amount))}", totals.Split("ทั้งช่วงข้อมูลแยกประเภท: ")[1]);
        foreach (var y in rows.GroupBy(r => r.PayMonth.Year))
            Assert.Contains($"รวม={M(y.Sum(r => r.Amount))}", totals.Split($"ปี {y.Key} (")[1].Split('\n')[0]);
        Assert.Contains("ห้ามบวกเลขเอง", ctx.MaskedText);
    }

    [Fact]
    public async Task Leave_UsedDays_ComputedFromApprovedOnly()
    {
        var (scope, builder, _, _, _, db) = Create();
        using var _s = scope;
        var e5 = await db.Employees.AsNoTracking().SingleAsync(x => x.EmployeeCode == "emp005");
        var vac = await db.LeaveTypes.AsNoTracking().SingleAsync(t => t.Code == "VACATION");
        var used = (await db.EmployeeLeaves.AsNoTracking()
            .Where(l => l.EmployeeId == e5.Id && l.LeaveTypeId == vac.Id && l.Status == "approved" && l.StartDate.Year == 2026).ToListAsync()).Sum(l => l.Days);

        var loaded = await builder.LoadAsync([e5.Id], new IntentResult(["leave"], 2026, false), default);
        var ctx = await builder.BuildAsync(Guid.NewGuid(), loaded!, true, default);
        var usedText = used == decimal.Truncate(used) ? decimal.Truncate(used).ToString() : used.ToString("0.0");
        Assert.Contains($"{vac.NameTh}: ใช้ {usedText} จาก {vac.DaysPerYear} วัน", ctx.MaskedText);
        Assert.DoesNotContain("[PERSON_", ctx.MaskedText.Split("#### การลา")[1]);
    }

    [Fact]
    public async Task OnlyRequestedSections_AreIncluded_MinimalData()
    {
        var (scope, builder, _, vault, _, db) = Create();
        using var _s = scope;
        var e = await db.Employees.AsNoTracking().SingleAsync(x => x.EmployeeCode == "emp003");
        var loaded = await builder.LoadAsync([e.Id], new IntentResult(["sensitive"], null, false), default);
        var ctx = await builder.BuildAsync(Guid.NewGuid(), loaded!, true, default);
        Assert.Contains("[RELIGION_", ctx.MaskedText);
        foreach (var t in new[] { "[PHONE_", "[EMAIL_", "[ADDRESS_", "[BANK_ACCOUNT_", "[THAI_ID_", "เงินได้รายเดือน", "การลา" })
        {
            Assert.DoesNotContain(t, ctx.MaskedText);
        }
        // 🔍 vault มีเฉพาะที่ใช้จริง (ไม่ tokenize field ที่ไม่ถูกถาม)
        Assert.DoesNotContain(vault.Entries, v => v.Type is PiiEntityType.Phone or PiiEntityType.Address or PiiEntityType.Email);
    }

    [Fact]
    public async Task ThreeEmployeesAllIncome_ExceedsCap_TrimsOldestMonths()
    {
        var (scope, builder, _, _, _, db) = Create();
        using var _s = scope;
        var ids = await db.Employees.AsNoTracking().Where(e => new[] { "emp001", "emp003", "emp005" }.Contains(e.EmployeeCode)).Select(e => e.Id).ToListAsync();
        var loaded = await builder.LoadAsync(ids, All(), default);
        var ctx = await builder.BuildAsync(Guid.NewGuid(), loaded!, true, default);

        Assert.True(ctx.MaskedText.Length <= EmployeeContextBuilder.MaxContextChars);
        Assert.Contains("(ตัดข้อมูลเก่าออกเพื่อจำกัดขนาด)", ctx.MaskedText);
        // ⚠️ เดือนล่าสุดต้องอยู่ เดือนเก่าสุดถูกตัดก่อน
        Assert.Contains("เดือน 2026-10:", ctx.MaskedText);
        Assert.DoesNotContain("เดือน 2024-11:", ctx.MaskedText);
        Assert.True(new OutboundGuard().Check([ctx.MaskedText]).Passed);
    }

    [Fact]
    public async Task Tokens_AreConsistentWithMaskAsyncOfTypedText_SameConversation()
    {
        var (scope, builder, masking, vault, _, db) = Create();
        using var _s = scope;
        var e = await db.Employees.AsNoTracking().SingleAsync(x => x.EmployeeCode == "emp001");
        var conv = Guid.NewGuid();
        var fullName = $"{e.FirstNameTh} {e.LastNameTh}";

        // 📤 ผู้ใช้พิมพ์ชื่อ + ศาสนา + เบอร์ในข้อความ (typed values เหมือนที่ ChatService ส่ง) — ชื่อมาจาก known_names
        var loaded = await builder.LoadAsync([e.Id], new IntentResult(["profile", "sensitive"], null, false), default);
        var text = $"ชื่อ {fullName} ศาสนา {e.Religion} เบอร์ {e.Phone}";
        var mask = await masking.MaskAsync(conv, text, [], [fullName], loaded!.UserTextValues, persist: true, default);
        var ctx = await builder.BuildAsync(conv, loaded, persist: true, default);

        string TokenOf(PiiEntityType t) => mask.Spans.Single(s => s.Type == t).Token;
        Assert.Contains($"ชื่อ-นามสกุล: {TokenOf(PiiEntityType.Person)}", ctx.MaskedText);
        Assert.Contains($"ศาสนา: {TokenOf(PiiEntityType.Religion)}", ctx.MaskedText);
        Assert.Contains($"เบอร์โทร: {TokenOf(PiiEntityType.Phone)}", ctx.MaskedText);
        Assert.DoesNotContain(e.Religion, mask.MaskedText);
        // 💾 vault: ค่าเดียวกัน = แถวเดียว ไม่ซ้ำ
        Assert.Equal(vault.Entries.Count, vault.Entries.Select(v => v.Token).Distinct().Count());
    }

    [Fact]
    public async Task UserTextValues_ExcludeCommonWordFields_AndSentinelHealth()
    {
        var (scope, builder, _, _, _, db) = Create();
        using var _s = scope;
        var e = await db.Employees.AsNoTracking().SingleAsync(x => x.EmployeeCode == "emp001");
        var loaded = await builder.LoadAsync([e.Id], new IntentResult(["profile"], null, false), default);
        var types = loaded!.UserTextValues.Select(v => v.Type).ToHashSet();
        Assert.DoesNotContain(PiiEntityType.Nationality, types);
        Assert.DoesNotContain(PiiEntityType.MaritalStatus, types);
        Assert.DoesNotContain(PiiEntityType.BloodType, types);
        // 🔍 emp001 chronic_disease = "ไม่มี" (sentinel) ไม่ถูก tag
        Assert.DoesNotContain(PiiEntityType.Health, types);
        Assert.Contains(PiiEntityType.Religion, types);
        Assert.Contains(PiiEntityType.EmployeeId, types);
        Assert.Contains(PiiEntityType.DateOfBirth, types);

        var e3 = await db.Employees.AsNoTracking().SingleAsync(x => x.EmployeeCode == "emp003");
        var l3 = await builder.LoadAsync([e3.Id], new IntentResult(["profile"], null, false), default);
        Assert.Contains(l3!.UserTextValues, v => v.Type == PiiEntityType.Health && v.Value == e3.ChronicDisease);
    }

    [Fact]
    public async Task NoTargets_ReturnsNull()
    {
        var (scope, builder, _, _, _, _) = Create();
        using var _s = scope;
        Assert.Null(await builder.LoadAsync([], All(), default));
        Assert.Null(await builder.LoadAsync([99999], All(), default));
    }

    [Fact]
    public async Task OverCap_WithoutIncome_FallbackCutAppendsTrimNote()
    {
        var (scope, builder, _, _, _, db) = Create();
        using var _s = scope;
        // 📦 ถามทุกคน (20) ทุก section ยกเว้น income → เกิน cap แน่นอน ไม่มีเดือนให้ตัด → เข้า fallback
        var ids = await db.Employees.AsNoTracking().OrderBy(e => e.EmployeeCode).Select(e => e.Id).ToListAsync();
        var sections = AllSections.Where(x => x != "income").ToArray();
        var loaded = await builder.LoadAsync(ids, new IntentResult(sections, null, false), default);
        var ctx = await builder.BuildAsync(Guid.NewGuid(), loaded!, persist: false, default);

        // 🔍 ไม่เกิน cap และมี TrimNote ต่อท้าย (ข้อมูลท้ายหายต้องบอกชัด)
        Assert.True(ctx.MaskedText.Length <= EmployeeContextBuilder.MaxContextChars, $"length={ctx.MaskedText.Length}");
        Assert.EndsWith("(ตัดข้อมูลเก่าออกเพื่อจำกัดขนาด)", ctx.MaskedText);
    }
}
