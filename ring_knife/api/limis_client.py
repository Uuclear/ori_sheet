from __future__ import annotations

import base64
import re
from datetime import datetime
from typing import Any

import httpx

from ring_knife.schemas.models import ProjectInfo, TaskItem
from ring_knife.settings_store import load_settings

TASK_PAGE_PATH = "/UI/Task/TaskManagement.html"
TESTING_ORDERS_PATH = "/AjaxRequest/TestingOrders/TestingOrders.ashx"
NONSTANDARD_PATH = "/AjaxRequest/Task/NonStandard.ashx"
NONSTANDARD_PAGE_PATH = "/UI/Task/NonStandardReport.aspx"
MAX_TASK_RESULTS = 500


class LimisClient:
    def __init__(self) -> None:
        self._client: httpx.AsyncClient | None = None
        self.user_id: str | None = None
        self._base_url: str | None = None
        self._username: str | None = None
        self._password: str | None = None
        self._task_session_ready = False

    def configure(
        self,
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> None:
        if base_url:
            self._base_url = base_url.rstrip("/")
        if username is not None:
            self._username = username
        if password is not None and password != "":
            self._password = password

    def _resolve_credentials(
        self,
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> tuple[str, str, str]:
        stored = load_settings()
        url = (base_url or self._base_url or stored["base_url"]).rstrip("/")
        user = username or self._username or stored["username"]
        pwd = password if password not in (None, "") else (self._password or stored["password"])
        return url, user, pwd

    async def _get_client(self, base_url: str) -> httpx.AsyncClient:
        if self._client is None or self._client.is_closed or self._base_url != base_url:
            if self._client and not self._client.is_closed:
                await self._client.aclose()
            self._base_url = base_url
            self._client = httpx.AsyncClient(
                base_url=base_url,
                timeout=120.0,
                headers={
                    "X-Requested-With": "XMLHttpRequest",
                    "Referer": f"{base_url}{TASK_PAGE_PATH}",
                    "Origin": base_url,
                },
                follow_redirects=True,
            )
            self._task_session_ready = False
            if self.user_id:
                self._client.cookies.set("UserId", self.user_id, path="/")
        return self._client

    async def close(self) -> None:
        if self._client and not self._client.is_closed:
            await self._client.aclose()
        self._client = None
        self.user_id = None
        self._task_session_ready = False

    async def login(
        self,
        username: str | None = None,
        password: str | None = None,
        base_url: str | None = None,
    ) -> dict[str, Any]:
        url, user, pwd = self._resolve_credentials(base_url, username, password)
        if not user or not pwd:
            return {"success": False, "message": "未配置 LIMIS 用户名或密码，请在设置页填写"}

        self.configure(url, user, pwd)
        client = await self._get_client(url)
        encoded_pwd = base64.b64encode(pwd.encode("utf-8")).decode("ascii")
        try:
            resp = await client.post(
                "/AjaxRequest/Index/HomeIndex.ashx",
                data={"method": "Login", "username": user, "pwd": encoded_pwd},
            )
            resp.raise_for_status()
            data = resp.json()
        except httpx.HTTPError as exc:
            return {"success": False, "message": f"登录请求失败: {exc}"}
        except ValueError:
            return {"success": False, "message": "登录响应解析失败"}

        if str(data.get("state")) != "1":
            return {"success": False, "message": data.get("msg", "登录失败")}

        self.user_id = str(data.get("UserId", ""))
        if self.user_id:
            client.cookies.set("UserId", self.user_id, path="/")
        self._task_session_ready = False

        return {
            "success": True,
            "message": data.get("msg", "登录成功"),
            "user_id": self.user_id,
            "real_name": data.get("RealName", ""),
        }

    async def _ensure_login(
        self,
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> dict[str, Any]:
        if not self.user_id:
            return await self.login(username, password, base_url)
        return {"success": True}

    async def _warm_task_session(self, base_url: str) -> None:
        if self._task_session_ready:
            return
        client = await self._get_client(base_url)
        await client.get(TASK_PAGE_PATH)
        self._task_session_ready = True

    async def _warm_detail_session(self, base_url: str, testing_order_id: str) -> None:
        client = await self._get_client(base_url)
        detail_url = (
            f"/UI/Task/TaskDetailsEngineering.html?testingOrderId={testing_order_id}"
        )
        client.headers["Referer"] = f"{base_url}{detail_url}"
        await client.get(detail_url)

    async def _warm_report_session(
        self,
        base_url: str,
        testing_order_id: str,
        sample_id: str,
        task_id: str | None = None,
    ) -> None:
        client = await self._get_client(base_url)
        page = (
            f"{NONSTANDARD_PAGE_PATH}?testingOrderId={testing_order_id}"
            f"&sampleId={sample_id}"
        )
        if task_id:
            page += f"&taskId={task_id}"
        client.headers["Referer"] = f"{base_url}{page}"
        await client.get(page)

    @staticmethod
    def _build_task_query_payload(
        testing_order_no: str = "",
        sample_no: str = "",
        page_load: int = 2,
    ) -> dict[str, str]:
        # 与官网 TaskManagement.html -> queryParamsInfo() 保持一致
        return {
            "method": "GetTaskManagementList",
            "testingOrderNo": testing_order_no,
            "sampleNo": sample_no,
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
            "pageLoad": str(page_load),
        }

    async def _fetch_testing_order_base(
        self,
        testing_order_id: str,
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> dict[str, Any] | None:
        url, _, _ = self._resolve_credentials(base_url, username, password)
        await self._warm_detail_session(url, testing_order_id)
        client = await self._get_client(url)
        resp = await client.post(
            TESTING_ORDERS_PATH,
            data={
                "method": "GetTestingOrdersBaseType",
                "testingOrderId": testing_order_id,
            },
        )
        if resp.status_code >= 400:
            detail = _extract_error_text(resp.text)
            raise httpx.HTTPStatusError(
                f"委托详情接口返回 {resp.status_code}: {detail}",
                request=resp.request,
                response=resp,
            )
        data = resp.json()
        if isinstance(data, list) and data:
            return data[0]
        if isinstance(data, dict):
            rows = data.get("rows") or data.get("data") or []
            if rows:
                return rows[0]
        return None

    @staticmethod
    def _format_contact(person: Any, phone: Any) -> str:
        name = str(person or "").strip()
        tel = str(phone or "").strip()
        if name and tel:
            return f"{name} {tel}"
        return name or tel

    @staticmethod
    def _pick_unit_address(row: dict[str, Any]) -> str:
        # LIMIS 委托单「单位地址」对应 clientAddress（与委托单 HTML 一致，非 projectAddress）
        for key in ("clientAddress", "clientArea"):
            val = str(row.get(key) or "").strip()
            if val:
                return val
        return ""

    @staticmethod
    def _pick_task_row(
        tasks: list[dict[str, Any]],
        task_id: str | None = None,
        task_no: str | None = None,
    ) -> dict[str, Any] | None:
        if task_id:
            for row in tasks:
                if str(row.get("taskId") or "") == str(task_id):
                    return row
        if task_no and str(task_no).strip():
            needle = str(task_no).strip()
            for row in tasks:
                if str(row.get("sampleNo") or "") == needle:
                    return row
                if str(row.get("taskName") or "") == needle:
                    return row
        if len(tasks) == 1:
            return tasks[0]
        return None

    async def _fetch_report_info(
        self,
        testing_order_id: str,
        sample_id: str,
        task_id: str | None = None,
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> dict[str, Any] | None:
        url, _, _ = self._resolve_credentials(base_url, username, password)
        await self._warm_report_session(url, testing_order_id, sample_id, task_id)
        client = await self._get_client(url)
        payload: dict[str, str] = {
            "method": "GetReport",
            "testingOrderId": testing_order_id,
            "sampleId": sample_id,
        }
        if task_id:
            payload["taskId"] = task_id
        resp = await client.post(NONSTANDARD_PATH, data=payload)
        if resp.status_code >= 400:
            return None
        try:
            data = resp.json()
        except ValueError:
            return None
        if isinstance(data, list) and data:
            row = data[0]
            return row if isinstance(row, dict) else None
        if isinstance(data, dict):
            rows = data.get("rows") or data.get("data") or []
            if rows and isinstance(rows[0], dict):
                return rows[0]
        return None

    @staticmethod
    def _resolve_sample_name_from_row(row: dict[str, Any]) -> str:
        """环刀法任务在 LIMIS 中常无独立样品名称，sampleName 与 sampleNo 相同时不采用。"""
        sample_no = str(row.get("sampleNo") or "").strip()
        for key in ("sampleName", "SampleName", "sampleDesc", "manufacturer", "typeSpecification"):
            val = str(row.get(key) or "").strip()
            if val and val != sample_no:
                return val
        return ""

    def _map_order_row_to_project(
        self,
        row: dict[str, Any],
        entrust_no: str = "",
        report_no: str = "",
    ) -> ProjectInfo:
        section = str(row.get("projectSection") or "").strip()
        if section == "/":
            section = ""
        return ProjectInfo(
            entrust_no=str(row.get("testingOrderNo") or entrust_no),
            report_no=report_no,
            entrust_unit=str(row.get("testingOrderUnitName") or ""),
            contact=self._format_contact(row.get("clientPostNo"), row.get("clientTel")),
            supervision_unit=str(
                row.get("supervisorUnitName")
                or row.get("supervisionUnitName")
                or row.get("jlUnitName")
                or ""
            ),
            construction_unit=str(
                row.get("constructionUnitName")
                or row.get("buildUnitName")
                or row.get("sgUnitName")
                or ""
            ),
            project_name=str(row.get("projectName") or ""),
            unit_address=self._pick_unit_address(row),
            project_address=str(row.get("projectAddress") or ""),
            entrust_date=_format_date(row.get("testingOrderTime")),
            project_section=section,
            report_date=_format_date(row.get("reportDate")),
            test_nature=_normalize_test_nature(
                row.get("testingTypeDesc") or row.get("testingTypeName") or ""
            ),
        )

    async def _resolve_testing_order_id(
        self,
        entrust_no: str,
        testing_order_id: str | None = None,
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> str | None:
        if testing_order_id:
            return str(testing_order_id)
        rows = await self._fetch_task_list(
            testing_order_no=entrust_no,
            base_url=base_url,
            username=username,
            password=password,
        )
        if not rows:
            return None
        for row in rows:
            no = str(row.get("testingOrderNo") or "")
            if no == entrust_no or entrust_no in no:
                order_id = row.get("testingOrderId")
                if order_id is not None:
                    return str(order_id)
        order_id = rows[0].get("testingOrderId")
        return str(order_id) if order_id is not None else None

    async def get_entrust_by_no(
        self,
        entrust_no: str,
        testing_order_id: str | None = None,
        task_id: str | None = None,
        task_no: str | None = None,
        sample_id: str | None = None,
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> dict[str, Any]:
        login_result = await self._ensure_login(base_url, username, password)
        if not login_result.get("success"):
            return {
                "success": False,
                "message": login_result.get("message", "登录失败"),
                "project": None,
                "sample_no": "",
                "sample_name": "",
            }

        try:
            order_id = await self._resolve_testing_order_id(
                entrust_no,
                testing_order_id=testing_order_id,
                base_url=base_url,
                username=username,
                password=password,
            )
            if not order_id:
                return {
                    "success": False,
                    "message": f"未找到委托编号: {entrust_no}",
                    "project": None,
                    "sample_no": "",
                    "sample_name": "",
                }

            row = await self._fetch_testing_order_base(
                order_id,
                base_url=base_url,
                username=username,
                password=password,
            )
            if not row:
                return {
                    "success": False,
                    "message": f"未获取到委托详情: {entrust_no}",
                    "project": None,
                    "sample_no": "",
                    "sample_name": "",
                }

            task_rows = await self._fetch_task_list(
                testing_order_no=entrust_no,
                base_url=base_url,
                username=username,
                password=password,
            )
            task_row = self._pick_task_row(task_rows, task_id, task_no)

            resolved_sample_id = sample_id or (
                str(task_row.get("sampleId")) if task_row and task_row.get("sampleId") else ""
            )
            resolved_task_id = task_id or (
                str(task_row.get("taskId")) if task_row and task_row.get("taskId") else None
            )

            report_no = ""
            sample_no = ""
            sample_name = ""
            report_date_override = ""

            if resolved_sample_id:
                report_row = await self._fetch_report_info(
                    order_id,
                    resolved_sample_id,
                    task_id=resolved_task_id,
                    base_url=base_url,
                    username=username,
                    password=password,
                )
                if report_row:
                    report_no = str(report_row.get("testingReportCode") or "").strip()
                    sample_no = str(report_row.get("sampleNo") or "").strip()
                    report_date_override = _format_date(report_row.get("reportDate"))

            if not sample_no and task_row:
                sample_no = str(task_row.get("sampleNo") or "").strip()
            if task_row:
                sample_name = self._resolve_sample_name_from_row(task_row)

            project = self._map_order_row_to_project(row, entrust_no, report_no=report_no)
            if report_date_override:
                project.report_date = report_date_override

            return {
                "success": True,
                "message": "查询成功",
                "project": project,
                "sample_no": sample_no,
                "sample_name": sample_name,
            }
        except httpx.HTTPError as exc:
            return {
                "success": False,
                "message": f"查询请求失败: {exc}",
                "project": None,
                "sample_no": "",
                "sample_name": "",
            }
        except ValueError:
            return {
                "success": False,
                "message": "查询响应解析失败",
                "project": None,
                "sample_no": "",
                "sample_name": "",
            }

    async def _fetch_task_list(
        self,
        testing_order_no: str = "",
        sample_no: str = "",
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> list[dict[str, Any]]:
        url, _, _ = self._resolve_credentials(base_url, username, password)
        await self._warm_task_session(url)
        client = await self._get_client(url)
        payload = self._build_task_query_payload(
            testing_order_no=testing_order_no,
            sample_no=sample_no,
            page_load=2 if testing_order_no or sample_no else 1,
        )
        resp = await client.post("/AjaxRequest/Task/Task.ashx", data=payload)
        if resp.status_code >= 400:
            detail = _extract_error_text(resp.text)
            raise httpx.HTTPStatusError(
                f"任务接口返回 {resp.status_code}: {detail}",
                request=resp.request,
                response=resp,
            )
        data = resp.json()
        if isinstance(data, list):
            return data
        if isinstance(data, dict):
            if data.get("msg"):
                return []
            return data.get("rows") or data.get("data") or []
        return []

    def _to_task_item(self, row: dict[str, Any]) -> TaskItem:
        return TaskItem(
            task_id=str(row.get("taskId") or row.get("TaskId") or ""),
            testing_order_id=str(row.get("testingOrderId") or row.get("TestingOrderId") or ""),
            task_no=str(row.get("sampleNo") or row.get("taskName") or row.get("TaskNo") or ""),
            testing_order_no=str(row.get("testingOrderNo") or row.get("TestingOrderNo") or ""),
            sample_name=self._resolve_sample_name_from_row(row),
            project_name=str(row.get("projectName") or ""),
            principal_part=str(row.get("deptName") or row.get("PrincipalPartName") or ""),
            testing_type=str(row.get("testingTypeCode") or ""),
            status_code=str(row.get("taskStatusCode") or row.get("TaskExecutiveCode") or ""),
            status_name=str(row.get("taskStatusName") or row.get("TaskExecutiveName") or ""),
            executor=str(row.get("editor") or row.get("TaskExecutor") or ""),
            test_items=str(row.get("taskName") or row.get("sampleNo") or ""),
            remain_days=row.get("remainingDay") if row.get("remainingDay") is not None else row.get("RemainDays"),
        )

    @staticmethod
    def _dedupe_tasks(tasks: list[TaskItem]) -> list[TaskItem]:
        seen: set[str] = set()
        unique: list[TaskItem] = []
        for task in tasks:
            key = task.task_id or f"{task.testing_order_no}|{task.task_no}|{task.sample_name}"
            if key in seen:
                continue
            seen.add(key)
            unique.append(task)
        return unique

    async def search_tasks_by_entrust(
        self,
        entrust_keyword: str,
        base_url: str | None = None,
        username: str | None = None,
        password: str | None = None,
    ) -> dict[str, Any]:
        keyword = entrust_keyword.strip()
        if not keyword:
            return {
                "success": False,
                "message": "请输入委托单编号关键词",
                "tasks": [],
                "query_keyword": "",
            }

        login_result = await self._ensure_login(base_url, username, password)
        if not login_result.get("success"):
            return {
                "success": False,
                "message": login_result.get("message", "登录失败"),
                "tasks": [],
                "query_keyword": keyword,
            }

        try:
            task_rows = await self._fetch_task_list(
                testing_order_no=keyword,
                base_url=base_url,
                username=username,
                password=password,
            )
            tasks = self._dedupe_tasks([self._to_task_item(row) for row in task_rows])
            tasks.sort(key=lambda t: (t.testing_order_no, t.task_no))

            if not tasks:
                return {
                    "success": True,
                    "message": f"未找到委托编号包含「{keyword}」的任务",
                    "tasks": [],
                    "query_keyword": keyword,
                }

            total = len(tasks)
            truncated = total > MAX_TASK_RESULTS
            if truncated:
                tasks = tasks[:MAX_TASK_RESULTS]

            message = f"共找到 {total} 条任务"
            if truncated:
                message += f"，显示前 {MAX_TASK_RESULTS} 条，请缩小关键词"

            return {
                "success": True,
                "message": message,
                "tasks": tasks,
                "query_keyword": keyword,
            }
        except httpx.HTTPError as exc:
            return {
                "success": False,
                "message": f"任务查询失败: {exc}",
                "tasks": [],
                "query_keyword": keyword,
            }
        except ValueError:
            return {
                "success": False,
                "message": "任务响应解析失败",
                "tasks": [],
                "query_keyword": keyword,
            }


def _normalize_test_nature(value: Any) -> str:
    text = str(value or "").strip()
    if not text:
        return ""
    return re.sub(r"^\d+-", "", text).strip() or text


def _format_date(value: Any) -> str:
    if value is None:
        return ""
    text = str(value).strip()
    if not text:
        return ""
    date_part = text.split("T")[0].split(" ")[0]
    for fmt in ("%Y-%m-%d", "%Y/%m/%d", "%Y.%m.%d"):
        try:
            return datetime.strptime(date_part, fmt).date().isoformat()
        except ValueError:
            continue
    normalized = date_part.replace("/", "-").replace(".", "-")
    parts = normalized.split("-")
    if len(parts) >= 3:
        try:
            y, m, d = int(parts[0]), int(parts[1]), int(parts[2])
            return datetime(y, m, d).date().isoformat()
        except ValueError:
            pass
    return date_part


def _extract_error_text(text: str) -> str:
    if "Object reference not set" in text:
        return "服务端参数不完整（NullReferenceException）"
    if "<title>" in text:
        start = text.find("<title>") + 7
        end = text.find("</title>", start)
        if end > start:
            return text[start:end].strip()
    return text[:200]


limis_client = LimisClient()
