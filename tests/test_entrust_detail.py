from ring_knife.api.limis_client import LimisClient, _format_date, _normalize_test_nature


def test_format_contact():
    assert LimisClient._format_contact("闫海瑞", "15831211877") == "闫海瑞 15831211877"
    assert LimisClient._format_contact("", "15831211877") == "15831211877"


def test_format_date_iso():
    assert _format_date("2026/6/3 0:00:00") == "2026-06-03"
    assert _format_date("2024-03-15") == "2024-03-15"
    assert _format_date("2026/6/3") == "2026-06-03"
    assert _format_date("") == ""


def test_pick_unit_address():
    row = {"clientAddress": "江苏省无锡市江阴市", "projectAddress": "工程地址不应使用"}
    assert LimisClient._pick_unit_address(row) == "江苏省无锡市江阴市"
    assert LimisClient._pick_unit_address({"clientArea": "某区", "projectAddress": "x"}) == "某区"


def test_pick_task_row():
    tasks = [
        {"taskId": 1, "sampleNo": "A-01", "sampleId": 101},
        {"taskId": 2, "sampleNo": "A-02", "sampleId": 102},
    ]
    assert LimisClient._pick_task_row(tasks, task_id="2")["sampleNo"] == "A-02"
    assert LimisClient._pick_task_row(tasks, task_no="A-01")["taskId"] == 1
    assert LimisClient._pick_task_row([tasks[0]])["sampleNo"] == "A-01"
    assert LimisClient._pick_task_row(tasks) is None


def test_resolve_sample_name_from_row():
    assert LimisClient._resolve_sample_name_from_row({"sampleNo": "A-01", "sampleName": "A-01"}) == ""
    assert (
        LimisClient._resolve_sample_name_from_row({"sampleNo": "A-01", "sampleName": "回填土"})
        == "回填土"
    )


def test_normalize_test_nature():
    assert _normalize_test_nature("01-现场检测") == "现场检测"
    assert _normalize_test_nature("现场检测") == "现场检测"
    assert _normalize_test_nature("") == ""


def test_map_order_row_to_project():
    client = LimisClient()
    row = {
        "testingOrderNo": "LJ01-260364",
        "testingOrderUnitName": "委托单位A",
        "clientPostNo": "闫海瑞",
        "clientTel": "15831211877",
        "projectName": "测试工程",
        "clientAddress": "江苏省无锡市江阴市",
        "projectAddress": "江苏省无锡市江阴市",
        "testingOrderTime": "2026/6/3 0:00:00",
        "projectSection": "/",
        "testingTypeDesc": "01-现场检测",
    }
    project = client._map_order_row_to_project(row, report_no="LJ018-260373")
    assert project.contact == "闫海瑞 15831211877"
    assert project.project_address == "江苏省无锡市江阴市"
    assert project.test_nature == "现场检测"
    assert project.unit_address == "江苏省无锡市江阴市"
    assert project.project_section == ""
    assert project.entrust_date == "2026-06-03"
    assert project.report_no == "LJ018-260373"
