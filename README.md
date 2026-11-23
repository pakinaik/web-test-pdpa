# WebTestPDPA

เว็บ chatbot สำหรับพนักงานพิมพ์เรื่องร้องเรียน/ปัญหา แล้วส่งให้ LLM วิเคราะห์ โดย **ข้อมูลส่วนบุคคล (PDPA) ไม่ถูกส่งไปยัง LLM**
และ **คำตอบที่พนักงานได้รับมีค่าจริงครบถ้วน** (ใส่ค่าจริงกลับหลังตอบ)

> ⚠️ **DeepSeek host อยู่ในประเทศจีน → ใช้กับข้อมูลทดสอบเท่านั้น** (ค่า default ตอน dev ในไฟล์นี้คือ `mock` ซึ่งไม่เรียกภายนอก)
>
> ⚠️ โปรเจกต์นี้ **ไม่มี git / CI-CD / deploy** — เป็นโค้ดสำหรับรันในเครื่องด้วย Docker Compose เท่านั้น

## แนวคิดหลัก: Tokenization (ไม่ใช่ลบทิ้ง)

```
ข้อความเดิม : คุณสมชาย ใจดี เบอร์ 081-234-5678 โดนหักเงินเดือนผิด
ส่ง LLM     : [PERSON_1] เบอร์ [PHONE_1] โดนหักเงินเดือนผิด
LLM ตอบ     : ... [PERSON_1] ... [PHONE_1] ...
ผู้ใช้เห็น   : ... คุณสมชาย ใจดี ... 081-234-5678 ...   (unmask จาก Vault)
```

- token ค่าเดิม = token เดิมภายใน conversation เดียว (แม้พิมพ์ต่างรูปแบบ เช่น `081-234-5678` กับ `0812345678`)
- Vault (token ↔ ค่าจริง) เก็บใน PostgreSQL เข้ารหัส AES-GCM, key จาก env, **ไม่ส่งให้ LLM**
- ประวัติที่ส่งเป็น context ให้ LLM เป็นฉบับ masked เท่านั้น
- **Fail-closed**: PII service ล่ม/timeout/ผลผิดปกติ → ไม่ส่ง LLM และแจ้ง error (503 `PII_UNAVAILABLE`)
- **Guard ชั้นสุดท้าย** ใน .NET: scan ข้อความ masked ซ้ำ (เลข 13 หลัก, เบอร์, email) ถ้าเจอ → block (422 `GUARD_BLOCKED`)

## Architecture

```
Browser ── nginx (web) ──/api──> api (.NET 10) ──> pii (FastAPI + PyThaiNLP)   [detection เท่านั้น]
                                   │  └──────────> LLM provider (mock/deepseek/openai/claude/ollama)
                                   └─ PostgreSQL 17 (db)
```

| Service | เทคโนโลยี | หมายเหตุ |
|---|---|---|
| web | Angular 20 + nginx | proxy `/api` → api, เปิดพอร์ต `WEB_PORT` (default 8080) |
| api | ASP.NET Core .NET 10, EF Core, Identity, JWT (httpOnly cookie) | tokenization / vault / guard / audit / retention |
| pii | Python 3.12, FastAPI, PyThaiNLP (`thainer`) | อยู่บน network `internal` ไม่ publish port, ต้องมี header `X-Internal-Token` |
| db | PostgreSQL 17 | ไม่ publish port |
| ollama | `ollama/ollama` | เป็น profile เสริม |

ชนิดข้อมูลที่ตรวจ: `PERSON`, `PHONE`, `THAI_ID` (ตรวจ checksum), `GENDER`, `EMAIL`, `BANK_ACCOUNT` และ `CUSTOM` (ผู้ใช้เลือก mask เพิ่มเอง)

## เริ่มใช้งาน

ต้องมี Docker Desktop (PowerShell บน Windows)

```powershell
.\scripts\new-env.ps1          # สร้าง .env + สุ่ม secret (LLM_PROVIDER=mock, ไม่มี API key)
docker compose up --build
```

เปิด `http://localhost:8080` แล้ว login ด้วย `ADMIN_USERNAME` / `ADMIN_PASSWORD` ใน `.env`
(ไม่มี self-register — Admin สร้าง user ที่เมนู "ผู้ใช้"; ชื่อไทย/อังกฤษของ user จะถูกใช้เป็น `known_names` ตรวจชื่อพนักงาน)

migration และ seed (roles, admin, dataset PII test) ทำอัตโนมัติตอน api เริ่ม

## ตัวแปร env (`.env`, ดูตัวอย่างใน `.env.example`)

| กลุ่ม | ตัวแปร | ความหมาย |
|---|---|---|
| DB | `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | ฐานข้อมูล (compose ประกอบ connection string ให้) |
| Auth | `JWT_SIGNING_KEY` (≥ 32 bytes), `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRES_MINUTES`, `COOKIE_SECURE`, `ADMIN_USERNAME`, `ADMIN_PASSWORD` | `COOKIE_SECURE=true` เมื่อใช้ผ่าน HTTPS |
| Vault | `VAULT_ENCRYPTION_KEY` | base64 ของ 32 bytes |
| PII | `PII_SERVICE_TOKEN`, `PII_NER_ENABLED`, `PII_TIMEOUT_SECONDS`, `PII_MAX_TEXT_LENGTH` | `PII_NER_ENABLED=false` = ปิด NER โดยตั้งใจ (มี banner เตือนในหน้าแชท + audit บันทึก `ner_enabled=false`) |
| LLM | `LLM_PROVIDER`, `LLM_MODEL`, `LLM_BASE_URL`, `LLM_API_KEY`, `LLM_TIMEOUT_SECONDS`, `LLM_MAX_OUTPUT_TOKENS`, `LLM_TEMPERATURE`, `LLM_HISTORY_LIMIT` | ดูหัวข้อ provider |
| Retention | `RETENTION_DAYS` (30), `AUDIT_RETENTION_DAYS` (180, ต้อง ≥ RETENTION_DAYS), `RETENTION_INTERVAL_MINUTES` | ลบ vault/messages และ audit เก่า |
| Limit | `RATE_LIMIT_PER_MINUTE` (20 ต่อ user) | |
| พนักงาน (mock) | `EMPLOYEE_SEED_PASSWORD` | รหัสผ่านตั้งต้นของ user พนักงาน `emp001`–`emp020` (ว่าง = ไม่สร้าง user, สั้นกว่า 10 ตัวอักษร = api start ไม่ผ่าน); `scripts/new-env.ps1` สุ่มให้ และเติมต่อท้าย `.env` เดิมโดยไม่แก้บรรทัดอื่น |
| อื่นๆ | `WEB_PORT` | พอร์ตเว็บบน host |

secret ทั้งหมดอยู่ใน `.env` เท่านั้น (`.env` อยู่ใน `.gitignore`) — ห้ามใส่ API key จริงในไฟล์อื่น

## ข้อมูลพนักงาน (mock) + chatbot ตอบจากข้อมูล (R7)

ระบบ seed ข้อมูลพนักงานสมมติ 20 คน (ประวัติส่วนตัว, ตำแหน่ง, เงินเดือน, เงินได้รายเดือน 24 เดือน, วันลา, ผู้ติดต่อฉุกเฉิน) — **ข้อมูลสมมติทั้งหมด ไม่ใช่ข้อมูลจริง**

- **User**: `emp001`–`emp020` (username = รหัสพนักงาน, role `Employee`) ใช้รหัสผ่านจาก `EMPLOYEE_SEED_PASSWORD`; `emp020` มี role `HR` เพิ่ม; Admin ใช้ได้เหมือน HR. เปลี่ยน `EMPLOYEE_SEED_PASSWORD` ใน `.env` ภายหลัง **ไม่ทับ** user ที่สร้างไปแล้ว · ⚠️ ถ้ามี user ชื่อ `empNNN` อยู่ก่อนแล้ว (เช่น Admin สร้างเอง) seeder จะ **ผูก user นั้นกับข้อมูลพนักงาน** ที่ตรงรหัส (และ log warning เป็นจำนวน) — Admin ไม่ควรสร้าง user ชื่อ `emp001`–`emp020` เอง
- **ตัวอย่างคำถาม**: "ขอดูศาสนาและโรคประจำตัวของฉัน", "เงินเดือนของฉัน", "ลาพักร้อนปี 2026 ไปกี่วัน", "ธนาคารและเลขบัญชีของฉัน", (HR/Admin) "emp005 ลาพักร้อนไปกี่วัน"
- **สิทธิ์ (ตัดสินที่ server เท่านั้น)**: Employee ได้ข้อมูลของ **ตัวเองเท่านั้น** ไม่ว่าจะพิมพ์รหัส/ชื่อคนอื่นหรือสั่งให้ข้ามสิทธิ์; HR/Admin ระบุได้ด้วยรหัส `empNNN` / ชื่อเต็ม (ไทย/อังกฤษ) / ชื่อต้นที่ตรงคนเดียว สูงสุด 3 คนต่อข้อความ และไม่ sticky (ต้องระบุใหม่ทุกข้อความ)
- **flow**: ตรวจ keyword intent → หา target → ดึงข้อมูลเฉพาะ section ที่ถาม → tokenize ค่าอ่อนไหวผ่าน vault เดิม → แนบเป็น system message ที่สอง → guard ตรวจ → ส่ง LLM; คำตอบถูก unmask เฉพาะเจ้าของ conversation; audit บันทึก `context_employee_ids` + `context_sections` (ไม่มีชื่อ/ค่า) ดูใน audit detail
- **PII type ใหม่** (ฝั่ง .NET เท่านั้น ไม่ผ่าน detector): `RELIGION`, `ETHNICITY`, `HEALTH`, `BLOOD_TYPE`, `NATIONALITY`, `MARITAL_STATUS`, `ADDRESS`, `DATE_OF_BIRTH`, `EMPLOYEE_ID`
- **ถูก mask ก่อนส่ง LLM**: ชื่อ, เลขบัตร/ภาษี/ประกันสังคม, เบอร์, อีเมล, เลขบัญชี, เพศ, วันเกิด, ที่อยู่, สัญชาติ, สถานภาพ, ศาสนา, กรุ๊ปเลือด, โรคประจำตัว, เชื้อชาติ, รหัสพนักงาน, ชื่อ/เบอร์ผู้ติดต่อฉุกเฉิน
- **ส่งเป็น plain** (ไม่ถือเป็น PII ใน mock นี้): ตำแหน่ง/ระดับ/ฝ่าย, ชื่อธนาคาร, ความสัมพันธ์ผู้ติดต่อฉุกเฉิน, จำนวนเงิน (เงินเดือน/เงินได้), ประเภทวันลา/จำนวนวัน/วันที่

ข้อจำกัดของส่วนนี้
- intent ใช้ keyword (ไทย/อังกฤษ) ไม่ใช่ NLU — ถามอ้อมๆ อาจไม่ได้ context; ไม่มี aggregate/เปรียบเทียบหลายคน (เช่น "เงินเดือนเฉลี่ยทั้งฝ่าย")
- ข้อมูลพนักงานใน DB เก็บเป็น plaintext — ระบบจริงควรเข้ารหัส column
- role ที่เปลี่ยนมีผลหลัง JWT เดิมหมดอายุ (`JWT_EXPIRES_MINUTES`)
- ข้อความอิสระเรื่องสุขภาพที่ผู้ใช้พิมพ์เองและไม่ตรงกับค่าของ target ยังไม่ถูก mask (พึ่ง preview + CUSTOM mask)
- web (Angular) ไม่ได้แก้: สร้าง user role `HR` เพิ่มผ่าน seed หรือ `POST /api/admin/users`

## เลือก LLM provider

ตั้งผ่าน env อย่างเดียว (แก้ `.env` แล้ว `docker compose up -d --build api`)

| `LLM_PROVIDER` | default model | หมายเหตุ |
|---|---|---|
| `mock` (default) | `mock-echo` | ไม่เรียกภายนอก ตอบโดย echo ข้อความ masked ที่ได้รับ |
| `deepseek` | `deepseek-chat` | base URL `https://api.deepseek.com`, ต้องมี `LLM_API_KEY` — ⚠️ ข้อมูลทดสอบเท่านั้น |
| `openai` | ต้องตั้ง `LLM_MODEL` | ต้องมี `LLM_API_KEY` |
| `claude` | ต้องตั้ง `LLM_MODEL` | ต้องมี `LLM_API_KEY` |
| `ollama` | `qwen2.5:3b` | base URL `http://ollama:11434` |

### ใช้ Ollama

```powershell
docker compose --profile ollama up --build
docker compose exec ollama ollama pull qwen2.5:3b
# แล้วตั้ง LLM_PROVIDER=ollama ใน .env
```

## การทดสอบ

```powershell
# PII service (pytest — รวม bench dataset ≥ 50 เคส)
cd pii; .\.venv\Scripts\python -m pytest -q          # ถ้ายังไม่มี venv: py -3.12 -m venv .venv; .\.venv\Scripts\pip install -r requirements-dev.txt

# API (xUnit — integration ใช้ Testcontainers ต้องเปิด Docker Desktop)
cd api; dotnet test

# Frontend (compile)
cd web; npm ci; npm run build

# E2E (ต้อง LLM_PROVIDER=mock) — รัน docker compose up --build และตรวจว่าไม่มี PII รั่วไป outbound / audit / logs
.\scripts\e2e.ps1
```

หน้า Admin → "PII test bench" รัน dataset ผ่าน PII service จริงแล้วแสดง recall/precision ต่อประเภท

## ฟีเจอร์

- Login (JWT httpOnly cookie, roles `Employee` / `Admin`)
- Chat หลาย conversation + ประวัติ (แสดงฉบับ unmask เฉพาะเจ้าของ)
- Preview mask ก่อนส่ง + เลือกข้อความเพิ่มเพื่อ mask เอง (`CUSTOM`) — ยกเลิก mask ที่ระบบตรวจพบไม่ได้
- Admin: audit log (ใคร/เมื่อไหร่/provider/model/จำนวน mask/NER + payload ทั้งก้อนที่ส่ง LLM จริงพร้อม SHA-256), PII test bench, จัดการ user
- Retention job, rate limit ต่อ user

## ข้อจำกัดที่ควรรู้

- **ระบบตรวจ PII ไม่ใช่ 100%** (false negative): ชื่อที่ไม่มีคำนำหน้าและไม่อยู่ในรายชื่อพนักงาน (พึ่ง NER ซึ่งแม่นไม่สูง), ชื่อเล่น, เบอร์ที่เขียนเป็นตัวอักษร, เลขบัญชีที่ไม่มีคำว่า "บัญชี", ที่อยู่, ข้อมูลอ่อนไหวประเภทอื่น (ศาสนา/สุขภาพ) — ผู้ใช้ควรตรวจ preview ก่อนส่งทุกครั้ง
- guard ชั้นสุดท้ายครอบคลุมเฉพาะเลข 13 หลัก/เบอร์/email — ไม่จับ PERSON
- ระบบ bias ไปทาง over-mask (ยอม false positive); เลขบัตร 13 หลักที่ checksum ผิดจะถูก guard block (ให้ใช้ "Mask ส่วนที่เลือกเพิ่ม")
- Logout เป็นการลบ cookie — JWT ยัง valid จนหมดอายุ (`JWT_EXPIRES_MINUTES`, default 60)
- Non-streaming; CSRF ป้องกันด้วย `SameSite=Strict` + same-origin (ผ่าน nginx) ไม่ใช่ antiforgery เต็มรูป
- ถ้า vault ถูกลบโดย retention ประวัติเก่าจะแสดง token (`[PERSON_1]`) แทนค่าจริง
- ค่าที่ unmask ใช้ format ของครั้งแรกที่พิมพ์ (first-seen)
- guard ตรวจ history ทั้งหมดรวมข้อความ assistant ด้วย: ถ้า LLM ตอบเลข 13 หลัก/เบอร์/email (เช่น เบอร์ hotline หรือเลขสมมติ) ออกมา ข้อความถัดไปใน conversation นั้นจะถูก block ด้วย `422 GUARD_BLOCKED` ต่อไปเรื่อยๆ (ไม่ใช่การรั่วไหล แต่ conversation นั้นใช้ต่อไม่ได้ — ให้เริ่ม conversation ใหม่)
- รวม span ที่ซ้อนกันแบบ union (D27): ถ้าตรวจเจอหลายประเภทที่ซ้อนกันบางส่วน ระบบ mask ทั้งช่วงรวมเป็น token เดียว (over-mask) เพื่อไม่ให้ส่วนที่ซ้อนหลุดไป LLM
- ตัวคั่นระหว่างตัวเลขของเบอร์/เลขบัตรรับได้ 0-3 ตัว (เช่น CRLF, 2 ช่องว่าง, ` - `); คั่นเกิน 3 ตัวไม่ถือว่าเป็นเลขชุดเดียวกัน
- guard ตัวสุดท้ายเป็น fail-closed: ตัวเลขที่คั่นด้วยตัวคั่นสั้นๆ แล้วรวมเป็น 13 หลักจะถูก block แม้ไม่ใช่เลขบัตร (เช่น `ยอด 1234567 - 891011 บาท`, รายการเลขข้อ `1. 2. 3. ... 11.`) และ detector ฝั่ง pii อาจมองเลขแบบ `ยอด 06 - 1234 - 567 บาท` เป็นเบอร์โทร (over-mask) — ผู้ใช้แก้ได้โดยเขียนตัวเลขให้ติดกัน/แยกบรรทัดด้วยข้อความคั่น
- ชื่อไทยที่ขึ้นต้นด้วย `คุณ` + ชื่อที่ newmm ตัดได้ stop token และไม่มีนามสกุล (เช่น `คุณมานิตย์ แจ้งว่า`, newmm ตัด `มา|นิตย์`) จะไม่ถูก mask เพราะแยกไม่ได้จากสรรพนาม `คุณมา...`; ถ้ามีนามสกุล (`คุณมานิตย์ ใจดี`) หรือใช้คำนำหน้าอื่น (`นาย`/`นาง`/`ดร.` ฯลฯ) จะ mask ทั้งชื่อ+นามสกุล (D30)
- นามสกุลที่ newmm ตัดได้ stop token (เช่น `มีสุข`, `มาดี`, `เป็นเอก`) จะถูก mask ครบชื่อ+นามสกุลแม้ปิด NER (D31: ปฏิเสธเฉพาะคำที่ทั้งคำหรือทุก token เป็น stop word เช่น `แจ้งว่า`, `มาแล้ว`, `โทรมา`) — ผลข้างเคียง: `นายสมชาย ได้รับเงินแล้ว` อาจ mask ทั้งวลี (over-mask ตาม D8); การปิด NER ยังมี banner เตือน + audit บันทึก `ner_enabled=false`
- ข้อความที่พิมพ์ติดหลังนามสกุลถูกตัดที่ stop word ชุดนามสกุล (D32: เบอร์ โทร อีเมล เลข บัตร บัญชี แผนก ฝ่าย เพศ โดน ไม่ ซึ่ง และ จึง เมื่อ ครับ ค่ะ คะ ว่า) เช่น `ใจดีเลขบัตร` → mask `ใจดี`; นามสกุลที่มีคำชุดนี้ปนอยู่หลังคำแรก (เช่น `ศรีไม่`, `ทองเลข`, `ทองไม่รู้โรย`) จะถูกตัดตั้งแต่คำนั้น ส่วนที่เหลือของนามสกุลไม่ถูก mask (ใช้ CUSTOM mask เองได้); ถ้า newmm ไม่เจอ stop จะ mask ทั้งคำ (over-mask); เมื่อเปิด NER อาจยัง over-mask จากการ union (D27) — ไม่มีเพดานความยาวนามสกุลแล้ว
- ชื่อ+นามสกุลที่เขียนติดกันยาวเกิน 10 ตัวอักษร และนามสกุลขึ้นต้นด้วย stop token (เช่น `นางสาวมาลีมาดีแจ้งว่าโดนหักเงิน`, `นายบุญมีใจดีแจ้งว่า`) จะถูกตัดที่ stop token (D28) นามสกุลอาจหลุด แม้เปิด NER — เขียนเว้นวรรคระหว่างชื่อกับนามสกุลเพื่อให้ตรวจได้ครบ
- คำนำหน้าที่เขียนติดคำไทยข้างหน้า (เช่น `ร้องเรียนโดยนายสมชาย ใจดี`, `นายสมชาย และนางมาลี ใจดี`) จะไม่ถูกจับด้วย pattern คำนำหน้า เพราะ `_TH_TITLE` มี lookbehind `(?<![ก-๎])` กัน false positive (`ขอบคุณ`, `บุญคุณ`, `เจ้านาย`) → ชื่อ/นามสกุลอาจหลุด (NER อาจจับได้บางส่วน) — แนะนำให้เว้นวรรคก่อนคำนำหน้า หรือใช้ CUSTOM mask; งานต่อ: ยอม match เมื่อหน้าคำนำหน้าเป็นคำเชื่อม (`โดย/กับ/และ/ของ/จาก/ให้/ถึง`)
