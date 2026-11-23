"""🚀 pytest config — ให้ import app.* ได้โดยไม่ต้องติดตั้ง package"""
import pathlib
import sys

# 🚀 เพิ่ม root ของ pii เข้า sys.path
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))


import pytest


@pytest.fixture(scope="session")
def ner_available():
    # 🚀 ถ้าโหลด NER ไม่ได้ให้ skip เคส marker ner
    try:
        from app.detectors import person
        person.get_ner()
    except Exception:  # noqa: BLE001
        pytest.skip("โหลดโมเดล NER ไม่ได้")
