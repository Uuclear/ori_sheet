using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector
{
    public partial class MainWindow : Window
    {
        private readonly CalculationService _calculationService;
        private readonly LimisService _limisService;
        private readonly ExcelExportService _excelExportService;
        private readonly WordExportService _wordExportService;
        private readonly DraftService _draftService;
        private readonly SettingsService _settingsService;

        private ProjectInfo _currentProject = new();
        private RecordParams _currentParams = new();
        private List<RingKnifeSample> _currentSamples = new();
        private List<SamplePointResult> _currentResults = new();
        private string _currentEntrustNo = string.Empty;

        // Pagination
        private List<TaskItem> _allTasks = new();
        private int _currentPage = 1;
        private const int PageSize = 20;
        private int _lastTemplateIndex;

        public MainWindow()
        {
            InitializeComponent();

            _calculationService = new CalculationService();
            _limisService = new LimisService();
            _excelExportService = new ExcelExportService();
            _wordExportService = new WordExportService();
            _draftService = new DraftService();
            _settingsService = new SettingsService();

            InitializeDefaultValues();
            LoadSettings();

            recordTable.DeleteBlockRequested += (_, idx) => RemoveSampleBlock(idx);
            recordTable.SamplesChanged += (_, _) => { /* data already in _currentSamples */ };
            _lastTemplateIndex = cmbRecordTemplate.SelectedIndex;
        }

        private int GetRingsPerBlock() =>
            (cmbRecordTemplate.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "group3" ? 3 : 2;

        private void RefreshRecordTable()
        {
            var globalSampling = _currentSamples.FirstOrDefault()?.SamplingDate ?? DateTime.Now.ToString("yyyy-MM-dd");
            var globalTest = _currentSamples.FirstOrDefault()?.TestDate ?? DateTime.Now.ToString("yyyy-MM-dd");
            recordTable.Configure(
                _currentSamples, _currentParams, _currentResults,
                GetRingsPerBlock(),
                rbCompactionPercent.IsChecked == true ? "compaction_percent" : "compaction_coeff",
                globalSampling, globalTest);
        }

        private void InitializeDefaultValues()
        {
            _currentParams.MaxDryDensity = 1.92m;
            _currentParams.DesignRequirement = 0.93m;
            _currentParams.SoilType = "土";
            _currentParams.CompactionMethod = "重型击实";
            _currentParams.RingSpec = "200cm³";
            _currentParams.SampleName = "回填土";
            _currentParams.RecordTemplate = "group2";

            AddNewSample();
            RefreshRecordTable();
        }

        private RingKnifeSample CreateSample(string sampleNo)
        {
            var rings = GetRingsPerBlock();
            var sample = new RingKnifeSample
            {
                SampleNo = sampleNo,
                SamplingDate = DateTime.Now.ToString("yyyy-MM-dd"),
                TestDate = DateTime.Now.ToString("yyyy-MM-dd"),
                RingVolume = 200
            };
            for (int i = 0; i < rings; i++)
            {
                sample.Rings.Add(new RingMeasurement
                {
                    RingVolume = 200,
                    Boxes = new List<AluminumBox> { new(), new() }
                });
            }
            return sample;
        }

        private void AddNewSample()
        {
            var prefix = GetSampleNoPrefix();
            var seq = _currentSamples.Count + 1;
            var sampleNo = string.IsNullOrEmpty(prefix) ? seq.ToString() : $"{prefix}-{seq:D2}";
            _currentSamples.Add(CreateSample(sampleNo));
        }

        private string GetSampleNoPrefix()
        {
            var no = _currentProject.EntrustNo ?? _currentEntrustNo ?? "";
            return no.Trim();
        }

        private void RenumberSampleNos()
        {
            var prefix = GetSampleNoPrefix();
            for (int i = 0; i < _currentSamples.Count; i++)
            {
                _currentSamples[i].SampleNo = string.IsNullOrEmpty(prefix)
                    ? (i + 1).ToString()
                    : $"{prefix}-{i + 1:D2}";
            }
            RefreshRecordTable();
        }

        private void LoadSettings()
        {
            var settings = _settingsService.LoadSettings();
            txtBaseUrl.Text = settings.BaseUrl;
            txtUsername.Text = settings.Username;
            txtRealName.Text = IsValidPersonName(settings.RealName, settings.Username)
                ? settings.RealName
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(settings.DefaultReportRemarks))
                txtReportRemarks.Text = settings.DefaultReportRemarks;
            else
                txtReportRemarks.Text = ReportDefaults.DefaultReportRemarks;
        }

        private static void SetDatePicker(DatePicker picker, string? text)
        {
            picker.SelectedDate = DateHelper.TryParse(text) ?? null;
        }

        private static string GetDatePickerText(DatePicker picker) =>
            DateHelper.Format(picker.SelectedDate);

        private void ShowView(string viewName)
        {
            taskListView.Visibility = viewName == "Tasks" ? Visibility.Visible : Visibility.Collapsed;
            recordView.Visibility = viewName == "Record" ? Visibility.Visible : Visibility.Collapsed;
            settingsView.Visibility = viewName == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        }

        // ========== Navigation ==========
        private void BtnTaskList_Click(object sender, RoutedEventArgs e) => ShowView("Tasks");
        private void BtnRecord_Click(object sender, RoutedEventArgs e) => ShowView("Record");
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => ShowView("Settings");

        // ========== Task List Pagination ==========
        private void UpdateTaskPage()
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)_allTasks.Count / PageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pageItems = _allTasks
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            dgTasks.ItemsSource = pageItems;
            txtPageInfo.Text = $"第 {_currentPage}/{totalPages} 页，共 {_allTasks.Count} 条";
            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < totalPages;
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e) { _currentPage--; UpdateTaskPage(); }
        private void BtnNextPage_Click(object sender, RoutedEventArgs e) { _currentPage++; UpdateTaskPage(); }

        // ========== Search ==========
        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            var keyword = txtSearchKeyword.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("请输入搜索关键词", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            txtStatus.Text = "正在搜索任务...";
            try
            {
                var result = await _limisService.SearchTasksByEntrustAsync(keyword);
                if (result.Success)
                {
                    _allTasks = result.Tasks;
                    _currentPage = 1;
                    UpdateTaskPage();
                    txtStatus.Text = result.Message;
                }
                else
                {
                    MessageBox.Show(result.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtStatus.Text = "搜索失败";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "搜索失败";
            }
        }

        private async void DgTasks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgTasks.SelectedItem is not TaskItem task) return;
            _currentEntrustNo = task.TestingOrderNo;
            txtEntrustNo.Text = task.TestingOrderNo;
            txtProjectName.Text = task.ProjectName;
            ShowView("Record");
            await FetchLimisAsync(task.TestingOrderNo, task.TestingOrderId, task.TaskId, task.TaskNo);
        }

        private async Task FetchLimisAsync(string entrustNo, string? orderId = null, string? taskId = null, string? taskNo = null)
        {
            txtStatus.Text = "正在从LIMIS系统拉取信息...";
            try
            {
                var result = await _limisService.GetEntrustByNoAsync(entrustNo, orderId, taskId, taskNo);
                if (result.Success && result.Project != null)
                {
                    ApplyLimisResult(result, entrustNo);
                    TryPersistRealName();
                    txtStatus.Text = "信息拉取成功";
                }
                else
                {
                    MessageBox.Show(result.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtStatus.Text = "拉取失败";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拉取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "拉取失败";
            }
        }

        private void ApplyLimisResult(LimisEntrustResponse result, string entrustNo)
        {
            _currentProject = result.Project!;
            _currentEntrustNo = entrustNo;
            FillProjectForm(_currentProject);
            if (!string.IsNullOrEmpty(result.SampleName))
                txtSampleName.Text = result.SampleName;
            if (!string.IsNullOrEmpty(result.Remark))
                txtLimisRemark.Text = result.Remark;
            if (!string.IsNullOrEmpty(result.SampleNo))
                RenumberSampleNosFromPrefix(result.SampleNo);
            TryLoadDraft(entrustNo);
        }

        private void TryLoadDraft(string entrustNo)
        {
            var loaded = _draftService.LoadDraft(entrustNo);
            if (!loaded.Success || loaded.Draft == null) return;

            var d = loaded.Draft;
            if (d.Project != null) FillProjectForm(d.Project);
            if (d.Params != null)
            {
                _currentParams = d.Params;
                txtSampleName.Text = d.Params.SampleName;
                txtMaterialType.Text = d.Params.MaterialType;
                txtRingSpec.Text = d.Params.RingSpec;
                txtCompactionMethod.Text = d.Params.CompactionMethod;
                txtTestLocation.Text = d.Params.TestLocation;
                txtTestBasis.Text = d.Params.TestBasis;
                txtJudgeBasis.Text = d.Params.JudgeBasis;
                txtLimisRemark.Text = d.Params.LimisRemark;
                txtMaxDryDensity.Text = d.Params.MaxDryDensity?.ToString() ?? "";
                txtDesignRequirement.Text = d.Params.DesignRequirement?.ToString() ?? "";
                txtOptimalMoisture.Text = d.Params.OptimalMoisture?.ToString() ?? "";
            }
            if (d.Samples.Count > 0)
            {
                _currentSamples = d.Samples;
                RefreshRecordTable();
            }
            if (d.CalcResults.Count > 0)
            {
                _currentResults = d.CalcResults;
                dgResults.ItemsSource = _currentResults;
            }
            if (!string.IsNullOrEmpty(d.OverallConclusion))
                txtConclusion.Text = d.OverallConclusion;
            if (!string.IsNullOrWhiteSpace(d.ReportRemarks))
                txtReportRemarks.Text = d.ReportRemarks;
        }

        private void RenumberSampleNosFromPrefix(string sampleNo)
        {
            var idx = sampleNo.LastIndexOf('-');
            if (idx <= 0) return;
            var prefix = sampleNo[..idx];
            for (int i = 0; i < _currentSamples.Count; i++)
                _currentSamples[i].SampleNo = $"{prefix}-{(i + 1):D2}";
            RefreshRecordTable();
        }

        // ========== LIMIS Fetch ==========
        private async void BtnFetchFromLimis_Click(object sender, RoutedEventArgs e)
        {
            var entrustNo = txtEntrustNo.Text.Trim();
            if (string.IsNullOrEmpty(entrustNo))
            {
                MessageBox.Show("请输入委托编号", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await FetchLimisAsync(entrustNo);
        }

        private void FillProjectForm(ProjectInfo p)
        {
            txtTestNature.Text = p.TestNature;
            txtEntrustNo.Text = p.EntrustNo;
            txtReportNo.Text = p.ReportNo;
            txtEntrustUnit.Text = p.EntrustUnit;
            txtContact.Text = p.Contact;
            txtSupervisionUnit.Text = p.SupervisionUnit;
            txtConstructionUnit.Text = p.ConstructionUnit;
            txtProjectName.Text = p.ProjectName;
            txtUnitAddress.Text = p.UnitAddress;
            txtProjectAddress.Text = p.ProjectAddress;
            SetDatePicker(dpEntrustDate, p.EntrustDate);
            txtProjectSection.Text = p.ProjectSection;
            SetDatePicker(dpReportDate, p.ReportDate);
            RenumberSampleNos();
        }

        // ========== Add Row ==========
        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            AddNewSample();
            RefreshRecordTable();
        }

        private void RemoveSampleBlock(int blockIndex)
        {
            if (blockIndex < 0 || blockIndex >= _currentSamples.Count) return;
            _currentSamples.RemoveAt(blockIndex);
            if (_currentSamples.Count == 0)
                AddNewSample();
            else
                RenumberSampleNos();
            _currentResults.Clear();
            dgResults.ItemsSource = null;
            txtConclusion.Text = "";
            RefreshRecordTable();
        }

        private void CmbRecordTemplate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (MessageBox.Show("切换模版将清空当前原始记录数据，是否继续？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                cmbRecordTemplate.SelectionChanged -= CmbRecordTemplate_Changed;
                cmbRecordTemplate.SelectedIndex = _lastTemplateIndex;
                cmbRecordTemplate.SelectionChanged += CmbRecordTemplate_Changed;
                return;
            }
            _lastTemplateIndex = cmbRecordTemplate.SelectedIndex;
            _currentSamples.Clear();
            _currentResults.Clear();
            AddNewSample();
            dgResults.ItemsSource = null;
            txtConclusion.Text = "";
            RefreshRecordTable();
        }

        private void ResultType_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            RefreshRecordTable();
        }

        // ========== Clear ==========
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要清空所有数据吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _currentSamples.Clear();
            _currentResults.Clear();
            _currentProject = new ProjectInfo();
            _currentParams = new RecordParams();

            txtTestNature.Text = "";
            txtEntrustNo.Text = "";
            txtReportNo.Text = "";
            txtEntrustUnit.Text = "";
            txtContact.Text = "";
            txtSupervisionUnit.Text = "";
            txtConstructionUnit.Text = "";
            txtProjectName.Text = "";
            txtUnitAddress.Text = "";
            txtProjectAddress.Text = "";
            dpEntrustDate.SelectedDate = null;
            txtProjectSection.Text = "";
            dpReportDate.SelectedDate = null;
            txtSampleName.Text = "回填土";
            txtMaterialType.Text = "";
            txtRingSpec.Text = "200cm³";
            txtCompactionMethod.Text = "";
            txtDesignRequirement.Text = "";
            txtMaxDryDensity.Text = "";
            txtTestLocation.Text = "";
            txtOptimalMoisture.Text = "";
            txtTestBasis.Text = "JTG 3450-2019";
            txtJudgeBasis.Text = "JTG 3450-2019";
            txtConclusion.Text = "";
            txtLimisRemark.Text = "";
            txtReportRemarks.Text = _settingsService.LoadSettings().DefaultReportRemarks;
            rbCompactionCoeff.IsChecked = true;

            _currentSamples.Clear();
            InitializeDefaultValues();
            dgResults.ItemsSource = null;
            txtStatus.Text = "已清空所有数据";
        }

        // ========== Calculate ==========
        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateCurrentParams();
                UpdateCurrentSamples();

                var request = new CalcRequest { Params = _currentParams, Samples = _currentSamples };
                var response = _calculationService.CalculateAll(request);
                _currentResults = response.Results;
                dgResults.ItemsSource = null;
                dgResults.ItemsSource = _currentResults;
                txtConclusion.Text = response.OverallConclusion;
                RefreshRecordTable();
                txtStatus.Text = $"计算完成，共{response.Results.Count}个测点";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCurrentParams()
        {
            _currentProject.EntrustNo = txtEntrustNo.Text;
            _currentProject.TestNature = txtTestNature.Text;
            _currentProject.ProjectName = txtProjectName.Text;
            _currentProject.EntrustUnit = txtEntrustUnit.Text;
            _currentProject.Contact = txtContact.Text;
            _currentProject.ReportNo = txtReportNo.Text;
            _currentProject.EntrustDate = GetDatePickerText(dpEntrustDate);
            _currentProject.SupervisionUnit = txtSupervisionUnit.Text;
            _currentProject.ConstructionUnit = txtConstructionUnit.Text;
            _currentProject.UnitAddress = txtUnitAddress.Text;
            _currentProject.ProjectAddress = txtProjectAddress.Text;
            _currentProject.ProjectSection = txtProjectSection.Text;
            _currentProject.ReportDate = GetDatePickerText(dpReportDate);

            _currentParams.SampleName = txtSampleName.Text;
            _currentParams.MaterialType = txtMaterialType.Text;
            _currentParams.RingSpec = txtRingSpec.Text;
            _currentParams.CompactionMethod = txtCompactionMethod.Text;
            _currentParams.TestLocation = txtTestLocation.Text;
            _currentParams.TestBasis = txtTestBasis.Text;
            _currentParams.JudgeBasis = txtJudgeBasis.Text;
            _currentParams.ResultType = rbCompactionPercent.IsChecked == true ? "compaction_percent" : "compaction_coeff";
            _currentParams.RecordTemplate = GetRingsPerBlock() == 3 ? "group3" : "group2";
            _currentParams.LimisRemark = txtLimisRemark.Text;

            if (decimal.TryParse(txtMaxDryDensity.Text, out var maxDD)) _currentParams.MaxDryDensity = maxDD;
            if (decimal.TryParse(txtDesignRequirement.Text, out var dr)) _currentParams.DesignRequirement = dr;
            if (decimal.TryParse(txtOptimalMoisture.Text, out var om)) _currentParams.OptimalMoisture = om;
        }

        private void UpdateCurrentSamples()
        {
            // 样品数据由 RecordTableControl 直接编辑 _currentSamples
        }

        private string GetExportFileName(string extension)
        {
            var reportNo = txtReportNo.Text?.Trim();
            var entrustNo = txtEntrustNo.Text?.Trim();
            if (!string.IsNullOrEmpty(reportNo))
                return $"{reportNo}.{extension}";
            if (!string.IsNullOrEmpty(entrustNo))
                return $"{entrustNo}.{extension}";
            return $"环刀法压实度检测报告_{DateTime.Now:yyyyMMddHHmmss}.{extension}";
        }

        // ========== Export Excel ==========
        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResults.Count == 0)
            {
                MessageBox.Show("请先计算数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                Title = "导出Excel文件",
                FileName = GetExportFileName("xlsx")
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    txtStatus.Text = "正在导出Excel...";
                    _excelExportService.ExportToExcel(_currentProject, _currentParams, _currentResults, txtConclusion.Text, dlg.FileName);
                    txtStatus.Text = "导出成功";
                    MessageBox.Show($"导出成功\n\n{dlg.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtStatus.Text = "导出失败";
                }
            }
        }

        // ========== Export Word ==========
        private void BtnExportWord_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResults.Count == 0)
            {
                MessageBox.Show("请先计算数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Word文件|*.docx",
                Title = "导出Word文件",
                FileName = GetExportFileName("docx")
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    UpdateCurrentParams();
                    txtStatus.Text = "正在导出Word...";
                    var settings = _settingsService.LoadSettings();
                    var inspector = GetInspectorName(settings);
                    _wordExportService.ExportToWord(
                        _currentProject, _currentParams, _currentResults, txtConclusion.Text,
                        txtReportRemarks.Text, inspector, dlg.FileName);
                    txtStatus.Text = "导出成功";
                    MessageBox.Show($"导出成功\n\n{dlg.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtStatus.Text = "导出失败";
                }
            }
        }

        // ========== Save Draft ==========
        private void BtnSaveDraft_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentEntrustNo))
            {
                MessageBox.Show("请输入委托编号", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                UpdateCurrentParams();
                UpdateCurrentSamples();
                var draft = new DraftSaveRequest
                {
                    Project = _currentProject,
                    Params = _currentParams,
                    Samples = _currentSamples,
                    CalcResults = _currentResults,
                    OverallConclusion = txtConclusion.Text,
                    ReportRemarks = txtReportRemarks.Text
                };
                var result = _draftService.SaveDraft(_currentEntrustNo, draft);
                if (result.Success)
                {
                    var settings = _settingsService.LoadSettings();
                    settings.DefaultReportRemarks = txtReportRemarks.Text;
                    _settingsService.SaveSettings(settings);
                    txtStatus.Text = "草稿保存成功";
                    MessageBox.Show("草稿保存成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                    MessageBox.Show(result.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存草稿失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========== Settings ==========
        private async void BtnTestLogin_Click(object sender, RoutedEventArgs e)
        {
            var baseUrl = txtBaseUrl.Text.Trim();
            var username = txtUsername.Text.Trim();
            var password = txtPassword.Password;

            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("请填写完整的服务器地址、用户名和密码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            txtStatus.Text = "正在测试登录...";
            try
            {
                _limisService.Configure(baseUrl, username, password);
                var result = await _limisService.LoginAsync(username, password, baseUrl);
                if (result.Success)
                {
                    var settings = _settingsService.LoadSettings();
                    settings.BaseUrl = baseUrl;
                    settings.Username = username;
                    if (!string.IsNullOrEmpty(password)) settings.Password = password;
                    _settingsService.SaveSettings(settings);
                    TryPersistRealName(result.RealName);
                    txtStatus.Text = "登录成功";
                    var shownName = GetInspectorName(_settingsService.LoadSettings());
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(shownName) ? "登录成功" : $"登录成功，主检：{shownName}",
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(result.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtStatus.Text = "登录失败";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"登录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "登录失败";
            }
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var existing = _settingsService.LoadSettings();
                var realName = string.IsNullOrWhiteSpace(txtRealName.Text) ? existing.RealName : txtRealName.Text.Trim();
                if (!IsValidPersonName(realName, txtUsername.Text.Trim()))
                    realName = IsValidPersonName(existing.RealName, txtUsername.Text.Trim()) ? existing.RealName : string.Empty;

                var settings = new AppSettings
                {
                    BaseUrl = txtBaseUrl.Text.Trim(),
                    Username = txtUsername.Text.Trim(),
                    Password = string.IsNullOrEmpty(txtPassword.Password) ? existing.Password : txtPassword.Password,
                    RealName = realName,
                    DefaultReportRemarks = existing.DefaultReportRemarks
                };
                if (_settingsService.SaveSettings(settings))
                {
                    _limisService.Configure(settings.BaseUrl, settings.Username, settings.Password);
                    txtStatus.Text = "设置已保存";
                    MessageBox.Show("设置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                    MessageBox.Show("保存设置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool IsValidPersonName(string? name, string? username)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            if (!string.IsNullOrWhiteSpace(username) &&
                string.Equals(trimmed, username.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
            if (trimmed.Length >= 11 && trimmed.All(char.IsDigit))
                return false;
            return true;
        }

        private string GetInspectorName(AppSettings settings)
        {
            var fromLimis = _limisService.RealName?.Trim();
            if (IsValidPersonName(fromLimis, settings.Username))
                return fromLimis!;
            var fromSettings = settings.RealName?.Trim();
            if (IsValidPersonName(fromSettings, settings.Username))
                return fromSettings!;
            return string.Empty;
        }

        private void TryPersistRealName(string? realNameFromLogin = null)
        {
            var settings = _settingsService.LoadSettings();
            var name = (realNameFromLogin ?? _limisService.RealName)?.Trim();
            if (!IsValidPersonName(name, settings.Username))
                return;

            settings.RealName = name!;
            txtRealName.Text = name;
            _settingsService.SaveSettings(settings);
        }

        protected override void OnClosed(EventArgs e)
        {
            _limisService.Dispose();
            base.OnClosed(e);
        }
    }
}