"""🔄 resolve overlap ของ span จากหลาย detector (AL2 Overlap priority)"""
from app.spans import Span

# 🔍 ลำดับความสำคัญเมื่อยาวเท่ากัน (น้อย = ชนะ)
_PRIORITY = {"THAI_ID": 0, "BANK_ACCOUNT": 1, "PHONE": 2, "EMAIL": 3, "PERSON": 4, "GENDER": 5}


def _rank(s: Span) -> tuple:
    # 🔍 เรียงตามความยาว (ยาวกว่าชนะ) > priority ตามประเภท > ตำแหน่งเริ่ม; กรณี BANK_ACCOUNT vs PHONE จัดการแยกใน _wins()
    return (-(s.end - s.start), _PRIORITY.get(s.type, 99), s.start)


def _wins(a: Span, b: Span) -> bool:
    types = {a.type, b.type}
    if types == {"BANK_ACCOUNT", "PHONE"}:
        return a.type == "BANK_ACCOUNT"
    return _rank(a) < _rank(b)


def merge_spans(spans: list[Span]) -> list[Span]:
    """🔄 D27: รวม span ที่ซ้อนกันแบบ union (transitive) เป็นช่วงเดียว [min start, max end]
    type/source/score เอาจากตัวที่ชนะตามกติกาเดิม — ไม่ทิ้งส่วนที่ไม่ซ้อนของผู้แพ้ (over-mask ตาม D8)"""
    ordered = sorted(spans, key=lambda x: (x.start, x.end))
    groups: list[list[Span]] = []
    group_end = -1
    for s in ordered:
        # 🔍 ซ้อนกับกลุ่มปัจจุบันเมื่อ start < end ของกลุ่ม (ติดกันเฉยๆ ไม่รวม)
        if groups and s.start < group_end:
            groups[-1].append(s)
            group_end = max(group_end, s.end)
        else:
            groups.append([s])
            group_end = s.end
    result: list[Span] = []
    for g in groups:
        # ⚠️ หาผู้ชนะในกลุ่มด้วยกติกาเดิม (ยาวกว่า/priority/BANK>PHONE) เรียงก่อนให้ผลคงที่
        winner = None
        for m in sorted(g, key=_rank):
            if winner is None or _wins(m, winner):
                winner = m
        start = min(m.start for m in g)
        end = max(m.end for m in g)
        result.append(Span(winner.type, start, end, winner.score, winner.source))
    return result
