using System.Text.RegularExpressions;

namespace WebTestPdpa.Api.Reports;

/// <summary>🔍 R8: ผู้ใช้ขอผลลัพธ์เป็น PDF หรือไม่ (ตรวจจากข้อความต้นฉบับฝั่ง server)</summary>
public static partial class ReportIntent
{
    [GeneratedRegex(@"pdf|พีดีเอฟ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdfRx();

    public static bool WantsPdf(string? text) => !string.IsNullOrEmpty(text) && PdfRx().IsMatch(text);

    /// <summary>📤 system message เพิ่มเติมเมื่อขอ PDF — ไม่มีข้อมูลส่วนบุคคล</summary>
    public const string Instruction =
        "ผู้ใช้ขอผลลัพธ์เป็นไฟล์ PDF — ระบบจะนำคำตอบของคุณไปสร้างไฟล์ PDF ให้ผู้ใช้ดาวน์โหลดเอง " +
        "ห้ามตอบว่าคุณสร้างไฟล์ไม่ได้ ให้เขียนเนื้อหารายงานให้ครบถ้วนในรูปแบบ Markdown อย่างง่ายเท่านั้น: " +
        "หัวข้อใช้ # หรือ ##, ตารางใช้ | คั่นคอลัมน์ (แถวแรกเป็นหัวตาราง), รายการใช้ - นำหน้า " +
        "ห้ามใช้ code block และห้ามใส่ลิงก์ คง placeholder [TYPE_N] ไว้ตามเดิมทุกตัว";
}
