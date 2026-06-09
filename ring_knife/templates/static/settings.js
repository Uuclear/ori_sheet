renderNav("settings");

async function loadForm() {
  try {
    const data = await loadServerSettings();
    document.getElementById("base_url").value = data.base_url || "";
    document.getElementById("username").value = data.username || "";
    document.getElementById("password-hint").textContent = data.password_set
      ? "已保存密码，留空则不修改"
      : "尚未设置密码";
  } catch (e) {
    toast(`加载设置失败: ${e.message}`, "error");
  }
}

async function saveForm(silent = false) {
  const payload = {
    base_url: document.getElementById("base_url").value.trim(),
    username: document.getElementById("username").value.trim(),
    password: document.getElementById("password").value,
  };
  if (!payload.base_url || !payload.username) {
    if (!silent) toast("请填写服务器地址和用户名", "error");
    return false;
  }
  try {
    const data = await saveServerSettings(payload);
    document.getElementById("password").value = "";
    document.getElementById("password-hint").textContent = data.password_set
      ? "已保存密码，留空则不修改"
      : "尚未设置密码";
    if (!silent) toast("设置已保存", "success");
    return true;
  } catch (e) {
    if (!silent) toast(`保存失败: ${e.message}`, "error");
    return false;
  }
}

async function testForm() {
  const resultEl = document.getElementById("test-result");
  resultEl.classList.remove("hidden");
  resultEl.textContent = "正在测试登录...";
  try {
    const saved = await saveForm(true);
    if (!saved) {
      resultEl.className = "text-sm text-red-600";
      resultEl.textContent = "请先填写服务器地址和用户名";
      return;
    }
    const data = await testLogin();
    if (data.success) {
      resultEl.className = "text-sm text-green-700";
      resultEl.textContent = `登录成功，用户 ID: ${data.user_id || "-"}`;
      toast("登录测试成功", "success");
    } else {
      resultEl.className = "text-sm text-red-600";
      resultEl.textContent = data.message || "登录失败";
      toast(data.message || "登录失败", "error");
    }
  } catch (e) {
    resultEl.className = "text-sm text-red-600";
    resultEl.textContent = e.message;
    toast(`测试失败: ${e.message}`, "error");
  }
}

document.getElementById("btn-save").addEventListener("click", saveForm);
document.getElementById("btn-test").addEventListener("click", testForm);
loadForm();
