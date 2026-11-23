using System.Text.RegularExpressions;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Employees;

/// <summary>🔍 ผลวิเคราะห์ข้อความ: section ที่ผู้ใช้ถามถึง + ปีที่ระบุ (ค.ศ.) + อ้างถึงตัวเองหรือไม่</summary>
public sealed record IntentResult(IReadOnlyList<string> Sections, int? Year, bool SelfReference)
{
    /// <summary>🔍 true = ถามข้อมูลพนักงาน (มี keyword อย่างน้อย 1 section)</summary>
    public bool HasIntent => Sections.Count > 0;
}

/// <summary>
/// 🔍 R7d (D39): จัดข้อความผู้ใช้เป็น section ข้อมูลพนักงานด้วย keyword ไทย/อังกฤษ (ไม่ใช้ LLM)
/// ⚠️ ไม่ตรง keyword = ไม่แนบ context → flow แชทร้องเรียนเดิมไม่เปลี่ยน; regex ทุกตัวมี match timeout
/// </summary>
public static class EmployeeIntent
{
    public const string Profile = "profile";
    public const string Sensitive = "sensitive";
    public const string Finance = "finance";
    public const string Emergency = "emergency";
    public const string Position = "position";
    public const string Salary = "salary";
    public const string Income = "income";
    public const string Leave = "leave";

    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static Regex Rx(string pattern) => new(pattern, Opts, PiiRegex.Timeout);

    // 🔄 ลำดับ section คงที่ (ใช้ลำดับนี้ใน context และ audit)
    private static readonly (string Section, Regex Rx)[] Rules =
    [
        // ⚠️ ไม่ใช้ "เพศ" / "อายุ" / "แผนก" เดี่ยวๆ — ชนกับแชทร้องเรียน (เช่น ล่วงละเมิดทางเพศในแผนก) ไม่ควรแนบ context
        // ⚠️ "ชื่อ" ต้องไม่ใช่ส่วนหนึ่งของ "เชื่อ" (เ + ชื่อ)
        (Profile, Rx(@"ข้อมูลส่วนตัว|ข้อมูลพนักงาน|(?<!เ)ชื่อ|นามสกุล|วันเกิด|ที่อยู่|สัญชาติ|สถานภาพ|สมรส|อีเมล|เบอร์|โทรศัพท์|บัตรประชาชน|เลขบัตร|รหัสพนักงาน|วันเริ่มงาน|อายุงาน|(?<![a-z])(?:name|birth|birthday|address|nationality|marital|email|phone|gender|national id|profile)(?![a-z])")),
        // ⚠️ ไม่ใช้ "สุขภาพ" เดี่ยวๆ — ชนกับแชทร้องเรียน (เช่น เครียดจนสุขภาพแย่เพราะหัวหน้า) ใช้คำเจาะจงแทน
        (Sensitive, Rx(@"ศาสนา|กรุ๊ปเลือด|กรุ๊บเลือด|หมู่เลือด|โรคประจำตัว|ประวัติสุขภาพ|ข้อมูลสุขภาพ|เชื้อชาติ|(?<![a-z])(?:religion|blood|disease|chronic|health|ethnic\w*)(?![a-z])")),
        (Finance, Rx(@"ธนาคาร|เลขบัญชี|บัญชีธนาคาร|ภาษี|ประกันสังคม|(?<![a-z])(?:bank|tax|social security|account (?:no|number))(?![a-z])")),
        (Emergency, Rx(@"ฉุกเฉิน|ผู้ติดต่อ|(?<![a-z])emergency(?![a-z])")),
        (Position, Rx(@"ตำแหน่ง|ระดับ|ฝ่าย|(?<![a-z])(?:position|job title|department|grade)(?![a-z])")),
        (Salary, Rx(@"เงินเดือน|ขึ้นเงินเดือน|ประวัติเงินเดือน|(?<![a-z])salary(?![a-z])")),
        (Income, Rx(@"เงินได้|รายได้|โบนัส|ค่ากะ|เบี้ยเลี้ยง|ค่าคอม|ค่าตำแหน่ง|ค่าครองชีพ|ค่าล่วงเวลา|เงินรางวัล|สลิป|(?<![a-z])(?:ot|income|bonus|overtime|commission|allowance|per diem|payslip|earnings)(?![a-z])")),
        // ⚠️ ไม่ใช้ "ลา" เดี่ยวๆ (ชนกับ เวลา/ตลาด/ปลา) — ใช้คำเฉพาะของการลา
        (Leave, Rx(@"วันลา|ลาป่วย|ลากิจ|ลาพัก|พักร้อน|ลาคลอด|ลาบวช|ลาไป|ใบลา|การลา|ลางาน|ขอลา|สิทธิ์ลา|สิทธิลา|(?<![a-z])(?:leave|vacation|sick day)(?![a-z])")),
    ];

    // 🔍 ปี ค.ศ. 2024–2026 / พ.ศ. 2567–2569
    private static readonly Regex YearRx = Rx(@"(?<![0-9])(202[4-6]|256[7-9])(?![0-9])");

    private static readonly Regex SelfRx = Rx(@"ของฉัน|ของผม|ของดิฉัน|ของหนู|ตัวเอง|(?<![a-z])my(?![a-z])");

    public static IntentResult Analyze(string text)
    {
        // 🔄 normalize (เลขไทย / zero-width / whitespace) ก่อน match
        var t = TextNormalizer.Normalize(text);

        var sections = new List<string>();
        foreach (var (section, rx) in Rules)
        {
            if (rx.IsMatch(t)) sections.Add(section);
        }

        int? year = null;
        var m = YearRx.Match(t);
        if (m.Success)
        {
            var y = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            year = y >= 2500 ? y - 543 : y;
        }

        return new IntentResult(sections, year, SelfRx.IsMatch(t));
    }
}
