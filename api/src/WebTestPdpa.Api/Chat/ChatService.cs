using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebTestPdpa.Api.Contracts;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Employees;
using WebTestPdpa.Api.Llm;
using WebTestPdpa.Api.Options;
using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Reports;

namespace WebTestPdpa.Api.Chat;

/// <summary>🚀 AL10: orchestrate preview / send — ห้าม log ข้อความหรือค่า PII ใดๆ</summary>
public sealed class ChatService
{
    private const int MaxText = 4000;
    private const int MaxCustom = 20;
    private const int MaxCustomLen = 200;

    private readonly AppDbContext _db;
    private readonly MaskingService _masking;
    private readonly UnmaskService _unmask;
    private readonly OutboundGuard _guard;
    private readonly ILlmGateway _llm;
    private readonly KnownNamesProvider _names;
    private readonly ChatLimitOptions _limits;
    private readonly TimeProvider _time;
    private readonly ILogger<ChatService> _logger;
    private readonly EmployeeTargetResolver _targets;
    private readonly EmployeeContextBuilder _context;
    private readonly ReportService _reports;

    public ChatService(AppDbContext db, MaskingService masking, UnmaskService unmask, OutboundGuard guard,
        ILlmGateway llm, KnownNamesProvider names, IOptions<ChatLimitOptions> limits, TimeProvider time,
        ILogger<ChatService> logger, EmployeeTargetResolver targets, EmployeeContextBuilder context,
        ReportService reports)
    {
        _reports = reports;
        _targets = targets;
        _context = context;
        _db = db;
        _masking = masking;
        _unmask = unmask;
        _guard = guard;
        _llm = llm;
        _names = names;
        _limits = limits.Value;
        _time = time;
        _logger = logger;
    }

    // 🔍 validate request (D: 400 VALIDATION_ERROR)
    private static (string Text, List<string> Custom) Validate(ChatRequest req)
    {
        var text = req.Text ?? "";
        var custom = req.CustomValues ?? [];
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxText
            || custom.Count > MaxCustom || custom.Any(c => c is null || c.Length > MaxCustomLen))
        {
            throw new ChatException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR");
        }
        return (text, custom);
    }

    private async Task EnsureOwnerAsync(Guid? conversationId, Guid userId, CancellationToken ct)
    {
        if (conversationId is null) return;
        var ok = await _db.Conversations.AsNoTracking().AnyAsync(c => c.Id == conversationId && c.UserId == userId, ct);
        if (!ok) throw new ChatException(StatusCodes.Status404NotFound, "CONVERSATION_NOT_FOUND");
    }

    // 🔍 R7d (D39/D40): วิเคราะห์ intent + ระบุ target ที่ได้รับอนุญาต (อ่าน DB อย่างเดียว) — ไม่ตรง intent/ไม่มี target = null
    private async Task<(IntentResult Intent, LoadedEmployees? Loaded)> ResolveEmployeeDataAsync(
        Guid userId, IReadOnlyCollection<string>? roles, string text, CancellationToken ct)
    {
        var intent = EmployeeIntent.Analyze(text);
        if (!intent.HasIntent) return (intent, null);

        var targets = await _targets.ResolveAsync(userId, roles, text, intent.SelfReference, ct);
        if (targets.EmployeeIds.Count == 0) return (intent, null);

        var loaded = await _context.LoadAsync(targets.EmployeeIds, intent, ct);
        return (intent, loaded);
    }

    public async Task<PreviewResponse> PreviewAsync(Guid userId, ChatRequest req, CancellationToken ct,
        IReadOnlyCollection<string>? roles = null)
    {
        var (text, custom) = Validate(req);
        await EnsureOwnerAsync(req.ConversationId, userId, ct);
        var names = await _names.GetAsync(ct);

        // 🔍 R7d: tag ข้อความด้วยค่าของ target ที่ได้รับอนุญาตเท่านั้น (F25) — preview ไม่สร้าง context
        var (_, loaded) = await ResolveEmployeeDataAsync(userId, roles, text, ct);
        IReadOnlyList<TypedValue> typed = loaded?.UserTextValues ?? [];

        // 📤 preview: ไม่บันทึกอะไรลง DB (persist=false)
        MaskResult mask;
        try
        {
            mask = await _masking.MaskAsync(req.ConversationId, text, custom, names, typed, persist: false, ct);
        }
        catch (PiiUnavailableException)
        {
            throw new ChatException(StatusCodes.Status503ServiceUnavailable, "PII_UNAVAILABLE");
        }

        var guard = _guard.Check([mask.MaskedText]);
        return new PreviewResponse(
            mask.MaskedText,
            mask.Spans.Select(s => new PreviewEntity(PiiEntityTypes.ToWire(s.Type), s.Start, s.End, s.Token, s.Source)).ToList(),
            mask.MaskCounts,
            new GuardInfo(guard.Passed, guard.Reasons));
    }

    public async Task<SendResponse> SendAsync(Guid userId, string userName, ChatRequest req, CancellationToken ct,
        IReadOnlyCollection<string>? roles = null)
    {
        var (text, custom) = Validate(req);
        await EnsureOwnerAsync(req.ConversationId, userId, ct);

        var now = _time.GetUtcNow();
        var audit = new AuditLog
        {
            CreatedAt = now,
            UserId = userId,
            UserName = userName,
            Action = "chat_send",
            Provider = _llm.Provider,
            Model = _llm.Model
        };

        // 🚀 D17 (1): transaction — lock conversation → detect → tokenize/vault → guard → save user message → commit
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Conversation conv;
        if (req.ConversationId is null)
        {
            conv = new Conversation { UserId = userId, Title = "", CreatedAt = now, UpdatedAt = now };
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            var locked = await _db.Conversations
                .FromSqlInterpolated($"SELECT * FROM conversations WHERE id = {req.ConversationId.Value} FOR UPDATE")
                .ToListAsync(ct);
            conv = locked.FirstOrDefault(c => c.UserId == userId)
                   ?? throw new ChatException(StatusCodes.Status404NotFound, "CONVERSATION_NOT_FOUND");
        }
        audit.ConversationId = conv.Id;

        // 🔍 R7d: intent + target ตัดสินฝั่ง server (อ่าน DB อย่างเดียว) ก่อนเรียก pii
        var (intent, loaded) = await ResolveEmployeeDataAsync(userId, roles, text, ct);
        IReadOnlyList<TypedValue> typed = loaded?.UserTextValues ?? [];

        MaskResult mask;
        try
        {
            var names = await _names.GetAsync(ct);
            mask = await _masking.MaskAsync(conv.Id, text, custom, names, typed, persist: true, ct);
        }
        catch (PiiUnavailableException ex)
        {
            // ⚠️ fail-closed: rollback, ไม่เรียก LLM, ไม่บันทึก message (ยังไม่สร้าง context)
            await tx.RollbackAsync(ct);
            await WriteBlockedAuditAsync(audit, "blocked_pii_unavailable", ex.ReasonCode, null, false, null, ct);
            throw new ChatException(StatusCodes.Status503ServiceUnavailable, "PII_UNAVAILABLE");
        }

        audit.NerEnabled = mask.NerUsed;
        audit.MaskCounts = JsonSerializer.Serialize(mask.MaskCounts);

        // 🔄 R7d (D38): context จาก label + token (tokenize ใน transaction เดียวกัน — guard fail = rollback vault ด้วย)
        EmployeeContext? empContext = null;
        if (loaded is not null)
        {
            empContext = await _context.BuildAsync(conv.Id, loaded, persist: true, ct);
            _logger.LogInformation("employee context sections={Sections} targets={Targets} length={Length}",
                intent.Sections.Count, empContext.EmployeeIds.Count, empContext.MaskedText.Length);
        }

        // 📦 history masked ล่าสุด N ข้อความ (ก่อนข้อความปัจจุบัน)
        var history = await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conv.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(_limits.HistoryLimit)
            .ToListAsync(ct);
        history.Reverse();

        // 🔍 guard ชั้นสุดท้าย: history + ข้อความปัจจุบัน
        var guardInputs = history.Select(h => h.MaskedContent).Append(mask.MaskedText);
        if (empContext is not null) guardInputs = guardInputs.Append(empContext.MaskedText);
        var guard = _guard.Check(guardInputs);
        if (!guard.Passed)
        {
            await tx.RollbackAsync(ct);
            await WriteBlockedAuditAsync(audit, "blocked_guard", "GUARD_BLOCKED", guard.Reasons, mask.NerUsed, mask.MaskCounts, ct);
            throw new ChatException(StatusCodes.Status422UnprocessableEntity, "GUARD_BLOCKED",
                new Dictionary<string, object?> { ["reasons"] = guard.Reasons });
        }

        // 💾 บันทึก user message (masked) แล้ว commit
        var userMsg = new Message
        {
            ConversationId = conv.Id,
            Role = "user",
            MaskedContent = mask.MaskedText,
            MaskCounts = audit.MaskCounts,
            CreatedAt = now
        };
        _db.Messages.Add(userMsg);
        conv.UpdatedAt = now;
        if (string.IsNullOrEmpty(conv.Title))
        {
            // ⚠️ ห้ามตัดกลาง surrogate pair (emoji) — ถ้าตำแหน่งที่ 39 เป็น high surrogate ให้ตัดที่ 39
            var masked = mask.MaskedText;
            var cut = masked.Length > 40 && char.IsHighSurrogate(masked[39]) ? 39 : 40;
            conv.Title = masked.Length <= 40 ? masked : masked[..cut];
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // 🔄 D23: payload ทั้งก้อนที่จะส่ง LLM (masked ทั้งหมด) + hash
        var payload = new List<LlmMessage> { new("system", SystemPrompt.Text) };
        // 🔍 R8: ผู้ใช้ขอ PDF -> สั่ง LLM ให้ตอบเป็นเนื้อหารายงาน (ตรวจจากข้อความต้นฉบับ ไม่ใช่จาก LLM)
        var wantsPdf = ReportIntent.WantsPdf(text);
        // 🔄 R7d: context (system ลำดับที่ 2) ไม่บันทึกใน messages — เก็บเฉพาะใน audit outbound_payload
        if (empContext is not null)
        {
            payload.Add(new LlmMessage("system", empContext.MaskedText));
            audit.ContextEmployeeIds = JsonSerializer.Serialize(empContext.EmployeeIds);
            audit.ContextSections = JsonSerializer.Serialize(empContext.Sections);
        }
        // ⚠️ วางหลัง context เพื่อให้ context ยังเป็น system ลำดับที่ 2 เสมอ (mock/e2e อ้างอิงตำแหน่งนี้)
        if (wantsPdf) payload.Add(new LlmMessage("system", ReportIntent.Instruction));
        payload.AddRange(history.Select(h => new LlmMessage(h.Role, h.MaskedContent)));
        payload.Add(new LlmMessage("user", mask.MaskedText));
        audit.MessageId = userMsg.Id;
        audit.MaskedPayload = mask.MaskedText;
        audit.OutboundPayload = AuditPayload.Serialize(payload);
        audit.PayloadSha256 = AuditPayload.Sha256Hex(audit.OutboundPayload);
        audit.HistoryMessageCount = history.Count;

        // 📤 D17 (2): เรียก LLM นอก transaction
        // ⚠️ D23: payload ถูกส่งออกไปแล้วเมื่อเริ่มเรียก → ทุกทางออก (error/cancel/exception) ต้องมีแถว audit เสมอ
        // ⚠️ และเขียนด้วย CancellationToken.None เพราะ ct อาจถูก cancel แล้ว (ผู้ใช้ปิดแท็บ)
        LlmResult result;
        try
        {
            result = await _llm.CompleteAsync(payload, ct);
        }
        catch (LlmException ex)
        {
            var code = ex.Message.Length > 64 ? ex.Message[..64] : ex.Message;
            await WriteLlmFailureAuditAsync(audit, code);
            _logger.LogWarning("llm error code={Code}", code);
            throw new ChatException(StatusCodes.Status502BadGateway, "LLM_ERROR");
        }
        catch (OperationCanceledException)
        {
            await WriteLlmFailureAuditAsync(audit, "CLIENT_CANCELLED");
            throw;
        }
        catch (Exception ex) when (ex is not ChatException)
        {
            // ⚠️ exception ที่ไม่คาดไว้หลัง payload ออกไปแล้ว — เก็บแค่ชนิด ไม่เก็บข้อความ
            var code = "UNEXPECTED:" + ex.GetType().Name;
            await WriteLlmFailureAuditAsync(audit, code.Length > 64 ? code[..64] : code);
            throw;
        }

        // 💾 D17 (3): assistant message + audit sent
        var assistant = new Message
        {
            ConversationId = conv.Id,
            Role = "assistant",
            MaskedContent = result.Text,
            MaskCounts = "{}",
            CreatedAt = _time.GetUtcNow()
        };
        _db.Messages.Add(assistant);
        conv.UpdatedAt = assistant.CreatedAt;
        audit.Status = "sent";
        audit.LatencyMs = result.LatencyMs;
        audit.Provider = result.Provider;
        audit.Model = result.Model;
        _db.AuditLogs.Add(audit);
        try
        {
            // 💾 ไม่ผูกกับ ct: ถ้า LLM ตอบแล้วต้องไม่เสีย assistant message + audit เพราะ client หลุด
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not ChatException)
        {
            await WriteLlmFailureAuditAsync(audit, "SAVE_FAILED:" + ex.GetType().Name);
            throw;
        }

        // 🔄 unmask ทั้งสองข้อความ → response
        var plain = await _unmask.UnmaskManyAsync(conv.Id, [userMsg.MaskedContent, assistant.MaskedContent], ct);

        // 💾 R8: สร้าง PDF จากคำตอบที่ unmask แล้ว — ⚠️ ล้มเหลว = reportId null แต่ chat ยังสำเร็จ
        Guid? reportId = wantsPdf
            ? await _reports.TryCreateAsync(userId, userName, conv.Id, assistant.Id, plain[1])
            : null;

        return new SendResponse(
            conv.Id,
            ToDto(userMsg, plain[0], mask.MaskCounts),
            ToDto(assistant, plain[1], new Dictionary<string, int>()) with { ReportId = reportId },
            result.Provider,
            result.Model);
    }

    private static MessageDto ToDto(Message m, string content, IReadOnlyDictionary<string, int> counts) =>
        new(m.Id, m.Role, content, m.MaskedContent, counts, m.CreatedAt);

    // 💾 audit กรณี payload ออกไปถึง LLM แล้วแต่ไม่สำเร็จ (เก็บ outbound_payload/hash ไว้) — ใช้ CancellationToken.None
    private async Task WriteLlmFailureAuditAsync(AuditLog audit, string errorCode)
    {
        _db.ChangeTracker.Clear();
        audit.Status = "llm_error";
        audit.ErrorCode = errorCode.Length > 64 ? errorCode[..64] : errorCode;
        _db.AuditLogs.Add(audit);
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    // 💾 audit กรณี blocked: ⚠️ masked_payload / outbound_payload = null (ห้ามเก็บข้อความที่สงสัยว่ามี PII)
    private async Task WriteBlockedAuditAsync(AuditLog audit, string status, string errorCode,
        IReadOnlyList<string>? guardReasons, bool nerEnabled, IReadOnlyDictionary<string, int>? counts, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        audit.Status = status;
        audit.ErrorCode = errorCode;
        audit.NerEnabled = nerEnabled;
        audit.MaskedPayload = null;
        audit.OutboundPayload = null;
        audit.PayloadSha256 = null;
        audit.GuardReasons = guardReasons is null ? null : JsonSerializer.Serialize(guardReasons);
        audit.MaskCounts = counts is null ? "{}" : JsonSerializer.Serialize(counts);
        // ⚠️ conversation ใหม่ที่ถูก rollback ไม่มีอยู่จริง → ห้ามอ้าง FK
        if (audit.ConversationId is { } cid && !await _db.Conversations.AnyAsync(c => c.Id == cid, ct))
        {
            audit.ConversationId = null;
        }
        _db.AuditLogs.Add(audit);
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>🔄 serialize payload แบบ canonical เพื่อคำนวณ SHA-256 ซ้ำได้</summary>
public static class AuditPayload
{
    private sealed record Item(string role, string content);

    public static string Serialize(IEnumerable<LlmMessage> messages) =>
        JsonSerializer.Serialize(messages.Select(m => new Item(m.Role, m.Content)).ToList());

    /// <summary>🔄 อ่านจาก jsonb (spacing เปลี่ยนได้) แล้ว serialize ใหม่แบบ canonical</summary>
    public static IReadOnlyList<LlmMessage> Parse(string json)
    {
        var items = JsonSerializer.Deserialize<List<Item>>(json) ?? [];
        return items.Select(i => new LlmMessage(i.role, i.content)).ToList();
    }

    public static string Sha256Hex(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    public static string HashOfStored(string storedJson) => Sha256Hex(Serialize(Parse(storedJson)));
}
