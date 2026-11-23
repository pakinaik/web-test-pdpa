import pytest
from fastapi.testclient import TestClient

from app.config import Config
from app.main import create_app

TOKEN = "test-token-not-real"


@pytest.fixture()
def client():
    app = create_app(Config(service_token=TOKEN, ner_enabled=False, max_text_length=200))
    return TestClient(app, raise_server_exceptions=False)


H = {"X-Internal-Token": TOKEN}


def test_health_no_token(client):
    r = client.get("/health")
    assert r.status_code == 200 and r.json()["ner_enabled"] is False


def test_401_without_token(client):
    assert client.post("/detect", json={"text": "x"}).status_code == 401


def test_401_wrong_token(client):
    assert client.post("/detect", json={"text": "x"}, headers={"X-Internal-Token": "bad"}).status_code == 401


def test_detect_shape_and_utf16(client):
    text = "😀 คุณสมชาย ใจดี เบอร์ ๐๘๑-๒๓๔-๕๖๗๘ เพศชาย"
    r = client.post("/detect", json={"text": text, "known_names": ["สมชาย ใจดี"]}, headers=H)
    assert r.status_code == 200
    body = r.json()
    assert body["ner_used"] is False
    types = {e["type"] for e in body["entities"]}
    assert {"PERSON", "PHONE", "GENDER"} <= types
    u16 = text.encode("utf-16-le")
    for e in body["entities"]:
        assert set(e) == {"type", "start", "end", "score", "source"}
    ph = next(e for e in body["entities"] if e["type"] == "PHONE")
    assert u16[ph["start"] * 2: ph["end"] * 2].decode("utf-16-le") == "๐๘๑-๒๓๔-๕๖๗๘"


def test_422_no_echo(client):
    secret = "SECRET-VALUE-0812345678"
    r = client.post("/detect", json={"text": 123, "extra": secret, "known_names": secret}, headers=H)
    assert r.status_code == 422
    assert secret not in r.text and r.json() == {"error": "INVALID_REQUEST"}


def test_413_too_long(client):
    r = client.post("/detect", json={"text": "ก" * 201}, headers=H)
    assert r.status_code == 413 and r.json() == {"error": "TEXT_TOO_LONG"}


def test_response_has_no_text_field(client):
    r = client.post("/detect", json={"text": "081-234-5678"}, headers=H)
    assert "081" not in r.text


def test_known_name_too_long(client):
    r = client.post("/detect", json={"text": "a", "known_names": ["x" * 201]}, headers=H)
    assert r.status_code == 422
