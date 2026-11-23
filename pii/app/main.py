"""🚀 FastAPI PII detection service — detection เท่านั้น ไม่คืนค่าข้อความ (D1)"""
import hmac
import logging
import time
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI, Header, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from app.config import Config, load_config
from app.detectors import person
from app.models import DetectRequest, DetectResponse, EntityOut
from app.pipeline import detect_all

VERSION = "0.1.0"
logger = logging.getLogger("pii")


def create_app(config: Config | None = None) -> FastAPI:
    cfg = config or load_config()

    @asynccontextmanager
    async def lifespan(_app: FastAPI):
        # 🚀 ⚠️ D24: NER เปิดอยู่แต่โหลดไม่ได้ -> raise ให้ service ไม่ start (fail-closed)
        if not cfg.service_token:
            raise RuntimeError("PII_SERVICE_TOKEN ไม่ได้ตั้งค่า")
        if cfg.ner_enabled:
            person.get_ner()
        else:
            logger.warning("PII_NER_ENABLED=false — การตรวจชื่อทำงานไม่เต็มที่")
        yield

    app = FastAPI(title="pii", version=VERSION, lifespan=lifespan)

    def check_token(x_internal_token: str | None = Header(default=None)) -> None:
        # 🔍 เทียบ token แบบ constant-time
        if not x_internal_token or not hmac.compare_digest(
            x_internal_token.encode(), cfg.service_token.encode()
        ):
            raise HTTPException(status_code=401, detail="UNAUTHORIZED")

    @app.exception_handler(HTTPException)
    async def http_exc(_req: Request, exc: HTTPException):
        return JSONResponse({"error": str(exc.detail)}, status_code=exc.status_code)

    @app.exception_handler(RequestValidationError)
    async def validation_exc(_req: Request, _exc: RequestValidationError):
        # ⚠️ ห้าม echo input กลับ
        return JSONResponse({"error": "INVALID_REQUEST"}, status_code=422)

    @app.exception_handler(Exception)
    async def unhandled_exc(_req: Request, exc: Exception):
        # ⚠️ log เฉพาะชนิดของ exception
        logger.error("unhandled %s", type(exc).__name__)
        return JSONResponse({"error": "INTERNAL_ERROR"}, status_code=500)

    @app.get("/health")
    def health():
        return {
            "status": "ok",
            "ner_enabled": cfg.ner_enabled,
            "ner_loaded": person.ner_loaded(),
            "version": VERSION,
        }

    @app.post("/detect", response_model=DetectResponse, dependencies=[Depends(check_token)])
    def detect(req: DetectRequest):
        if len(req.text) > cfg.max_text_length:
            return JSONResponse({"error": "TEXT_TOO_LONG"}, status_code=413)
        if any(len(n) > 200 for n in req.known_names):
            return JSONResponse({"error": "INVALID_REQUEST"}, status_code=422)
        t0 = time.perf_counter()
        use_ner = cfg.ner_enabled if req.enable_ner is None else (req.enable_ner and cfg.ner_enabled)
        spans = detect_all(req.text, req.known_names, use_ner)
        return DetectResponse(
            entities=[EntityOut(type=s.type, start=s.start, end=s.end, score=s.score, source=s.source) for s in spans],
            ner_used=use_ner,
            elapsed_ms=int((time.perf_counter() - t0) * 1000),
        )

    return app


# 🚀 instance สำหรับ uvicorn app.main:app
app = create_app()
