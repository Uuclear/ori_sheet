"""Probe experiment APIs for sample name."""
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
    async with httpx.AsyncClient(
        base_url=base,
        timeout=60,
        follow_redirects=True,
        headers={"X-Requested-With": "XMLHttpRequest", "Origin": base},
    ) as c:
        pwd = base64.b64encode(s["password"].encode()).decode()
        r = await c.post(
            "/AjaxRequest/Index/HomeIndex.ashx",
            data={"method": "Login", "username": s["username"], "pwd": pwd},
        )
        c.cookies.set("UserId", str(r.json()["UserId"]), path="/")
        exp = (
            f"/UI/Experiment/ExperimentResult_pj.aspx?sampleId={SAMPLE_ID}"
            f"&taskId={TASK_ID}&deptCode=S125&sampleName=LJ01-260364-01"
            f"&taskStatusCode=2&RWindex=1"
        )
        c.headers["Referer"] = base + exp
        html = (await c.get(exp)).text
        for pat in ["样品名称", "sampleName", "SampleName", "回填", "材料种类"]:
            if pat in html:
                print("found in html:", pat)
        inputs = re.findall(r'id="(txt_[^"]+)"[^>]*value="([^"]*)"', html)
        for i, v in inputs:
            if v and v not in ("2026-06-03", "LJ01-260364", "/"):
                print("input", i, ":", v[:120])

        for method in [
            "GetExperimentResult",
            "GetSampleExperiment",
            "GetSamplesExperiment",
            "GetResultData",
            "GetExperimentData",
            "GetSamplesBaseType",
            "GetSampleBaseInfo",
            "GetSamplesBaseFrom",
        ]:
            for path in [
                "/AjaxRequest/Experiment/Experiment.ashx",
                "/AjaxRequest/TestingOrders/TestingOrders.ashx",
            ]:
                rr = await c.post(
                    path,
                    data={
                        "method": method,
                        "sampleId": SAMPLE_ID,
                        "taskId": TASK_ID,
                        "testingOrderId": ORDER_ID,
                    },
                )
                if (
                    rr.status_code == 200
                    and rr.text
                    and not rr.text.startswith("<!")
                    and "Object reference" not in rr.text
                ):
                    try:
                        payload = rr.json()
                        if payload and payload != [] and payload != {}:
                            print("OK", path, method, json.dumps(payload, ensure_ascii=False)[:800])
                    except Exception:
                        pass


if __name__ == "__main__":
    asyncio.run(main())
