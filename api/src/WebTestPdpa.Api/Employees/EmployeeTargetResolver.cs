using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Employees;

/// <summary>🔍 ผลระบุพนักงานเป้าหมาย (employees.id) — Ambiguous = ชื่อต้นตรงหลายคน (ไม่เลือกให้)</summary>
public sealed record TargetResolution(IReadOnlyList<int> EmployeeIds, bool Ambiguous);

/// <summary>
/// 🔍 R7d (D40): ตัดสินสิทธิ์ฝั่ง server เท่านั้น จาก role (JWT claim) + employees.user_id
/// ⚠️ role Employee = ตัวเองเสมอ ไม่สนข้อความ; HR/Admin ระบุคนอื่นได้ด้วยรหัส/ชื่อ (สูงสุด 3, ไม่ sticky)
/// ⚠️ ห้าม log ข้อความ/ชื่อ/รหัสที่ resolve
/// </summary>
public sealed class EmployeeTargetResolver
{
    public const int MaxTargets = 3;

    private static readonly Regex CodeRx = new(@"(?<![a-z0-9])emp[0-9]{3}(?![0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, PiiRegex.Timeout);

    private readonly AppDbContext _db;

    public EmployeeTargetResolver(AppDbContext db) => _db = db;

    private static bool IsPrivileged(IReadOnlyCollection<string>? roles) =>
        roles is not null && roles.Any(r => string.Equals(r, "HR", StringComparison.OrdinalIgnoreCase)
                                            || string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));

    public async Task<TargetResolution> ResolveAsync(
        Guid userId, IReadOnlyCollection<string>? roles, string text, bool selfReference, CancellationToken ct)
    {
        // 📦 พนักงานที่ผูกกับ user นี้ (ถ้ามี)
        var selfId = await _db.Employees.AsNoTracking()
            .Where(e => e.UserId == userId).Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);

        // ⚠️ role ทั่วไป: ตัวเองเท่านั้น ไม่สนว่าข้อความพิมพ์รหัส/ชื่อใคร
        if (!IsPrivileged(roles))
        {
            return new TargetResolution(selfId is null ? [] : [selfId.Value], false);
        }

        var norm = Collapse(TextNormalizer.Normalize(text));
        var all = await _db.Employees.AsNoTracking()
            .Select(e => new { e.Id, e.EmployeeCode, e.FirstNameTh, e.LastNameTh, e.FirstNameEn, e.LastNameEn })
            .ToListAsync(ct);

        // 🔄 (id -> ตำแหน่งแรกที่พบในข้อความ)
        var found = new Dictionary<int, int>();
        void Add(int id, int pos)
        {
            if (!found.TryGetValue(id, out var cur) || pos < cur) found[id] = pos;
        }

        // 🔍 1) รหัสพนักงานที่มีจริง
        var byCode = all.ToDictionary(e => e.EmployeeCode.ToLowerInvariant(), e => e.Id);
        foreach (Match m in CodeRx.Matches(norm))
        {
            if (byCode.TryGetValue(m.Value.ToLowerInvariant(), out var id)) Add(id, m.Index);
        }

        // 🔍 2) ชื่อเต็ม th/en
        foreach (var e in all)
        {
            foreach (var full in new[] { $"{e.FirstNameTh} {e.LastNameTh}", $"{e.FirstNameEn} {e.LastNameEn}" })
            {
                var f = Collapse(full);
                if (f.Length < 3) continue;
                var i = norm.IndexOf(f, StringComparison.OrdinalIgnoreCase);
                if (i >= 0) Add(e.Id, i);
            }
        }

        // 🔍 3) ชื่อต้นไทยเดี่ยว (>= 3 ตัว) ที่ตรงคนเดียวเท่านั้น; ตรงหลายคน = กำกวม
        var ambiguous = false;
        foreach (var grp in all.Where(e => Collapse(e.FirstNameTh).Length >= 3).GroupBy(e => Collapse(e.FirstNameTh).ToLowerInvariant()))
        {
            var i = norm.IndexOf(grp.Key, StringComparison.OrdinalIgnoreCase);
            if (i < 0) continue;
            if (grp.Count() == 1) Add(grp.First().Id, i);
            else ambiguous = true;
        }

        var ids = found.OrderBy(kv => kv.Value).Select(kv => kv.Key).Take(MaxTargets).ToList();
        if (ids.Count == 0 && !ambiguous && selfReference && selfId is not null) ids.Add(selfId.Value);

        // ⚠️ ชื่อกำกวมและไม่มี target อื่น = ไม่มี target (ไม่เดา)
        return new TargetResolution(ids, ambiguous && ids.Count == 0);
    }

    // 🔄 ยุบ whitespace ให้เป็นช่องว่างเดียว + trim
    private static string Collapse(string s) => PiiRegex.Whitespace.Replace(s, " ").Trim();
}
