using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;

namespace WebTestPdpa.Api.Reports;

/// <summary>🚀 R8: สร้าง/ดาวน์โหลดรายงาน PDF — ⚠️ log เฉพาะ id/ขนาด/ชนิด error ห้าม log เนื้อหา</summary>
public sealed class ReportService
{
    /// <summary>⚠️ อายุไฟล์รายงาน — หมดอายุแล้วดาวน์โหลดไม่ได้และถูกลบโดย RetentionService</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public const string Title = "รายงานจากผู้ช่วย AI";
    public const string Disclaimer =
        "เนื้อหาและตัวเลขในรายงานนี้สร้างโดย AI อาจคลาดเคลื่อน ต้องตรวจสอบกับข้อมูลในระบบ HR ก่อนนำไปใช้ " +
        "เอกสารนี้มีข้อมูลส่วนบุคคล ใช้ภายในเท่านั้น ห้ามเผยแพร่";

    private readonly AppDbContext _db;
    private readonly IReportRenderer _renderer;
    private readonly ReportCrypto _crypto;
    private readonly TimeProvider _time;
    private readonly ILogger<ReportService> _logger;

    public ReportService(AppDbContext db, IReportRenderer renderer, ReportCrypto crypto, TimeProvider time, ILogger<ReportService> logger)
    {
        _db = db;
        _renderer = renderer;
        _crypto = crypto;
        _time = time;
        _logger = logger;
    }

    /// <summary>💾 render + เข้ารหัส + บันทึก — ⚠️ ล้มเหลวต้องไม่ทำให้ chat ล้ม: คืน null และ log เฉพาะชนิด error</summary>
    public async Task<Guid?> TryCreateAsync(Guid userId, string userName, Guid conversationId, Guid messageId, string plainBody)
    {
        try
        {
            var now = _time.GetUtcNow();
            var pdf = _renderer.Render(new ReportDocument(Title, userName, now, plainBody, Disclaimer));
            var report = new ReportFile
            {
                UserId = userId,
                ConversationId = conversationId,
                MessageId = messageId,
                CreatedAt = now,
                ExpiresAt = now.Add(Lifetime),
                SizeBytes = pdf.Length
            };
            report.Cipher = _crypto.Encrypt(pdf, report.Id, userId);
            _db.ReportFiles.Add(report);
            _db.AuditLogs.Add(NewAudit(now, userId, userName, conversationId, messageId, "report_create"));
            // 💾 ไม่ผูกกับ ct ของ request (assistant message บันทึกไปแล้ว)
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("report created id={ReportId} size={Size}", report.Id, pdf.Length);
            return report.Id;
        }
        catch (Exception ex)
        {
            // ⚠️ log เฉพาะชนิด ไม่ log ข้อความ exception (อาจมีเนื้อหา)
            _logger.LogWarning("report create failed type={Type}", ex.GetType().Name);
            _db.ChangeTracker.Clear();
            return null;
        }
    }

    /// <summary>📦 ดาวน์โหลด: เจ้าของเท่านั้นและยังไม่หมดอายุ — ไม่ใช่เจ้าของ/หมดอายุ/ไม่มี = null (ไม่เปิดเผยว่ามีอยู่)</summary>
    public async Task<byte[]?> DownloadAsync(Guid reportId, Guid userId, string userName, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var report = await _db.ReportFiles.FirstOrDefaultAsync(r => r.Id == reportId && r.UserId == userId, ct);
        if (report is null || report.ExpiresAt <= now) return null;

        var pdf = _crypto.Decrypt(report.Cipher, report.Id, userId);
        report.DownloadCount++;
        report.LastDownloadedAt = now;
        _db.AuditLogs.Add(NewAudit(now, userId, userName, report.ConversationId, report.MessageId, "report_download"));
        await _db.SaveChangesAsync(ct);
        return pdf;
    }

    /// <summary>📦 report ที่ยังไม่หมดอายุของ message ชุดนี้ (สำหรับ history)</summary>
    public async Task<Dictionary<Guid, Guid>> ActiveByMessageAsync(Guid userId, IReadOnlyCollection<Guid> messageIds, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var rows = await _db.ReportFiles.AsNoTracking()
            .Where(r => r.UserId == userId && messageIds.Contains(r.MessageId) && r.ExpiresAt > now)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.MessageId, r.Id })
            .ToListAsync(ct);
        var map = new Dictionary<Guid, Guid>();
        foreach (var r in rows) map[r.MessageId] = r.Id;
        return map;
    }

    // 💾 audit ไม่มีเนื้อหา (masked_payload/outbound_payload = null)
    private static AuditLog NewAudit(DateTimeOffset now, Guid userId, string userName, Guid conversationId, Guid messageId, string action) => new()
    {
        CreatedAt = now,
        UserId = userId,
        UserName = userName,
        ConversationId = conversationId,
        MessageId = messageId,
        Action = action,
        Status = "ok"
    };
}
