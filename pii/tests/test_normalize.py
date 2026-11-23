from app.normalize import map_span_to_original, normalize_text
from app.offsets import build_utf16_prefix, to_utf16_span


def test_thai_digits():
    n, _ = normalize_text("๐๘๑-๒๓๔-๕๖๗๘")
    assert n == "081-234-5678"


def test_fullwidth_digits():
    n, _ = normalize_text("０８１２３４５６７８")
    assert n == "0812345678"


def test_zero_width_removed_and_map():
    orig = "08\u200b1\u200c234"
    n, m = normalize_text(orig)
    assert n == "081234"
    assert orig[m[0]:m[-1] + 1] == orig
    s, e = map_span_to_original(m, 0, 3)
    assert orig[s:e] == "08\u200b1"


def test_no_nfkc_on_thai_vowel():
    n, _ = normalize_text("ทำงาน")
    assert n == "ทำงาน"


def test_utf16_offset_emoji():
    t = "😀ab"
    p = build_utf16_prefix(t)
    assert to_utf16_span(p, 1, 3) == (2, 4)


def test_utf16_offset_thai_same():
    t = "สวัสดี"
    p = build_utf16_prefix(t)
    assert to_utf16_span(p, 0, len(t)) == (0, len(t))


def test_whitespace_variants_map_to_space_one_to_one():
    # 🔄 NBSP / tab / newline ต้องกลายเป็นช่องว่างปกติแบบ 1:1 (ความยาวและ offset ไม่เลื่อน)
    orig = "081 234\t567\n8"
    n, m = normalize_text(orig)
    assert n == "081 234 567 8"
    assert m == list(range(len(orig)))


def test_control_separators_1c_to_1f_become_space():
    # 🔄 N3: U+001C-U+001F ถูกนับเป็น whitespace (ตรงกับ TextNormalizer.cs)
    n, m = normalize_text("081\x1c234\x1d567\x1e8\x1f9")
    assert n == "081 234 567 8 9"
    assert len(m) == len(n)
