"""🔍 คำนำหน้าชื่อ (AL3) ใช้ร่วมกันระหว่าง person detector"""
import re

# 🔍 เรียงยาวก่อนสั้น
TITLE_TH = [r"นางสาว", r"น\.ส\.", r"นาง", r"นาย", r"คุณ", r"ด\.ช\.", r"ด\.ญ\.", r"ดร\."]
TITLE_EN = r"(?:Mrs|Mr|Ms|Miss|Dr)\b\.?"
TITLE_ANY = "(?:" + "|".join(TITLE_TH) + "|" + TITLE_EN + ")"

# 🔍 ใช้หาคำนำหน้าที่ติดอยู่ท้ายข้อความก่อนชื่อ (ขยาย span ให้รวมคำนำหน้า)
TITLE_BEFORE = re.compile(r"(?:" + "|".join(TITLE_TH) + r"|(?i:" + TITLE_EN + r"))\s*$")
