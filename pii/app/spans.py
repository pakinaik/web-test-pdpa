"""🔄 โครงสร้าง Span กลางที่ detector ทุกตัวคืนค่า (offset บนข้อความที่ส่งเข้ามา)"""
from dataclasses import dataclass


@dataclass(frozen=True)
class Span:
    # 🔄 type ∈ PERSON | PHONE | THAI_ID | GENDER | EMAIL | BANK_ACCOUNT
    type: str
    start: int
    end: int  # 🔍 end exclusive
    score: float = 1.0
    source: str = "regex"
