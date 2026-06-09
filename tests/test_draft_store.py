from fastapi.testclient import TestClient

from ring_knife.data_store import delete_draft, init_db, load_draft, save_draft
from ring_knife.main import app

SAMPLE_DRAFT = {
    "project": {
        "entrust_no": "WT-DRAFT-001",
        "report_no": "BG001",
        "entrust_unit": "单位A",
        "contact": "10086",
        "supervision_unit": "监理甲",
        "construction_unit": "施工乙",
        "project_name": "工程",
        "unit_address": "地址",
        "project_address": "工程地址",
        "entrust_date": "2026-06-01",
        "project_section": "部位",
        "report_date": "2026-06-09",
        "test_nature": "现场检测",
    },
    "params": {
        "max_dry_density": 1.85,
        "design_requirement": 0.93,
        "sample_name": "回填土",
        "result_type": "compaction_coeff",
        "record_template": "group2",
    },
    "samples": [],
    "calc_results": [],
    "overall_conclusion": "",
    "sample_no_prefix": "",
}


def test_sqlite_draft_roundtrip(tmp_path, monkeypatch):
    db_file = tmp_path / "test.db"
    monkeypatch.setattr("ring_knife.data_store.DB_PATH", db_file)
    init_db()
    save_draft("WT-DRAFT-001", SAMPLE_DRAFT)
    loaded = load_draft("WT-DRAFT-001")
    assert loaded is not None
    assert loaded["project"]["supervision_unit"] == "监理甲"
    assert delete_draft("WT-DRAFT-001") is True
    assert load_draft("WT-DRAFT-001") is None


def test_draft_api(tmp_path, monkeypatch):
    db_file = tmp_path / "api_test.db"
    monkeypatch.setattr("ring_knife.data_store.DB_PATH", db_file)
    init_db()
    client = TestClient(app)
    resp = client.put("/api/drafts/WT-API-001", json=SAMPLE_DRAFT)
    assert resp.status_code == 200, resp.text
    assert resp.json()["success"] is True

    resp = client.get("/api/drafts/WT-API-001")
    assert resp.status_code == 200, resp.text
    data = resp.json()
    assert data["success"] is True
    assert data["draft"]["project"]["construction_unit"] == "施工乙"

    resp = client.delete("/api/drafts/WT-API-001")
    assert resp.status_code == 200
    resp = client.get("/api/drafts/WT-API-001")
    assert resp.json()["success"] is False
