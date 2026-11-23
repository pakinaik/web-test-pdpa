import pytest

from app.detectors.gender import detect
from app.normalize import normalize_text


def _found(text):
    n, _ = normalize_text(text)
    return [n[s.start:s.end] for s in detect(n)]


@pytest.mark.parametrize("text,expected", [
    ("เพศชาย", "ชาย"),
    ("เพศ: หญิง", "หญิง"),
    ("พนักงาน (ชาย) โดนหัก", "ชาย"),
    ("gender: female", "female"),
    ("Female employee", "Female"),
    ("sex = M", "M"),
    ("เป็น หญิง อายุ 30", "หญิง"),
])
def test_positive(text, expected):
    assert expected in _found(text)


@pytest.mark.parametrize("text", ["ไปเที่ยวชายหาด", "ผู้ชายคนนั้น", "ไม่มีข้อมูล", "ผู้หญิงสวย"])
def test_negative(text):
    assert _found(text) == []
