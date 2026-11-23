namespace WebTestPdpa.Api.Pii;

/// <summary>🔄 span ที่ผ่านการ resolve overlap แล้ว (Expanded = ช่วงถูกขยายเกินช่วงของผู้ชนะจากการ union — N1)</summary>
public sealed record MaskSpan(PiiEntityType Type, int Start, int End, string Source, bool Expanded = false);

/// <summary>🔄 AL5 ข้อ 3: รวม entity จาก pii + custom แล้ว resolve overlap</summary>
public static class SpanResolver
{
    // 🔍 priority เมื่อยาวเท่ากัน (น้อย = ชนะ) CUSTOM ท้ายสุด
    private static int Priority(PiiEntityType t) => t switch
    {
        PiiEntityType.ThaiId => 0,
        PiiEntityType.BankAccount => 1,
        PiiEntityType.Phone => 2,
        PiiEntityType.Email => 3,
        PiiEntityType.Person => 4,
        PiiEntityType.Gender => 5,
        PiiEntityType.Custom => 7,
        _ => 6
    };

    private static bool Wins(MaskSpan a, MaskSpan b)
    {
        // ⚠️ BANK_ACCOUNT (มีบริบท) ชนะ PHONE เสมอเมื่อ overlap
        if (a.Type == PiiEntityType.BankAccount && b.Type == PiiEntityType.Phone) return true;
        if (a.Type == PiiEntityType.Phone && b.Type == PiiEntityType.BankAccount) return false;
        var la = a.End - a.Start;
        var lb = b.End - b.Start;
        if (la != lb) return la > lb;
        var pa = Priority(a.Type);
        var pb = Priority(b.Type);
        if (pa != pb) return pa < pb;
        return a.Start < b.Start;
    }

    /// <exception cref="PiiUnavailableException">⚠️ offset ผิดช่วง (fail-closed)</exception>
    public static IReadOnlyList<MaskSpan> Merge(IEnumerable<MaskSpan> spans, int textLength)
    {
        var list = new List<MaskSpan>();
        foreach (var s in spans)
        {
            // 🔍 validate offset: 0 <= start < end <= length
            if (s.Start < 0 || s.Start >= s.End || s.End > textLength)
            {
                throw new PiiUnavailableException("INVALID_OFFSET");
            }
            list.Add(s);
        }

        // 🔄 D27: รวม span ที่ซ้อนกันแบบ union (transitive) เป็นช่วงเดียว [min start, max end]
        // 🔄 type/source เอาจากผู้ชนะตามกติกาเดิม — ไม่ทิ้งส่วนที่ไม่ซ้อนของผู้แพ้ (over-mask ตาม D8)
        var ordered = list.OrderBy(s => s.Start).ThenBy(s => s.End).ToList();
        var groups = new List<List<MaskSpan>>();
        var groupEnd = -1;
        foreach (var s in ordered)
        {
            // 🔍 ซ้อนกับกลุ่มปัจจุบันเมื่อ start < end ของกลุ่ม (ติดกันเฉยๆ ไม่รวม)
            if (groups.Count > 0 && s.Start < groupEnd)
            {
                groups[^1].Add(s);
                groupEnd = Math.Max(groupEnd, s.End);
            }
            else
            {
                groups.Add([s]);
                groupEnd = s.End;
            }
        }

        var result = new List<MaskSpan>(groups.Count);
        foreach (var g in groups)
        {
            // ⚠️ หาผู้ชนะในกลุ่มด้วยกติกาเดิม (ยาวกว่า/priority/BANK>PHONE) เรียงก่อนให้ผลคงที่
            MaskSpan? winner = null;
            foreach (var m in g.OrderByDescending(x => x.End - x.Start).ThenBy(x => Priority(x.Type)).ThenBy(x => x.Start))
            {
                if (winner is null || Wins(m, winner)) winner = m;
            }
            var start = g.Min(x => x.Start);
            var end = g.Max(x => x.End);
            // ⚠️ N1: ถ้าช่วงรวมกว้างกว่าผู้ชนะ ต้องแจ้งให้ canonical ไม่ใช้กติกาของ type ผู้ชนะ (เช่น digits-only)
            result.Add(new MaskSpan(winner!.Type, start, end, winner.Source, start != winner.Start || end != winner.End));
        }
        return result;
    }
}
