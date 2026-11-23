"""🔄 AL1: normalize ข้อความก่อนตรวจ PII พร้อม index map กลับข้อความต้นฉบับ"""

# 🔄 zero-width characters ที่ใช้หลบเลี่ยงการตรวจ
_ZERO_WIDTH = {0x200B, 0x200C, 0x200D, 0x2060, 0xFEFF}


def _map_digit(cp: int) -> int:
    # 🔄 เลขไทย ๐-๙ และ full-width ０-９ -> 0-9 (1:1)
    if 0x0E50 <= cp <= 0x0E59:
        return cp - 0x0E50 + 0x30
    if 0xFF10 <= cp <= 0xFF19:
        return cp - 0xFF10 + 0x30
    return cp


def normalize_text(text: str) -> tuple[str, list[int]]:
    """🔄 คืน (normalized, index_map) โดย index_map[i] = index ใน original ของตัวอักษรที่ i"""
    out: list[str] = []
    index_map: list[int] = []
    for i, ch in enumerate(text):
        cp = ord(ch)
        if cp in _ZERO_WIDTH:
            continue
        # 🔄 whitespace ทุกชนิด (NBSP, tab, newline ฯลฯ) -> ช่องว่างปกติ แบบ 1:1 เพื่อไม่ให้ offset เลื่อน
        out.append(" " if ch.isspace() else chr(_map_digit(cp)))
        index_map.append(i)
    return "".join(out), index_map


def map_span_to_original(index_map: list[int], ns: int, ne: int) -> tuple[int, int]:
    """⚠️ map span บน normalized กลับเป็น span บนข้อความต้นฉบับ (end exclusive)"""
    return index_map[ns], index_map[ne - 1] + 1
