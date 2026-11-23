using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Options;

namespace WebTestPdpa.Api.Background;

/// <summary>🧹 AL12: ลบ vault/messages เก่ากว่า RETENTION_DAYS และ audit เก่ากว่า AUDIT_RETENTION_DAYS + R8 รายงานที่หมดอายุ</summary>
public sealed class RetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(IServiceScopeFactory scopes, IOptions<RetentionOptions> options, ILogger<RetentionService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.IntervalMinutes));
            do
            {
                try
                {
                    await RunOnceAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // ⚠️ log เฉพาะชนิด แล้ววนรอบต่อไป ไม่ crash host
                    _logger.LogError("retention failed type={Type}", ex.GetType().Name);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // 🧹 host กำลังหยุด
        }
    }

    /// <summary>🧹 แยกออกมาเพื่อ test</summary>
    public async Task RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = now.AddDays(-_options.Days);
        var auditCutoff = now.AddDays(-_options.AuditDays);

        var msgs = await db.Messages.Where(m => m.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
        var vault = await db.PiiVault.Where(v => v.LastUsedAt < cutoff).ExecuteDeleteAsync(ct);
        var convs = await db.Conversations
            .Where(c => c.UpdatedAt < cutoff && !db.Messages.Any(m => m.ConversationId == c.Id))
            .ExecuteDeleteAsync(ct);
        var audits = await db.AuditLogs.Where(a => a.CreatedAt < auditCutoff).ExecuteDeleteAsync(ct);
        // 🧹 R8: รายงาน PDF ที่หมดอายุ
        var reports = await db.ReportFiles.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync(ct);

        _logger.LogInformation("retention deleted messages={Messages} vault={Vault} conversations={Convs} audit={Audit} reports={Reports}",
            msgs, vault, convs, audits, reports);
    }
}
