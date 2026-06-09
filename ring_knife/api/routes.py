from __future__ import annotations

from datetime import date
from urllib.parse import quote

from fastapi import APIRouter, HTTPException, Query
from fastapi.responses import Response

from ring_knife.api.limis_client import limis_client
from ring_knife.calc.ring_knife import calculate_all
from ring_knife.report.docx_generator import generate_report
from ring_knife.schemas.models import (
    AppSettingsResponse,
    AppSettingsSaveRequest,
    CalcRequest,
    CalcResponse,
    DraftLoadResponse,
    DraftSaveRequest,
    DraftSaveResponse,
    LimisLoginRequest,
    LimisLoginResponse,
    LimisEntrustResponse,
    ReportRequest,
    TaskListRequest,
    TaskListResponse,
)
from ring_knife.data_store import delete_draft, load_draft, save_draft
from ring_knife.settings_store import load_settings, save_settings

router = APIRouter(prefix="/api")


@router.get("/settings", response_model=AppSettingsResponse)
async def get_settings() -> AppSettingsResponse:
    stored = load_settings()
    return AppSettingsResponse(
        base_url=stored["base_url"],
        username=stored["username"],
        password_set=bool(stored["password"]),
    )


@router.post("/settings", response_model=AppSettingsResponse)
async def post_settings(request: AppSettingsSaveRequest) -> AppSettingsResponse:
    stored = save_settings(request.base_url, request.username, request.password or None)
    limis_client.configure(stored["base_url"], stored["username"], stored["password"])
    limis_client.user_id = None
    return AppSettingsResponse(
        base_url=stored["base_url"],
        username=stored["username"],
        password_set=bool(stored["password"]),
    )


@router.post("/calc", response_model=CalcResponse)
async def calc_endpoint(request: CalcRequest) -> CalcResponse:
    return calculate_all(request)


@router.post("/limis/login", response_model=LimisLoginResponse)
async def limis_login(request: LimisLoginRequest) -> LimisLoginResponse:
    result = await limis_client.login(request.username, request.password)
    return LimisLoginResponse(
        success=result.get("success", False),
        message=result.get("message", ""),
        user_id=result.get("user_id"),
    )


@router.get("/limis/entrust/{entrust_no}", response_model=LimisEntrustResponse)
async def limis_entrust(
    entrust_no: str,
    order_id: str = Query("", description="委托单ID，来自任务列表可加快查询"),
    task_id: str = Query("", description="任务ID，用于定位样品并查询报告编号"),
    task_no: str = Query("", description="样品编号，用于匹配任务"),
    sample_id: str = Query("", description="样品ID，可加快 GetReport 查询"),
) -> LimisEntrustResponse:
    result = await limis_client.get_entrust_by_no(
        entrust_no,
        testing_order_id=order_id or None,
        task_id=task_id or None,
        task_no=task_no or None,
        sample_id=sample_id or None,
    )
    return LimisEntrustResponse(
        success=result.get("success", False),
        message=result.get("message", ""),
        project=result.get("project"),
        sample_no=result.get("sample_no") or "",
        sample_name=result.get("sample_name") or "",
    )


@router.post("/limis/tasks", response_model=TaskListResponse)
async def limis_tasks_post(request: TaskListRequest) -> TaskListResponse:
    result = await limis_client.search_tasks_by_entrust(
        entrust_keyword=request.entrust_no,
        base_url=request.base_url,
        username=request.username,
        password=request.password,
    )
    return TaskListResponse(
        success=result.get("success", False),
        message=result.get("message", ""),
        tasks=result.get("tasks") or [],
        query_keyword=result.get("query_keyword") or "",
    )


@router.get("/limis/tasks", response_model=TaskListResponse)
async def limis_tasks_get(
    entrust_no: str = Query("", description="委托单编号，支持模糊匹配"),
) -> TaskListResponse:
    result = await limis_client.search_tasks_by_entrust(entrust_keyword=entrust_no)
    return TaskListResponse(
        success=result.get("success", False),
        message=result.get("message", ""),
        tasks=result.get("tasks") or [],
        query_keyword=result.get("query_keyword") or "",
    )


@router.get("/drafts/{entrust_no}", response_model=DraftLoadResponse)
async def get_draft(entrust_no: str) -> DraftLoadResponse:
    data = load_draft(entrust_no)
    if not data:
        return DraftLoadResponse(success=False, message="未找到本地草稿")
    updated_at = str(data.pop("updated_at", ""))
    try:
        draft = DraftSaveRequest.model_validate(data)
    except Exception as exc:
        return DraftLoadResponse(success=False, message=f"草稿数据无效: {exc}")
    return DraftLoadResponse(success=True, draft=draft, updated_at=updated_at)


@router.put("/drafts/{entrust_no}", response_model=DraftSaveResponse)
async def put_draft(entrust_no: str, request: DraftSaveRequest) -> DraftSaveResponse:
    key = entrust_no.strip() or request.project.entrust_no.strip()
    if not key:
        raise HTTPException(status_code=400, detail="委托编号不能为空")
    payload = request.model_dump()
    try:
        updated_at = save_draft(key, payload)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    return DraftSaveResponse(success=True, message="已保存", updated_at=updated_at)


@router.delete("/drafts/{entrust_no}")
async def remove_draft(entrust_no: str) -> dict[str, bool]:
    return {"success": delete_draft(entrust_no)}


@router.post("/report/generate")
async def report_generate(request: ReportRequest) -> Response:
    try:
        content = generate_report(request)
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"报告生成失败: {exc}") from exc

    entrust = request.project.entrust_no or "report"
    today = date.today().isoformat()
    filename_utf8 = f"报告_{entrust}_{today}.docx"
    filename_ascii = f"report_{entrust}_{today}.docx"
    disposition = (
        f'attachment; filename="{filename_ascii}"; '
        f"filename*=UTF-8''{quote(filename_utf8)}"
    )
    return Response(
        content=content,
        media_type="application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        headers={"Content-Disposition": disposition},
    )
