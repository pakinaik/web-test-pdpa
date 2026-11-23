"""🔄 AL2/AL3: รวม detector ทุกตัว -> merge -> map offset กลับข้อความต้นฉบับ -> UTF-16"""
from app.detectors import bank, email, gender, person, phone, thai_id
from app.merge import merge_spans
from app.normalize import map_span_to_original, normalize_text
from app.offsets import build_utf16_prefix, to_utf16_span
from app.spans import Span


def detect_all(text: str, known_names: list[str] | None, enable_ner: bool) -> list[Span]:
    # 🔄 normalize ก่อนตรวจ (เลขไทย/full-width/zero-width)
    normalized, index_map = normalize_text(text)
    if not normalized:
        return []
    # 🔍 รัน detector ทุกประเภทบน normalized text
    spans: list[Span] = []
    for mod in (phone, thai_id, email, bank, gender):
        spans += mod.detect(normalized)
    spans += person.detect(normalized, known_names or [], enable_ner)
    merged = merge_spans(spans)
    # 🔄 map กลับข้อความต้นฉบับ แล้วแปลงเป็น UTF-16 offset (D2)
    prefix = build_utf16_prefix(text)
    result: list[Span] = []
    for s in merged:
        os_, oe = map_span_to_original(index_map, s.start, s.end)
        u_s, u_e = to_utf16_span(prefix, os_, oe)
        result.append(Span(s.type, u_s, u_e, s.score, s.source))
    return result
