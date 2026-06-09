"""Exhaustive probe for GetSamples* and report APIs."""
from __future__ import annotations

import asyncio
import base64
import json

import httpx

from ring_knife.settings_store import load_settings

ORDER_ID = "1262331"
SAMPLE_ID = "1866178"
TASK_ID = "1998773"
ENTRUST = "LJ01-260364"


async def try_post(c: httpx.AsyncClient, path: str, data: dict) -> None:
    rr = await c.post(path, data=data)
    text = rr.text.strip()
    if rr.status_code != 200 or not text or text.startswith("<!") or "Object reference" in text:
        return
    try:
        payload = rr.json()
    except Exception:
        return
    blob = json.dumps(payload, ensure_ascii=False)
    if len(blob) < 3:
        return
    keys_of_interest = ["sampleName", "SampleName", "reportNo", "ReportNo", "testingReportsNo", "manufacturer", "typeSpecification", "sampleDesc", "material"]
    if not any(k in blob for k in keys_of_interest) and "sampleNo" not in blob:
        return
    print(f"\n>>> {path} {data}")
    if isinstance(payload, list):
        for i, row in enumerate(payload[:3]):
            if isinstance(row, dict):
                print(f"  row[{i}]:", {k: v for k, v in row.items() if v not in (None, "", 0, False)})
    elif isinstance(payload, dict):
        rows = payload.get("rows") or payload.get("data")
        if rows:
            for i, row in enumerate(rows[:3]):
                if isinstance(row, dict):
                    print(f"  row[{i}]:", {k: v for k, v in row.items() if v not in (None, "", 0, False)})
        else:
            print(" ", {k: v for k, v in payload.items() if v not in (None, "", 0, False)})


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    detail = (
        f"/UI/Task/TaskDetailsEngineering.html?testingOrderId={ORDER_ID}"
        f"&sampleId={SAMPLE_ID}&taskId={TASK_ID}"
    )
    c = httpx.AsyncClient(
        base_url=base,
        timeout=60,
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

    orders = "/AjaxRequest/TestingOrders/TestingOrders.ashx"
    task = "/AjaxRequest/Task/Task.ashx"
    iq = "/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx"

    sample_methods = [
        {"method": "GetSamplesBaseType", "testingOrderId": ORDER_ID},
        {"method": "GetSamplesBaseType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetSamplesBaseType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID, "taskId": TASK_ID},
        {"method": "GetSamplesListType", "testingOrderId": ORDER_ID},
        {"method": "GetSamplesList", "testingOrderId": ORDER_ID},
        {"method": "GetSampleListByOrderId", "testingOrderId": ORDER_ID},
        {"method": "GetTestingSamplesList", "testingOrderId": ORDER_ID},
        {"method": "GetSamplesInfoList", "testingOrderId": ORDER_ID},
        {"method": "GetSamplesTestingBasisType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetTaskBaseList", "taskId": TASK_ID},
    ]
    for data in sample_methods:
        path = task if data["method"].startswith("GetTask") else orders
        await try_post(c, path, data)

    # Integrated query with sample/report filters - dump ALL keys for auth 1
    for auth in ["1", "3"]:
        data = {
            "method": "GetIntegratedQueryInfo",
            "type": "4",
            "size": "5",
            "page": "1",
            "testingOrderNo": ENTRUST,
            "authType": auth,
            "cha": "1",
        }
        rr = await c.post(iq, data=data)
        if rr.status_code == 200 and rr.text.startswith("["):
            row = rr.json()[0]
            print(f"\n>>> IQ auth={auth} ALL KEYS:")
            print(json.dumps(row, ensure_ascii=False, indent=2))

    # Pages that might list reports
    pages = [
        "/UI/IntegratedQuery/IntegratedQuery.html",
        "/UI/Report/ReportList.html",
        "/UI/TestingOrders/TestingOrdersList.html",
    ]
    for page in pages:
        rr = await c.get(page)
        if rr.status_code == 200 and "ReportNo" in rr.text:
            print("page has ReportNo:", page)

    await c.aclose()


if __name__ == "__main__":
    asyncio.run(main())
