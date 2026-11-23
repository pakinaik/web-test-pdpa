using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Admin;

public sealed record TypeScore(string Type, int Tp, int Fp, int Fn, double Precision, double Recall, double F1);
public sealed record BenchEntity(string Type, string Value);
public sealed record BenchFailure(string CaseKey, IReadOnlyList<BenchEntity> Missed, IReadOnlyList<BenchEntity> Unexpected);
public sealed record BenchOverall(int Tp, int Fp, int Fn, double Precision, double Recall, double F1);
public sealed record BenchReport(int TotalCases, IReadOnlyList<TypeScore> PerType, BenchOverall Overall, IReadOnlyList<BenchFailure> Failures);

/// <summary>🔍 รัน dataset ผ่าน PII service แล้ววัด recall/precision ต่อประเภท (match rule เดียวกับ pytest bench)</summary>
public sealed class PiiBenchService
{
    private static readonly PiiEntityType[] Types =
        [PiiEntityType.Person, PiiEntityType.Phone, PiiEntityType.ThaiId, PiiEntityType.Gender, PiiEntityType.Email, PiiEntityType.BankAccount];

    private readonly AppDbContext _db;
    private readonly IPiiDetectionClient _pii;

    public PiiBenchService(AppDbContext db, IPiiDetectionClient pii)
    {
        _db = db;
        _pii = pii;
    }

    private sealed record Exp(PiiEntityType Type, string Value);

    private static bool OverlapOk(int ps, int pe, int es, int ee)
    {
        var inter = Math.Min(pe, ee) - Math.Max(ps, es);
        return inter > 0 && (double)inter / (ee - es) >= 0.5;
    }

    private static double Ratio(int a, int b) => a + b == 0 ? 1.0 : (double)a / (a + b);

    private static double F1(double p, double r) => p + r == 0 ? 0 : 2 * p * r / (p + r);

    /// <exception cref="PiiUnavailableException">📤 pii ล่ม</exception>
    public async Task<BenchReport> RunAsync(CancellationToken ct)
    {
        var cases = await _db.PiiTestCases.AsNoTracking().OrderBy(c => c.CaseKey).ToListAsync(ct);
        var tp = Types.ToDictionary(t => t, _ => 0);
        var fp = Types.ToDictionary(t => t, _ => 0);
        var fn = Types.ToDictionary(t => t, _ => 0);
        var failures = new List<BenchFailure>();

        foreach (var c in cases)
        {
            var names = JsonSerializer.Deserialize<List<string>>(c.KnownNames) ?? [];
            var expected = new List<Exp>();
            foreach (var e in JsonSerializer.Deserialize<List<JsonElement>>(c.Expected) ?? [])
            {
                if (PiiEntityTypes.TryParse(e.GetProperty("type").GetString(), out var t))
                    expected.Add(new Exp(t, e.GetProperty("value").GetString() ?? ""));
            }

            var result = await _pii.DetectAsync(c.Text, names, ct);
            var used = new HashSet<int>();
            var missed = new List<BenchEntity>();
            foreach (var e in expected)
            {
                var idx = c.Text.IndexOf(e.Value, StringComparison.Ordinal);
                var hit = -1;
                if (idx >= 0)
                {
                    for (var i = 0; i < result.Entities.Count; i++)
                    {
                        var p = result.Entities[i];
                        if (!used.Contains(i) && p.Type == e.Type && OverlapOk(p.Start, p.End, idx, idx + e.Value.Length)) { hit = i; break; }
                    }
                }
                if (hit < 0) { fn[e.Type]++; missed.Add(new BenchEntity(PiiEntityTypes.ToWire(e.Type), e.Value)); }
                else { used.Add(hit); tp[e.Type]++; }
            }

            var unexpected = new List<BenchEntity>();
            for (var i = 0; i < result.Entities.Count; i++)
            {
                if (used.Contains(i)) continue;
                var p = result.Entities[i];
                if (fp.ContainsKey(p.Type)) fp[p.Type]++;
                unexpected.Add(new BenchEntity(PiiEntityTypes.ToWire(p.Type), c.Text[p.Start..p.End]));
            }
            if (missed.Count > 0 || unexpected.Count > 0) failures.Add(new BenchFailure(c.CaseKey, missed, unexpected));
        }

        var perType = Types.Select(t =>
        {
            var p = Ratio(tp[t], fp[t]);
            var r = Ratio(tp[t], fn[t]);
            return new TypeScore(PiiEntityTypes.ToWire(t), tp[t], fp[t], fn[t], Math.Round(p, 3), Math.Round(r, 3), Math.Round(F1(p, r), 3));
        }).ToList();
        var TP = tp.Values.Sum(); var FP = fp.Values.Sum(); var FN = fn.Values.Sum();
        var op = Ratio(TP, FP); var or = Ratio(TP, FN);
        return new BenchReport(cases.Count, perType, new BenchOverall(TP, FP, FN, Math.Round(op, 3), Math.Round(or, 3), Math.Round(F1(op, or), 3)), failures);
    }
}
