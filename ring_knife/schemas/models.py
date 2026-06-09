from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field


class ProjectInfo(BaseModel):
    entrust_no: str = ""
    report_no: str = ""
    entrust_unit: str = ""
    contact: str = ""
    supervision_unit: str = ""
    construction_unit: str = ""
    project_name: str = ""
    unit_address: str = ""
    project_address: str = ""
    entrust_date: str = ""
    project_section: str = ""
    report_date: str = ""
    test_nature: str = ""


class RecordParams(BaseModel):
    soil_type: str = ""
    max_dry_density: float | None = None
    compaction_method: str = ""
    optimal_moisture: float | None = None
    standards: list[str] = Field(default_factory=list)
    ring_spec: str = "200cm³"
    design_requirement: float | None = None
    sample_name: str = "回填土"
    material_type: str = ""
    test_basis: str = "JTG 3450-2019"
    judge_basis: str = "JTG 3450-2019"
    result_type: Literal["compaction_coeff", "compaction_percent"] = "compaction_coeff"
    record_template: Literal["group2", "group3"] = "group2"
    equipment_balance: list[str] = Field(default_factory=list)
    equipment_oven: list[str] = Field(default_factory=list)
    test_location: str = ""
    remark: str = ""
    witness_unit: str = ""
    witness_person: str = ""
    sampling_unit: str = ""
    sampling_person: str = ""


class AluminumBox(BaseModel):
    box_no: str = ""
    box_mass: float | None = None
    wet_sample_mass: float | None = None
    dry_sample_mass: float | None = None


class RingMeasurement(BaseModel):
    ring_sample_mass: float | None = None
    ring_mass: float | None = None
    ring_volume: float | None = None
    boxes: list[AluminumBox] = Field(default_factory=lambda: [AluminumBox(), AluminumBox()])


class RingKnifeSample(BaseModel):
    sample_no: str = ""
    elevation: str = ""
    sampling_date: str = ""
    test_date: str = ""
    thickness: str = ""
    ring_sample_mass: float | None = None
    ring_mass: float | None = None
    ring_volume: float | None = None
    boxes: list[AluminumBox] = Field(default_factory=lambda: [AluminumBox(), AluminumBox()])
    rings: list[RingMeasurement] = Field(default_factory=list)


class RingPointResult(BaseModel):
    wet_mass: float | None = None
    wet_density: float | None = None
    moisture_rates: list[float | None] = Field(default_factory=list)
    avg_moisture: float | None = None
    dry_density: float | None = None


class SamplePointResult(BaseModel):
    sample_no: str = ""
    elevation: str = ""
    thickness: str = ""
    sampling_date: str = ""
    test_date: str = ""
    wet_mass: float | None = None
    wet_density: float | None = None
    avg_wet_density: float | None = None
    moisture_rates: list[float | None] = Field(default_factory=list)
    avg_moisture: float | None = None
    dry_density: float | None = None
    avg_dry_density: float | None = None
    compaction_coeff: float | None = None
    compaction_percent: float | None = None
    conclusion: str = ""
    rings: list[RingPointResult] = Field(default_factory=list)


class CalcRequest(BaseModel):
    params: RecordParams
    samples: list[RingKnifeSample]


class CalcResponse(BaseModel):
    results: list[SamplePointResult]
    overall_conclusion: str = ""


class ReportRequest(BaseModel):
    project: ProjectInfo
    params: RecordParams
    samples: list[RingKnifeSample]


class DraftSaveRequest(BaseModel):
    project: ProjectInfo
    params: RecordParams
    samples: list[RingKnifeSample] = Field(default_factory=list)
    calc_results: list[SamplePointResult] = Field(default_factory=list)
    overall_conclusion: str = ""
    sample_no_prefix: str = ""


class DraftSaveResponse(BaseModel):
    success: bool
    message: str
    updated_at: str = ""


class DraftLoadResponse(BaseModel):
    success: bool
    message: str = ""
    draft: DraftSaveRequest | None = None
    updated_at: str = ""


class LimisLoginRequest(BaseModel):
    username: str | None = None
    password: str | None = None


class LimisLoginResponse(BaseModel):
    success: bool
    message: str
    user_id: str | None = None


class LimisEntrustResponse(BaseModel):
    success: bool
    message: str
    project: ProjectInfo | None = None
    sample_no: str = ""
    sample_name: str = ""


class AppSettings(BaseModel):
    base_url: str = "http://10.1.228.22"
    username: str = ""
    password: str = ""


class AppSettingsResponse(BaseModel):
    base_url: str
    username: str
    password_set: bool


class AppSettingsSaveRequest(BaseModel):
    base_url: str
    username: str
    password: str = ""


class TaskItem(BaseModel):
    task_id: str = ""
    testing_order_id: str = ""
    task_no: str = ""
    testing_order_no: str = ""
    sample_name: str = ""
    project_name: str = ""
    principal_part: str = ""
    testing_type: str = ""
    status_code: str = ""
    status_name: str = ""
    executor: str = ""
    test_items: str = ""
    remain_days: int | None = None


class TaskListRequest(BaseModel):
    entrust_no: str = ""
    base_url: str | None = None
    username: str | None = None
    password: str | None = None


class TaskListResponse(BaseModel):
    success: bool
    message: str
    tasks: list[TaskItem] = Field(default_factory=list)
    query_keyword: str = ""
