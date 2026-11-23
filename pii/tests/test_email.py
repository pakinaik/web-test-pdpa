import pytest

from app.detectors.email import detect
from app.normalize import map_span_to_original, normalize_text


def _found(text):
    n, m = normalize_text(text)
    return [text[slice(*map_span_to_original(m, s.start, s.end))] for s in detect(n)]


@pytest.mark.parametrize("text,expected", [
    ("อีเมล somchai@example.com ครับ", "somchai@example.com"),
    ("SOMCHAI@Example.CO.TH", "SOMCHAI@Example.CO.TH"),
    ("somchai (at) example (dot) com", "somchai (at) example (dot) com"),
    ("a.b+c@example.co.th", "a.b+c@example.co.th"),
    ("x[at]example[dot]com", "x[at]example[dot]com"),
    ("somchai @ example . com", "somchai @ example . com"),
])
def test_positive(text, expected):
    assert expected in _found(text)


@pytest.mark.parametrize("text", ["ไม่มีอีเมล", "a@b", "ราคา 5 @ 10 บาท"])
def test_negative(text):
    assert _found(text) == []
