"""Probe LIMIS fields for report_no, unit_address, entrust_date mapping."""
from __future__ import annotations

import asyncio
import base64
import json

import httpx

from ring_knife.settings_store import load_settings

ENTRUST = "LJ01-260364"
ORDER_ID = "1262331"
TASK_ID = "1998773"
SAMPLE_ID = "1866178"


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    task_page = "/UI/Task/TaskManagement.html"
    detail = (
        f"/UI/Task/TaskDetailsEngineering.html?testingOrderId={ORDER_ID}"
        f"&sampleId={SAMPLE_ID}&taskId={TASK_ID}"
    )
    async with httpx.AsyncClient(
        base_url=base,
        timeout=60,
        headers={
            "X-Requested-With": "XMLHttpRequest",
            "Referer": f"{base}{task_page}",
            "Origin": base,
        },
        follow_redirects=True,
    ) as c:
        pwd = base64.b64encode(s["password"].encode()).decode()
        await c.get(task_page)
        r = await c.post(
            "/AjaxRequest/Index/HomeIndex.ashx",
            data={"method": "Login", "username": s["username"], "pwd": pwd},
        )
        c.cookies.set("UserId", str(r.json()["UserId"]), path="/")

        # Task list
        tr = await c.post(
            "/AjaxRequest/Task/Task.ashx",
            data={
                "method": "GetTaskManagementList",
                "testingOrderNo": ENTRUST,
                "sampleNo": "",
                "standardcode": "",
                "back": "",
                "principalPartName": "",
                "testingTypeCode": "",
                "setlementStatus": "",
                "setlementType": "",
                "taskExecutiveCode": "",
                "taskExecutor": "",
                "day_s": "",
                "day_e": "",
                "type": "",
                "taskStatusCode": "",
                "pageLoad": "2",
            },
        )
        print("TASK status", tr.status_code)
        tasks = tr.json() if tr.text.startswith("[") else []
        if tasks:
            row = tasks[0]
            print("=== TASK ROW (filtered) ===")
            for k in sorted(row):
                v = row[k]
                if v not in (None, "", 0, False):
                    kl = k.lower()
                    if any(x in kl for x in ["report", "address", "date", "time", "unit", "client", "sample", "no", "name"]):
                        print(f"  {k}: {v!r}")

        # Base type
        c.headers["Referer"] = f"{base}{detail}"
        await c.get(detail)
        br = await c.post(
            "/AjaxRequest/TestingOrders/TestingOrders.ashx",
            data={"method": "GetTestingOrdersBaseType", "testingOrderId": ORDER_ID},
        )
        base_row = br.json()[0]
        print("\n=== GetTestingOrdersBaseType (address/date/report) ===")
        for k in sorted(base_row):
            v = base_row[k]
            if v not in (None, "", 0, False):
                kl = k.lower()
                if any(x in kl for x in ["report", "address", "date", "time", "unit", "client", "post", "area", "phone"]):
                    print(f"  {k}: {v!r}")

        # Integrated query
        iq = await c.post(
            "/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx",
            data={
                "method": "GetIntegratedQueryInfo",
                "type": "4",
                "size": "1",
                "page": "1",
                "testingOrderNo": ENTRUST,
                "authType": "4",
                "cha": "1",
            },
        )
        print("\nIQ status", iq.status_code, iq.text[:80])
        if iq.text.startswith("["):
            iq_row = iq.json()[0]
        elif iq.text.startswith("{"):
            d = iq.json()
            iq_row = (d.get("rows") or [None])[0]
        else:
            iq_row = None
        if iq_row:
            print("=== IntegratedQuery (filtered) ===")
            for k in sorted(iq_row):
                v = iq_row[k]
                if v not in (None, "", 0, False):
                    kl = k.lower()
                    if any(x in kl for x in ["report", "address", "date", "time", "unit", "client", "link", "phone", "properties", "nature"]):
                        print(f"  {k}: {v!r}")

        # Try report-related methods
        report_methods = [
            {"method": "GetTestingReportsByOrderId", "testingOrderId": ORDER_ID},
            {"method": "GetReportListByTestingOrderId", "testingOrderId": ORDER_ID},
            {"method": "GetTestingReportInfo", "testingOrderId": ORDER_ID},
            {"method": "GetTestingReportList", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        ]
        paths = [
            "/AjaxRequest/TestingOrders/TestingOrders.ashx",
            "/AjaxRequest/Report/Report.ashx",
            "/AjaxRequest/TestingReport/TestingReport.ashx",
            "/AjaxRequest/Business/ReportManage.ashx",
        ]
        print("\n=== Report API probe ===")
        for path in paths:
            for data in report_methods:
                rr = await c.post(path, data=data)
                if rr.status_code == 200 and rr.text and "Object reference" not in rr.text and "无法找到" not in rr.text and rr.text.strip() not in ("", "[]", "{}"):
                    if rr.text.startswith("[") and len(rr.text) > 2:
                        print(path, data["method"], "=>", rr.text[:400])
                    elif rr.text.startswith("{") and "state" in rr.text:
                        print(path, data["method"], "=>", rr.text[:400])


if __name__ == "__main__":
    asyncio.run(main())
