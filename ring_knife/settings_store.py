from __future__ import annotations

import json
from pathlib import Path

from ring_knife.config import ROOT_DIR, settings as env_settings

SETTINGS_FILE = ROOT_DIR / "data" / "app_settings.json"


def _default() -> dict[str, str]:
    return {
        "base_url": env_settings.limis_base_url,
        "username": env_settings.limis_username,
        "password": env_settings.limis_password,
    }


def load_settings() -> dict[str, str]:
    if not SETTINGS_FILE.exists():
        return _default()
    try:
        data = json.loads(SETTINGS_FILE.read_text(encoding="utf-8"))
        merged = _default()
        merged.update({k: str(v) for k, v in data.items() if v is not None})
        return merged
    except (json.JSONDecodeError, OSError):
        return _default()


def save_settings(base_url: str, username: str, password: str | None = None) -> dict[str, str]:
    current = load_settings()
    stored = {
        "base_url": base_url.strip() or current["base_url"],
        "username": username.strip(),
        "password": password if password is not None and password != "" else current["password"],
    }
    SETTINGS_FILE.parent.mkdir(parents=True, exist_ok=True)
    SETTINGS_FILE.write_text(json.dumps(stored, ensure_ascii=False, indent=2), encoding="utf-8")
    return stored
