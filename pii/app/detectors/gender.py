"""🔍 GENDER detector (AL2) — ตรวจเฉพาะบริบทเพศ ไม่จับคำที่เป็นส่วนของคำอื่น เช่น ชายหาด"""
import re

from app.spans import Span

_THAI = r"\u0E00-\u0E7F"

# 🔍 (1) "เพศ: ชาย" -> mask เฉพาะคำหลังเพศ
_AFTER_SEX = re.compile(r"เพศ\s*[:：]?\s*(ชาย|หญิง|ทางเลือก)")
# 🔍 (2) ชาย/หญิง ที่ทั้งสองฝั่งเป็นขอบข้อความ/whitespace/วงเล็บ/สแลช/คอมมา (ไม่ใช่ส่วนของคำ)
_STANDALONE = re.compile(r"(?:^|(?<=[\s(\[/,]))(ชาย|หญิง)(?=$|[\s)\]/,.!?])")
# 🔍 (3) อังกฤษ: gender/sex: male|female|m|f และ male/female เดี่ยวๆ
_EN_LABEL = re.compile(r"\b(?:gender|sex)\s*[:=]?\s*(male|female|m|f)\b", re.IGNORECASE | re.ASCII)
_EN_WORD = re.compile(r"\b(male|female)\b", re.IGNORECASE | re.ASCII)


def detect(text: str) -> list[Span]:
    spans: list[Span] = []
    for rx, score in ((_AFTER_SEX, 0.95), (_EN_LABEL, 0.95), (_STANDALONE, 0.8), (_EN_WORD, 0.9)):
        for m in rx.finditer(text):
            spans.append(Span("GENDER", m.start(1), m.end(1), score, "rule"))
    return spans
