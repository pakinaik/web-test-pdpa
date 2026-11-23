"""🔍 BANK_ACCOUNT detector (AL2) — ตัวเลข 10-12 หลักที่มี keyword บริบทภายใน 30 ตัวอักษรก่อนหน้า"""
import re

from app.spans import Span

_NUM = re.compile(r"(?<![0-9])[0-9](?:[ \-]?[0-9]){9,11}(?![0-9])", re.ASCII)
_CTX = re.compile(r"บัญชี|เลขที่บัญชี|บช\.?|ธนาคาร|a/c|acct|account", re.IGNORECASE)
_WINDOW = 30


def detect(text: str) -> list[Span]:
    spans: list[Span] = []
    for m in _NUM.finditer(text):
        digits = re.sub(r"[^0-9]", "", m.group())
        if not 10 <= len(digits) <= 12:
            continue
        # 🔍 ต้องมี keyword บริบทบัญชีก่อนหน้า
        before = text[max(0, m.start() - _WINDOW): m.start()]
        # ⚠️ ตัดบริบทที่อยู่ก่อนตัวเลขตัวสุดท้ายของเลขชุดก่อนหน้า กัน keyword ของเลขอื่นมาครอบเบอร์โทร
        last_digit = max((i for i, ch in enumerate(before) if ch.isdigit()), default=-1)
        before = before[last_digit + 1:]
        if _CTX.search(before):
            spans.append(Span("BANK_ACCOUNT", m.start(), m.end(), 0.9, "regex"))
    return spans
