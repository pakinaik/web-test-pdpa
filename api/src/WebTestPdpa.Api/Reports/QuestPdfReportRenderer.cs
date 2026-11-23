using System.Globalization;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace WebTestPdpa.Api.Reports;

/// <summary>🔄 R8: render รายงานด้วย QuestPDF + ฟอนต์ไทยจากระบบ (Linux: fonts-thai-tlwg, Windows: Leelawadee/Tahoma)</summary>
public sealed class QuestPdfReportRenderer : IReportRenderer
{
    // 🔍 ชื่อ family ตามที่เก็บในไฟล์ฟอนต์ (QuestPDF 2026.9+ ไม่รองรับตั้งชื่อเอง)
    private static string _fontFamily = "Sarabun";
    private static readonly TimeZoneInfo Bangkok = FindBangkok();

    // ⚠️ ลงทะเบียนฟอนต์ครั้งเดียวต่อ process (FontManager เป็น static)
    private static readonly Lazy<bool> FontReady = new(RegisterThaiFont, LazyThreadSafetyMode.ExecutionAndPublication);

    // 🔍 ลำดับความสำคัญ: regular ก่อน แล้วตามด้วย bold (ถ้ามี)
    private static readonly (string Family, string[] Files)[] Candidates =
    [
        ("Sarabun", ["Sarabun.ttf", "Sarabun.otf", "Sarabun-Bold.ttf", "Sarabun-Bold.otf"]),
        ("Garuda", ["Garuda.ttf", "Garuda.otf", "Garuda-Bold.ttf", "Garuda-Bold.otf"]),
        ("Loma", ["Loma.ttf", "Loma.otf", "Loma-Bold.ttf", "Loma-Bold.otf"]),
        ("Leelawadee UI", ["LeelawUI.ttf", "LeelaUIb.ttf"]),
        ("Tahoma", ["tahoma.ttf", "tahomabd.ttf"]),
    ];

    private static readonly string[] FontDirs =
    [
        "/usr/share/fonts",
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
    ];

    static QuestPdfReportRenderer()
    {
        // ⚠️ Community license (โปรเจกต์ทดสอบภายใน)
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDetailedLayoutErrors = false;
    }

    public byte[] Render(ReportDocument doc)
    {
        if (!FontReady.Value)
        {
            throw new InvalidOperationException("THAI_FONT_NOT_FOUND");
        }
        var blocks = ReportMarkdown.Parse(doc.Body);
        var local = TimeZoneInfo.ConvertTime(doc.CreatedAt, Bangkok);
        var created = local.ToString("d MMMM yyyy HH:mm", CultureInfo.GetCultureInfo("th-TH"));

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily(_fontFamily).FontSize(11).LineHeight(1.3f));

                page.Header().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Column(col =>
                {
                    col.Item().Text(doc.Title).FontSize(18).Bold();
                    col.Item().Text($"ผู้ขอรายงาน: {doc.RequestedBy}").FontSize(10);
                    col.Item().Text($"สร้างเมื่อ: {created} น.").FontSize(10);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(5);
                    foreach (var block in blocks) RenderBlock(col, block);
                });

                page.Footer().Column(col =>
                {
                    col.Item().BorderTop(0.5f).BorderColor(Colors.Grey.Medium).PaddingTop(4)
                        .Text(doc.Disclaimer).FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().AlignCenter().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(8));
                        t.Span("หน้า ");
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void RenderBlock(ColumnDescriptor col, ReportMarkdown.Block block)
    {
        switch (block)
        {
            case ReportMarkdown.Heading h:
                col.Item().PaddingTop(h.Level <= 1 ? 6 : 3).Text(h.Text).FontSize(h.Level <= 1 ? 15 : 13).Bold();
                break;
            case ReportMarkdown.Bullet b:
                col.Item().Row(row =>
                {
                    row.ConstantItem(14).Text("•");
                    row.RelativeItem().Text(b.Text);
                });
                break;
            case ReportMarkdown.Table t:
                RenderTable(col, t);
                break;
            case ReportMarkdown.Paragraph p:
                col.Item().Text(p.Text);
                break;
        }
    }

    private static void RenderTable(ColumnDescriptor col, ReportMarkdown.Table t)
    {
        var columns = t.Rows.Max(r => r.Count);
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                for (var i = 0; i < columns; i++) cd.RelativeColumn();
            });
            for (var r = 0; r < t.Rows.Count; r++)
            {
                var header = r == 0;
                for (var c = 0; c < columns; c++)
                {
                    var value = c < t.Rows[r].Count ? t.Rows[r][c] : "";
                    var cell = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
                    if (header) cell = cell.Background(Colors.Grey.Lighten3);
                    var text = cell.Text(value).FontSize(10);
                    if (header) text.Bold();
                }
            }
        });
    }

    // 🚀 หาไฟล์ฟอนต์ไทยในเครื่อง แล้วลงทะเบียน (regular + bold ถ้ามี) และจำชื่อ family
    private static bool RegisterThaiFont()
    {
        var files = FontDirs.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
            .SelectMany(d => Directory.EnumerateFiles(d, "*.*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            .ToList();
        foreach (var (family, group) in Candidates)
        {
            var found = group
                .Select(n => files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), n, StringComparison.OrdinalIgnoreCase)))
                .Where(f => f is not null)
                .Distinct()
                .ToList();
            if (found.Count == 0) continue;
            foreach (var path in found)
            {
                using var stream = File.OpenRead(path!);
                FontManager.RegisterFontFromStream(stream);
            }
            _fontFamily = family;
            return true;
        }
        return false;
    }

    private static TimeZoneInfo FindBangkok()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT"); }
    }
}
