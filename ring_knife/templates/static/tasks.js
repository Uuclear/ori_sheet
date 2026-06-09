renderNav("tasks");

function statusBadge(code, name) {
  const colors = {
    "0": "bg-slate-200 text-slate-700",
    "1": "bg-blue-100 text-blue-800",
    "3": "bg-green-100 text-green-800",
    "5": "bg-yellow-100 text-yellow-800",
    "9": "bg-red-100 text-red-800",
  };
  const cls = colors[code] || "bg-slate-100 text-slate-600";
  return `<span class="px-2 py-0.5 rounded text-xs ${cls}">${name || code || "-"}</span>`;
}

function openRecord(task) {
  const params = new URLSearchParams();
  if (task.testing_order_no) params.set("entrust_no", task.testing_order_no);
  if (task.testing_order_id) params.set("order_id", task.testing_order_id);
  if (task.task_id) params.set("task_id", task.task_id);
  if (task.task_no) params.set("task_no", task.task_no);
  window.location.href = `/record?${params.toString()}`;
}

function renderTasks(tasks) {
  const tbody = document.getElementById("task-tbody");
  if (!tasks.length) {
    tbody.innerHTML = '<tr><td colspan="10" class="text-slate-400 py-8">未找到匹配任务，请尝试更短的编号关键词</td></tr>';
    return;
  }
  tbody.innerHTML = tasks
    .map(
      (t, i) => `
    <tr class="hover:bg-slate-50">
      <td class="font-mono text-xs">${t.task_no || "-"}</td>
      <td class="font-mono text-xs">${t.testing_order_no || "-"}</td>
      <td>${t.sample_name || t.task_no || "-"}</td>
      <td class="text-left max-w-[180px] truncate" title="${t.project_name || ""}">${t.project_name || "-"}</td>
      <td class="text-left max-w-[160px] truncate" title="${t.test_items || ""}">${t.test_items || "-"}</td>
      <td>${t.principal_part || "-"}</td>
      <td>${statusBadge(t.status_code, t.status_name)}</td>
      <td>${t.executor || "-"}</td>
      <td>${t.remain_days ?? "-"}</td>
      <td>
        <button data-idx="${i}" class="open-record bg-blue-600 hover:bg-blue-700 text-white px-3 py-1 rounded text-xs">填写记录</button>
      </td>
    </tr>`
    )
    .join("");

  const taskCache = tasks;
  tbody.querySelectorAll(".open-record").forEach((btn) => {
    btn.addEventListener("click", () => {
      const idx = parseInt(btn.dataset.idx, 10);
      openRecord(taskCache[idx]);
    });
  });
}

async function queryTasks() {
  const keyword = document.getElementById("entrust_keyword").value.trim();
  const status = document.getElementById("query-status");
  const btn = document.getElementById("btn-query-tasks");

  if (!keyword) {
    toast("请输入委托单编号关键词", "error");
    return;
  }

  btn.disabled = true;
  status.textContent = "查询中...";
  try {
    const resp = await fetch(
      `${API}/api/limis/tasks?entrust_no=${encodeURIComponent(keyword)}`
    );
    const data = await resp.json();
    if (!data.success) {
      toast(data.message || "查询失败", "error");
      status.textContent = data.message || "查询失败";
      renderTasks([]);
      return;
    }
    renderTasks(data.tasks);
    status.textContent = `${data.message}（关键词: ${data.query_keyword || keyword}）`;
    if (data.tasks.length) {
      toast(`找到 ${data.tasks.length} 条任务`, "success");
    } else {
      toast(data.message || "未找到任务", "info");
    }
  } catch (e) {
    toast(`请求失败: ${e.message}`, "error");
    status.textContent = "请求失败";
  } finally {
    btn.disabled = false;
  }
}

document.getElementById("btn-query-tasks").addEventListener("click", queryTasks);
document.getElementById("entrust_keyword").addEventListener("keydown", (e) => {
  if (e.key === "Enter") queryTasks();
});
