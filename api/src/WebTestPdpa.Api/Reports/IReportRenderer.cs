namespace WebTestPdpa.Api.Reports;

/// <summary>📦 R8: เนื้อหารายงาน (plaintext หลัง unmask) — ⚠️ มีข้อมูลส่วนบุคคล ห้าม log</summary>
public sealed record ReportDocument(string Title, string RequestedBy, DateTimeOffset CreatedAt, string Body, string Disclaimer);

/// <summary>🔄 R8: render รายงานเป็น PDF bytes</summary>
public interface IReportRenderer
{
    byte[] Render(ReportDocument doc);
}
