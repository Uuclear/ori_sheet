from ring_knife.api.limis_client import LimisClient


def test_build_task_query_payload_matches_website():
    payload = LimisClient._build_task_query_payload("LJ01", page_load=2)
    assert payload["method"] == "GetTaskManagementList"
    assert payload["testingOrderNo"] == "LJ01"
    assert payload["pageLoad"] == "2"
    assert "standardcode" in payload
    assert "setlementStatus" in payload
    assert "setlementType" in payload
    assert "taskStatusCode" in payload


def test_to_task_item_real_fields():
    client = LimisClient()
    row = {
        "taskId": 1998773,
        "testingOrderId": 1262331,
        "testingOrderNo": "LJ01-260364",
        "sampleNo": "LJ01-260364-01",
        "sampleName": "LJ01-260364-01",
        "projectName": "测试工程",
        "deptName": "公路组",
        "testingTypeCode": "工程",
        "taskStatusCode": "2",
        "taskStatusName": "进行中",
        "editor": "张三",
        "remainingDay": 7,
    }
    task = client._to_task_item(row)
    assert task.testing_order_id == "1262331"
    assert task.testing_order_no == "LJ01-260364"
    assert task.task_no == "LJ01-260364-01"
    assert task.sample_name == ""
    assert task.project_name == "测试工程"
    assert task.remain_days == 7
