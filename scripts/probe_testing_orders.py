import asyncio
import base64
import json

import httpx

from ring_knife.settings_store import load_settings


async def post(c, path: str, data: dict) -> None:
    rr = await c.post(path, data=data)
    print("---", data)
    print("status", rr.status_code)
    text = rr.text
    if text.startswith("{") or text.startswith("["):
        try:
            print(json.dumps(rr.json(), ensure_ascii=False, indent=2)[:3000])
        except Exception:
            print(text[:500])
    else:
        print(text[:300])


async def main() -> None:
    s = load_settings()
    base = s["base_url"].rstrip("/")
    detail = (
        "/UI/Task/TaskDetailsEngineering.html?testingOrderId=1262331"
        "&sampleId=1866178&taskId=1998773"
    )
    async with httpx.AsyncClient(
        base_url=base,
        timeout=60,
        headers={
            "X-Requested-With": "XMLHttpRequest",
            "Referer": f"{base}{detail}",
            "Origin": base,
        },
        follow_redirects=True,
    ) as c:
        pwd = base64.b64encode(s["password"].encode()).decode()
        await c.get(detail)
        r = await c.post(
            "/AjaxRequest/Index/HomeIndex.ashx",
            data={"method": "Login", "username": s["username"], "pwd": pwd},
        )
        c.cookies.set("UserId", str(r.json()["UserId"]), path="/")

        tid = "1262331"
        sid = "1866178"
        taskid = "1998773"
        path = "/AjaxRequest/TestingOrders/TestingOrders.ashx"

        await post(c, path, {"method": "GetTestingOrdersBaseType", "testingOrderId": tid})
        await post(
            c,
            path,
            {
                "method": "GetTaskExcuterTestingOrder",
                "testingOrderId": tid,
                "taskId": taskid,
            },
        )
        await post(
            c,
            path,
            {
                "method": "GetSamplesTestingBasisType",
                "testingOrderId": tid,
                "sampleId": sid,
            },
        )
        await post(
            c,
            path,
            {
                "method": "GetSamplesTestingItemType_Task",
                "testingOrderId": tid,
                "sampleId": sid,
                "taskId": taskid,
            },
        )


if __name__ == "__main__":
    asyncio.run(main())
