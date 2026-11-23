"""🔍 PHONE detector (AL2) — รันบน normalized text (เลขอารบิกแล้ว)"""
import re

from app.spans import Span

# 🔍 ตัวคั่นที่อนุญาตระหว่างตัวเลข (ช่องว่าง . - ได้ 0-3 ตัว เช่น CRLF ที่ normalize เป็น 2 ช่องว่าง, " - ")
_SEP = r"[ .\-]{0,3}"


def _digits(n: int) -> str:
    return "".join(f"{_SEP}[0-9]" for _ in range(n))


# 🔍 มือถือ: 0[689]xxxxxxxx หรือ +66/66 ตามด้วย [689]... (รองรับ (0) หลัง +66)
_MOBILE = re.compile(
    r"(?<![0-9])(?:\+?66[ \-]?\(?0?\)?[ \-]?|0)[689][0-9]" + _digits(7) + r"(?![0-9])",
    re.ASCII,
)
# 🔍 เบอร์บ้าน: 0[2-7]xxxxxxx รองรับ (02) 123 4567
_LANDLINE = re.compile(
    r"(?<![0-9])(?:\+?66[ \-]?\(?0?\)?[ \-]?|\(?0\)?[ \-]?)[2-7]\)?" + _digits(7) + r"(?![0-9])",
    re.ASCII,
)


def detect(text: str) -> list[Span]:
    spans: list[Span] = []
    for rx in (_MOBILE, _LANDLINE):
        for m in rx.finditer(text):
            s, e = m.start(), m.end()
            # 🔍 ตัดตัวคั่นท้ายที่อาจติดมา
            while e > s and text[e - 1] in " .-":
                e -= 1
            spans.append(Span("PHONE", s, e, 1.0, "regex"))
    return spans
