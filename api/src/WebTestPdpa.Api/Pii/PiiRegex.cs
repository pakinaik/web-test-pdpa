using System.Text.RegularExpressions;

namespace WebTestPdpa.Api.Pii;

/// <summary>⚠️ regex กลาง — ทุกตัวมี match timeout กัน ReDoS (AC18)</summary>
internal static class PiiRegex
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private const RegexOptions OptsI = Opts | RegexOptions.IgnoreCase;

    // 🔍 token รูป [TYPE_N] (ทนต่อการที่ LLM เปลี่ยนรูปเล็กน้อย) — AL6
    // 🔄 R7c: type ใหม่ (RELIGION ... EMPLOYEE_ID) ใช้ร่วมกันทั้ง Unmask + Guard; type หลายคำยอมให้คั่นด้วย _ / ช่องว่าง / - (UnmaskService normalize ก่อน parse)
    public static readonly Regex Token = new(
        @"\[\s*(PERSON|PHONE|THAI_ID|GENDER|EMAIL|BANK_ACCOUNT|CUSTOM|RELIGION|ETHNICITY|HEALTH|NATIONALITY|ADDRESS|BLOOD[_\s\-]?TYPE|MARITAL[_\s\-]?STATUS|DATE[_\s\-]?OF[_\s\-]?BIRTH|EMPLOYEE[_\s\-]?ID)[_\s\-]?([0-9]{1,4})\s*\]",
        OptsI, Timeout);

    // 🔍 AL8 guard: เลข 13 หลัก (ไม่สน checksum — D7)
    public static readonly Regex ThaiIdLike = new(
        @"(?<![0-9])[0-9](?:[ .\-]{0,3}[0-9]){12}(?![0-9])", Opts, Timeout);

    // 🔍 AL2/AL8: เบอร์มือถือ / บ้าน
    public static readonly Regex PhoneMobile = new(
        @"(?<![0-9])(?:\+?66[ \-]?\(?0?\)?[ \-]?|0)[689][0-9](?:[ .\-]{0,3}[0-9]){7}(?![0-9])", Opts, Timeout);

    public static readonly Regex PhoneLandline = new(
        @"(?<![0-9])(?:\+?66[ \-]?\(?0?\)?[ \-]?|\(?0\)?[ \-]?)[2-7]\)?(?:[ .\-]{0,3}[0-9]){7}(?![0-9])", Opts, Timeout);

    public static readonly Regex EmailLike = new(
        @"[A-Za-z0-9._%+\-]+\s*@\s*[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", Opts, Timeout);

    // 🔍 AL4: คำนำหน้าชื่อ (เรียงยาวก่อนสั้น)
    public static readonly Regex LeadingTitle = new(
        @"^\s*(?:นางสาว|น\.ส\.|นาง|นาย|คุณ|ด\.ช\.|ด\.ญ\.|ดร\.|Mrs\.?|Mr\.?|Ms\.?|Miss|Dr\.?)\s*",
        OptsI, Timeout);

    // 🔄 ตัวคั่นระหว่างคำใน type ของ token (ช่องว่าง / ขีด) — ใช้ normalize เป็น _
    public static readonly Regex TypeSeparator = new(@"[\s\-]+", Opts, Timeout);

    public static readonly Regex Whitespace = new(@"\s+", Opts, Timeout);
    public static readonly Regex NonDigit = new(@"[^0-9]", Opts, Timeout);
}
