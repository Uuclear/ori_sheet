"""Probe TestReport.html for report number and sample name fields."""
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
    page = (
        "/UI/Task/TestReport.html?testingOrderNo=LJ01-260364&testingOrderId=1262331"
        "&sampleNo=LJ01-260364-01&sampleId=1866178&taskId=1998773"
    )
    c = httpx.AsyncClient(base_url=base, timeout=60, follow_redirects=True)
    pwd = base64.b64encode(s["password"].encode()).decode()
    r = await c.post(
        "/AjaxRequest/Index/HomeIndex.ashx",
        data={"method": "Login", "username": s["username"], "pwd": pwd},
    )
    c.cookies.set("UserId", str(r.json()["UserId"]), path="/")
    html = (await c.get(page)).text
    with open("d:/github/jsscript/test_report_page.html", "w", encoding="utf-8", errors="replace") as f:
        f.write(html)
    print("status len", len(html))
    methods = sorted(set(re.findall(r"method\s*[:=]\s*['\"]([A-Za-z0-9_]+)['\"]", html)))
    ashx = sorted(set(re.findall(r"AjaxRequest/[A-Za-z0-9_/]+\.ashx", html)))
    print("methods", methods)
    print("ashx", ashx)
    for pat in ["ReportNo", "reportNo", "报告编号", "样品名称", "sampleName", "txt_sample", "GetReport", "GetTestingReport"]:
        if pat.lower() in html.lower() or pat in html:
            print("HAS", pat)
    for line in html.splitlines():
        if any(k in line for k in ["报告编号", "样品名称", "ReportNo", "sampleName", "txt_report", "GetReport", "GetTestingReport", "GetSample"]):
            print(line.strip()[:220])

    headers = {"X-Requested-With": "XMLHttpRequest", "Referer": f"{base}{page}", "Origin": base}
    for method in methods:
        for rel in ashx:
            path = "/" + rel if not rel.startswith("/") else rel
            data = {
                "method": method,
                "testingOrderId": "1262331",
                "sampleId": "1866178",
                "taskId": "1998773",
                "testingOrderNo": "LJ01-260364",
                "sampleNo": "LJ01-260364-01",
            }
            rr = await c.post(path, data=data, headers=headers)
            text = rr.text.strip()
            if rr.status_code == 200 and text and not text.startswith("<!") and text not in ("[]", "{}", "null") and "Object reference" not in text:
                print(f"\n{path} {method} => {text[:800]}")

    await c.aclose()


if __name__ == "__main__":
    asyncio.run(main())
