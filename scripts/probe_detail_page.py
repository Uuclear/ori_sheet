"""Fetch detail page and probe report/sample APIs."""
from __future__ import annotations

import asyncio
import base64
import json
import re

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
    async with httpx.AsyncClient(base_url=base, timeout=60, follow_redirects=True) as c:
        pwd = base64.b64encode(s["password"].encode()).decode()
        r = await c.post(
            "/AjaxRequest/Index/HomeIndex.ashx",
            data={"method": "Login", "username": s["username"], "pwd": pwd},
        )
        c.cookies.set("UserId", str(r.json()["UserId"]), path="/")
        html = (await c.get(detail)).text
        with open("d:/github/jsscript/detail_page.html", "w", encoding="utf-8", errors="replace") as f:
            f.write(html)
        methods = sorted(set(re.findall(r"method\s*[:=]\s*['\"]([A-Za-z0-9_]+)['\"]", html)))
        print("detail methods:", methods)
        for line in html.splitlines():
            if any(
                k in line
                for k in [
                    "GetSamples",
                    "sampleName",
                    "ReportNo",
                    "reportNo",
                    "样品名称",
                    "报告编号",
                    "GetTestingReport",
                    "GetReport",
                ]
            ):
                print(line.strip()[:240])

        headers = {
            "X-Requested-With": "XMLHttpRequest",
            "Referer": f"{base}{detail}",
            "Origin": base,
        }
        probes = [
            (
                "/AjaxRequest/Report/ReportManage.ashx",
                {"method": "GetReportList", "page": "1", "pageSize": "20", "TaskId": TASK_ID},
            ),
            (
                "/AjaxRequest/Report/ReportManage.ashx",
                {"method": "GetReportList", "page": "1", "pageSize": "20", "testingOrderId": ORDER_ID},
            ),
            (
                "/AjaxRequest/TestingOrders/TestingOrders.ashx",
                {"method": "GetSamplesBaseType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
            ),
            (
                "/AjaxRequest/TestingOrders/TestingOrders.ashx",
                {"method": "GetSamplesBaseType", "testingOrderId": ORDER_ID},
            ),
            (
                "/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx",
                {
                    "method": "GetIntegratedQueryInfo",
                    "type": "4",
                    "size": "10",
                    "page": "1",
                    "testingOrderNo": "LJ01-260364",
                    "testingSamplesNo": "LJ01-260364-01",
                    "authType": "1",
                    "cha": "1",
                },
            ),
        ]
        print("\n=== probes ===")
        for path, data in probes:
            rr = await c.post(path, data=data, headers=headers)
            print(path, data.get("method"), rr.status_code, rr.text[:400].replace("\n", " "))


if __name__ == "__main__":
    asyncio.run(main())
