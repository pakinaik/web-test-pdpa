using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebTestPdpa.Api.Options;

namespace WebTestPdpa.Api.Pii;

/// <summary>📤 AL7: typed HttpClient → PII service, fail-closed ไม่มี retry</summary>
public sealed class PiiDetectionClient : IPiiDetectionClient
{
    private readonly HttpClient _http;
    private readonly PiiOptions _options;
    private readonly ILogger<PiiDetectionClient>? _logger;

    public PiiDetectionClient(HttpClient http, IOptions<PiiOptions> options, ILogger<PiiDetectionClient>? logger = null)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PiiDetectionResult> DetectAsync(string text, IReadOnlyList<string> knownNames, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "detect")
            {
                Content = JsonContent.Create(new { text, known_names = knownNames })
            };
            req.Headers.Add("X-Internal-Token", _options.ServiceToken);

            using var resp = await _http.SendAsync(req, timeoutCts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                throw new PiiUnavailableException("HTTP_" + (int)resp.StatusCode);
            }

            var body = await resp.Content.ReadAsStringAsync(timeoutCts.Token);
            return Parse(body, text.Length);
        }
        catch (PiiUnavailableException ex)
        {
            // ⚠️ log เฉพาะ reasonCode + elapsed (ห้ามมีข้อความ)
            _logger?.LogWarning("pii unavailable reason={Reason} elapsedMs={Elapsed}", ex.ReasonCode, sw.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger?.LogWarning("pii unavailable reason=TIMEOUT elapsedMs={Elapsed}", sw.ElapsedMilliseconds);
            throw new PiiUnavailableException("TIMEOUT");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning("pii unavailable reason=CONNECTION elapsedMs={Elapsed}", sw.ElapsedMilliseconds);
            throw new PiiUnavailableException("CONNECTION", ex);
        }
    }

    // 🔍 parse + validate ผลลัพธ์ทุก field (ผิดตรงไหน = fail-closed)
    private static PiiDetectionResult Parse(string body, int textLength)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // 🔍 root ต้องเป็น object (ไม่งั้น TryGetProperty จะโยน InvalidOperationException)
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("entities", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                throw new PiiUnavailableException("BAD_RESPONSE");
            }

            var list = new List<DetectedEntity>();
            foreach (var e in arr.EnumerateArray())
            {
                // 🔍 ตรวจ ValueKind ก่อนเสมอ (TryGetInt32 โยนเมื่อไม่ใช่ Number)
                if (e.ValueKind != JsonValueKind.Object
                    || !e.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String
                    || !e.TryGetProperty("start", out var s) || s.ValueKind != JsonValueKind.Number || !s.TryGetInt32(out var start)
                    || !e.TryGetProperty("end", out var en) || en.ValueKind != JsonValueKind.Number || !en.TryGetInt32(out var end))
                {
                    throw new PiiUnavailableException("BAD_RESPONSE");
                }
                if (!PiiEntityTypes.TryParse(t.GetString(), out var type) || !PiiEntityTypes.IsDetectorType(type))
                {
                    throw new PiiUnavailableException("UNKNOWN_TYPE");
                }
                if (start < 0 || start >= end || end > textLength)
                {
                    throw new PiiUnavailableException("INVALID_OFFSET");
                }
                var source = e.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.String
                    ? src.GetString() ?? "" : "";
                list.Add(new DetectedEntity(type, start, end, source));
            }

            var ner = root.TryGetProperty("ner_used", out var n) && n.ValueKind == JsonValueKind.True;
            return new PiiDetectionResult(list, ner);
        }
        catch (JsonException)
        {
            throw new PiiUnavailableException("BAD_JSON");
        }
    }

    public async Task<PiiHealth?> GetHealthAsync(CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var resp = await _http.GetAsync("health", timeoutCts.Token);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(timeoutCts.Token));
            var ner = doc.RootElement.TryGetProperty("ner_enabled", out var n) && n.ValueKind == JsonValueKind.True;
            return new PiiHealth(ner);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            // 🔍 ใช้ตอบ health เท่านั้น — ไม่ใช่เส้นทางส่งข้อมูล
            return null;
        }
    }
}
