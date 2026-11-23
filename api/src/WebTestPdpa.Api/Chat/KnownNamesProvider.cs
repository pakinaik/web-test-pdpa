using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebTestPdpa.Api.Data;

namespace WebTestPdpa.Api.Chat;

/// <summary>📦 รายชื่อพนักงาน (th + en) ส่งให้ PII service เป็น known_names — cache 5 นาที</summary>
public sealed class KnownNamesProvider
{
    private const string Key = "known_names";
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public KnownNamesProvider(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<string>> GetAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(Key, out IReadOnlyList<string>? cached) && cached is not null) return cached;

        var rows = await _db.Users.AsNoTracking()
            .Select(u => new { u.FirstNameTh, u.LastNameTh, u.FirstNameEn, u.LastNameEn })
            .ToListAsync(ct);
        var names = new List<string>();
        foreach (var r in rows)
        {
            var th = $"{r.FirstNameTh} {r.LastNameTh}".Trim();
            var en = $"{r.FirstNameEn} {r.LastNameEn}".Trim();
            if (th.Length > 0 && th.Length <= 200) names.Add(th);
            if (en.Length > 0 && en.Length <= 200) names.Add(en);
        }
        IReadOnlyList<string> result = names.Distinct().Take(5000).ToList();
        _cache.Set(Key, result, TimeSpan.FromMinutes(5));
        return result;
    }

    /// <summary>🧹 เรียกเมื่อสร้างผู้ใช้ใหม่</summary>
    public void Invalidate() => _cache.Remove(Key);
}
