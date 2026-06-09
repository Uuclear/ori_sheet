import asyncio
import base64
import json
import re

import httpx

from ring_knife.settings_store import load_settings


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    async with httpx.AsyncClient(
        base_url=base,
        timeout=60,
        headers={"X-Requested-With": "XMLHttpRequest"},
        follow_redirects=True,
    ) as c:
        pwd = base64.b64encode(s["password"].encode()).decode()
        r = await c.post(
            "/AjaxRequest/Index/HomeIndex.ashx",
            data={"method": "Login", "username": s["username"], "pwd": pwd},
        )
        c.cookies.set("UserId", str(r.json()["UserId"]), path="/")

        entrust = "LJ01-260364"
        tid = "1262331"
        sid = "1866178"
        taskid = "1998773"

        # integrated query variants
        for auth in ["1", "3", "4"]:
            iq = {
                "method": "GetIntegratedQueryInfo",
                "type": "4",
                "size": "5",
                "page": "1",
                "testingOrderNo": entrust,
                "authType": auth,
                "cha": "1",
            }
            rr = await c.post(
                "/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx", data=iq
            )
            print("IQ auth", auth, rr.status_code)
            try:
                d = rr.json()
            except Exception:
                print(rr.text[:200])
                continue
            if isinstance(d, list) and d:
                row = d[0]
            elif isinstance(d, dict) and d.get("rows"):
                row = d["rows"][0]
            else:
                print("  no rows", str(d)[:200])
                continue
            print("  keys", len(row.keys()))
            interesting = {
                k: v
                for k, v in row.items()
                if v not in (None, "", 0, False)
                and any(
                    x in k.lower()
                    for x in [
                        "contact",
                        "phone",
                        "link",
                        "address",
                        "nature",
                        "report",
                        "project",
                        "unit",
                        "delegate",
                        "properties",
                    ]
                )
            }
            print(json.dumps(interesting, ensure_ascii=False, indent=2))

        page = (
            f"/UI/Task/TaskDetailsEngineering.html?testingOrderId={tid}"
            f"&sampleId={sid}&taskId={taskid}"
        )
        rr = await c.get(page)
        print("DETAIL PAGE", rr.status_code, len(rr.text))
        urls = sorted(set(re.findall(r"AjaxRequest/[A-Za-z0-9_/]+\.ashx", rr.text)))
        for u in urls[:20]:
            print(" ", u)
        for pat in ["linkMan", "linkPhone", "检测性质", "reportProperties", "projectAddress"]:
            if pat in rr.text:
                print(" found text", pat)

        # try common entrust detail methods
        candidates = [
            {
                "path": "/AjaxRequest/Business/EntrustManage.ashx",
                "data": {"method": "GetEntrustDetail", "testingOrderId": tid},
            },
            {
                "path": "/AjaxRequest/Business/EntrustManage.ashx",
                "data": {"method": "GetEntrustInfo", "testingOrderId": tid},
            },
            {
                "path": "/AjaxRequest/Business/EntrustManage.ashx",
                "data": {"method": "GetEntrustById", "testingOrderId": tid},
            },
            {
                "path": "/AjaxRequest/Business/SampleManage.ashx",
                "data": {"method": "GetSampleInfo", "sampleId": sid},
            },
            {
                "path": "/AjaxRequest/Task/Task.ashx",
                "data": {"method": "GetTaskDetail", "taskId": taskid},
            },
        ]
        for item in candidates:
            rr = await c.post(item["path"], data=item["data"])
            print(item["data"]["method"], rr.status_code, rr.text[:300].replace("\n", " "))


async def probe_detail_page() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    async with httpx.AsyncClient(
        base_url=base,
        timeout=60,
        headers={"X-Requested-With": "XMLHttpRequest"},
        follow_redirects=True,
    ) as c:
        pwd = base64.b64encode(s["password"].encode()).decode()
        r = await c.post(
            "/AjaxRequest/Index/HomeIndex.ashx",
            data={"method": "Login", "username": s["username"], "pwd": pwd},
        )
        c.cookies.set("UserId", str(r.json()["UserId"]), path="/")
        page = (
            "/UI/Task/TaskDetailsEngineering.html?testingOrderId=1262331"
            "&sampleId=1866178&taskId=1998773"
        )
        rr = await c.get(page)
        text = rr.text
        methods = sorted(set(re.findall(r"method[\"'\s]*[:=]\s*[\"']([A-Za-z0-9_]+)", text)))
        print("DETAIL methods", methods)
        for line in text.splitlines():
            if any(
                k in line
                for k in [
                    "TestingOrders",
                    "linkMan",
                    "linkPhone",
                    "reportProperties",
                    "检测性质",
                    "projectAddress",
                ]
            ):
                print(line.strip()[:220])

        order_methods = [
            "GetTestingOrderInfo",
            "GetTestingOrderDetail",
            "GetTestingOrdersInfo",
            "GetOrderInfo",
            "GetTestingOrderById",
            "GetTestingOrder",
        ]
        for method in order_methods:
            for key in ["testingOrderId", "testingOrderNo"]:
                val = "1262331" if key == "testingOrderId" else "LJ01-260364"
                rr = await c.post(
                    "/AjaxRequest/TestingOrders/TestingOrders.ashx",
                    data={"method": method, key: val},
                )
                if rr.status_code == 200 and "Object reference" not in rr.text and "无法找到" not in rr.text:
                    print(method, key, "=>", rr.text[:500])


if __name__ == "__main__":
    asyncio.run(probe_detail_page())
