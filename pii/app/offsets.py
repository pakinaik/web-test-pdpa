"""🔄 F3: แปลง offset ระหว่าง code point (Python) กับ UTF-16 code unit (.NET)"""


def build_utf16_prefix(text: str) -> list[int]:
    # 🔄 prefix[i] = จำนวน UTF-16 unit ก่อน code point ที่ i; ตัวอักษร > U+FFFF นับ 2
    prefix = [0] * (len(text) + 1)
    acc = 0
    for i, ch in enumerate(text):
        prefix[i] = acc
        acc += 2 if ord(ch) > 0xFFFF else 1
    prefix[len(text)] = acc
    return prefix


def to_utf16_span(prefix: list[int], start: int, end: int) -> tuple[int, int]:
    return prefix[start], prefix[end]
