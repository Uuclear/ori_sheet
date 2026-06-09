"""Probe integrated query variants and discover ashx report endpoints."""
from __future__ import annotations

import asyncio
import base64
import json
import re

import httpx

from ring_knife.settings_store import load_settings


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    c = httpx.AsyncClient(base_url=base, timeout=60, follow_redirects=True)
    pwd = base64.b64encode(s["password"].encode()).decode()
    r = await c.post(
        "/AjaxRequest/Index/HomeIndex.ashx",
        data={"method": "Login", "username": s["username"], "pwd": pwd},
    )
    c.cookies.set("UserId", str(r.json()["UserId"]), path="/")
    headers = {"X-Requested-With": "XMLHttpRequest", "Origin": base}

    iq = "/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx"
    base_q = {
        "method": "GetIntegratedQueryInfo",
        "testingOrderNo": "LJ01-260364",
        "testingSamplesNo": "LJ01-260364-01",
        "cha": "1",
        "page": "1",
        "size": "10",
    }
    print("=== type x authType matrix ===")
    for t in ["1", "2", "3", "4", "5"]:
        for auth in ["1", "2", "3", "4"]:
            data = {**base_q, "type": t, "authType": auth}
            rr = await c.post(iq, data=data, headers=headers)
            if rr.status_code != 200 or not rr.text or rr.text in ("[]", ""):
                continue
            try:
                payload = rr.json()
            except Exception:
                continue
            rows = payload if isinstance(payload, list) else payload.get("rows") or []
            if not rows:
                continue
            row = rows[0]
            print(f"type={t} auth={auth} keys={list(row.keys())[:20]}...")
            interesting = {k: v for k, v in row.items() if v not in (None, "", 0, False)}
            for k in sorted(interesting):
                kl = k.lower()
                if any(x in kl for x in ["report", "sample", "name", "no", "spec", "manu"]):
                    print(f"  {k}: {interesting[k]}")

    # discover report ashx
    print("\n=== discover ashx ===")
    for page in [
        "/UI/IntegratedQuery/IntegratedQuery.html",
        "/default.aspx",
        "/UI/Index.html",
    ]:
        rr = await c.get(page)
        if rr.status_code != 200:
            print(page, rr.status_code)
            continue
        paths = sorted(set(re.findall(r"/AjaxRequest/[A-Za-z0-9_/]+\.ashx", rr.text)))
        report_paths = [p for p in paths if "report" in p.lower() or "sample" in p.lower()]
        if report_paths:
            print(page, report_paths)

    # try discovered report paths with common methods
    candidates = [
        "/AjaxRequest/report/reportBorrowing.ashx",
        "/AjaxRequest/ReportManage/ReportManage.ashx",
        "/AjaxRequest/TestingReports/TestingReportsManage.ashx",
    ]
    for path in candidates:
        for method in ["GetReportList", "GetList", "GetTestingReportList"]:
            rr = await c.post(
                path,
                data={"method": method, "testingOrderId": "1262331", "page": "1", "pageSize": "10"},
                headers=headers,
            )
            if rr.status_code == 200 and rr.text and not rr.text.startswith("<!") and len(rr.text) > 5:
                print(path, method, rr.text[:300])

    await c.aclose()


if __name__ == "__main__":
    asyncio.run(main())
