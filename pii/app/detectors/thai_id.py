"""🔍 THAI_ID detector (AL2) — regex 13 หลัก + checksum mod 11"""
import re

from app.spans import Span

_RX = re.compile(r"(?<![0-9])[0-9](?:[ \-.]{0,3}[0-9]){12}(?![0-9])", re.ASCII)


def is_valid_thai_id(digits: str) -> bool:
    # 🔍 s = Σ d[i]*(13-i), i=0..11 ; valid เมื่อ (11 - s % 11) % 10 == d[12]
    if len(digits) != 13 or not digits.isdigit():
        return False
    s = sum(int(digits[i]) * (13 - i) for i in range(12))
    return (11 - s % 11) % 10 == int(digits[12])


def detect(text: str) -> list[Span]:
    spans: list[Span] = []
    for m in _RX.finditer(text):
        digits = re.sub(r"[^0-9]", "", m.group())
        if is_valid_thai_id(digits):
            spans.append(Span("THAI_ID", m.start(), m.end(), 1.0, "checksum"))
    return spans
