# Spec: WebTestPDPA — Chatbot ร้องเรียนที่กรองข้อมูล PDPA ก่อนส่ง LLM

> สถานะ: ตกลงกับผู้ใช้แล้ว (2026-11-22) — ใช้เป็น input ของ planner

## 1. เป้าหมาย
เว็บ chatbot ให้พนักงานพิมพ์เรื่องร้องเรียน/ปัญหา แล้วส่งให้ LLM ตอบหรือหาสาเหตุ
โดย **ข้อมูลส่วนบุคคล (PDPA) ต้องไม่ถูกส่งออกไปยัง LLM** และ **คำตอบที่คืนให้พนักงานต้องครบถ้วน** (ใส่ค่าจริงกลับ)

## 2. แนวทางหลัก: Tokenization (ไม่ใช่ลบทิ้ง)
1. ข้อความเดิม: `คุณสมชาย ใจดี เบอร์ 081-234-5678 โดนหักเงินเดือนผิด`
2. Mask: `[PERSON_1] เบอร์ [PHONE_1] โดนหักเงินเดือนผิด` → ส่ง LLM เฉพาะข้อความนี้
3. LLM ตอบโดยอ้าง `[PERSON_1]`
4. Backend unmask จาก Vault → พนักงานได้คำตอบที่มีข้อมูลจริงครบ

กฎ:
- token ต้อง consistent ภายใน conversation เดียว (ค่าเดิม = token เดิม ข้ามหลายข้อความ)
- Vault (token ↔ ค่าจริง) เก็บใน DB แบบเข้ารหัส AES-GCM, key จาก env, **ไม่ส่งให้ LLM เด็ดขาด**
- ประวัติ conversation ที่ส่งเป็น context ให้ LLM ต้องเป็นฉบับ masked เท่านั้น
- ⚠️ **Fail-closed**: PII service ล่ม / timeout / error → ไม่ส่ง LLM, แจ้ง error ผู้ใช้
- ก่อนส่ง LLM ต้องมี **guard ชั้นสุดท้าย** ใน .NET: scan masked text ซ้ำด้วย regex (เลขบัตร, เบอร์, email) — ถ้าเจอ → block

## 3. การตรวจหา PII (หลายชั้น)
| ประเภท | Token | วิธีตรวจ |
|---|---|---|
| ชื่อ-นามสกุล | `PERSON` | dictionary ชื่อพนักงานจาก DB + PyThaiNLP NER + pattern คำนำหน้า (นาย/นาง/นางสาว/คุณ/Mr./Ms.) + ชื่ออังกฤษ |
| เบอร์โทร | `PHONE` | regex มือถือ/บ้านไทย รองรับ `-`, ช่องว่าง, `+66`, เลขไทย ๐-๙ |
| เลขบัตรประชาชน | `THAI_ID` | 13 หลัก (มี/ไม่มี `-`/ช่องว่าง, เลขไทย) + ตรวจ checksum mod 11 |
| เพศ | `GENDER` | "เพศชาย/หญิง/ชาย/หญิง" ในบริบทเพศ, male/female; คำนำหน้าที่บอกเพศถูก mask ไปพร้อมชื่อ |
| Email | `EMAIL` | regex |
| เลขบัญชีธนาคาร | `BANK_ACCOUNT` | regex 10-12 หลักในบริบท "บัญชี" |

- Normalize เลขไทย → อารบิก ก่อนตรวจ (แต่ Vault เก็บค่าตามที่ผู้ใช้พิมพ์จริง)
- PII service รับ `known_names` (รายชื่อพนักงาน) จาก API ในแต่ละ request หรือ cache

## 4. ฟีเจอร์
1. **Login** พนักงาน — ASP.NET Identity, JWT ใน httpOnly cookie, roles `Employee` / `Admin`; seed admin จาก env; admin สร้าง user ได้ (ไม่มี self-register)
2. **Chat** — หลาย conversation ต่อ user, ดูประวัติย้อนหลัง (แสดงฉบับ unmask ให้เจ้าของเท่านั้น)
3. **Preview mask** — ก่อนส่ง แสดงว่าส่วนไหนจะถูก mask; ผู้ใช้เลือกข้อความเพิ่ม mask เองได้ (`CUSTOM`)
4. **Admin: Audit log** — ใคร, เมื่อไหร่, provider/model, จำนวน mask แยกประเภท, ข้อความ masked ที่ส่งออก (ไม่เก็บค่าจริงใน log)
5. **Admin: PII test bench** — dataset ตัวอย่าง (≥ 50 เคส, ไทย+อังกฤษ, เคสหลบเลี่ยง) รันแล้วแสดง recall/precision ต่อประเภท
6. **Retention** — background job ลบ Vault + ข้อความเก่ากว่า `RETENTION_DAYS` (default 30)
7. **Rate limit** ต่อ user (ASP.NET built-in rate limiter)

## 5. LLM Gateway
- `Microsoft.Extensions.AI` (`IChatClient`), เลือก provider ผ่าน env `LLM_PROVIDER`
- Providers: `deepseek` (OpenAI-compatible, base URL `https://api.deepseek.com`, default ช่วง dev), `openai`, `claude`, `ollama`, `mock`
- `mock` = ไม่เรียกภายนอก, ตอบโดย echo ข้อความ masked ที่ได้รับ (ใช้ test ว่าไม่มี PII รั่ว + ใช้ตอนไม่มี key)
- Model name, base URL, API key ตั้งผ่าน env ทั้งหมด
- System prompt บอก LLM ให้คง token `[TYPE_N]` ไว้ตามเดิมในคำตอบ
- Non-streaming ใน MVP (streaming = งานถัดไป)
- ⚠️ DeepSeek host ในจีน → ใช้กับข้อมูลทดสอบเท่านั้น (ระบุใน README)

## 6. Stack
| ชั้น | เทคโนโลยี |
|---|---|
| Frontend | Angular 20 (standalone components), served โดย nginx; nginx proxy `/api` → api |
| Backend | ASP.NET Core .NET 10 Web API, EF Core + Npgsql, ASP.NET Identity |
| Database | PostgreSQL 17 |
| PII Service | Python 3.12 FastAPI + PyThaiNLP (internal network เท่านั้น ไม่ expose port ออก host) |
| Docker | docker-compose: `web`, `api`, `pii`, `db`; `ollama` เป็น profile เสริม |

## 7. Non-functional
- Secrets ทั้งหมดใน `.env` (มี `.env.example`), ห้าม hardcode, `.env` อยู่ใน `.gitignore`
- Comment ใน code เป็นภาษาไทยมี emoji นำหน้า (🔍📦💾🔄📤⚠️🧹🚀)
- Tests: xUnit (mask/unmask, guard, vault encryption, auth) + pytest (detector แต่ละประเภท, เคสหลบเลี่ยง)
- `docker compose up --build` แล้วใช้งานได้ทันที (migration อัตโนมัติตอน start)
- ห้ามทำ git / CI-CD / deploy ใดๆ
- ห้าม log ค่า PII จริงลง console/log
