"""🔍 EMAIL detector (AL2) รองรับรูปแบบหลบเลี่ยง (at)/(dot)"""
import re

from app.spans import Span

_AT = r"(?:@|\(at\)|\[at\])"
_DOT = r"(?:\.|\(dot\)|\[dot\])"
_RX = re.compile(
    r"[A-Za-z0-9._%+\-]+\s*" + _AT + r"\s*[A-Za-z0-9\-]+(?:\s*" + _DOT + r"\s*[A-Za-z0-9\-]+)+",
    re.IGNORECASE | re.ASCII,
)
_TLD = re.compile(r"[A-Za-z]{2,}$")


def detect(text: str) -> list[Span]:
    spans: list[Span] = []
    for m in _RX.finditer(text):
        # 🔍 TLD (ส่วนสุดท้าย) ต้องเป็นตัวอักษร ≥ 2 ตัว
        last = re.split(r"\.|\(dot\)|\[dot\]", m.group(), flags=re.IGNORECASE)[-1].strip()
        if _TLD.search(last):
            spans.append(Span("EMAIL", m.start(), m.end(), 1.0, "regex"))
    return spans
