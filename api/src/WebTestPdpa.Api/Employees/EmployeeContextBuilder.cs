using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Employees;

/// <summary>📦 ข้อมูลพนักงานที่โหลดแล้ว (เฉพาะ target ที่ได้รับอนุญาต) + ค่าสำหรับ tag ในข้อความผู้ใช้ (D42)</summary>
public sealed class LoadedEmployees
{
    internal List<Employee> Employees { get; init; } = [];
    internal Dictionary<int, List<EmployeeEmergencyContact>> Contacts { get; init; } = [];
    internal Dictionary<int, List<EmployeeSalaryHistory>> Salaries { get; init; } = [];
    internal Dictionary<int, List<EmployeeMonthlyIncome>> Incomes { get; init; } = [];
    internal Dictionary<int, List<EmployeeLeave>> Leaves { get; init; } = [];
    internal Dictionary<int, IncomeType> IncomeTypes { get; init; } = [];
    internal Dictionary<int, LeaveType> LeaveTypes { get; init; } = [];
    internal Dictionary<int, Position> Positions { get; init; } = [];
    internal IReadOnlyList<string> Sections { get; init; } = [];
    internal int? Year { get; init; }

    public IReadOnlyList<int> EmployeeIds => Employees.Select(e => e.Id).ToList();

    /// <summary>🔄 D42: ค่าของ target ที่ใช้ tag ในข้อความผู้ใช้ (preview + send) — ต้องมาจาก target ที่ resolver อนุญาตเท่านั้น (F25)</summary>
    public IReadOnlyList<TypedValue> UserTextValues { get; init; } = [];
}

/// <summary>🔄 context ที่ mask แล้ว — ไม่เก็บใน messages (เก็บใน audit outbound_payload เท่านั้น)</summary>
public sealed record EmployeeContext(string MaskedText, IReadOnlyList<int> EmployeeIds, IReadOnlyList<string> Sections);

/// <summary>
/// 🔄 R7d (D38): สร้างข้อความ context จาก "label + token + ค่า plain ที่อนุญาต" โดย tokenize ค่าจาก field ด้วย type ที่รู้อยู่แล้ว
/// ⚠️ ไม่ประกอบ plaintext context และไม่ผ่าน pii detect — ค่าจริงไม่เคยรวมเป็นข้อความขาออก; ห้าม log ค่า/ข้อความ context
/// </summary>
public sealed class EmployeeContextBuilder
{
    /// <summary>🔍 บรรทัดแรกของ context (mock ใช้ตรวจ; ไม่ match PiiRegex.Token)</summary>
    public const string Marker = "### ข้อมูลพนักงาน (EMPLOYEE_CONTEXT)";
    public const int MaxContextChars = 8000;
    private const string TrimNote = "(ตัดข้อมูลเก่าออกเพื่อจำกัดขนาด)";
    private const string HealthSentinel = "ไม่มี";

    private const string Instruction =
        "ข้อมูลด้านล่างมาจากระบบ HR และเป็นข้อมูลที่ผู้ใช้มีสิทธิ์ดูเท่านั้น ค่ารูป [TYPE_N] คือข้อมูลส่วนบุคคล " +
        "ต้องคงไว้ตามเดิมทุกตัวอักษร ห้ามเดาค่าจริง ถ้าไม่มีข้อมูลให้ตอบว่าไม่มีข้อมูล " +
        "ถ้าต้องใช้ยอดรวมเงินได้ ให้ใช้ตัวเลขจากหัวข้อ \"ยอดรวมเงินได้ (คำนวณโดยระบบ)\" เท่านั้น ห้ามบวกเลขเอง";

    private readonly AppDbContext _db;
    private readonly MaskingService _masking;

    public EmployeeContextBuilder(AppDbContext db, MaskingService masking)
    {
        _db = db;
        _masking = masking;
    }

    // 📦 โหลดเฉพาะ target ids (AsNoTracking) ตาม section ที่ถาม
    public async Task<LoadedEmployees?> LoadAsync(IReadOnlyList<int> ids, IntentResult intent, CancellationToken ct)
    {
        if (ids.Count == 0) return null;
        var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

        var employees = await _db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        if (employees.Count == 0) return null;
        employees = employees.OrderBy(e => order[e.Id]).ToList();

        var has = intent.Sections.ToHashSet(StringComparer.Ordinal);

        var contacts = (await _db.EmployeeEmergencyContacts.AsNoTracking().Where(c => ids.Contains(c.EmployeeId)).OrderBy(c => c.Id).ToListAsync(ct))
            .GroupBy(c => c.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

        var positions = await _db.Positions.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);

        Dictionary<int, List<EmployeeSalaryHistory>> salaries = [];
        if (has.Contains(EmployeeIntent.Salary))
        {
            salaries = (await _db.EmployeeSalaryHistory.AsNoTracking().Where(s => ids.Contains(s.EmployeeId)).OrderBy(s => s.EffectiveDate).ToListAsync(ct))
                .GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        }

        Dictionary<int, List<EmployeeMonthlyIncome>> incomes = [];
        Dictionary<int, IncomeType> incomeTypes = [];
        if (has.Contains(EmployeeIntent.Income))
        {
            var q = _db.EmployeeMonthlyIncomes.AsNoTracking().Where(i => ids.Contains(i.EmployeeId));
            if (intent.Year is { } y)
            {
                var from = new DateOnly(y, 1, 1);
                var to = new DateOnly(y, 12, 31);
                q = q.Where(i => i.PayMonth >= from && i.PayMonth <= to);
            }
            incomes = (await q.OrderBy(i => i.PayMonth).ToListAsync(ct))
                .GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            incomeTypes = await _db.IncomeTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        }

        Dictionary<int, List<EmployeeLeave>> leaves = [];
        Dictionary<int, LeaveType> leaveTypes = [];
        if (has.Contains(EmployeeIntent.Leave))
        {
            var q = _db.EmployeeLeaves.AsNoTracking().Where(l => ids.Contains(l.EmployeeId));
            if (intent.Year is { } y)
            {
                var from = new DateOnly(y, 1, 1);
                var to = new DateOnly(y, 12, 31);
                q = q.Where(l => l.StartDate >= from && l.StartDate <= to);
            }
            leaves = (await q.OrderBy(l => l.StartDate).ToListAsync(ct))
                .GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            leaveTypes = await _db.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        }

        return new LoadedEmployees
        {
            Employees = employees,
            Contacts = contacts,
            Salaries = salaries,
            Incomes = incomes,
            Leaves = leaves,
            IncomeTypes = incomeTypes,
            LeaveTypes = leaveTypes,
            Positions = positions,
            Sections = intent.Sections,
            Year = intent.Year,
            UserTextValues = BuildUserTextValues(employees, contacts)
        };
    }

    // 🔄 D42: ค่าที่ tag ในข้อความผู้ใช้ — ไม่ tag NATIONALITY / MARITAL_STATUS / BLOOD_TYPE (คำทั่วไป); ข้ามค่าที่ยาว < 2
    private static List<TypedValue> BuildUserTextValues(List<Employee> employees, Dictionary<int, List<EmployeeEmergencyContact>> contacts)
    {
        var list = new List<TypedValue>();
        void Add(PiiEntityType type, string? value)
        {
            var v = (value ?? "").Trim();
            if (v.Length >= 2) list.Add(new TypedValue(type, v));
        }

        foreach (var e in employees)
        {
            Add(PiiEntityType.EmployeeId, e.EmployeeCode);
            Add(PiiEntityType.Religion, e.Religion);
            if (!string.Equals(e.ChronicDisease?.Trim(), HealthSentinel, StringComparison.Ordinal)) Add(PiiEntityType.Health, e.ChronicDisease);
            Add(PiiEntityType.Ethnicity, e.Ethnicity);
            Add(PiiEntityType.Address, e.Address);
            Add(PiiEntityType.DateOfBirth, e.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Add(PiiEntityType.Phone, e.Phone);
            Add(PiiEntityType.BankAccount, e.BankAccountNo);
            Add(PiiEntityType.Email, e.Email);
            Add(PiiEntityType.ThaiId, e.NationalId);
            // ⚠️ ชื่อของ target: กันกรณีชื่อไม่อยู่ใน known_names (ไม่ใช่ทุกคนมี user) — เป็น PERSON เหมือนที่ pii จะ mask
            Add(PiiEntityType.Person, $"{e.FirstNameTh} {e.LastNameTh}");
            Add(PiiEntityType.Person, $"{e.FirstNameEn} {e.LastNameEn}");
            if (e.FirstNameTh.Length >= 3) Add(PiiEntityType.Person, e.FirstNameTh);
            if (e.FirstNameEn.Length >= 3) Add(PiiEntityType.Person, e.FirstNameEn);
            foreach (var c in contacts.GetValueOrDefault(e.Id) ?? [])
            {
                Add(PiiEntityType.Person, c.Name);
                Add(PiiEntityType.Phone, c.Phone);
            }
        }
        return list;
    }

    private static string Money(decimal v) => v.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static string Days(decimal d) =>
        d == decimal.Truncate(d) ? decimal.Truncate(d).ToString(CultureInfo.InvariantCulture) : d.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>🔄 สร้าง context (mask ผ่าน TokenizeValuesAsync — HMAC/vault เดิม) persist=true ต้องอยู่ใน transaction เดียวกับข้อความผู้ใช้</summary>
    public async Task<EmployeeContext> BuildAsync(Guid conversationId, LoadedEmployees data, bool persist, CancellationToken ct)
    {
        var has = data.Sections.ToHashSet(StringComparer.Ordinal);
        bool Show(string s) => has.Contains(s);
        var showPosition = Show(EmployeeIntent.Position) || Show(EmployeeIntent.Salary) || Show(EmployeeIntent.Income);

        // 🔍 รวบรวมค่าที่ต้อง mask ตาม section แล้ว tokenize ครั้งเดียว
        var values = new List<TypedValue>();
        void Need(PiiEntityType type, string? v)
        {
            if (!string.IsNullOrWhiteSpace(v)) values.Add(new TypedValue(type, v.Trim()));
        }
        foreach (var e in data.Employees)
        {
            Need(PiiEntityType.EmployeeId, e.EmployeeCode);
            Need(PiiEntityType.Person, $"{e.FirstNameTh} {e.LastNameTh}");
            if (Show(EmployeeIntent.Profile))
            {
                Need(PiiEntityType.Person, $"{e.FirstNameEn} {e.LastNameEn}");
                Need(PiiEntityType.Gender, e.Gender);
                Need(PiiEntityType.DateOfBirth, e.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                Need(PiiEntityType.Nationality, e.Nationality);
                Need(PiiEntityType.MaritalStatus, e.MaritalStatus);
                Need(PiiEntityType.Phone, e.Phone);
                Need(PiiEntityType.Email, e.Email);
                Need(PiiEntityType.ThaiId, e.NationalId);
                Need(PiiEntityType.Address, e.Address);
            }
            if (Show(EmployeeIntent.Sensitive))
            {
                Need(PiiEntityType.Religion, e.Religion);
                Need(PiiEntityType.BloodType, e.BloodType);
                Need(PiiEntityType.Health, e.ChronicDisease);
                Need(PiiEntityType.Ethnicity, e.Ethnicity);
            }
            if (Show(EmployeeIntent.Finance))
            {
                Need(PiiEntityType.BankAccount, e.BankAccountNo);
                Need(PiiEntityType.ThaiId, e.TaxId);
                Need(PiiEntityType.ThaiId, e.SocialSecurityNo);
            }
            if (Show(EmployeeIntent.Emergency))
            {
                foreach (var c in data.Contacts.GetValueOrDefault(e.Id) ?? [])
                {
                    Need(PiiEntityType.Person, c.Name);
                    Need(PiiEntityType.Phone, c.Phone);
                }
            }
        }
        var tokens = await _masking.TokenizeValuesAsync(conversationId, values, persist, ct);
        string Tok(PiiEntityType type, string? v) =>
            string.IsNullOrWhiteSpace(v) ? "-" : tokens[new TypedValue(type, v.Trim())];

        // 🔄 บรรทัดรายเดือนของเงินได้ (plain ตาม D43) — แยกเก็บไว้เพื่อตัดเดือนเก่าเมื่อเกิน cap
        var monthLines = new Dictionary<int, List<(DateOnly Month, string Line)>>();
        foreach (var e in data.Employees)
        {
            var rows = data.Incomes.GetValueOrDefault(e.Id) ?? [];
            var lines = new List<(DateOnly, string)>();
            foreach (var g in rows.GroupBy(r => r.PayMonth).OrderBy(g => g.Key))
            {
                var items = g.Where(r => data.IncomeTypes.ContainsKey(r.IncomeTypeId))
                    .OrderBy(r => CategoryRank(data.IncomeTypes[r.IncomeTypeId].Category)).ThenBy(r => r.IncomeTypeId)
                    .Select(r => $"{data.IncomeTypes[r.IncomeTypeId].NameTh}={Money(r.Amount)}");
                lines.Add((g.Key, $"เดือน {g.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture)}: {string.Join("; ", items)}"));
            }
            monthLines[e.Id] = lines;
        }

        var trimmed = false;
        var text = Compose(data, has, showPosition, Tok, monthLines);
        while (text.Length > MaxContextChars && monthLines.Values.Any(l => l.Count > 0))
        {
            // ⚠️ ตัดเดือนเก่าสุด (ทุกคน) ก่อน
            var oldest = monthLines.Values.Where(l => l.Count > 0).Min(l => l[0].Month);
            foreach (var l in monthLines.Values.Where(l => l.Count > 0 && l[0].Month == oldest)) l.RemoveAt(0);
            trimmed = true;
            text = Compose(data, has, showPosition, Tok, monthLines);
        }
        if (trimmed) text += "\n" + TrimNote;
        if (text.Length > MaxContextChars)
        {
            // ⚠️ ยังเกิน (ไม่น่าเกิดกับข้อมูลจริง) — ตัดที่ท้ายบรรทัดสุดท้ายที่ไม่เกิน cap
            // ⚠️ เผื่อที่ให้ TrimNote เพื่อไม่ให้ข้อมูลท้ายหายเงียบ
            var limit = MaxContextChars - TrimNote.Length - 1;
            var cut = text.LastIndexOf('\n', limit - 1);
            text = (cut > 0 ? text[..cut] : text[..limit]).TrimEnd('\n') + "\n" + TrimNote;
        }

        return new EmployeeContext(text, data.EmployeeIds, data.Sections);
    }

    private static int CategoryRank(string c) => c switch
    {
        IncomeType.Regular => 0,
        IncomeType.Irregular => 1,
        _ => 2
    };

    private static string Compose(LoadedEmployees data, HashSet<string> has, bool showPosition,
        Func<PiiEntityType, string?, string> tok, Dictionary<int, List<(DateOnly Month, string Line)>> monthLines)
    {
        bool Show(string s) => has.Contains(s);
        var sb = new StringBuilder();
        sb.Append(Marker).Append('\n').Append(Instruction);

        foreach (var e in data.Employees)
        {
            sb.Append("\n#### พนักงาน ").Append(tok(PiiEntityType.EmployeeId, e.EmployeeCode));
            sb.Append("\nชื่อ-นามสกุล: ").Append(tok(PiiEntityType.Person, $"{e.FirstNameTh} {e.LastNameTh}"));

            if (Show(EmployeeIntent.Profile))
            {
                sb.Append("\nชื่อภาษาอังกฤษ: ").Append(tok(PiiEntityType.Person, $"{e.FirstNameEn} {e.LastNameEn}"));
                sb.Append("\nเพศ: ").Append(tok(PiiEntityType.Gender, e.Gender))
                  .Append(" | วันเกิด: ").Append(tok(PiiEntityType.DateOfBirth, e.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
                  .Append(" | สัญชาติ: ").Append(tok(PiiEntityType.Nationality, e.Nationality))
                  .Append(" | สถานภาพ: ").Append(tok(PiiEntityType.MaritalStatus, e.MaritalStatus));
                sb.Append("\nเบอร์โทร: ").Append(tok(PiiEntityType.Phone, e.Phone))
                  .Append(" | อีเมล: ").Append(tok(PiiEntityType.Email, e.Email));
                sb.Append("\nเลขบัตรประชาชน: ").Append(tok(PiiEntityType.ThaiId, e.NationalId));
                sb.Append("\nที่อยู่: ").Append(tok(PiiEntityType.Address, e.Address));
                // ⚠️ วันที่ ISO ใช้ตัวคั่น - เท่านั้น (ไม่ใช่เลข 13 หลักติดกัน) และไม่ต่อด้วยตัวเลขอื่น
                sb.Append("\nวันเริ่มงาน: ").Append(e.HireDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            if (showPosition && data.Positions.TryGetValue(e.PositionId, out var pos))
            {
                sb.Append("\nตำแหน่ง: ").Append(pos.NameTh).Append(" | ระดับ: ").Append(pos.Level.ToString(CultureInfo.InvariantCulture))
                  .Append(" | ฝ่าย: ").Append(pos.DepartmentTh);
            }

            if (Show(EmployeeIntent.Sensitive))
            {
                sb.Append("\nศาสนา: ").Append(tok(PiiEntityType.Religion, e.Religion))
                  .Append(" | กรุ๊ปเลือด: ").Append(tok(PiiEntityType.BloodType, e.BloodType))
                  .Append(" | โรคประจำตัว: ").Append(tok(PiiEntityType.Health, e.ChronicDisease))
                  .Append(" | เชื้อชาติ: ").Append(tok(PiiEntityType.Ethnicity, e.Ethnicity));
            }

            if (Show(EmployeeIntent.Finance))
            {
                sb.Append("\nธนาคาร: ").Append(e.BankName)
                  .Append(" | เลขบัญชี: ").Append(tok(PiiEntityType.BankAccount, e.BankAccountNo))
                  .Append(" | เลขประจำตัวผู้เสียภาษี: ").Append(tok(PiiEntityType.ThaiId, e.TaxId))
                  .Append(" | เลขประกันสังคม: ").Append(tok(PiiEntityType.ThaiId, e.SocialSecurityNo));
            }

            if (Show(EmployeeIntent.Emergency))
            {
                var cs = data.Contacts.GetValueOrDefault(e.Id) ?? [];
                if (cs.Count == 0) sb.Append("\nผู้ติดต่อฉุกเฉิน: ไม่มีข้อมูล");
                foreach (var c in cs)
                {
                    sb.Append("\nผู้ติดต่อฉุกเฉิน: ").Append(tok(PiiEntityType.Person, c.Name))
                      .Append(" (ความสัมพันธ์: ").Append(c.Relationship).Append(") เบอร์โทร: ").Append(tok(PiiEntityType.Phone, c.Phone));
                }
            }

            if (Show(EmployeeIntent.Salary))
            {
                sb.Append("\n#### ประวัติเงินเดือน (บาท)");
                var hist = data.Salaries.GetValueOrDefault(e.Id) ?? [];
                if (hist.Count == 0) sb.Append("\nไม่มีข้อมูล");
                foreach (var h in hist)
                {
                    var posName = data.Positions.TryGetValue(h.PositionId, out var hp) ? hp.NameTh : "-";
                    sb.Append("\nมีผลวันที่ ").Append(h.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                      .Append(": เงินเดือน=").Append(Money(h.BaseSalary)).Append("; ตำแหน่ง=").Append(posName);
                }
            }

            if (Show(EmployeeIntent.Income))
            {
                sb.Append("\n#### เงินได้รายเดือน (บาท)");
                var lines = monthLines.GetValueOrDefault(e.Id) ?? [];
                if (lines.Count == 0) sb.Append("\nไม่มีข้อมูล");
                foreach (var l in lines) sb.Append('\n').Append(l.Line);
                AppendIncomeTotals(sb, data, e.Id);
            }

            if (Show(EmployeeIntent.Leave)) AppendLeaves(sb, data, e.Id);
        }
        return sb.ToString();
    }

    // 🔄 R8: ยอดรวมเงินได้คำนวณฝั่ง server (ต่อปี/ต่อประเภท/ต่อกลุ่ม/ทั้งช่วง) — ⚠️ LLM บวกเลขผิดได้ (F28)
    // ⚠️ คำนวณจากข้อมูลที่โหลดทั้งหมด ไม่ขึ้นกับการตัดบรรทัดรายเดือนเมื่อเกิน cap
    private static void AppendIncomeTotals(StringBuilder sb, LoadedEmployees data, int employeeId)
    {
        var rows = (data.Incomes.GetValueOrDefault(employeeId) ?? [])
            .Where(r => data.IncomeTypes.ContainsKey(r.IncomeTypeId)).ToList();
        if (rows.Count == 0) return;

        string Types(IEnumerable<EmployeeMonthlyIncome> rs) => string.Join("; ", rs
            .GroupBy(r => r.IncomeTypeId)
            .OrderBy(g => CategoryRank(data.IncomeTypes[g.Key].Category)).ThenBy(g => g.Key)
            .Select(g => $"{data.IncomeTypes[g.Key].NameTh}={Money(g.Sum(r => r.Amount))}"));
        static string Months(IEnumerable<EmployeeMonthlyIncome> rs) =>
            rs.Select(r => r.PayMonth).Distinct().Count().ToString(CultureInfo.InvariantCulture);

        var first = rows.Min(r => r.PayMonth).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var last = rows.Max(r => r.PayMonth).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        sb.Append("\n#### ยอดรวมเงินได้ (คำนวณโดยระบบ, บาท)");
        sb.Append("\nช่วงข้อมูล ").Append(first).Append(" ถึง ").Append(last).Append(" (").Append(Months(rows)).Append(" เดือน)");
        foreach (var y in rows.GroupBy(r => r.PayMonth.Year).OrderBy(g => g.Key))
        {
            sb.Append("\nปี ").Append(y.Key.ToString(CultureInfo.InvariantCulture))
              .Append(" (").Append(Months(y)).Append(" เดือน): ")
              .Append(Types(y)).Append("; รวม=").Append(Money(y.Sum(r => r.Amount)));
        }
        sb.Append("\nทั้งช่วงข้อมูลแยกประเภท: ").Append(Types(rows));
        sb.Append("\nทั้งช่วงข้อมูลแยกกลุ่ม: ").Append(string.Join("; ", rows
            .GroupBy(r => data.IncomeTypes[r.IncomeTypeId].Category)
            .OrderBy(g => CategoryRank(g.Key))
            .Select(g => $"{CategoryName(g.Key)}={Money(g.Sum(r => r.Amount))}")));
        sb.Append("\nรวมทั้งสิ้น: ").Append(Money(rows.Sum(r => r.Amount)));
    }

    private static string CategoryName(string c) => c switch
    {
        IncomeType.Regular => "เงินได้ประจำ",
        IncomeType.Irregular => "เงินได้ไม่ประจำ",
        _ => "เงินได้พิเศษ"
    };

    // 🔄 สรุปวันลาที่ใช้ต่อปีต่อประเภท (นับเฉพาะ approved; pending แยกบอก) — คำนวณฝั่ง server
    private static void AppendLeaves(StringBuilder sb, LoadedEmployees data, int employeeId)
    {
        var rows = data.Leaves.GetValueOrDefault(employeeId) ?? [];
        var years = rows.Select(r => r.StartDate.Year).Distinct().OrderBy(y => y).ToList();
        if (data.Year is { } fy && !years.Contains(fy)) years = [fy];
        if (years.Count == 0)
        {
            sb.Append("\n#### การลา\nไม่มีข้อมูลการลา");
            return;
        }
        foreach (var y in years)
        {
            sb.Append("\n#### การลา ปี ").Append(y.ToString(CultureInfo.InvariantCulture));
            var inYear = rows.Where(r => r.StartDate.Year == y).ToList();
            if (inYear.Count == 0)
            {
                sb.Append("\nไม่มีข้อมูลการลา");
                continue;
            }
            var parts = new List<string>();
            foreach (var g in inYear.GroupBy(r => r.LeaveTypeId).OrderBy(g => g.Key))
            {
                if (!data.LeaveTypes.TryGetValue(g.Key, out var lt)) continue;
                var used = g.Where(r => r.Status == "approved").Sum(r => r.Days);
                var pending = g.Where(r => r.Status == "pending").Sum(r => r.Days);
                var part = $"{lt.NameTh}: ใช้ {Days(used)} จาก {lt.DaysPerYear.ToString(CultureInfo.InvariantCulture)} วัน";
                if (pending > 0) part += $" (รออนุมัติ {Days(pending)} วัน)";
                parts.Add(part);
            }
            sb.Append('\n').Append(parts.Count == 0 ? "ไม่มีข้อมูลการลา" : string.Join(" | ", parts));
        }
    }
}
