"""Brute Task.ashx and TestingOrders.ashx method names from common patterns."""
from __future__ import annotations

import asyncio
import base64
import json

import httpx

from ring_knife.settings_store import load_settings

ORDER_ID = "1262331"
SAMPLE_ID = "1866178"
TASK_ID = "1998773"


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    detail = (
        f"/UI/Task/TaskDetailsEngineering.html?testingOrderId={ORDER_ID}"
        f"&sampleId={SAMPLE_ID}&taskId={TASK_ID}"
    )
    c = httpx.AsyncClient(
        base_url=base,
        timeout=30,
        headers={"X-Requested-With": "XMLHttpRequest", "Referer": f"{base}{detail}", "Origin": base},
        follow_redirects=True,
    )
    pwd = base64.b64encode(s["password"].encode()).decode()
    await c.get(detail)
    r = await c.post(
        "/AjaxRequest/Index/HomeIndex.ashx",
        data={"method": "Login", "username": s["username"], "pwd": pwd},
    )
    c.cookies.set("UserId", str(r.json()["UserId"]), path="/")

    prefixes = ["GetSample", "GetSamples", "GetTestingSample", "GetTestingReport", "GetReport", "GetTask"]
    suffixes = ["", "List", "Info", "BaseType", "Detail", "ById", "ByOrderId", "ByTaskId", "BySampleId"]
    methods = {f"{p}{s}" for p in prefixes for s in suffixes}
    methods.update(
        [
            "GetSamplesBaseFrom",
            "GetSamplesBaseTypeList",
            "GetTestingReportsListByOrderId",
            "GetTestingReportsListBySampleId",
            "GetTestingReportsListByTaskId",
            "GetReportNoByTaskId",
            "GetReportNoBySampleId",
            "GetReportNoByTestingOrderId",
        ]
    )

    for path in ["/AjaxRequest/TestingOrders/TestingOrders.ashx", "/AjaxRequest/Task/Task.ashx"]:
        print(f"\n=== {path} ===")
        for method in sorted(methods):
            data = {
                "method": method,
                "testingOrderId": ORDER_ID,
                "sampleId": SAMPLE_ID,
                "taskId": TASK_ID,
            }
            rr = await c.post(path, data=data)
            text = rr.text.strip()
            if rr.status_code != 200 or not text or text.startswith("<!") or "Object reference" in text:
                continue
            if text in ("[]", "{}", "null"):
                continue
            try:
                payload = rr.json()
            except Exception:
                continue
            blob = json.dumps(payload, ensure_ascii=False)
            if len(blob) <= 2:
                continue
            print(method, blob[:500])

    await c.aclose()


if __name__ == "__main__":
    asyncio.run(main())
