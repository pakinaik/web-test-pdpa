"""🔍 Bench: วัด recall/precision ต่อประเภทจาก dataset กลาง (ไม่ใช้ NER)"""
import json
import os
import pathlib
from collections import defaultdict

import pytest

from app.pipeline import detect_all

_DEFAULT = (
    pathlib.Path(__file__).resolve().parents[2]
    / "api" / "src" / "WebTestPdpa.Api" / "SeedData" / "pii_test_cases.json"
)
TYPES = ["PERSON", "PHONE", "THAI_ID", "GENDER", "EMAIL", "BANK_ACCOUNT"]
RECALL_MIN = {"PHONE": 0.95, "THAI_ID": 0.95, "EMAIL": 0.95, "BANK_ACCOUNT": 0.90, "GENDER": 0.90, "PERSON": 0.85}
PRECISION_MIN = 0.80


def _load():
    path = pathlib.Path(os.environ.get("PII_CASES_PATH", str(_DEFAULT)))
    return json.loads(path.read_text(encoding="utf-8"))


def _occurrences(text, value):
    out, i = [], text.find(value)
    while i != -1:
        out.append((i, i + len(value)))
        i = text.find(value, i + len(value))
    return out


def _overlap_ok(pred, exp):
    inter = min(pred[1], exp[1]) - max(pred[0], exp[0])
    return inter > 0 and inter / (exp[1] - exp[0]) >= 0.5


def _score(cases):
    tp, fp, fn = defaultdict(int), defaultdict(int), defaultdict(int)
    for c in cases:
        text = c["text"]
        preds = [(s.type, s.start, s.end) for s in detect_all(text, c.get("knownNames", []), False)]
        # 🔍 expected -> ช่วง (UTF-16 offset; dataset ไม่มีอักษรนอก BMP จึงเท่ากับ index)
        exps = []
        for e in c["expected"]:
            occ = _occurrences(text, e["value"])
            assert occ, c["caseKey"]
            exps.append((e["type"], occ[0]))
        matched_pred = set()
        for t, span in exps:
            hit = next((i for i, p in enumerate(preds)
                        if i not in matched_pred and p[0] == t and _overlap_ok((p[1], p[2]), span)), None)
            if hit is None:
                fn[t] += 1
            else:
                matched_pred.add(hit)
                tp[t] += 1
        for i, p in enumerate(preds):
            if i not in matched_pred:
                fp[p[0]] += 1
    return tp, fp, fn


def test_dataset_size():
    assert len(_load()) >= 50


def test_bench(capsys):
    tp, fp, fn = _score(_load())
    rows = []
    for t in TYPES:
        p = tp[t] / (tp[t] + fp[t]) if tp[t] + fp[t] else 1.0
        r = tp[t] / (tp[t] + fn[t]) if tp[t] + fn[t] else 1.0
        rows.append((t, tp[t], fp[t], fn[t], p, r))
    TP, FP = sum(tp.values()), sum(fp.values())
    overall_p = TP / (TP + FP) if TP + FP else 1.0
    with capsys.disabled():
        print("\n{:<14}{:>4}{:>4}{:>4}{:>9}{:>9}".format("type", "tp", "fp", "fn", "prec", "recall"))
        for t, a, b, c, p, r in rows:
            print("{:<14}{:>4}{:>4}{:>4}{:>9.3f}{:>9.3f}".format(t, a, b, c, p, r))
        print("overall precision = {:.3f}".format(overall_p))
    for t, _a, _b, _c, _p, r in rows:
        assert r >= RECALL_MIN[t], f"recall {t}={r:.3f} < {RECALL_MIN[t]}"
    assert overall_p >= PRECISION_MIN
