using System.Text.RegularExpressions;

namespace WebTestPdpa.Api.Reports;

/// <summary>🔄 R8: แปลง Markdown อย่างง่ายจาก LLM เป็น block (หัวข้อ / ย่อหน้า / รายการ / ตาราง) — ไม่รองรับ HTML/ลิงก์/รูป</summary>
public static partial class ReportMarkdown
{
    public abstract record Block;
    public sealed record Heading(int Level, string Text) : Block;
    public sealed record Paragraph(string Text) : Block;
    public sealed record Bullet(string Text) : Block;
    public sealed record Table(IReadOnlyList<IReadOnlyList<string>> Rows) : Block;

    [GeneratedRegex(@"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$")]
    private static partial Regex SeparatorRx();

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRx();

    [GeneratedRegex(@"^\s*(?:[-*•]|\d+[.)])\s+(.*)$")]
    private static partial Regex BulletRx();

    // 🧹 ตัด markup inline ที่ PDF ไม่ต้องแสดง (**ตัวหนา**, `code`)
    public static string Clean(string s) => s.Replace("**", "").Replace("__", "").Replace("`", "").Trim();

    public static IReadOnlyList<Block> Parse(string? body)
    {
        var blocks = new List<Block>();
        var table = new List<IReadOnlyList<string>>();

        void FlushTable()
        {
            if (table.Count > 0) blocks.Add(new Table(table.ToList()));
            table.Clear();
        }

        foreach (var raw in (body ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.Trim();

            // 🔍 ตาราง: บรรทัดขึ้นต้นด้วย | (ข้ามแถวคั่น |---|)
            if (trimmed.StartsWith('|'))
            {
                if (SeparatorRx().IsMatch(trimmed)) continue;
                var cells = trimmed.Trim('|').Split('|').Select(Clean).ToList();
                table.Add(cells);
                continue;
            }
            FlushTable();

            if (trimmed.Length == 0 || trimmed.StartsWith("```", StringComparison.Ordinal)) continue;

            var h = HeadingRx().Match(trimmed);
            if (h.Success)
            {
                blocks.Add(new Heading(h.Groups[1].Value.Length, Clean(h.Groups[2].Value)));
                continue;
            }
            var b = BulletRx().Match(line);
            if (b.Success)
            {
                blocks.Add(new Bullet(Clean(b.Groups[1].Value)));
                continue;
            }
            blocks.Add(new Paragraph(Clean(trimmed)));
        }
        FlushTable();
        return blocks;
    }
}
