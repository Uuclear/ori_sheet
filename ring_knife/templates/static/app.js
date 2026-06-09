let blockCount = 0;
let calcResults = [];
let lastRecordTemplate = "group2";
let draftSaveTimer = null;
let draftLoadTimer = null;
let isApplyingDraft = false;
let overallConclusion = "";

renderNav("record");

function numVal(id) {
  const v = document.getElementById(id)?.value;
  if (v === "" || v == null) return null;
  const n = parseFloat(v);
  return Number.isFinite(n) ? n : null;
}

function strVal(id) {
  return document.getElementById(id)?.value?.trim() || "";
}

function normalizeTestNature(val) {
  const text = (val || "").trim();
  return text.replace(/^\d+-/, "").trim() || text;
}

const RING_VOLUME = 200;
let sampleSeq = 0;
let sampleNoPrefix = "";

function getRecordTemplate() {
  return document.getElementById("record-template")?.value === "group3" ? "group3" : "group2";
}

function ringsPerBlock() {
  // 2个1组 = 2 环刀 + 4 铝盒；3个1组 = 3 环刀 + 6 铝盒（每环刀 2 铝盒）
  return getRecordTemplate() === "group3" ? 3 : 2;
}

function rowsPerBlock() {
  return ringsPerBlock() * 2;
}

function getResultType() {
  const el = document.querySelector('input[name="result_type"]:checked');
  return el?.value === "compaction_percent" ? "compaction_percent" : "compaction_coeff";
}

function updateCompactionHeaders() {
  const isPercent = getResultType() === "compaction_percent";
  const label = isPercent ? "压实度%" : "压实系数";
  const recordHeader = document.getElementById("record-compaction-header");
  const resultHeader = document.getElementById("result-compaction-header");
  if (recordHeader) recordHeader.textContent = label;
  if (resultHeader) resultHeader.textContent = label;
}

function formatCompaction(result) {
  if (!result) return "";
  if (getResultType() === "compaction_percent") {
    return result.compaction_percent != null ? `${result.compaction_percent}` : "";
  }
  return result.compaction_coeff != null ? `${result.compaction_coeff}` : "";
}

function parseSampleNoParts(no) {
  const text = (no || "").trim();
  const match = text.match(/^(.*)-(\d+)$/);
  if (!match) return { prefix: "", suffix: null };
  return { prefix: match[1], suffix: parseInt(match[2], 10) };
}

function setSampleNoPrefixFromValue(no) {
  const { prefix } = parseSampleNoParts(no);
  if (prefix) sampleNoPrefix = prefix;
}

function getActiveSampleNoPrefix() {
  if (sampleNoPrefix) return sampleNoPrefix;
  const first = document.querySelector('#sample-tbody input[data-f="sample_no"]')?.value?.trim();
  const fromFirst = parseSampleNoParts(first).prefix;
  if (fromFirst) return fromFirst;
  return strVal("entrust_no");
}

function nextSampleNo(seq) {
  const prefix = getActiveSampleNoPrefix();
  if (!prefix) return "";
  return `${prefix}-${String(seq).padStart(2, "0")}`;
}

function renumberAllSampleNos() {
  const prefix = getActiveSampleNoPrefix();
  const blocks = getSampleBlocks();
  if (!prefix) {
    sampleSeq = blocks.length;
    return;
  }
  blocks.forEach((ringRow, i) => {
    const input = ringRow.querySelector('[data-f="sample_no"]');
    if (input) {
      input.value = `${prefix}-${String(i + 1).padStart(2, "0")}`;
    }
  });
  sampleSeq = blocks.length;
}

function ringDataFromRaw(raw, ringIdx) {
  if (raw.rings?.[ringIdx]) {
    const ring = raw.rings[ringIdx];
    const boxes = ring.boxes || [];
    return {
      sample_no: raw.sample_no || "",
      elevation: raw.elevation || "",
      ring_sample_mass: ring.ring_sample_mass,
      ring_mass: ring.ring_mass,
      box1: boxes[0] || {},
      box2: boxes[1] || {},
    };
  }
  if (ringIdx === 0) {
    return {
      sample_no: raw.sample_no || "",
      elevation: raw.elevation || "",
      ring_sample_mass: raw.ring_sample_mass,
      ring_mass: raw.ring_mass,
      box1: raw.box1 || {
        box_no: raw.box1_no || "",
        box_mass: raw.box1_mass,
        box_wet: raw.box1_wet,
        box_dry: raw.box1_dry,
      },
      box2: raw.box2 || {
        box_no: raw.box2_no || "",
        box_mass: raw.box2_mass,
        box_wet: raw.box2_wet,
        box_dry: raw.box2_dry,
      },
    };
  }
  return { sample_no: raw.sample_no || "", elevation: raw.elevation || "" };
}

function toDateInputValue(value) {
  if (!value) return "";
  const text = String(value).trim().split("T")[0].replace(/\//g, "-");
  const parts = text.split("-");
  if (parts.length >= 3) {
    const y = parts[0];
    const m = String(parts[1]).padStart(2, "0");
    const d = String(parts[2]).padStart(2, "0");
    return `${y}-${m}-${d}`;
  }
  return text;
}

function mergedFieldValue(field, fallback = "") {
  const el = document.querySelector(`#sample-tbody .global-date-cell [data-f="${field}"]`);
  return el?.value?.trim() || fallback;
}

function createRingPairRows(blockIdx, ringIdx, totalRings, blockRows, raw = {}) {
  const data = ringDataFromRaw(raw, ringIdx);
  const box1 = data.box1 || {};
  const box2 = data.box2 || {};
  const isFirstRing = ringIdx === 0;

  const metaCells = isFirstRing
    ? `<td rowspan="${blockRows}" class="readonly-cell"><input data-f="sample_no" type="text" class="cell-input" readonly value="${data.sample_no || ""}" /></td>
       <td rowspan="${blockRows}" class="editable-cell"><input data-f="elevation" type="text" class="cell-input" value="${data.elevation || ""}" /></td>`
    : "";
  const blockAvgWet = isFirstRing
    ? `<td rowspan="${blockRows}" class="readonly-cell" data-o="block_avg_wet"></td>`
    : "";
  const blockAvgMoisture = isFirstRing
    ? `<td rowspan="${blockRows}" class="readonly-cell" data-o="block_avg_moisture"></td>`
    : "";
  const blockAvgDry = isFirstRing
    ? `<td rowspan="${blockRows}" class="readonly-cell" data-o="block_avg_dry"></td>`
    : "";
  const blockCompaction = isFirstRing
    ? `<td rowspan="${blockRows}" class="readonly-cell" data-o="block_compaction"></td>`
    : "";
  const delCell = isFirstRing
    ? `<td rowspan="${blockRows}" class="readonly-cell op-cell"><button type="button" class="del-block">删除</button></td>`
    : "";

  const trRing = document.createElement("tr");
  trRing.className = `ring-row${ringIdx > 0 ? " ring-pair-next" : ""}`;
  trRing.dataset.block = blockIdx;
  trRing.dataset.ring = ringIdx;
  trRing.innerHTML = `
    ${metaCells}
    <td rowspan="2" class="editable-cell ring-density-cell"><input data-f="ring_sample_mass" type="text" class="cell-input" inputmode="decimal" value="${data.ring_sample_mass ?? ""}" /></td>
    <td rowspan="2" class="editable-cell ring-density-cell"><input data-f="ring_mass" type="text" class="cell-input" inputmode="decimal" value="${data.ring_mass ?? ""}" /></td>
    <td rowspan="2" class="readonly-cell ring-density-cell">${RING_VOLUME}</td>
    <td rowspan="2" class="readonly-cell ring-density-cell" data-o="wet_mass"></td>
    <td rowspan="2" class="readonly-cell ring-density-cell" data-o="wet_density"></td>
    ${blockAvgWet}
    <td class="editable-cell"><input data-f="box1_no" type="text" class="cell-input" value="${box1.box_no || box1.boxNo || ""}" /></td>
    <td class="editable-cell"><input data-f="box1_mass" type="text" class="cell-input" inputmode="decimal" value="${box1.box_mass ?? box1.boxMass ?? ""}" /></td>
    <td colspan="2" class="editable-cell"><input data-f="box1_wet" type="text" class="cell-input" inputmode="decimal" value="${box1.wet_sample_mass ?? box1.box_wet ?? ""}" /></td>
    <td class="editable-cell"><input data-f="box1_dry" type="text" class="cell-input" inputmode="decimal" value="${box1.dry_sample_mass ?? box1.box_dry ?? ""}" /></td>
    <td class="readonly-cell" data-o="moisture_1"></td>
    <td rowspan="2" class="readonly-cell" data-o="ring_avg_moisture"></td>
    ${blockAvgMoisture}
    <td rowspan="2" class="readonly-cell" data-o="ring_dry_density"></td>
    ${blockAvgDry}
    ${blockCompaction}
    ${delCell}
  `;

  const trBox = document.createElement("tr");
  trBox.className = "box-row";
  trBox.dataset.block = blockIdx;
  trBox.dataset.ring = ringIdx;
  trBox.innerHTML = `
    <td class="editable-cell"><input data-f="box2_no" type="text" class="cell-input" value="${box2.box_no || box2.boxNo || ""}" /></td>
    <td class="editable-cell"><input data-f="box2_mass" type="text" class="cell-input" inputmode="decimal" value="${box2.box_mass ?? box2.boxMass ?? ""}" /></td>
    <td colspan="2" class="editable-cell"><input data-f="box2_wet" type="text" class="cell-input" inputmode="decimal" value="${box2.wet_sample_mass ?? box2.box_wet ?? ""}" /></td>
    <td class="editable-cell"><input data-f="box2_dry" type="text" class="cell-input" inputmode="decimal" value="${box2.dry_sample_mass ?? box2.box_dry ?? ""}" /></td>
    <td class="readonly-cell" data-o="moisture_2"></td>
  `;

  if (isFirstRing) {
    trRing.querySelector(".del-block")?.addEventListener("click", () => removeSampleBlock(blockIdx));
  }

  return [trRing, trBox];
}

function createSampleBlock(raw = {}) {
  const idx = blockCount++;
  const totalRings = ringsPerBlock();
  const blockRows = rowsPerBlock();
  const rows = [];
  for (let ri = 0; ri < totalRings; ri += 1) {
    rows.push(...createRingPairRows(idx, ri, totalRings, blockRows, raw));
  }
  return rows;
}

function removeSampleBlock(blockId) {
  document.querySelectorAll(`#sample-tbody tr[data-block="${blockId}"]`).forEach((tr) => tr.remove());
  if (!document.querySelector("#sample-tbody tr")) {
    addSampleBlock();
  } else {
    renumberAllSampleNos();
    rebuildMergedCells();
    syncResultRows();
  }
}

function rebuildMergedCells() {
  const saved = {
    sampling_date: mergedFieldValue("sampling_date"),
    test_date: mergedFieldValue("test_date", todayISO()),
  };

  document.querySelectorAll(".global-date-cell").forEach((el) => el.remove());

  const firstRing = document.querySelector('#sample-tbody tr.ring-row[data-ring="0"]');
  if (!firstRing) return;
  const totalRows = document.querySelectorAll("#sample-tbody tr").length;
  const elevTd = firstRing.querySelector('input[data-f="elevation"]')?.closest("td");
  if (!elevTd) return;

  const makeDateCell = (field, val, extraClass) => {
    const td = document.createElement("td");
    td.rowSpan = totalRows;
    td.className = `global-date-cell editable-cell ${extraClass}`;
    td.innerHTML = `<input data-f="${field}" type="date" class="cell-input date-input" value="${toDateInputValue(val)}" />`;
    return td;
  };

  elevTd.after(makeDateCell("test_date", saved.test_date, "global-date-test"));
  elevTd.after(makeDateCell("sampling_date", saved.sampling_date, "global-date-sampling"));

  if (calcResults.length) applyResults(calcResults);
}

function addSampleBlock(data = {}) {
  const tbody = document.getElementById("sample-tbody");
  createSampleBlock({ ...data, sample_no: "" }).forEach((tr) => tbody.appendChild(tr));
  renumberAllSampleNos();
  rebuildMergedCells();
  syncResultRows();
}

function createResultRow() {
  const tr = document.createElement("tr");
  tr.innerHTML = `
    <td class="result-data-cell" data-r="sample_no"></td>
    <td class="result-data-cell" data-r="elevation"></td>
    <td class="result-data-cell" data-r="thickness"></td>
    <td class="result-data-cell" data-r="sampling_date"></td>
    <td class="result-data-cell" data-r="test_date"></td>
    <td class="result-data-cell" data-r="wet_density"></td>
    <td class="result-data-cell" data-r="avg_moisture"></td>
    <td class="result-data-cell" data-r="dry_density"></td>
    <td class="result-data-cell" data-r="compaction_coeff"></td>
  `;
  return tr;
}

function syncResultRows() {
  const tbody = document.getElementById("result-tbody");
  const blockCountNow = getSampleBlocks().length;
  while (tbody.children.length < blockCountNow) {
    tbody.appendChild(createResultRow());
  }
  while (tbody.children.length > blockCountNow) {
    tbody.lastElementChild?.remove();
  }
}

function formatDisplayDate(value) {
  if (!value) return "";
  return String(value).split("T")[0];
}

function updateResultTable(results, conclusion = "") {
  if (conclusion) overallConclusion = conclusion;
  syncResultRows();
  const rows = [...document.querySelectorAll("#result-tbody tr")];
  const samples = collectSamples();
  rows.forEach((tr, i) => {
    const r = results[i] || {};
    const s = samples[i] || {};
    const set = (key, val) => {
      const cell = tr.querySelector(`[data-r="${key}"]`);
      if (cell) cell.textContent = val ?? "";
    };
    set("sample_no", r.sample_no || s.sample_no);
    set("elevation", r.elevation || s.elevation);
    set("thickness", r.thickness || s.thickness);
    set("sampling_date", formatDisplayDate(r.sampling_date || s.sampling_date));
    set("test_date", formatDisplayDate(r.test_date || s.test_date));
    set("wet_density", r.wet_density ?? r.avg_wet_density);
    set("avg_moisture", r.avg_moisture);
    set("dry_density", r.dry_density);
    set("compaction_coeff", formatCompaction(r));
  });
  document.getElementById("result-conclusion").value = overallConclusion;
}

function clearResultTable() {
  document.querySelectorAll("#result-tbody [data-r]").forEach((cell) => {
    cell.textContent = "";
  });
  overallConclusion = "";
  document.getElementById("result-conclusion").value = "";
}

function getSampleBlocks() {
  return [...document.querySelectorAll('#sample-tbody tr.ring-row[data-ring="0"]')];
}

function collectProject() {
  return {
    entrust_no: strVal("entrust_no"),
    report_no: strVal("report_no"),
    entrust_unit: strVal("entrust_unit"),
    contact: strVal("contact"),
    supervision_unit: strVal("supervision_unit"),
    construction_unit: strVal("construction_unit"),
    project_name: strVal("project_name"),
    unit_address: strVal("unit_address"),
    project_address: strVal("project_address"),
    entrust_date: strVal("entrust_date"),
    project_section: strVal("project_section"),
    report_date: strVal("report_date") || todayISO(),
    test_nature: normalizeTestNature(strVal("test_nature")),
  };
}

function applyProject(project = {}) {
  const set = (id, val) => {
    const el = document.getElementById(id);
    if (el && val != null) el.value = val;
  };
  set("entrust_no", project.entrust_no);
  set("report_no", project.report_no);
  set("entrust_unit", project.entrust_unit);
  set("contact", project.contact);
  set("supervision_unit", project.supervision_unit);
  set("construction_unit", project.construction_unit);
  set("project_name", project.project_name);
  set("unit_address", project.unit_address);
  set("project_address", project.project_address);
  set("entrust_date", project.entrust_date);
  set("project_section", project.project_section);
  set("report_date", project.report_date);
  set("test_nature", normalizeTestNature(project.test_nature || ""));
}

function applyParams(params = {}) {
  const set = (id, val) => {
    const el = document.getElementById(id);
    if (el && val != null && val !== "") el.value = val;
  };
  set("soil_type", params.soil_type);
  set("max_dry_density", params.max_dry_density);
  set("compaction_method", params.compaction_method);
  set("optimal_moisture", params.optimal_moisture);
  set("ring_spec", params.ring_spec);
  set("design_requirement", params.design_requirement);
  set("sample_name", params.sample_name);
  set("material_type", params.material_type);
  set("test_basis", params.test_basis);
  set("judge_basis", params.judge_basis);
  set("test_location", params.test_location);
  if (params.record_template) {
    const tpl = document.getElementById("record-template");
    if (tpl) {
      tpl.value = params.record_template;
      lastRecordTemplate = params.record_template;
    }
  }
  if (params.result_type) {
    const radio = document.querySelector(`input[name="result_type"][value="${params.result_type}"]`);
    if (radio) radio.checked = true;
    updateCompactionHeaders();
  }
}

function setDraftStatus(text) {
  const el = document.getElementById("draft-status");
  if (el) el.textContent = text || "";
}

function collectDraftPayload() {
  return {
    project: collectProject(),
    params: collectParams(),
    samples: collectSamples(),
    calc_results: calcResults,
    overall_conclusion: overallConclusion,
    sample_no_prefix: sampleNoPrefix,
  };
}

async function saveDraftNow() {
  const no = strVal("entrust_no");
  if (!no || isApplyingDraft) return;
  try {
    const resp = await fetch(`${API}/api/drafts/${encodeURIComponent(no)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(collectDraftPayload()),
    });
    if (!resp.ok) throw new Error(await resp.text());
    const data = await resp.json();
    setDraftStatus(data.updated_at ? `已保存 ${new Date(data.updated_at).toLocaleString()}` : "已保存");
  } catch (e) {
    setDraftStatus("保存失败");
    console.warn("draft save failed", e);
  }
}

function scheduleDraftSave() {
  if (isApplyingDraft) return;
  clearTimeout(draftSaveTimer);
  draftSaveTimer = setTimeout(saveDraftNow, 800);
}

async function loadDraftForEntrust(no) {
  const entrustNo = (no || "").trim();
  if (!entrustNo) return false;
  try {
    const resp = await fetch(`${API}/api/drafts/${encodeURIComponent(entrustNo)}`);
    if (!resp.ok) return false;
    const data = await resp.json();
    if (!data.success || !data.draft) return false;
    isApplyingDraft = true;
    const draft = data.draft;
    applyProject(draft.project || {});
    applyParams(draft.params || {});
    sampleNoPrefix = draft.sample_no_prefix || "";
    overallConclusion = draft.overall_conclusion || "";
    const tbody = document.getElementById("sample-tbody");
    tbody.innerHTML = "";
    blockCount = 0;
    const samples = draft.samples?.length ? draft.samples : [{}];
    samples.forEach((sample) => {
      createSampleBlock(sample).forEach((tr) => tbody.appendChild(tr));
    });
    renumberAllSampleNos();
    rebuildMergedCells();
    syncResultRows();
    if (draft.calc_results?.length) {
      applyResults(draft.calc_results);
      updateResultTable(draft.calc_results, overallConclusion);
    } else {
      calcResults = [];
      clearResultTable();
      if (overallConclusion) {
        document.getElementById("result-conclusion").value = overallConclusion;
      }
    }
    setDraftStatus(data.updated_at ? `已恢复 ${new Date(data.updated_at).toLocaleString()}` : "已恢复本地草稿");
    return true;
  } catch (e) {
    console.warn("draft load failed", e);
    return false;
  } finally {
    isApplyingDraft = false;
  }
}

function scheduleDraftLoad() {
  clearTimeout(draftLoadTimer);
  draftLoadTimer = setTimeout(async () => {
    const no = strVal("entrust_no");
    if (!no) {
      setDraftStatus("");
      return;
    }
    await loadDraftForEntrust(no);
  }, 400);
}

function collectParams() {
  return {
    soil_type: strVal("soil_type"),
    max_dry_density: numVal("max_dry_density"),
    compaction_method: strVal("compaction_method"),
    optimal_moisture: numVal("optimal_moisture"),
    ring_spec: strVal("ring_spec") || "200cm³",
    design_requirement: numVal("design_requirement"),
    sample_name: strVal("sample_name") || "回填土",
    material_type: strVal("material_type"),
    test_basis: strVal("test_basis"),
    judge_basis: strVal("judge_basis"),
    result_type: getResultType(),
    record_template: getRecordTemplate(),
    test_location: strVal("test_location"),
    remark: "",
  };
}

function parseInput(row, field) {
  const el = row.querySelector(`[data-f="${field}"]`);
  if (!el) return field.includes("no") || field.includes("date") ? "" : null;
  if (field.includes("date") || field.includes("no") || field === "elevation") return el.value.trim();
  if (el.value === "") return null;
  const n = parseFloat(el.value);
  return Number.isFinite(n) ? n : null;
}

function collectRingPair(blockId, ringIdx) {
  const ringRow = document.querySelector(
    `#sample-tbody tr.ring-row[data-block="${blockId}"][data-ring="${ringIdx}"]`
  );
  const boxRow = document.querySelector(
    `#sample-tbody tr.box-row[data-block="${blockId}"][data-ring="${ringIdx}"]`
  );
  if (!ringRow || !boxRow) return null;
  return {
    ring_sample_mass: parseInput(ringRow, "ring_sample_mass"),
    ring_mass: parseInput(ringRow, "ring_mass"),
    ring_volume: RING_VOLUME,
    boxes: [
      {
        box_no: parseInput(ringRow, "box1_no"),
        box_mass: parseInput(ringRow, "box1_mass"),
        wet_sample_mass: parseInput(ringRow, "box1_wet"),
        dry_sample_mass: parseInput(ringRow, "box1_dry"),
      },
      {
        box_no: parseInput(boxRow, "box2_no"),
        box_mass: parseInput(boxRow, "box2_mass"),
        wet_sample_mass: parseInput(boxRow, "box2_wet"),
        dry_sample_mass: parseInput(boxRow, "box2_dry"),
      },
    ],
  };
}

function collectSamples() {
  const samplingDate = mergedFieldValue("sampling_date");
  const testDate = mergedFieldValue("test_date");
  return getSampleBlocks().map((ringRow) => {
    const block = ringRow.dataset.block;
    const rings = [];
    for (let ri = 0; ri < ringsPerBlock(); ri += 1) {
      const ring = collectRingPair(block, ri);
      if (ring) rings.push(ring);
    }
    const firstRing = rings[0] || {};
    return {
      sample_no: parseInput(ringRow, "sample_no"),
      elevation: parseInput(ringRow, "elevation"),
      sampling_date: samplingDate,
      test_date: testDate,
      thickness: "",
      ring_sample_mass: firstRing.ring_sample_mass,
      ring_mass: firstRing.ring_mass,
      ring_volume: RING_VOLUME,
      boxes: firstRing.boxes || [],
      rings,
    };
  });
}

function setCell(row, field, val) {
  if (!row) return;
  const cell = row.querySelector(`[data-o="${field}"]`);
  if (cell) cell.textContent = val ?? "";
}

function applyResults(results) {
  calcResults = results;
  const blocks = getSampleBlocks();
  results.forEach((r, i) => {
    const ringRow = blocks[i];
    if (!ringRow) return;
    const block = ringRow.dataset.block;
    const ringResults = r.rings?.length ? r.rings : [r];

    ringResults.forEach((rr, ri) => {
      const row = document.querySelector(
        `#sample-tbody tr.ring-row[data-block="${block}"][data-ring="${ri}"]`
      );
      const boxRow = document.querySelector(
        `#sample-tbody tr.box-row[data-block="${block}"][data-ring="${ri}"]`
      );
      setCell(row, "wet_mass", rr.wet_mass ?? r.wet_mass);
      setCell(row, "wet_density", rr.wet_density ?? r.wet_density);
      setCell(row, "ring_avg_moisture", rr.avg_moisture);
      setCell(row, "ring_dry_density", rr.dry_density ?? r.dry_density);
      if (rr.moisture_rates?.length) {
        setCell(row, "moisture_1", rr.moisture_rates[0]);
        setCell(boxRow, "moisture_2", rr.moisture_rates[1]);
      }
    });

    setCell(ringRow, "block_avg_wet", r.avg_wet_density ?? r.wet_density);
    setCell(ringRow, "block_avg_moisture", r.avg_moisture);
    setCell(ringRow, "block_avg_dry", r.avg_dry_density ?? r.dry_density);
    setCell(ringRow, "block_compaction", formatCompaction(r));
  });

  updateResultTable(results);
}

async function doCalc() {
  const payload = { params: collectParams(), samples: collectSamples() };
  try {
    const resp = await fetch(`${API}/api/calc`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!resp.ok) throw new Error(await resp.text());
    const data = await resp.json();
    applyResults(data.results);
    updateResultTable(data.results, data.overall_conclusion || "");
    scheduleDraftSave();
    toast("计算完成", "success");
    return data;
  } catch (e) {
    toast(`计算失败: ${e.message}`, "error");
    throw e;
  }
}

async function fetchLimis(orderId = "") {
  const no = strVal("entrust_no");
  if (!no) {
    toast("请先输入委托编号", "error");
    return;
  }
  const params = new URLSearchParams(window.location.search);
  const resolvedOrderId = orderId || params.get("order_id") || "";
  const taskId = params.get("task_id") || "";
  const taskNo = params.get("task_no") || "";
  const sampleId = params.get("sample_id") || "";
  const query = new URLSearchParams();
  if (resolvedOrderId) query.set("order_id", resolvedOrderId);
  if (taskId) query.set("task_id", taskId);
  if (taskNo) query.set("task_no", taskNo);
  if (sampleId) query.set("sample_id", sampleId);
  const qs = query.toString() ? `?${query.toString()}` : "";
  try {
    const resp = await fetch(`${API}/api/limis/entrust/${encodeURIComponent(no)}${qs}`);
    const data = await resp.json();
    if (!data.success || !data.project) {
      toast(data.message || "查询失败", "error");
      return;
    }
    const p = data.project;
    const set = (id, val) => {
      const el = document.getElementById(id);
      if (el && val != null && val !== "") el.value = val;
    };
    set("entrust_no", p.entrust_no);
    set("report_no", p.report_no);
    set("entrust_unit", p.entrust_unit);
    set("contact", p.contact);
    set("supervision_unit", p.supervision_unit);
    set("construction_unit", p.construction_unit);
    set("project_name", p.project_name);
    set("unit_address", p.unit_address);
    set("project_address", p.project_address);
    set("entrust_date", p.entrust_date);
    set("project_section", p.project_section);
    set("report_date", p.report_date);
    set("test_nature", normalizeTestNature(p.test_nature));
    if (data.sample_name) {
      set("sample_name", data.sample_name);
    }
    if (data.sample_no) {
      setSampleNoPrefixFromValue(data.sample_no);
      renumberAllSampleNos();
    }
    scheduleDraftSave();
    toast("工程信息已填充", "success");
  } catch (e) {
    toast(`LIMIS 请求失败: ${e.message}`, "error");
  }
}

async function generateReport() {
  await doCalc();
  const payload = {
    project: collectProject(),
    params: collectParams(),
    samples: collectSamples(),
  };
  try {
    const resp = await fetch(`${API}/api/report/generate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!resp.ok) throw new Error(await resp.text());
    const blob = await resp.blob();
    const disp = resp.headers.get("Content-Disposition") || "";
    const match = disp.match(/filename="?([^"]+)"?/);
    const filename = match ? match[1] : `报告_${strVal("entrust_no") || "output"}.docx`;
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
    toast("报告已生成", "success");
  } catch (e) {
    toast(`报告生成失败: ${e.message}`, "error");
  }
}

function exportJson() {
  const data = {
    project: collectProject(),
    params: collectParams(),
    samples: collectSamples(),
    results: calcResults,
  };
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `环刀记录_${todayISO()}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

async function clearAll() {
  if (!confirm("确定清空所有数据？")) return;
  const no = strVal("entrust_no");
  document.querySelectorAll("input[type=text], input[type=number], input[type=date]").forEach((el) => {
    if (el.id === "ring_spec") el.value = "200cm³";
    else if (el.id === "sample_name") el.value = "回填土";
    else if (el.id === "test_basis" || el.id === "judge_basis") el.value = "JTG 3450-2019";
    else el.value = "";
  });
  document.getElementById("sample-tbody").innerHTML = "";
  blockCount = 0;
  sampleSeq = 0;
  sampleNoPrefix = "";
  initEmptyBlocks(1);
  calcResults = [];
  clearResultTable();
  setDraftStatus("");
  if (no) {
    try {
      await fetch(`${API}/api/drafts/${encodeURIComponent(no)}`, { method: "DELETE" });
    } catch (e) {
      console.warn("draft delete failed", e);
    }
  }
}

function initEmptyBlocks(count = 1) {
  const tbody = document.getElementById("sample-tbody");
  blockCount = 0;
  sampleSeq = 0;
  tbody.innerHTML = "";
  for (let i = 0; i < count; i += 1) {
    createSampleBlock({ sample_no: "" }).forEach((tr) => tbody.appendChild(tr));
  }
  renumberAllSampleNos();
  rebuildMergedCells();
  syncResultRows();
}

function switchRecordTemplate(event) {
  const select = event.target;
  const next = select.value;
  if (!confirm("切换模版将清空当前原始记录数据，是否继续？")) {
    select.value = lastRecordTemplate;
    return;
  }
  lastRecordTemplate = next;
  calcResults = [];
  clearResultTable();
  sampleNoPrefix = "";
  initEmptyBlocks(1);
}

async function loadFromUrlParams() {
  const params = new URLSearchParams(window.location.search);
  const entrustNo = params.get("entrust_no");
  const orderId = params.get("order_id");
  const sampleName = params.get("sample_name");
  const taskNo = params.get("task_no");

  if (sampleName) {
    const el = document.getElementById("sample_name");
    if (el) el.value = sampleName;
  }
  if (taskNo) {
    setSampleNoPrefixFromValue(taskNo);
    renumberAllSampleNos();
  }
  if (entrustNo) {
    document.getElementById("entrust_no").value = entrustNo;
    await loadDraftForEntrust(entrustNo);
    fetchLimis(orderId || "");
  }
}

function bindAutosave() {
  const main = document.querySelector("main");
  const triggerSave = () => {
    if (!isApplyingDraft) scheduleDraftSave();
  };
  main?.addEventListener("input", triggerSave);
  main?.addEventListener("change", triggerSave);
  document.getElementById("entrust_no")?.addEventListener("change", scheduleDraftLoad);
}

document.getElementById("btn-add-row").addEventListener("click", () => addSampleBlock());
document.getElementById("btn-calc").addEventListener("click", doCalc);
document.getElementById("btn-generate").addEventListener("click", generateReport);
document.getElementById("btn-export").addEventListener("click", exportJson);
document.getElementById("btn-clear").addEventListener("click", clearAll);
document.getElementById("btn-fetch-limis").addEventListener("click", () => fetchLimis());
document.getElementById("record-template").addEventListener("change", switchRecordTemplate);
document.querySelectorAll('input[name="result_type"]').forEach((el) => {
  el.addEventListener("change", () => {
    updateCompactionHeaders();
    if (calcResults.length) applyResults(calcResults);
  });
});

document.getElementById("report_date").value = todayISO();
document.getElementById("max_dry_density").value = "1.85";
document.getElementById("design_requirement").value = "0.93";
updateCompactionHeaders();
bindAutosave();
initEmptyBlocks(1);
loadFromUrlParams();
