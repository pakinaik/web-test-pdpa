import pytest

from app.detectors.phone import detect
from app.normalize import map_span_to_original, normalize_text


def _found(text):
    n, m = normalize_text(text)
    out = []
    for s in detect(n):
        a, b = map_span_to_original(m, s.start, s.end)
        out.append(text[a:b])
    return out


@pytest.mark.parametrize("text,expected", [
    ("โทร 081-234-5678 ด่วน", "081-234-5678"),
    ("โทร 0812345678", "0812345678"),
    ("โทร 081 234 5678", "081 234 5678"),
    ("โทร 081.234.5678", "081.234.5678"),
    ("โทร ๐๘๑-๒๓๔-๕๖๗๘", "๐๘๑-๒๓๔-๕๖๗๘"),
    ("โทร +66 81 234 5678", "+66 81 234 5678"),
    ("โทร +66812345678", "+66812345678"),
    ("โทร 08\u200b1-234-5678", "08\u200b1-234-5678"),
    ("โทร ０８１２３４５６７８", "０８１２３４５６７８"),
    ("โทร (02) 123 4567", "(02) 123 4567"),
    ("โทร 02-123-4567", "02-123-4567"),
])
def test_positive(text, expected):
    assert expected in _found(text)


@pytest.mark.parametrize("text", [
    "ยอดเงิน 25,000 บาท",
    "เลข 12345 ไม่ใช่เบอร์",
    "วันที่ 12/03/2567",
    "เลขคำร้อง 1101700234568",
])
def test_negative(text):
    assert _found(text) == []


def test_offset_points_to_original():
    t = "ติดต่อ 081-234-5678 นะ"
    n, m = normalize_text(t)
    s = detect(n)[0]
    a, b = map_span_to_original(m, s.start, s.end)
    assert t[a:b] == "081-234-5678"


@pytest.mark.parametrize("sep", [" ", "\t", "\n", " ", "　"])
def test_phone_separated_by_unicode_whitespace(sep):
    # 🔍 B2: เบอร์ที่คั่นด้วย NBSP/tab/newline ต้อง detect และ offset ชี้กลับต้นฉบับถูก
    from app.pipeline import detect_all

    text = f"tel 081{sep}234{sep}5678 ok"
    spans = detect_all(text, [], False)
    assert [s.type for s in spans] == ["PHONE"]
    assert text[spans[0].start:spans[0].end] == f"081{sep}234{sep}5678"


@pytest.mark.parametrize("text", [
    "โทร 081\r\n234\r\n5678 ครับ",
    "โทร 081  234  5678 ครับ",
    "โทร 081 - 234 - 5678 ครับ",
])
def test_phone_multi_char_separators(text):
    # 🔍 N2: ตัวคั่นหลายตัว (CRLF, 2 ช่องว่าง, " - ") ต้องถูกจับและ offset ชี้กลับต้นฉบับ
    from app.pipeline import detect_all

    spans = [s for s in detect_all(text, [], False) if s.type == "PHONE"]
    assert len(spans) == 1
    assert text[spans[0].start:spans[0].end].startswith("081")
    assert text[spans[0].start:spans[0].end].endswith("5678")


def test_phone_separator_limit_not_overreaching():
    # 🔍 N2: ตัวคั่นเกิน 3 ตัวไม่ถือเป็นเบอร์เดียวกัน
    assert _found("โทร 081    234    5678") == []
