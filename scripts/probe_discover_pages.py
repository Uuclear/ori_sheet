"""Discover UI pages and report-related ashx on LIMIS."""
from __future__ import annotations

import asyncio
import base64
import re

import httpx

from ring_knife.settings_store import load_settings


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    c = httpx.AsyncClient(base_url=base, timeout=30, follow_redirects=True)
    pwd = base64.b64encode(s["password"].encode()).decode()
    r = await c.post(
        "/AjaxRequest/Index/HomeIndex.ashx",
        data={"method": "Login", "username": s["username"], "pwd": pwd},
    )
    c.cookies.set("UserId", str(r.json()["UserId"]), path="/")

    seeds = [
        "/UI/Task/TaskManagement.html",
        "/UI/Task/TaskDetailsEngineering.html?testingOrderId=1262331&sampleId=1866178&taskId=1998773",
        "/UI/IntegratedQuery/IntegratedQueryManage.html",
        "/UI/IntegratedQuery/IntegratedQueryManageList.html",
        "/UI/Report/ReportManagement.html",
        "/UI/Report/ReportListManage.html",
        "/UI/TestingReport/TestingReportList.html",
        "/UI/Experiment/ExperimentResult3.aspx?sampleId=1866178&taskId=1998773&deptCode=S125&sampleName=LJ01-260364-01&taskStatusCode=2&RWindex=1",
    ]
    all_ashx: set[str] = set()
    for page in seeds:
        rr = await c.get(page)
        print(page, rr.status_code, len(rr.text))
        if rr.status_code == 200:
            paths = set(re.findall(r"/AjaxRequest/[A-Za-z0-9_/]+\.ashx", rr.text))
            reportish = [p for p in paths if re.search(r"report|sample|task|order|experiment", p, re.I)]
            if reportish:
                print(" ", reportish)
            all_ashx |= paths
            if "ReportNo" in rr.text or "reportNo" in rr.text or "样品名称" in rr.text:
                for line in rr.text.splitlines():
                    if any(k in line for k in ["ReportNo", "reportNo", "样品名称", "sampleName", "GetReport"]):
                        print("  LINE:", line.strip()[:200])

    print("\nAll unique ashx count:", len(all_ashx))
    for p in sorted(all_ashx):
        if re.search(r"report|sample", p, re.I):
            print(p)

    await c.aclose()


if __name__ == "__main__":
    asyncio.run(main())
