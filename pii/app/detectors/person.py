"""🔍 PERSON detector (AL3): known_names + title pattern + อังกฤษ + NER (ตัวเสริม)"""
import re
import string

from app.spans import Span
from app.titles import TITLE_ANY, TITLE_BEFORE, TITLE_EN, TITLE_TH

_THAI_CH = "\u0E01-\u0E4E"
_THAI_RUN = re.compile("[" + _THAI_CH + "]+")
_LOWER = str.maketrans(string.ascii_uppercase, string.ascii_lowercase)

# 🔍 คำที่ตามหลังชื่อแล้วไม่ใช่นามสกุล
STOP_FOLLOW_WORDS = [
    "เบอร์", "โทร", "อีเมล", "เลข", "บัตร", "แผนก", "ฝ่าย", "โดน", "ได้", "ไม่", "ที่", "ซึ่ง",
    "และ", "ครับ", "ค่ะ", "คะ", "ช่วย", "บอก", "แจ้ง", "เพศ", "บัญชี", "ถูก", "มี", "เป็น", "ขอ",
    "ร้อง", "ทำ", "ไป", "มา", "ว่า", "แล้ว", "จึง", "เมื่อ", "กับ", "ของ", "ใน", "จาก",
]
# ⚠️ D32: stop word เฉพาะช่องนามสกุล (subset ของ STOP_FOLLOW_WORDS) ใช้ตัดข้อความที่พิมพ์ติดหลังนามสกุล
# ไม่ใส่คำสั้นที่เป็นส่วนของนามสกุลได้ (มี/มา/ได้/ใน/เป็น/ไป/ทำ/ที่/กับ/ของ/จาก/ถูก/ขอ/แจ้ง/ร้อง/บอก/แล้ว/ช่วย) เพราะจะตัดนามสกุลจนรั่ว
_SURNAME_STOP_WORDS = [
    "เบอร์", "โทร", "อีเมล", "เลข", "บัตร", "บัญชี", "แผนก", "ฝ่าย", "เพศ", "โดน", "ไม่",
    "ซึ่ง", "และ", "จึง", "เมื่อ", "ครับ", "ค่ะ", "คะ", "ว่า",
]
# 🔍 คำประสมที่ขึ้นต้นด้วยคำนำหน้าแต่ไม่ใช่ชื่อคน
_EXCLUDED_COMPOUNDS = [
    "นายจ้าง", "นายหน้า", "นายทุน", "คุณสมบัติ", "คุณภาพ", "คุณค่า", "คุณธรรม", "คุณครู",
    "นางแบบ", "นางงาม",
]
# ⚠️ D28: run ไทยที่ยาวเกินค่านี้จะตัดด้วย newmm + stop-word cut (ค่า 10 เพื่อให้ "สมชายโดนหักเงิน" 15 ตัวถูกตัด)
_MAX_UNSPACED_RUN = 10

_TH_TITLE = re.compile(r"(?<![" + _THAI_CH + r"])(" + "|".join(TITLE_TH) + r")[ \t]*")
_EN_TITLE = re.compile(
    r"(?<![A-Za-z])(?i:" + TITLE_EN + r")\s*([A-Z][a-z]+(?:\s+[A-Z][a-z]+){0,2})"
)

_SURNAME_RX = re.compile(r"[ \t]+([" + _THAI_CH + r"]{2,})(?![" + _THAI_CH + "])")
# ⚠️ D30: "คุณ" เป็นสรรพนามจึงมีโอกาสตามด้วยคำทั่วไป; คำนำหน้าไทยอื่นใน titles.py (นาย/นาง/นางสาว/น.ส./ด.ช./ด.ญ./ดร.) ไม่ใช่สรรพนาม
_PRONOUN_TITLE = "คุณ"

_ner_singleton = None


def ascii_lower(s: str) -> str:
    # 🔄 lower เฉพาะ ASCII เพื่อให้ความยาว/offset ไม่เปลี่ยน
    return s.translate(_LOWER)


def _extend_title(text: str, start: int) -> int:
    # 🔄 ถ้ามีคำนำหน้าติดอยู่ก่อนชื่อ ให้ขยาย span รวมคำนำหน้า
    m = TITLE_BEFORE.search(text[max(0, start - 12):start])
    if m:
        return max(0, start - 12) + m.start()
    return start


_STOP_SET = frozenset(STOP_FOLLOW_WORDS)
_SURNAME_STOP_SET = frozenset(_SURNAME_STOP_WORDS)


def _is_stop(word: str) -> bool:
    # 🔍 D29: เทียบ token ตรงตัว (ไม่ใช้ prefix) — ทั้งคำอยู่ในชุด หรือ token แรกของ newmm อยู่ในชุด
    if word in _STOP_SET:
        return True
    from pythainlp.tokenize import word_tokenize

    toks = word_tokenize(word, engine="newmm")
    return bool(toks) and toks[0] in _STOP_SET


def detect_known_names(text: str, known_names: list[str]) -> list[Span]:
    spans: list[Span] = []
    low = ascii_lower(text)
    for name in known_names:
        parts = name.split()
        if not parts:
            continue
        first = parts[0]
        last = parts[-1] if len(parts) > 1 else ""
        fl = ascii_lower(first)
        if fl not in low:
            continue
        # 🔍 (a) first + last (ช่องว่างยืดหยุ่น)
        if last:
            rx = re.compile(re.escape(first) + r"\s*" + re.escape(last), re.IGNORECASE)
            for m in rx.finditer(text):
                spans.append(Span("PERSON", _extend_title(text, m.start()), m.end(), 1.0, "known_name"))
        # 🔍 (b) first name เดี่ยว ยาว >= 3
        if len(first) >= 3:
            is_en = first.isascii()
            pos = low.find(fl)
            while pos != -1:
                end = pos + len(first)
                ok = True
                if is_en:
                    ok = (pos == 0 or not text[pos - 1].isalpha()) and (end == len(text) or not text[end].isalpha())
                if ok:
                    spans.append(Span("PERSON", _extend_title(text, pos), end, 0.9, "known_name"))
                pos = low.find(fl, pos + 1)
    return spans


def _surname_end(text: str, name_end: int) -> int | None:
    # 🔍 หานามสกุล: คำไทย >= 2 ตัวหลังช่องว่าง (R6.7) และต้องไม่ใช่ stop word ทั้งคำ (D31) -> คืนตำแหน่งจบ หรือ None
    sm = _SURNAME_RX.match(text, name_end)
    if not sm:
        return None
    word = sm.group(1)
    if _is_all_stop(word):
        return None
    # 🔄 D32: ตัดขอบนามสกุลที่พิมพ์ติดข้อความ — newmm แล้วเก็บ token จนก่อน stop ชุดนามสกุลแรก
    from pythainlp.tokenize import word_tokenize

    toks = word_tokenize(word, engine="newmm")
    if len(toks) < 2 or "".join(toks) != word:
        # ⚠️ newmm ได้ token เดียว/join ไม่ตรง -> เอาทั้งคำ (over-mask ตาม D8)
        return sm.end()
    # ⚠️ token แรกเก็บเสมอ (ถ้าสั้น <= 2 ตัวเก็บ 2 token ตามกติกา D28)
    keep = 2 if len(toks[0]) <= 2 else 1
    while keep < len(toks) and toks[keep] not in _SURNAME_STOP_SET:
        keep += 1
    return sm.start(1) + len("".join(toks[:keep]))


def _is_all_stop(word: str) -> bool:
    # 🔍 D31: คำหลังชื่อไม่ใช่นามสกุลเฉพาะเมื่อ "ทั้งคำ" หรือ "ทุก token ของ newmm" เป็น stop word
    # (แจ้งว่า, มาแล้ว, โทรมา) — นามสกุลที่ขึ้นต้น stop token แต่มี token อื่นปน (มา|ดี, เป็น|เอก) ต้องไม่ถูกทิ้ง
    if word in _STOP_SET:
        return True
    from pythainlp.tokenize import word_tokenize

    toks = word_tokenize(word, engine="newmm")
    return bool(toks) and all(t in _STOP_SET for t in toks)


def _thai_title_spans(text: str) -> list[Span]:
    from pythainlp.tokenize import word_tokenize

    spans: list[Span] = []
    for m in _TH_TITLE.finditer(text):
        title_end = m.end()
        run_m = _THAI_RUN.match(text, title_end)
        if not run_m:
            continue
        run = run_m.group()
        head = text[m.start(1):title_end] + run
        if any(head.startswith(x) for x in _EXCLUDED_COMPOUNDS):
            continue
        name_end = run_m.end()
        cut_by_stop = False
        if len(run) > _MAX_UNSPACED_RUN:
            # 🔄 D28/D29: ตัดคำด้วย newmm แล้วเก็บ token ไล่ไปจนก่อน token แรกที่เป็น stop word (ตรงตัว)
            toks = word_tokenize(run, engine="newmm")
            keep = 1
            if len(toks[0]) <= 2 and len(toks) > 1:
                keep = 2
            while keep < len(toks) and not _is_stop(toks[keep]):
                keep += 1
            if keep < len(toks):
                # ⚠️ เจอ stop word -> ตัดตรงนั้น (ชื่อติดคำอื่น ไม่มีนามสกุลต่อ)
                name_end = title_end + len("".join(toks[:keep]))
                cut_by_stop = True
            # ⚠️ ไม่เจอ stop word -> เอาทั้ง run (over-mask ตาม D8) แล้วหานามสกุลต่อ
        elif m.group(1) == _PRONOUN_TITLE and _is_stop(run) and _surname_end(text, name_end) is None:
            # ⚠️ D29/D30: ข้ามเฉพาะ "คุณ" (สรรพนาม) + run เป็น stop word จริง + ไม่มีนามสกุลตามมา
            # คำนำหน้าอื่น (นาย/นาง/ดร. ฯลฯ) ไม่ใช่สรรพนาม -> ไม่ข้าม เพราะ newmm อาจตัดชื่อ (มานิตย์ -> มา|นิตย์)
            continue
        # 🔍 นามสกุล: คำไทยถัดไปหลังช่องว่าง 1 คำ
        if not cut_by_stop:
            se = _surname_end(text, name_end)
            if se is not None:
                name_end = se
        spans.append(Span("PERSON", m.start(1), name_end, 0.9, "title"))
    return spans


def _english_title_spans(text: str) -> list[Span]:
    return [Span("PERSON", m.start(), m.end(), 0.9, "title") for m in _EN_TITLE.finditer(text)]


def get_ner():
    """🚀 โหลด NER (thainer) แบบ lazy singleton — โหลดไม่ได้ให้ throw (fail-closed D24)"""
    global _ner_singleton
    if _ner_singleton is None:
        from pythainlp.tag import NER

        _ner_singleton = NER(engine="thainer")
    return _ner_singleton


def ner_loaded() -> bool:
    return _ner_singleton is not None


def detect_ner(text: str) -> list[Span]:
    ner = get_ner()
    tagged = ner.tag(text, tag=False)
    spans: list[Span] = []
    cursor = 0
    cur_start = cur_end = None
    for tok, tag in tagged:
        # 🔄 หา offset ของ token (ต่อ token ตามลำดับ, fallback find)
        if text.startswith(tok, cursor):
            s = cursor
        else:
            s = text.find(tok, cursor)
            if s == -1:
                continue
        e = s + len(tok)
        cursor = e
        if tag == "B-PERSON":
            if cur_start is not None:
                spans.append(Span("PERSON", _extend_title(text, cur_start), cur_end, 0.7, "ner"))
            cur_start, cur_end = s, e
        elif tag == "I-PERSON" and cur_start is not None:
            cur_end = e
        else:
            if cur_start is not None:
                spans.append(Span("PERSON", _extend_title(text, cur_start), cur_end, 0.7, "ner"))
                cur_start = cur_end = None
    if cur_start is not None:
        spans.append(Span("PERSON", _extend_title(text, cur_start), cur_end, 0.7, "ner"))
    return spans


def detect(text: str, known_names: list[str], enable_ner: bool) -> list[Span]:
    spans = detect_known_names(text, known_names)
    spans += _thai_title_spans(text)
    spans += _english_title_spans(text)
    if enable_ner:
        spans += detect_ner(text)
    return spans
