"""Deep probe for report number and sample name fields in LIMIS."""
from __future__ import annotations

import asyncio
import base64
import json
import re

import httpx

from ring_knife.settings_store import load_settings

ENTRUST = "LJ01-260364"
ORDER_ID = "1262331"
SAMPLE_ID = "1866178"
TASK_ID = "1998773"


async def login_client(base: str, s: dict) -> httpx.AsyncClient:
    c = httpx.AsyncClient(
        base_url=base,
        timeout=60,
        headers={"X-Requested-With": "XMLHttpRequest", "Origin": base},
        follow_redirects=True,
    )
    pwd = base64.b64encode(s["password"].encode()).decode()
    r = await c.post(
        "/AjaxRequest/Index/HomeIndex.ashx",
        data={"method": "Login", "username": s["username"], "pwd": pwd},
    )
    c.cookies.set("UserId", str(r.json()["UserId"]), path="/")
    return c


def interesting(row: dict) -> dict:
    out = {}
    for k, v in row.items():
        if v in (None, "", 0, False):
            continue
        kl = k.lower()
        if any(
            x in kl
            for x in [
                "report",
                "sample",
                "name",
                "no",
                "material",
                "spec",
                "type",
                "manufacturer",
            ]
        ):
            out[k] = v
    return out


async def post_json(c: httpx.AsyncClient, path: str, data: dict) -> tuple[int, object | None]:
    rr = await c.post(path, data=data)
    if rr.status_code != 200 or not rr.text or rr.text.startswith("<!"):
        return rr.status_code, None
    try:
        return rr.status_code, rr.json()
    except Exception:
        return rr.status_code, rr.text[:200]


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    detail = (
        f"/UI/Task/TaskDetailsEngineering.html?testingOrderId={ORDER_ID}"
        f"&sampleId={SAMPLE_ID}&taskId={TASK_ID}"
    )
    c = await login_client(base, s)
    c.headers["Referer"] = f"{base}{detail}"
    await c.get(detail)

    orders_path = "/AjaxRequest/TestingOrders/TestingOrders.ashx"
    sample_methods = [
        {"method": "GetSamplesBaseType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetSamplesTestingBasisType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetSamplesTestingItemType_Task", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID, "taskId": TASK_ID},
        {"method": "GetSampleInfo", "sampleId": SAMPLE_ID},
        {"method": "GetSampleInfo", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetSamplesInfo", "testingOrderId": ORDER_ID},
        {"method": "GetSamplesList", "testingOrderId": ORDER_ID},
        {"method": "GetSamplesType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetSamplesTestingItemType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetTestingSamplesBaseType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetTestingSamplesInfo", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetTestingReportList", "testingOrderId": ORDER_ID},
        {"method": "GetTestingReportList", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetTestingReportsList", "testingOrderId": ORDER_ID},
        {"method": "GetReportListByTestingOrderId", "testingOrderId": ORDER_ID},
        {"method": "GetTestingReportsByOrderId", "testingOrderId": ORDER_ID},
        {"method": "GetTestingReportsBaseType", "testingOrderId": ORDER_ID},
        {"method": "GetTestingReportsBaseType", "testingOrderId": ORDER_ID, "sampleId": SAMPLE_ID},
        {"method": "GetTestingReportsBaseType", "testingOrderId": ORDER_ID, "taskId": TASK_ID},
        {"method": "GetTaskExcuterTestingOrder", "testingOrderId": ORDER_ID, "taskId": TASK_ID},
    ]

    print("=== TestingOrders.ashx sample/report methods ===")
    for data in sample_methods:
        status, payload = await post_json(c, orders_path, data)
        if payload is None:
            continue
        rows = payload if isinstance(payload, list) else [payload]
        if not rows:
            continue
        row = rows[0] if isinstance(rows[0], dict) else {}
        filt = interesting(row)
        if filt:
            print(f"\n{data['method']}:")
            print(json.dumps(filt, ensure_ascii=False, indent=2))

    # Integrated query auth types
    iq_path = "/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx"
    print("\n=== IntegratedQuery authType variants ===")
    for auth in ["1", "2", "3", "4"]:
        data = {
            "method": "GetIntegratedQueryInfo",
            "type": "4",
            "size": "5",
            "page": "1",
            "testingOrderNo": ENTRUST,
            "testingSamplesNo": "LJ01-260364-01",
            "authType": auth,
            "cha": "1",
        }
        status, payload = await post_json(c, iq_path, data)
        if not payload:
            print(f"authType={auth}: empty/non-json status={status}")
            continue
        if isinstance(payload, dict):
            rows = payload.get("rows") or payload.get("data") or []
        else:
            rows = payload
        if not rows:
            print(f"authType={auth}: no rows")
            continue
        row = rows[0]
        print(f"\nauthType={auth} keys={len(row)} interesting:")
        print(json.dumps(interesting(row), ensure_ascii=False, indent=2))

    # Other ashx paths from detail page
    html = (await c.get(detail)).text
    ashx = sorted(set(re.findall(r"(/AjaxRequest/[A-Za-z0-9_/]+\.ashx)", html)))
    print("\n=== ashx on detail page ===")
    for path in ashx:
        print(path)

    # Brute report paths
    report_paths = [
        "/AjaxRequest/Report/Report.ashx",
        "/AjaxRequest/Report/ReportManage.ashx",
        "/AjaxRequest/TestingReport/TestingReport.ashx",
        "/AjaxRequest/Business/ReportManage.ashx",
        "/AjaxRequest/TestingReports/TestingReports.ashx",
        "/AjaxRequest/Sample/Sample.ashx",
        "/AjaxRequest/Samples/Samples.ashx",
        "/AjaxRequest/Business/SampleManage.ashx",
    ]
    report_methods = [
        "GetReportList",
        "GetTestingReportList",
        "GetReportInfo",
        "GetTestingReportsInfo",
        "GetSampleDetail",
        "GetSampleList",
    ]
    print("\n=== Other paths ===")
    for path in report_paths:
        for method in report_methods:
            data = {
                "method": method,
                "testingOrderId": ORDER_ID,
                "sampleId": SAMPLE_ID,
                "taskId": TASK_ID,
            }
            status, payload = await post_json(c, path, data)
            if payload is None:
                continue
            rows = payload if isinstance(payload, list) else [payload]
            if isinstance(payload, dict) and payload.get("rows"):
                rows = payload["rows"]
            if not rows or not isinstance(rows[0], dict):
                continue
            filt = interesting(rows[0])
            if filt:
                print(path, method, json.dumps(filt, ensure_ascii=False)[:300])

    await c.aclose()


if __name__ == "__main__":
    asyncio.run(main())
