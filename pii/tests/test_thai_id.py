import pytest

from app.detectors.thai_id import detect, is_valid_thai_id
from app.normalize import map_span_to_original, normalize_text

VALID = ["1101700234568", "3100500123458", "5209900012347", "1234567890121"]


@pytest.mark.parametrize("v", VALID)
def test_checksum_valid(v):
    assert is_valid_thai_id(v)


def test_checksum_invalid():
    assert not is_valid_thai_id("1101700234567")


def _found(text):
    n, m = normalize_text(text)
    return [text[slice(*map_span_to_original(m, s.start, s.end))] for s in detect(n)]


@pytest.mark.parametrize("text", [
    "บัตร 1101700234568 ครับ",
    "บัตร 1-1017-00234-56-8 ครับ",
    "บัตร 1 1017 00234 56 8",
    "บัตร ๑๑๐๑๗๐๐๒๓๔๕๖๘",
    "บัตร 1 1 0 1 7 0 0 2 3 4 5 6 8",
    "บัตร 11017\u200b00234568",
])
def test_positive(text):
    assert len(_found(text)) == 1


def test_wrong_checksum_not_detected():
    assert _found("เลข 1101700234567") == []


def test_not_part_of_longer_number():
    assert _found("เลข 11017002345681") == []


def test_other_valid_ids():
    for v in VALID[1:]:
        assert _found(f"id {v}") == [v]


@pytest.mark.parametrize("sep", [" ", "\t", "\n"])
def test_thai_id_separated_by_unicode_whitespace(sep):
    # 🔍 B2: เลขบัตรที่คั่นด้วย NBSP/tab/newline ต้อง detect และ offset ชี้กลับต้นฉบับถูก
    from app.pipeline import detect_all

    value = sep.join(["1", "1017", "00234", "56", "8"])
    text = f"เลข {value} จบ"
    spans = detect_all(text, [], False)
    assert [s.type for s in spans] == ["THAI_ID"]
    assert text[spans[0].start:spans[0].end] == value


@pytest.mark.parametrize("sep", ["\r\n", "  ", " - "])
def test_thai_id_multi_char_separators(sep):
    # 🔍 N2: เลขบัตรคั่นด้วย CRLF / 2 ช่องว่าง / " - " ต้อง detect และ offset ถูก
    from app.pipeline import detect_all

    value = sep.join(["1", "1017", "00234", "56", "8"])
    text = f"เลข {value} จบ"
    spans = detect_all(text, [], False)
    assert [s.type for s in spans] == ["THAI_ID"]
    assert text[spans[0].start:spans[0].end] == value
