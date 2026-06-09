"""Parse NonStandardReport.aspx for report/sample API methods."""
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
        "/UI/Task/NonStandardReport.aspx?testingOrderId=1262331&sampleId=1866178"
        "&testingOrderNo=LJ01-260364&sampleNo=LJ01-260364-01&taskId=1998773"
    )
    c = httpx.AsyncClient(base_url=base, timeout=60, follow_redirects=True)
    pwd = base64.b64encode(s["password"].encode()).decode()
    r = await c.post(
        "/AjaxRequest/Index/HomeIndex.ashx",
        data={"method": "Login", "username": s["username"], "pwd": pwd},
    )
    c.cookies.set("UserId", str(r.json()["UserId"]), path="/")
    html = (await c.get(page)).text
    with open("d:/github/jsscript/nstd_report.html", "w", encoding="utf-8", errors="replace") as f:
        f.write(html)

    methods = sorted(set(re.findall(r"method\s*[:=]\s*['\"]([A-Za-z0-9_]+)['\"]", html)))
    ashx = sorted(set(re.findall(r"AjaxRequest/[A-Za-z0-9_/]+\.ashx", html)))
    print("methods:", [m for m in methods if re.search(r"report|sample|Report|Sample", m)])
    print("ashx:", ashx)

    keywords = [
        "testingReportCode",
        "样品名称",
        "sampleName",
        "sampleDesc",
        "Manufacturer",
        "TypeSpecification",
        "报告编号",
    ]
    for line in html.splitlines():
        if any(k in line for k in keywords):
            print(line.strip()[:240])

    headers = {"X-Requested-With": "XMLHttpRequest", "Referer": f"{base}{page}", "Origin": base}
    for method in methods:
        if not re.search(r"get|list|info|report|sample", method, re.I):
            continue
        for rel in ashx:
            path = "/" + rel
            data = {
                "method": method,
                "testingOrderId": "1262331",
                "sampleId": "1866178",
                "taskId": "1998773",
                "testingOrderNo": "LJ01-260364",
                "sampleNo": "LJ01-260364-01",
                "page": "1",
                "pageSize": "20",
            }
            rr = await c.post(path, data=data, headers=headers)
            text = rr.text.strip()
            if rr.status_code != 200 or not text or text.startswith("<!") or text in ("[]", "{}", "null"):
                continue
            if "Object reference" in text or "无法找到" in text:
                continue
            if "testingReport" in text or "ReportNo" in text or "sampleName" in text or "SampleName" in text:
                print(f"\n>>> {path} {method}")
                print(text[:1200])

    await c.aclose()


if __name__ == "__main__":
    asyncio.run(main())
