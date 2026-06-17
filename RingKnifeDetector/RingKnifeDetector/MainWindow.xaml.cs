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
        private readonly FieldSourceTracker _fieldTracker = new();
        private Dictionary<string, string> _draftInspectorMap = new(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();

            _calculationService = new CalculationService();
            _limisService = new LimisService();
            _wordExportService = new WordExportService();
            _draftService = new DraftService();
            _settingsService = new SettingsService();

            InitializeFieldSourceTracking();
            InitializeDefaultValues();
            LoadSettings();

            recordTable.DeleteBlockRequested += (_, idx) => RemoveSampleBlock(idx);
            recordTable.SamplesChanged += (_, _) => { /* data already in _currentSamples */ };
            recordTable.TestRangeEndChanged += OnTestRangeEndChanged;
            remarkViewer.TextChanged += (_, _) => _currentParams.LimisRemark = remarkViewer.Text;
            _lastTemplateIndex = cmbRecordTemplate.SelectedIndex;
        }

        private void InitializeFieldSourceTracking()
        {
            _fieldTracker.AttachIndicatorToGridCell(txtTestNature, "project.testNature", gridHeaderInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtEntrustNo, "project.entrustNo", gridHeaderInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtReportNo, "project.reportNo", gridHeaderInfo);

            _fieldTracker.AttachIndicatorToGridCell(txtEntrustUnit, "project.entrustUnit", gridProjectInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtContact, "project.contact", gridProjectInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtProjectName, "project.projectName", gridProjectInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtUnitAddress, "project.unitAddress", gridProjectInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtSupervisionUnit, "project.supervisionUnit", gridProjectInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtConstructionUnit, "project.constructionUnit", gridProjectInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtProjectAddress, "project.projectAddress", gridProjectInfo);
            _fieldTracker.AttachIndicatorToDatePicker(dpEntrustDate, "project.entrustDate", gridProjectInfo);
            _fieldTracker.AttachIndicatorToGridCell(txtProjectSection, "project.projectSection", gridProjectInfo);
            _fieldTracker.AttachIndicatorToDatePicker(dpReportDate, "project.reportDate", gridProjectInfo);

            _fieldTracker.AttachIndicatorToGridCell(txtSampleName, "params.sampleName", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtMaterialType, "params.materialType", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtRingSpec, "params.ringSpec", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtCompactionMethod, "params.compactionMethod", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtDesignRequirement, "params.designRequirement", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtMaxDryDensity, "params.maxDryDensity", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtTestLocation, "params.testLocation", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtOptimalMoisture, "params.optimalMoisture", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtTestBasis, "params.testBasis", gridParams);
            _fieldTracker.AttachIndicatorToGridCell(txtJudgeBasis, "params.judgeBasis", gridParams);
        }

        private void OnTestRangeEndChanged(string endDate)
        {
            _currentProject.ReportDate = endDate;
            SetDatePicker(dpReportDate, endDate);
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
                    _allTasks = ApplyTaskFilters(result.Tasks);
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
            ResetParamsForLimisFetch();
            _fieldTracker.ResetAll();
            _currentResults.Clear();
            dgResults.ItemsSource = null;
            txtConclusion.Text = string.Empty;
            _currentSamples.Clear();
            AddNewSample();

            _currentProject = result.Project!;
            _currentEntrustNo = entrustNo;
            FillProjectForm(_currentProject);
            MarkSystemFieldsFromLimis(result);

            if (!string.IsNullOrEmpty(result.SampleName))
            {
                _currentParams.SampleName = result.SampleName;
                txtSampleName.Text = result.SampleName;
            }

            if (!string.IsNullOrEmpty(result.Remark))
            {
                _currentParams.LimisRemark = result.Remark;
                remarkViewer.Text = result.Remark;
            }

            SanitizeRemarkPlaceholders(_currentProject, _currentParams);
            var parseResult = RemarkParser.FillMissing(_currentProject, _currentParams, _currentSamples, result.Remark);

            var draftLoaded = false;
            _fieldTracker.RunSuppressed(() =>
            {
                ApplyParsedFieldsToForm();
                draftLoaded = TryLoadDraft(entrustNo);
            });

            if (!draftLoaded)
                _fieldTracker.MarkRemark(parseResult.ExtractedFieldKeys);

            var remarkText = _currentParams.LimisRemark ?? result.Remark ?? string.Empty;
            remarkViewer.ApplyHighlights(remarkText, parseResult.Highlights);

            ApplyDefaultDatesFromProject();
            if (!string.IsNullOrEmpty(result.SampleNo))
                RenumberSampleNosFromPrefix(result.SampleNo);
            RefreshRecordTable();
        }

        private void MarkSystemFieldsFromLimis(LimisEntrustResponse result)
        {
            var keys = new List<string>();
            var p = _currentProject;
            if (!string.IsNullOrWhiteSpace(p.TestNature)) keys.Add("project.testNature");
            if (!string.IsNullOrWhiteSpace(p.EntrustNo)) keys.Add("project.entrustNo");
            if (!string.IsNullOrWhiteSpace(p.ReportNo)) keys.Add("project.reportNo");
            if (!string.IsNullOrWhiteSpace(p.EntrustUnit)) keys.Add("project.entrustUnit");
            if (!string.IsNullOrWhiteSpace(p.Contact)) keys.Add("project.contact");
            if (!string.IsNullOrWhiteSpace(p.ProjectName)) keys.Add("project.projectName");
            if (!string.IsNullOrWhiteSpace(p.UnitAddress)) keys.Add("project.unitAddress");
            if (!string.IsNullOrWhiteSpace(p.SupervisionUnit) && !RemarkParser.IsMissingValue(p.SupervisionUnit))
                keys.Add("project.supervisionUnit");
            if (!string.IsNullOrWhiteSpace(p.ConstructionUnit) && !RemarkParser.IsMissingValue(p.ConstructionUnit))
                keys.Add("project.constructionUnit");
            if (!string.IsNullOrWhiteSpace(p.ProjectAddress)) keys.Add("project.projectAddress");
            if (!string.IsNullOrWhiteSpace(p.EntrustDate)) keys.Add("project.entrustDate");
            if (!string.IsNullOrWhiteSpace(p.ProjectSection) && !RemarkParser.IsMissingValue(p.ProjectSection))
                keys.Add("project.projectSection");
            if (!string.IsNullOrWhiteSpace(p.ReportDate)) keys.Add("project.reportDate");
            if (!string.IsNullOrWhiteSpace(result.SampleName)) keys.Add("params.sampleName");
            _fieldTracker.MarkSystem(keys);
        }

        private List<TaskItem> ApplyTaskFilters(List<TaskItem> tasks)
        {
            _draftInspectorMap = _draftService.GetDraftInspectorMap();
            var statusFilter = (cmbTaskStatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;

            return tasks
                .Select(t =>
                {
                    t.DraftInspector = _draftInspectorMap.TryGetValue(t.TestingOrderNo, out var inspector)
                        ? inspector
                        : string.Empty;
                    return t;
                })
                .Where(t => MatchesTaskStatusFilter(t, statusFilter))
                .ToList();
        }

        private static bool MatchesTaskStatusFilter(TaskItem task, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            var status = task.StatusName?.Trim() ?? string.Empty;
            return filter switch
            {
                "已完成" => status.Contains("已完成", StringComparison.Ordinal),
                "进行中" => status.Contains("进行中", StringComparison.Ordinal),
                "待分配" => status.Contains("待分配", StringComparison.Ordinal),
                _ => true
            };
        }

        private RecordParams CreateDefaultParams() => new()
        {
            SoilType = "土",
            RingSpec = "200cm³",
            SampleName = "回填土",
            TestBasis = "JTG 3450-2019",
            JudgeBasis = "JTG 3450-2019",
            RecordTemplate = GetRingsPerBlock() == 3 ? "group3" : "group2",
            ResultType = "compaction_coeff"
        };

        private void ResetParamsForLimisFetch()
        {
            _currentParams = CreateDefaultParams();
            FillParamsForm();
        }

        private static void SanitizeRemarkPlaceholders(ProjectInfo project, RecordParams parameters)
        {
            if (RemarkParser.IsMissingValue(project.SupervisionUnit)) project.SupervisionUnit = string.Empty;
            if (RemarkParser.IsMissingValue(project.ConstructionUnit)) project.ConstructionUnit = string.Empty;
            if (RemarkParser.IsMissingValue(project.ProjectSection)) project.ProjectSection = string.Empty;
            if (RemarkParser.IsMissingValue(parameters.MaterialType)) parameters.MaterialType = string.Empty;
            if (RemarkParser.IsMissingValue(parameters.TestLocation)) parameters.TestLocation = string.Empty;
            if (RemarkParser.IsMissingValue(parameters.DesignRequirementText))
            {
                parameters.DesignRequirementText = string.Empty;
                parameters.DesignRequirement = null;
            }
        }

        private static string ExtractDesignNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var value = text.Trim();
            while (value.StartsWith('≥') || value.StartsWith('>'))
                value = value[1..].TrimStart('=');
            return value.TrimEnd('%', ' ').Trim();
        }

        private void FillParamsForm()
        {
            TextSanitizer.SanitizeParams(_currentParams);
            txtSampleName.Text = _currentParams.SampleName;
            txtMaterialType.Text = _currentParams.MaterialType;
            txtRingSpec.Text = _currentParams.RingSpec;
            txtCompactionMethod.Text = _currentParams.CompactionMethod;
            txtDesignRequirement.Text = !string.IsNullOrWhiteSpace(_currentParams.DesignRequirementText)
                ? ExtractDesignNumber(_currentParams.DesignRequirementText)
                : _currentParams.DesignRequirement?.ToString() ?? string.Empty;
            txtMaxDryDensity.Text = _currentParams.MaxDryDensity?.ToString() ?? string.Empty;
            txtTestLocation.Text = _currentParams.TestLocation;
            txtOptimalMoisture.Text = _currentParams.OptimalMoisture?.ToString() ?? string.Empty;
            txtTestBasis.Text = _currentParams.TestBasis;
            txtJudgeBasis.Text = _currentParams.JudgeBasis;
            remarkViewer.Text = _currentParams.LimisRemark;
            rbCompactionPercent.IsChecked = _currentParams.ResultType == "compaction_percent";
            rbCompactionCoeff.IsChecked = _currentParams.ResultType != "compaction_percent";
        }

        private void ApplyParsedFieldsToForm()
        {
            txtSupervisionUnit.Text = _currentProject.SupervisionUnit;
            txtConstructionUnit.Text = _currentProject.ConstructionUnit;
            txtProjectSection.Text = _currentProject.ProjectSection;
            FillParamsForm();
        }

        private void ApplyDefaultDatesFromProject()
        {
            var start = DateHelper.Normalize(_currentProject.EntrustDate);
            var end = DateHelper.Normalize(_currentProject.ReportDate);
            if (string.IsNullOrEmpty(start) && string.IsNullOrEmpty(end)) return;

            if (string.IsNullOrEmpty(end)) end = start;
            if (string.IsNullOrEmpty(start)) start = end;

            var testRange = DateHelper.FormatRange(start, end);
            foreach (var sample in _currentSamples)
            {
                sample.SamplingDate = start;
                sample.TestDate = testRange;
            }

            _currentProject.ReportDate = end;
            SetDatePicker(dpReportDate, end);
        }

        private bool TryLoadDraft(string entrustNo)
        {
            var loaded = _draftService.LoadDraft(entrustNo);
            if (!loaded.Success || loaded.Draft == null) return false;

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
                remarkViewer.Text = d.Params.LimisRemark;
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
            return true;
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
            TextSanitizer.SanitizeProject(p);
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
            _fieldTracker.ResetAll();

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
            remarkViewer.Text = "";
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
            _currentParams.LimisRemark = remarkViewer.Text;

            if (decimal.TryParse(txtMaxDryDensity.Text, out var maxDD)) _currentParams.MaxDryDensity = maxDD;
            else _currentParams.MaxDryDensity = null;

            var designInput = ExtractDesignNumber(txtDesignRequirement.Text);
            if (decimal.TryParse(designInput, out var dr)) _currentParams.DesignRequirement = dr;
            else _currentParams.DesignRequirement = null;

            if (!string.IsNullOrWhiteSpace(designInput))
            {
                var isPercent = _currentParams.ResultType == "compaction_percent"
                    || rbCompactionPercent.IsChecked == true;
                _currentParams.DesignRequirementText = isPercent
                    ? $"≥{designInput.TrimEnd('%', '％')}%"
                    : $"≥{designInput}";
            }
            else if (string.IsNullOrWhiteSpace(txtDesignRequirement.Text))
            {
                _currentParams.DesignRequirementText = string.Empty;
            }

            if (decimal.TryParse(txtOptimalMoisture.Text, out var om)) _currentParams.OptimalMoisture = om;
            else _currentParams.OptimalMoisture = null;

            TextSanitizer.SanitizeProject(_currentProject);
            TextSanitizer.SanitizeParams(_currentParams);
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
                    var savedPath = _wordExportService.ExportToWord(
                        _currentProject, _currentParams, _currentResults, txtConclusion.Text,
                        txtReportRemarks.Text, inspector, dlg.FileName);
                    txtStatus.Text = "导出成功";
                    if (!string.Equals(savedPath, dlg.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(
                            $"目标文件正在被其他程序使用，已另存为：\n\n{savedPath}",
                            "导出成功",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"导出成功\n\n{savedPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    var message = ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("正在由另一进程使用", StringComparison.OrdinalIgnoreCase)
                        ? "导出失败: 无法写入文件，请关闭正在打开的 Word 文档后重试。"
                        : $"导出失败: {ex.Message}";
                    MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var settings = _settingsService.LoadSettings();
                var inspector = GetInspectorName(settings);
                var draft = new DraftSaveRequest
                {
                    Project = _currentProject,
                    Params = _currentParams,
                    Samples = _currentSamples,
                    CalcResults = _currentResults,
                    OverallConclusion = txtConclusion.Text,
                    ReportRemarks = txtReportRemarks.Text,
                    SavedByInspector = inspector
                };
                var result = _draftService.SaveDraft(_currentEntrustNo, draft);
                if (result.Success)
                {
                    settings.DefaultReportRemarks = txtReportRemarks.Text;
                    _settingsService.SaveSettings(settings);
                    _draftInspectorMap[_currentEntrustNo] = inspector;
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