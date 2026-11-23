"""🔄 pydantic request/response ของ PII service (ไม่มี field ที่คืนค่าข้อความ)"""
from pydantic import BaseModel, Field


class DetectRequest(BaseModel):
    text: str
    known_names: list[str] = Field(default_factory=list, max_length=5000)
    enable_ner: bool | None = None


class EntityOut(BaseModel):
    type: str
    start: int
    end: int
    score: float
    source: str


class DetectResponse(BaseModel):
    entities: list[EntityOut]
    ner_used: bool
    elapsed_ms: int
