using System.Text;

namespace WebTestPdpa.Api.Pii;

/// <summary>🔄 span ที่ถูกแทนด้วย token แล้ว (offset บนข้อความต้นฉบับ)</summary>
public sealed record MaskedSpan(PiiEntityType Type, int Start, int End, string Token, string Source);

public sealed record MaskResult(
    string MaskedText,
    IReadOnlyList<MaskedSpan> Spans,
    IReadOnlyDictionary<string, int> MaskCounts,
    bool NerUsed);

/// <summary>🔄 R7 (D38/D42): ค่าที่รู้ type อยู่แล้ว (เช่น ศาสนาของพนักงาน) — ใช้ tag ในข้อความผู้ใช้ และ tokenize ลง context</summary>
public sealed record TypedValue(PiiEntityType Type, string Value);

/// <summary>🔄 AL5: tokenization ที่ consistent ต่อ conversation</summary>
public sealed class MaskingService
{
    private readonly IPiiDetectionClient _pii;
    private readonly IVaultStore _vault;
    private readonly VaultCrypto _crypto;
    private readonly TimeProvider _time;

    public MaskingService(IPiiDetectionClient pii, IVaultStore vault, VaultCrypto crypto, TimeProvider? time = null)
    {
        _pii = pii;
        _vault = vault;
        _crypto = crypto;
        _time = time ?? TimeProvider.System;
    }

    /// <param name="conversationId">🔍 null = conversation ใหม่ (ใช้ Guid ชั่วคราว ไม่ lookup vault)</param>
    /// <param name="persist">💾 true = บันทึก vault; false (preview) = ไม่เขียน DB</param>
    public Task<MaskResult> MaskAsync(
        Guid? conversationId,
        string text,
        IReadOnlyList<string> customValues,
        IReadOnlyList<string> knownNames,
        bool persist,
        CancellationToken ct) =>
        MaskAsync(conversationId, text, customValues, knownNames, [], persist, ct);

    /// <summary>🔄 R7: เหมือนเดิม + typedValues (ทุก occurrence แบบ ordinal ignore case เป็น span type นั้น source employee_data)</summary>
    public async Task<MaskResult> MaskAsync(
        Guid? conversationId,
        string text,
        IReadOnlyList<string> customValues,
        IReadOnlyList<string> knownNames,
        IReadOnlyList<TypedValue> typedValues,
        bool persist,
        CancellationToken ct)
    {
        var convId = conversationId ?? Guid.NewGuid();
        var useVault = conversationId.HasValue;

        // 📤 ขั้น 1: ตรวจ PII (fail-closed — throw PiiUnavailableException)
        var detection = await _pii.DetectAsync(text, knownNames, ct);

        // 🔄 ขั้น 2: custom values -> ทุก occurrence (ordinal, ignore case)
        var raw = detection.Entities.Select(e => new MaskSpan(e.Type, e.Start, e.End, e.Source)).ToList();
        foreach (var cv in customValues.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddOccurrences(raw, text, cv, PiiEntityType.Custom, "custom");
        }

        // 🔄 ขั้น 2b (R7): typed values ที่รู้ type — match ทุก occurrence เช่นเดียวกับ custom
        foreach (var tv in typedValues.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Distinct())
        {
            AddOccurrences(raw, text, tv.Value, tv.Type, "employee_data");
        }

        // 🔄 ขั้น 3: merge/resolve overlap + validate offset
        var spans = SpanResolver.Merge(raw, text.Length);

        // 📦 ขั้น 4: แปลงแต่ละ span เป็น token
        var alloc = await NewAllocatorAsync(convId, useVault, persist, ct);
        var masked = new List<MaskedSpan>();
        var counts = new Dictionary<string, int>();

        foreach (var s in spans)
        {
            var value = text[s.Start..s.End];
            // ⚠️ N1: span ที่ถูก union ขยายแล้วห้ามใช้ canonical ของ type ผู้ชนะ (เช่น เบอร์ digits-only) ไม่งั้น token ซ้ำกับค่าล้วน
            var canonical = s.Expanded
                ? ValueCanonicalizer.CanonicalGeneric(value)
                : ValueCanonicalizer.Canonical(s.Type, value);
            var token = await alloc.ResolveAsync(s.Type, value, canonical, ct);

            masked.Add(new MaskedSpan(s.Type, s.Start, s.End, token, s.Source));
            var wire = PiiEntityTypes.ToWire(s.Type);
            counts[wire] = counts.GetValueOrDefault(wire) + 1;
        }

        // 🔄 ขั้น 5: สร้างข้อความ masked
        var sb = new StringBuilder(text.Length);
        var pos = 0;
        foreach (var m in masked)
        {
            sb.Append(text, pos, m.Start - pos).Append(m.Token);
            pos = m.End;
        }
        sb.Append(text, pos, text.Length - pos);

        return new MaskResult(sb.ToString(), masked, counts, detection.NerUsed);
    }

    /// <summary>
    /// 📦 R7 (D38): tokenize ค่าที่รู้ type แล้วโดยตรง (ไม่ผ่าน pii detect) — ใช้ HMAC/vault/D4/D5 เดิม
    /// คืน token ต่อ TypedValue (ค่าเดียวกัน = token เดียวกัน, ตรงกับที่ MaskAsync จะให้ในข้อความผู้ใช้)
    /// </summary>
    public async Task<IReadOnlyDictionary<TypedValue, string>> TokenizeValuesAsync(
        Guid conversationId, IReadOnlyList<TypedValue> values, bool persist, CancellationToken ct)
    {
        var alloc = await NewAllocatorAsync(conversationId, useVault: true, persist, ct);
        var result = new Dictionary<TypedValue, string>();
        foreach (var tv in values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Distinct())
        {
            var canonical = ValueCanonicalizer.Canonical(tv.Type, tv.Value);
            result[tv] = await alloc.ResolveAsync(tv.Type, tv.Value, canonical, ct);
        }
        return result;
    }

    // 🔍 หาทุก occurrence ของค่า (ordinal, ignore case) แล้วเพิ่มเป็น span
    private static void AddOccurrences(List<MaskSpan> raw, string text, string value, PiiEntityType type, string source)
    {
        var idx = text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            raw.Add(new MaskSpan(type, idx, idx + value.Length, source));
            idx = text.IndexOf(value, idx + value.Length, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<TokenAllocator> NewAllocatorAsync(Guid convId, bool useVault, bool persist, CancellationToken ct)
    {
        var maxIdx = useVault
            ? new Dictionary<PiiEntityType, int>(await _vault.GetMaxIndexesAsync(convId, ct))
            : new Dictionary<PiiEntityType, int>();
        return new TokenAllocator(this, convId, useVault, persist, maxIdx);
    }

    // 📦 ขั้น 4 ที่ใช้ร่วมกันทั้ง MaskAsync และ TokenizeValuesAsync: lookup vault -> ใช้ token เดิม หรือออกใหม่ + เก็บค่า first-seen (D5)
    private sealed class TokenAllocator
    {
        private readonly MaskingService _svc;
        private readonly Guid _convId;
        private readonly bool _useVault;
        private readonly bool _persist;
        private readonly Dictionary<PiiEntityType, int> _maxIdx;
        private readonly Dictionary<(PiiEntityType, string), string> _requestCache = new();
        private readonly HashSet<Guid> _touched = new();

        public TokenAllocator(MaskingService svc, Guid convId, bool useVault, bool persist, Dictionary<PiiEntityType, int> maxIdx)
        {
            _svc = svc;
            _convId = convId;
            _useVault = useVault;
            _persist = persist;
            _maxIdx = maxIdx;
        }

        public async Task<string> ResolveAsync(PiiEntityType type, string value, string canonical, CancellationToken ct)
        {
            var hash = _svc._crypto.ComputeValueHash(_convId, type, canonical);
            var cacheKey = (type, Convert.ToHexString(hash));
            if (_requestCache.TryGetValue(cacheKey, out var cached)) return cached;

            var existing = _useVault ? await _svc._vault.FindAsync(_convId, type, hash, ct) : null;
            string token;
            if (existing is not null)
            {
                token = existing.Token;
                if (_persist && _touched.Add(existing.Id))
                {
                    await _svc._vault.TouchAsync(existing.Id, ct);
                }
            }
            else
            {
                _maxIdx.TryGetValue(type, out var cur);
                var next = cur + 1;
                _maxIdx[type] = next;
                token = $"[{PiiEntityTypes.ToWire(type)}_{next}]";
                if (_persist)
                {
                    // 💾 เก็บค่า first-seen แบบเข้ารหัส (D5)
                    await _svc._vault.AddAsync(new VaultEntry
                    {
                        ConversationId = _convId,
                        Type = type,
                        TokenIndex = next,
                        Token = token,
                        ValueHash = hash,
                        ValueCipher = _svc._crypto.Encrypt(value, _convId, token),
                        LastUsedAt = _svc._time.GetUtcNow()
                    }, ct);
                }
            }
            _requestCache[cacheKey] = token;
            return token;
        }
    }
}
