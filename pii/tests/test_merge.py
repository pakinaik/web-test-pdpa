from app.merge import merge_spans
from app.spans import Span


def test_longer_wins():
    r = merge_spans([Span("PHONE", 0, 5), Span("PERSON", 0, 9)])
    assert [s.type for s in r] == ["PERSON"]


def test_priority_when_equal():
    r = merge_spans([Span("PHONE", 0, 10), Span("THAI_ID", 0, 10)])
    assert [s.type for s in r] == ["THAI_ID"]


def test_bank_beats_phone():
    # 🔍 D27: union -> ช่วง [0,12] ประเภท BANK_ACCOUNT (ไม่ทิ้งส่วน 10..12 ของ PHONE)
    r = merge_spans([Span("PHONE", 0, 12), Span("BANK_ACCOUNT", 0, 10)])
    assert [(s.type, s.start, s.end) for s in r] == [("BANK_ACCOUNT", 0, 12)]


def test_sorted_no_overlap():
    r = merge_spans([Span("EMAIL", 20, 30), Span("PHONE", 0, 10)])
    assert [s.start for s in r] == [0, 20]


def test_partial_overlap_person_title_and_ner_is_union():
    # 🔍 B1: PERSON(title) [0,13] ซ้อน PERSON(ner) [9,24] ต้องได้ช่วงเดียว [0,24]
    r = merge_spans([Span("PERSON", 0, 13, 0.9, "title"), Span("PERSON", 9, 24, 0.7, "ner")])
    assert [(s.type, s.start, s.end) for s in r] == [("PERSON", 0, 24)]


def test_transitive_overlap_collapses_into_one_span():
    # 🔍 B1: CUSTOM[9,23] + PERSON[0,13] + PHONE[20,32] ต้องรวมเป็น [0,32] ช่วงเดียว
    r = merge_spans([Span("PERSON", 0, 13), Span("PHONE", 20, 32), Span("CUSTOM", 9, 23)])
    assert [(s.start, s.end) for s in r] == [(0, 32)]


def test_touching_spans_are_not_merged():
    r = merge_spans([Span("PHONE", 0, 5), Span("EMAIL", 5, 9)])
    assert [(s.start, s.end) for s in r] == [(0, 5), (5, 9)]
