"""🚀 อ่านค่า config จาก env (ห้าม hardcode secret)"""
import os
from dataclasses import dataclass


def _bool(v: str | None, default: bool) -> bool:
    if v is None or v == "":
        return default
    return v.strip().lower() in ("1", "true", "yes", "on")


@dataclass(frozen=True)
class Config:
    service_token: str
    ner_enabled: bool = True
    max_text_length: int = 5000


def load_config() -> Config:
    # 🚀 อ่าน env ตอน start
    return Config(
        service_token=os.environ.get("PII_SERVICE_TOKEN", ""),
        ner_enabled=_bool(os.environ.get("PII_NER_ENABLED"), True),
        max_text_length=int(os.environ.get("PII_MAX_TEXT_LENGTH", "5000")),
    )
