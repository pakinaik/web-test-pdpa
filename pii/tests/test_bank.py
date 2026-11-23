import pytest

from app.detectors.bank import detect
from app.normalize import map_span_to_original, normalize_text


def _found(text):
    n, m = normalize_text(text)
    return [text[slice(*map_span_to_original(m, s.start, s.end))] for s in detect(n)]


@pytest.mark.parametrize("text,expected", [
    ("บัญชี 123-4-56789-0 ธ.กรุงไทย", "123-4-56789-0"),
    ("เลขที่บัญชี 1234567890", "1234567890"),
    ("account 123456789012", "123456789012"),
    ("โอนเข้า ธนาคาร 987 6 54321 0", "987 6 54321 0"),
    ("บช. ๑๒๓๔๕๖๗๘๙๐", "๑๒๓๔๕๖๗๘๙๐"),
    ("A/C: 1234567890", "1234567890"),
])
def test_positive(text, expected):
    assert expected in _found(text)


@pytest.mark.parametrize("text", [
    "เลขอ้างอิง 1234567890",
    "บัญชี 12345",
    "บัญชี 1234567890123456",
])
def test_negative(text):
    assert _found(text) == []


def test_keyword_of_previous_number_does_not_cover_phone():
    t = "บัญชี 123-4-56789-0 โทร 0812345678"
    assert _found(t) == ["123-4-56789-0"]
