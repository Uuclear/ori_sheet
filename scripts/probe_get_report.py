"""Probe NonStandard.ashx GetReport and related for full field mapping."""
from __future__ import annotations

import asyncio
import base64
import json

import httpx

from ring_knife.settings_store import load_settings


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    page = (
        "/UI/Task/NonStandardReport.aspx?testingOrderId=1262331&sampleId=1866178"
        "&testingOrderNo=LJ01-260364&sampleNo=LJ01-260364-01&taskId=1998773"
    )
    c = httpx.AsyncClient(
        base_url=base,
        timeout=60,
        headers={"X-Requested-With": "XMLHttpRequest", "Referer": f"{base}{page}", "Origin": base},
        follow_redirects=True,
    )
    pwd = base64.b64encode(s["password"].encode()).decode()
    r = await c.post(
        "/AjaxRequest/Index/HomeIndex.ashx",
        data={"method": "Login", "username": s["username"], "pwd": pwd},
    )
    c.cookies.set("UserId", str(r.json()["UserId"]), path="/")
    path = "/AjaxRequest/Task/NonStandard.ashx"

    probes = [
        {"method": "GetReport", "testingOrderId": "1262331", "sampleId": "1866178", "taskId": "1998773"},
        {"method": "GetReport", "testingOrderId": "1262331", "sampleId": "1866178", "taskId": "1998773", "testingOrderNo": "LJ01-260364"},
        {"method": "GetReportField", "testingOrderId": "1262331", "sampleId": "1866178", "taskId": "1998773"},
        {"method": "GetReportField", "testingReportId": "2569194"},
        {"method": "GetReport", "testingReportId": "2569194"},
    ]
    for data in probes:
        rr = await c.post(path, data=data)
        print("\n===", data, "===")
        if rr.status_code == 200:
            try:
                print(json.dumps(rr.json(), ensure_ascii=False, indent=2)[:4000])
            except Exception:
                print(rr.text[:400])

    await c.aclose()


if __name__ == "__main__":
    asyncio.run(main())
