const API = "";

function toast(msg, type = "info") {
  const el = document.createElement("div");
  el.className = `toast ${type}`;
  el.textContent = msg;
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 3000);
}

function todayISO() {
  return new Date().toISOString().slice(0, 10);
}

function renderNav(active) {
  const items = [
    { href: "/", label: "任务导航", key: "tasks" },
    { href: "/record", label: "原始记录", key: "record" },
    { href: "/settings", label: "设置", key: "settings" },
  ];
  const nav = document.getElementById("main-nav");
  if (!nav) return;
  nav.innerHTML = items
    .map(
      (item) => `<a href="${item.href}"
        class="px-3 py-1.5 rounded text-sm ${active === item.key ? "bg-white/20 font-semibold" : "hover:bg-white/10"}">${item.label}</a>`
    )
    .join("");
}

async function loadServerSettings() {
  const resp = await fetch(`${API}/api/settings`);
  if (!resp.ok) throw new Error("无法加载设置");
  return resp.json();
}

async function saveServerSettings(data) {
  const resp = await fetch(`${API}/api/settings`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!resp.ok) throw new Error(await resp.text());
  return resp.json();
}

async function testLogin() {
  const resp = await fetch(`${API}/api/limis/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({}),
  });
  return resp.json();
}
