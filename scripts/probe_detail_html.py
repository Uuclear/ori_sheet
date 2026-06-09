"""Fetch TaskDetailsEngineering page and probe unit/report APIs."""
from __future__ import annotations

import asyncio
import base64
import json
import re

import httpx

from ring_knife.settings_store import load_settings

ORDER_ID = "1262331"
UNIT_ID = "106621"


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    detail = f"/UI/Task/TaskDetailsEngineering.html?testingOrderId={ORDER_ID}&sampleId=1866178&taskId=1998773"
    async with httpx.AsyncClient(
        base_url=base,
        timeout=60,
        headers={"X-Requested-With": "XMLHttpRequest", "Referer": f"{base}{detail}", "Origin": base},
        follow_redirects=True,
    ) as c:
        pwd = base64.b64encode(s["password"].encode()).decode()
        html = (await c.get(detail)).text
        r = await c.post(
            "/AjaxRequest/Index/HomeIndex.ashx",
            data={"method": "Login", "username": s["username"], "pwd": pwd},
        )
        c.cookies.set("UserId", str(r.json()["UserId"]), path="/")

        # Search HTML for address/report bindings
        patterns = [
            r"单位地址[^<]{0,80}",
            r"报告编号[^<]{0,80}",
            r"委托日期[^<]{0,80}",
            r"clientAddress[^\"']{0,40}",
            r"testingOrderUnitAddress[^\"']{0,40}",
            r"unitAddress[^\"']{0,40}",
            r"ReportNo[^\"']{0,40}",
            r"testingReportsNo[^\"']{0,40}",
            r"testingOrderTime[^\"']{0,40}",
        ]
        print("=== HTML snippets ===")
        for pat in patterns:
            for m in re.finditer(pat, html, re.I):
                print(m.group(0)[:120])

        methods = sorted(set(re.findall(r"method[\"'\s]*[:=]\s*[\"']([A-Za-z0-9_]+)", html)))
        print("\n=== Detail page methods ===")
        for m in methods:
            if any(x in m.lower() for x in ["report", "unit", "customer", "order", "address"]):
                print(m)

        # Probe customer/unit APIs
        probes = [
            ("/AjaxRequest/Customer/Customer.ashx", {"method": "GetCustomerById", "unitId": UNIT_ID}),
            ("/AjaxRequest/Customer/Customer.ashx", {"method": "GetCustomerInfo", "unitId": UNIT_ID}),
            ("/AjaxRequest/Customer/Customer.ashx", {"method": "GetCustomerById", "customerId": UNIT_ID}),
            ("/AjaxRequest/TestingOrders/TestingOrders.ashx", {"method": "GetCustomerInfo", "unitId": UNIT_ID}),
            ("/AjaxRequest/TestingOrders/TestingOrders.ashx", {"method": "GetTestingOrderUnitInfo", "unitId": UNIT_ID}),
            ("/AjaxRequest/TestingOrders/TestingOrders.ashx", {"method": "GetTestingOrderUnitInfo", "testingOrderId": ORDER_ID}),
            ("/AjaxRequest/Report/Report.ashx", {"method": "GetReportNoByTestingOrderId", "testingOrderId": ORDER_ID}),
            ("/AjaxRequest/Report/Report.ashx", {"method": "GetTestingReportNo", "testingOrderId": ORDER_ID}),
            ("/AjaxRequest/TestingReport/TestingReport.ashx", {"method": "GetTestingReportList", "testingOrderId": ORDER_ID}),
        ]
        print("\n=== API probes ===")
        for path, data in probes:
            rr = await c.post(path, data=data)
            txt = rr.text[:500]
            if rr.status_code == 200 and txt and "Object reference" not in txt and "无法找到" not in txt and txt.strip() not in ("", "[]", "null"):
                print(path, data, "=>", txt)


if __name__ == "__main__":
    asyncio.run(main())
