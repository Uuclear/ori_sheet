from __future__ import annotations

import json
import sqlite3
from datetime import datetime, timezone
from typing import Any

from ring_knife.config import ROOT_DIR

DB_PATH = ROOT_DIR / "data" / "ring_knife.db"


def _connect() -> sqlite3.Connection:
    DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn


def init_db() -> None:
    with _connect() as conn:
        conn.execute(
            """
            CREATE TABLE IF NOT EXISTS drafts (
                entrust_no TEXT PRIMARY KEY,
                payload TEXT NOT NULL,
                updated_at TEXT NOT NULL
            )
            """
        )
        conn.commit()


def save_draft(entrust_no: str, payload: dict[str, Any]) -> str:
    key = entrust_no.strip()
    if not key:
        raise ValueError("委托编号不能为空")
    now = datetime.now(timezone.utc).isoformat()
    body = json.dumps(payload, ensure_ascii=False)
    with _connect() as conn:
        conn.execute(
            """
            INSERT INTO drafts (entrust_no, payload, updated_at)
            VALUES (?, ?, ?)
            ON CONFLICT(entrust_no) DO UPDATE SET
                payload = excluded.payload,
                updated_at = excluded.updated_at
            """,
            (key, body, now),
        )
        conn.commit()
    return now


def load_draft(entrust_no: str) -> dict[str, Any] | None:
    key = entrust_no.strip()
    if not key:
        return None
    with _connect() as conn:
        row = conn.execute(
            "SELECT payload, updated_at FROM drafts WHERE entrust_no = ?",
            (key,),
        ).fetchone()
    if not row:
        return None
    data = json.loads(row["payload"])
    if isinstance(data, dict):
        data["updated_at"] = row["updated_at"]
    return data


def delete_draft(entrust_no: str) -> bool:
    key = entrust_no.strip()
    if not key:
        return False
    with _connect() as conn:
        cur = conn.execute("DELETE FROM drafts WHERE entrust_no = ?", (key,))
        conn.commit()
        return cur.rowcount > 0
