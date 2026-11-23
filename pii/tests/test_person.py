import pytest

from app.normalize import normalize_text
from app.pipeline import detect_all


def _persons(text, names=None, ner=False):
    return [text[s.start:s.end] for s in detect_all(text, names or [], ner) if s.type == "PERSON"]


def test_known_full_name():
    assert _persons("สมชาย ใจดี โดนหักเงิน", ["สมชาย ใจดี"]) == ["สมชาย ใจดี"]


def test_known_first_name_only():
    assert "สมชาย" in _persons("สมชายแจ้งปัญหา", ["สมชาย ใจดี"])


def test_known_name_with_title_expands():
    assert _persons("นายสมชาย ใจดี", ["สมชาย ใจดี"]) == ["นายสมชาย ใจดี"]


def test_known_english_case_insensitive():
    assert _persons("ask john smith please", ["John Smith"]) == ["john smith"]


def test_english_word_boundary():
    assert _persons("johnson is here", ["John Smith"]) == []


@pytest.mark.parametrize("text,expected", [
    ("นายสมชาย ใจดี โทร", "นายสมชาย ใจดี"),
    ("นางสาวสมหญิง รักงาน ร้อง", "นางสาวสมหญิง รักงาน"),
    ("คุณสมศรี มาแล้ว", "คุณสมศรี"),
    ("Mr. John Smith called", "Mr. John Smith"),
    ("Ms. Jane Doe called", "Ms. Jane Doe"),
])
def test_title_pattern(text, expected):
    assert expected in _persons(text)


def test_unspaced_title_does_not_eat_sentence():
    r = _persons("คุณสมชายโดนหักเงิน")
    assert r and "โดนหักเงิน" not in r[0]
    assert r[0].startswith("คุณสมชาย")


def test_stop_word_after_name_not_surname():
    r = _persons("คุณสมชาย เบอร์ 0812345678")
    assert r == ["คุณสมชาย"]


def test_excluded_compounds():
    assert _persons("คุณสมบัติของผู้สมัคร") == []
    assert _persons("นายจ้างไม่จ่ายเงิน") == []


def test_offset_with_zero_width():
    t = "สม\u200bชาย ใจดี"
    r = detect_all(t, ["สมชาย ใจดี"], False)
    assert t[r[0].start:r[0].end] == t


@pytest.mark.ner
def test_ner_detects_name(ner_available):
    r = _persons("วันนี้สมชาย ใจดี มาขอความช่วยเหลือ", [], True)
    assert any("สมชาย" in x for x in r)


@pytest.mark.parametrize("text,expected", [
    ("นายกิตติศักดิ์ ใจดี โดนหักเงิน", "นายกิตติศักดิ์ ใจดี"),
    ("นายประเสริฐศักดิ์ มั่นคง แจ้งว่า", "นายประเสริฐศักดิ์ มั่นคง"),
    ("คุณสมชายโดนหักเงิน", "คุณสมชาย"),
])
def test_long_thai_name_cut_by_stop_word_not_length(text, expected):
    # 🔍 D28: ชื่อไทยยาวที่เว้นวรรคถูกต้องต้องไม่ถูกตัดกลางชื่อ และต้องหานามสกุลต่อ
    assert _persons(text) == [expected]


@pytest.mark.parametrize("text,expected", [
    ("นางมาลี ใจดี แจ้งว่า", "นางมาลี ใจดี"),
    ("นายมานพ ใจดี โดนหักเงิน", "นายมานพ ใจดี"),
    ("นายเป็นหนึ่ง ใจดี", "นายเป็นหนึ่ง ใจดี"),
    ("นายสมชาย เป็นสุข ฝ่ายบัญชี", "นายสมชาย เป็นสุข"),
    ("นายศักดิ์ชัยมาลาใจดี", "นายศักดิ์ชัยมาลาใจดี"),
])
def test_name_starting_with_stop_word_is_not_skipped(text, expected):
    # 🔍 D29: ชื่อ/นามสกุลที่ขึ้นต้นด้วยตัวอักษรเหมือน stop word (มา/มี/เป็น) ต้องไม่ถูกข้าม
    assert _persons(text) == [expected]


@pytest.mark.parametrize("text,expected", [
    ("นายมานิตย์ ใจดี แจ้งว่า", "นายมานิตย์ ใจดี"),
    ("นายมาดี ใจดี แจ้งว่า", "นายมาดี ใจดี"),
    ("นายมีศักดิ์ ใจดี แจ้งว่า", "นายมีศักดิ์ ใจดี"),
    ("นายมาโนช ใจดี", "นายมาโนช ใจดี"),
    ("นายเป็นเอก ใจดี", "นายเป็นเอก ใจดี"),
    ("นายไปรยา ใจดี", "นายไปรยา ใจดี"),
    ("คุณมานิตย์ ใจดี แจ้งว่า", "คุณมานิตย์ ใจดี"),
])
def test_non_pronoun_title_with_newmm_stop_token_name_is_masked(text, expected):
    # 🔍 D30: newmm ตัดชื่อเป็น stop token + ส่วนที่เหลือ (มานิตย์ -> มา|นิตย์) ต้องไม่ถูกข้ามทั้งชื่อ
    assert _persons(text) == [expected]


def test_pronoun_khun_with_stop_run_and_no_surname_is_skipped():
    # 🔍 D30: "คุณ" + stop word จริง + ไม่มีนามสกุล = สรรพนาม ไม่ใช่ชื่อ (ข้อจำกัดที่ยอมรับ: คุณมานิตย์ แจ้งว่า หลุด)
    assert _persons("คุณมี ครับ") == []
    assert _persons("คุณมานิตย์ แจ้งว่า") == []


@pytest.mark.ner
def test_ner_on_non_pronoun_title_stop_token_name_not_leaked(ner_available):
    # 🔍 D30 repro (NER เปิด): ชื่อ+นามสกุลต้องไม่หลุด
    t = "ผู้ร้องเรียนคือ นายมานิตย์ ใจดี เบอร์ติดต่อ 0812345678"
    masked = t
    for s in sorted(detect_all(t, [], True), key=lambda x: -x.start):
        masked = masked[:s.start] + "[X]" + masked[s.end:]
    assert "มานิตย์" not in masked and "ใจดี" not in masked


@pytest.mark.parametrize("text,expected", [
    ("นายมานิตย์ มาดี แจ้งว่า", "นายมานิตย์ มาดี"),
    ("นางมาลี มาดี ร้องเรียน", "นางมาลี มาดี"),
    ("นายสมชาย เป็นเอก แจ้งว่า", "นายสมชาย เป็นเอก"),
    ("นายสมชาย มีสุข โทรมา", "นายสมชาย มีสุข"),
    ("นายสมชาย แจ้งว่า", "นายสมชาย"),
    ("คุณสมศรี มาแล้ว", "คุณสมศรี"),
])
def test_surname_with_stop_token_is_kept_but_stop_phrase_is_not(text, expected):
    # 🔍 D31 (NER ปิด): นามสกุลที่ newmm ตัดได้ stop token ต้องไม่หลุด แต่คำกริยาทั้งก้อน (แจ้งว่า/มาแล้ว) ไม่ถือเป็นนามสกุล
    assert _persons(text) == [expected]


def test_pronoun_khun_short_run_still_skipped_after_d31():
    # 🔍 D31: run สั้นหลัง "คุณ" ยังข้ามเหมือนเดิม
    assert _persons("คุณมี ครับ") == []
    assert _persons("คุณมานิตย์ แจ้งว่า") == []


def _mask_all(t, ner):
    # 🔄 แทน span ทั้งหมดด้วย [X] เพื่อตรวจว่า PII ไม่หลุด
    masked = t
    for s in sorted(detect_all(t, [], ner), key=lambda x: -x.start):
        masked = masked[:s.start] + "[X]" + masked[s.end:]
    return masked


@pytest.mark.parametrize("text,expected", [
    ("นาย สมชาย ใจดีเลขบัตร 1103700123458", "นาย สมชาย ใจดี"),
    ("นางสาวสุดา รักงานไม่ยอมแก้ไข", "นางสาวสุดา รักงาน"),
    ("นายสมชาย ใจดีโทรมา", "นายสมชาย ใจดี"),
    ("นายสมชาย ใจดีโดนหักเงิน", "นายสมชาย ใจดี"),
    ("นายสมชาย ใจดีไม่ได้รับเงิน", "นายสมชาย ใจดี"),
    ("นางสาวสมหญิง รักงานค่ะ", "นางสาวสมหญิง รักงาน"),
    ("คุณมานิตย์ ใจดีเบอร์ 0812345678", "คุณมานิตย์ ใจดี"),
])
def test_surname_glued_text_is_trimmed(text, expected):
    # 🔍 D32 (NER ปิด): ข้อความที่พิมพ์ติดหลังนามสกุลต้องไม่ถูก mask ไปด้วย
    assert _persons(text) == [expected]


@pytest.mark.parametrize("text,expected", [
    ("นายสมชาย บุญมีเบอร์ 0812345678", "นายสมชาย บุญมี"),
    ("นางมาลี มาดีแจ้งว่า", "นางมาลี มาดีแจ้ง"),
    ("นายสมชาย มีสุขโทรมา", "นายสมชาย มีสุข"),
    ("นายสมชาย เป็นเอกแจ้งว่า", "นายสมชาย เป็นเอกแจ้ง"),
    ("นายสมชาย แสงแจ้งแจ้งว่า", "นายสมชาย แสงแจ้งแจ้ง"),
    ("นายสมชาย ในดีบอกว่า", "นายสมชาย ในดีบอกว่า"),
    ("นาย สมชาย ใจดี เบอร์ 0812345678", "นาย สมชาย ใจดี"),
])
def test_surname_trim_does_not_leak_surname_with_short_stop_token(text, expected):
    # ⚠️ D32: นามสกุลที่มี token สั้นตรง stop ชุดเต็มต้องไม่ถูกตัดจนรั่ว (newmm ตัดเพี้ยน -> over-mask ทั้งคำ D8)
    assert _persons(text) == [expected]


@pytest.mark.parametrize("text,expected", [
    ("นายสมชาย แจ้งว่า", ["นายสมชาย"]),
    ("นายสมชาย แจ้งเบอร์", ["นายสมชาย"]),
    ("คุณสมศรี มาแล้ว", ["คุณสมศรี"]),
    ("คุณมี ครับ", []),
    ("คุณมานิตย์ แจ้งว่า", []),
])
def test_surname_trim_keeps_d31_d30_behavior(text, expected):
    # 🔍 D32 ต้องไม่กระทบ D31/D30
    assert _persons(text) == expected


def test_surname_stop_set_excludes_short_surname_tokens():
    # 🔍 D32: ชุดนามสกุลต้องเป็น subset ของ STOP_FOLLOW_WORDS และไม่มีคำสั้นที่เป็นส่วนของนามสกุลได้
    from app.detectors.person import _SURNAME_STOP_SET, STOP_FOLLOW_WORDS

    assert _SURNAME_STOP_SET <= set(STOP_FOLLOW_WORDS)
    banned = {"มี", "มา", "ได้", "ใน", "เป็น", "ไป", "ทำ", "ที่", "กับ", "ของ", "จาก", "ถูก", "ขอ",
              "แจ้ง", "ร้อง", "บอก", "แล้ว", "ช่วย"}
    assert not (_SURNAME_STOP_SET & banned)


@pytest.mark.parametrize("text,expected", [
    ("นางมาลี มาดีแจ้งว่าโดนหักเงิน", "นางมาลี มาดีแจ้ง"),
    ("นางสาวสุดา รักงานไม่ยอมแก้ไขเงินเดือนให้", "นางสาวสุดา รักงาน"),
])
def test_surname_run_longer_than_20_is_trimmed(text, expected):
    # 🔍 R6.7: ไม่มีเพดาน 20 ตัวแล้ว — D32 ตัดขอบเอง
    assert _persons(text) == [expected]


def test_surname_run_longer_than_20_without_stop_is_fully_masked():
    # ⚠️ R6.7: run ยาว > 20 ที่ไม่มี stop ชุดนามสกุล -> mask ทั้ง run (ไม่รั่ว)
    text = "นายสมชาย ใจดีมั่นคงเป็นสุขแก้วใสพงษ์ดีศรีสุขทองดี"
    assert _persons(text) == [text]


@pytest.mark.ner
@pytest.mark.parametrize("text,leaks", [
    ("นายสมชาย บุญมีเบอร์ 0812345678", ["สมชาย", "บุญมี"]),
    ("นางสาวสุดา รักงานไม่ยอมแก้ไข", ["สุดา", "รักงาน"]),
])
def test_ner_on_glued_surname_not_leaked(ner_available, text, leaks):
    # 🔍 D32 (NER เปิด): ไม่ assert ความยาว span (union D27 อาจ over-mask) แต่ชื่อ/นามสกุลต้องไม่หลุด
    masked = _mask_all(text, True)
    assert not any(x in masked for x in leaks)


@pytest.mark.ner
@pytest.mark.parametrize("text,leaks", [
    ("นางมาลี มาดีแจ้งว่าโดนหักเงิน", ["มาลี", "มาดี"]),
    ("นางสาวสุดา รักงานไม่ยอมแก้ไขเงินเดือนให้", ["สุดา", "รักงาน"]),
])
def test_ner_on_long_glued_surname_not_leaked(ner_available, text, leaks):
    # 🔍 R6.7 (NER เปิด): เคส run ยาว > 20 ต้องไม่มีชื่อ/นามสกุลหลุด
    masked = _mask_all(text, True)
    assert not any(x in masked for x in leaks)
