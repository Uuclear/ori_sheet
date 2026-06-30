using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using RingKnifeDetector.Helpers;
using RingKnifeDetector.Views;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector
{
    public partial class MainWindow : Window
    {
        private readonly CalculationService _calculationService;
        private readonly LimisService _limisService;
        private readonly WordExportService _wordExportService;
        private readonly RecordWordExportService _recordWordExportService;
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
            _recordWordExportService = new RecordWordExportService();
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
            _fieldTracker.AttachIndicatorToGridCell(fieldEntrustDate, "project.entrustDate", gridProjectInfo, fieldEntrustDate);
            _fieldTracker.AttachIndicatorToGridCell(txtProjectSection, "project.projectSection", gridProjectInfo);
            _fieldTracker.AttachIndicatorToGridCell(fieldReportDate, "project.reportDate", gridProjectInfo, fieldReportDate);

            _fieldTracker.AttachSideIndicator(txtSampleName, "params.sampleName", gridParams, 2, txtSampleName);
            _fieldTracker.AttachSideIndicator(txtMaterialType, "params.materialType", gridParams, 5, txtMaterialType);
            _fieldTracker.AttachSideIndicator(txtRingSpec, "params.ringSpec", gridParams, 2, txtRingSpec);
            _fieldTracker.AttachSideIndicator(txtCompactionMethod, "params.compactionMethod", gridParams, 5, txtCompactionMethod);
            _fieldTracker.AttachSideIndicator(
                (FrameworkElement)txtDesignRequirement.Parent!, "params.designRequirement", gridParams, 2, txtDesignRequirement);
            _fieldTracker.AttachSideIndicator(txtMaxDryDensity, "params.maxDryDensity", gridParams, 5, txtMaxDryDensity);
            _fieldTracker.AttachSideIndicator(txtTestLocation, "params.testLocation", gridParams, 2, txtTestLocation);
            _fieldTracker.AttachSideIndicator(txtOptimalMoisture, "params.optimalMoisture", gridParams, 5, txtOptimalMoisture);
            _fieldTracker.AttachSideIndicator(
                (FrameworkElement)txtTestBasis.Parent!, "params.testBasis", gridParams, 2, txtTestBasis);
            _fieldTracker.AttachSideIndicator(txtJudgeBasis, "params.judgeBasis", gridParams, 5, txtJudgeBasis);
        }

        private void OnTestRangeEndChanged(string endDate)
        {
            _currentProject.ReportDate = DateHelper.Normalize(endDate);
            fieldReportDate.SetDate(_currentProject.ReportDate);
        }

        private int GetRingsPerBlock() =>
            (cmbRecordTemplate.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "group3" ? 3 : 2;

        private void RefreshRecordTable()
        {
            var globalSampling = _currentSamples.FirstOrDefault()?.SamplingDate ?? DateHelper.FormatOrToday(DateTime.Today);
            var globalTest = DateHelper.EnsureRangeFormat(_currentSamples.FirstOrDefault()?.TestDate)
                ?? DateHelper.FormatRange(DateTime.Today, DateTime.Today);
            recordTable.Configure(
                _currentSamples, _currentParams, _currentResults,
                GetRingsPerBlock(),
                rbCompactionPercent.IsChecked == true ? "compaction_percent" : "compaction_coeff",
                globalSampling, globalTest);
            RefreshResultsGrid();
        }

        private void RefreshResultsGrid()
        {
            _currentParams.RecordTemplate = GetRingsPerBlock() == 3 ? "group3" : "group2";
            _currentParams.ResultType = rbCompactionPercent.IsChecked == true ? "compaction_percent" : "compaction_coeff";
            resultsTable.Configure(_currentResults, _currentParams);
        }

        private void InitializeDefaultValues()
        {
            _currentParams.MaxDryDensity = 1.92m;
            _currentParams.DesignRequirement = 0.93m;
            _currentParams.SoilType = "土";
            _currentParams.CompactionMethod = ReportDefaults.MissingFieldPlaceholder;
            _currentParams.JudgeBasis = ReportDefaults.DefaultJudgeBasis;
            _currentParams.RingSpec = "200cm³";
            _currentParams.SampleName = "回填土";
            _currentParams.RecordTemplate = "group2";

            AddNewSample();
            FillParamsForm();
            RefreshRecordTable();
        }

        private RingKnifeSample CreateSample(string sampleNo)
        {
            var rings = GetRingsPerBlock();
            var sample = new RingKnifeSample
            {
                SampleNo = sampleNo,
                SamplingDate = DateHelper.FormatOrToday(DateTime.Today),
                TestDate = DateHelper.FormatRange(DateTime.Today, DateTime.Today),
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
            var sample = CreateSample(sampleNo);
            if (_currentSamples.Count > 0)
            {
                var first = _currentSamples[0];
                sample.SamplingDate = first.SamplingDate;
                sample.TestDate = first.TestDate;
                sample.Thickness = first.Thickness;
            }
            _currentSamples.Add(sample);
        }

        private static void SyncSampleDatesFromFirst(List<RingKnifeSample> samples)
        {
            if (samples.Count <= 1) return;
            var first = samples[0];
            var sampling = DateHelper.Normalize(first.SamplingDate);
            var test = DateHelper.EnsureRangeFormat(first.TestDate) ?? first.TestDate;
            var thickness = first.Thickness;
            foreach (var sample in samples.Skip(1))
            {
                sample.SamplingDate = sampling;
                sample.TestDate = test;
                sample.Thickness = thickness;
            }
        }

        private void SyncResultSharedFieldsFromFirstSample()
        {
            if (_currentResults.Count == 0 || _currentSamples.Count == 0) return;
            var sampling = DateHelper.Normalize(_currentSamples[0].SamplingDate);
            var test = DateHelper.EnsureRangeFormat(_currentSamples[0].TestDate) ?? _currentSamples[0].TestDate;
            var thickness = _currentSamples[0].Thickness;
            foreach (var result in _currentResults)
            {
                result.SamplingDate = sampling;
                result.TestDate = test;
                result.Thickness = thickness;
            }
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
            txtApprover.Text = "";
            txtReviewer.Text = "";
            txtRecordInspector.Text = "";
            ApplyDefaultExportFields();

            ApplyDefaultExportFields(settings);
        }

        private void ApplySavedJudgeBasisFromSettings()
        {
            var saved = _settingsService.LoadSettings().DefaultJudgeBasis;
            if (string.IsNullOrWhiteSpace(saved)) return;
            _currentParams.JudgeBasis = saved;
            txtJudgeBasis.Text = saved;
        }

        private void ApplyDefaultExportFields(AppSettings? settings = null)
        {
            settings ??= _settingsService.LoadSettings();
            if (string.IsNullOrWhiteSpace(txtApprover.Text))
                txtApprover.Text = settings.DefaultApprover;
            if (string.IsNullOrWhiteSpace(txtReviewer.Text))
                txtReviewer.Text = settings.DefaultReviewer;
            if (string.IsNullOrWhiteSpace(txtRecordInspector.Text))
                txtRecordInspector.Text = GetInspectorName(settings);
            if (string.IsNullOrWhiteSpace(txtJudgeBasis.Text)
                || txtJudgeBasis.Text == ReportDefaults.MissingFieldPlaceholder)
            {
                txtJudgeBasis.Text = string.IsNullOrWhiteSpace(settings.DefaultJudgeBasis)
                    ? ReportDefaults.DefaultJudgeBasis
                    : settings.DefaultJudgeBasis;
            }
        }

        private void PersistExportDefaults()
        {
            var settings = _settingsService.LoadSettings();
            settings.DefaultApprover = txtApprover.Text.Trim();
            settings.DefaultReviewer = txtReviewer.Text.Trim();
            settings.DefaultJudgeBasis = txtJudgeBasis.Text.Trim();
            _settingsService.SaveSettings(settings);
        }

        private static void SetDateField(ChineseDateField field, string? text) => field.SetDate(text);

        private static string GetDateFieldValue(ChineseDateField field) => field.NormalizedValue;

        private void ApplySignatureFieldsFromDraft(DraftSaveRequest draft)
        {
            if (!string.IsNullOrWhiteSpace(draft.SavedApprover))
                txtApprover.Text = draft.SavedApprover;
            if (!string.IsNullOrWhiteSpace(draft.SavedReviewer))
                txtReviewer.Text = draft.SavedReviewer;
            if (!string.IsNullOrWhiteSpace(draft.SavedByInspector))
                txtRecordInspector.Text = draft.SavedByInspector;
            else
                ApplyDefaultExportFields();
        }

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
            txtConclusion.Text = string.Empty;
            _currentSamples.Clear();
            AddNewSample();

            _currentProject = result.Project!;
            _currentEntrustNo = entrustNo;
            FillProjectForm(_currentProject);
            UpdateWitnessFieldLabels(_currentProject.TestNature);
            ApplyLimisParamFields(result);

            if (!string.IsNullOrEmpty(result.Remark))
            {
                _currentParams.LimisRemark = result.Remark;
                remarkViewer.Text = result.Remark;
            }

            SanitizeRemarkPlaceholders(_currentProject, _currentParams);
            var parseResult = RemarkParser.FillMissing(_currentProject, _currentParams, _currentSamples, result.Remark);
            ApplySavedJudgeBasisFromSettings();
            ResultTypeHelper.SyncFromDesignText(_currentParams);

            _fieldTracker.RunSuppressed(() =>
            {
                if (!string.IsNullOrEmpty(result.SampleName))
                {
                    _currentParams.SampleName = result.SampleName;
                    txtSampleName.Text = result.SampleName;
                }

                ApplyParsedFieldsToForm();
                TryLoadDraft(entrustNo);
            });

            var remarkText = _currentParams.LimisRemark ?? result.Remark ?? string.Empty;
            remarkViewer.ApplyHighlights(remarkText, parseResult.Highlights);

            _fieldTracker.RunSuppressed(() =>
            {
                ApplyDefaultDatesFromProject(
                    keepExistingSamplingDate: parseResult.ExtractedFieldKeys.Contains("sample.samplingDate"));
                if (!string.IsNullOrEmpty(result.SampleNo))
                    RenumberSampleNosFromPrefix(result.SampleNo);
                ApplyDefaultExportFields();
            });

            _fieldTracker.ScheduleFinalizeSources(
                parseResult.ExtractedFieldKeys,
                () => MarkSystemFieldsFromLimis(result),
                () =>
                {
                    if (result.SampleNameFromHtml && !string.IsNullOrWhiteSpace(result.SampleName))
                        _fieldTracker.ForceSource("params.sampleName", FieldSource.System);
                });

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
            if (result.SampleNameFromHtml && !string.IsNullOrWhiteSpace(result.SampleName))
                keys.Add("params.sampleName");
            if (!string.IsNullOrWhiteSpace(result.TestBasis))
                keys.Add("params.testBasis");
            if (!string.IsNullOrWhiteSpace(result.TypeSpecification)) keys.Add("params.ringSpec");
            _fieldTracker.MarkSystem(keys);
        }

        private void UpdateWitnessFieldLabels(string? testNature = null)
        {
            var isWitness = TestNatureHelper.IsWitnessSampling(testNature ?? txtTestNature.Text);
            lblSupervisionUnit.Text = isWitness ? "工程见证：" : "监理单位：";
            lblConstructionUnit.Text = isWitness ? "样品取样：" : "施工单位：";
            lblRingSpec.Text = isWitness ? "规格型号：" : "环刀规格：";
            FieldLabels.SetWitnessSamplingMode(isWitness);
        }

        private void ApplyLimisParamFields(LimisEntrustResponse result)
        {
            if (!string.IsNullOrWhiteSpace(result.TestBasis))
                SetBasisFromLimis(result.TestBasis);

            if (!result.IsWitnessSampling) return;

            if (!string.IsNullOrWhiteSpace(result.TypeSpecification))
            {
                _currentParams.RingSpec = result.TypeSpecification;
                txtRingSpec.Text = result.TypeSpecification;
            }
        }

        private void SetBasisFromLimis(string fullBasis)
        {
            _currentParams.TestBasisFull = TestBasisNormalizer.Normalize(fullBasis);
            ApplyBasisDisplay();
        }

        private void ApplyBasisDisplay()
        {
            var source = !string.IsNullOrWhiteSpace(_currentParams.TestBasisFull)
                ? _currentParams.TestBasisFull
                : _currentParams.TestBasis;
            var display = TestBasisNormalizer.ToDisplay(source, _currentParams.UseFullBasisName);
            if (string.IsNullOrEmpty(display))
                display = source;

            _fieldTracker.RunSuppressed(() =>
            {
                tglFullBasisName.IsChecked = _currentParams.UseFullBasisName;
                _currentParams.TestBasis = display;
                txtTestBasis.Text = display;
            });
        }

        private void TglFullBasisName_Changed(object sender, RoutedEventArgs e)
        {
            _currentParams.UseFullBasisName = tglFullBasisName.IsChecked == true;
            ApplyBasisDisplay();
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
            JudgeBasis = ReportDefaults.DefaultJudgeBasis,
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
            var value = text.Trim().Replace('％', '%');
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
            ApplyBasisDisplay();
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

        private void ApplyDefaultDatesFromProject(bool keepExistingSamplingDate = false)
        {
            var start = DateHelper.Normalize(_currentProject.EntrustDate);
            var end = DateHelper.Normalize(_currentProject.ReportDate);
            if (string.IsNullOrEmpty(start) && string.IsNullOrEmpty(end)) return;

            if (string.IsNullOrEmpty(end)) end = start;
            if (string.IsNullOrEmpty(start)) start = end;

            var testRange = DateHelper.FormatRange(start, end);
            foreach (var sample in _currentSamples)
            {
                if (!keepExistingSamplingDate)
                    sample.SamplingDate = start;
                sample.TestDate = testRange;
            }

            _currentProject.ReportDate = end;
            fieldReportDate.SetDate(end);
            fieldEntrustDate.SetDate(start);
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
                ApplyBasisDisplay();
                txtJudgeBasis.Text = d.Params.JudgeBasis;
                remarkViewer.Text = d.Params.LimisRemark;
                txtMaxDryDensity.Text = d.Params.MaxDryDensity?.ToString() ?? "";
                txtDesignRequirement.Text = d.Params.DesignRequirement?.ToString() ?? "";
                txtOptimalMoisture.Text = d.Params.OptimalMoisture?.ToString() ?? "";
            }
            if (d.Samples.Count > 0)
            {
                _currentSamples = d.Samples;
                SyncSampleDatesFromFirst(_currentSamples);
                RefreshRecordTable();
            }
            if (d.CalcResults.Count > 0)
            {
                _currentResults = d.CalcResults;
                SyncResultSharedFieldsFromFirstSample();
                RefreshResultsGrid();
            }
            if (!string.IsNullOrEmpty(d.OverallConclusion))
                txtConclusion.Text = d.OverallConclusion;
            if (!string.IsNullOrWhiteSpace(d.ReportRemarks))
                txtReportRemarks.Text = d.ReportRemarks;
            ApplySignatureFieldsFromDraft(d);
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
            SetDateField(fieldEntrustDate, p.EntrustDate);
            txtProjectSection.Text = p.ProjectSection;
            SetDateField(fieldReportDate, p.ReportDate);
            UpdateWitnessFieldLabels(p.TestNature);
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
            if (!string.IsNullOrWhiteSpace(remarkViewer.Text))
            {
                var parseResult = RemarkParser.FillMissing(_currentProject, _currentParams, _currentSamples, remarkViewer.Text);
                ResultTypeHelper.SyncFromDesignText(_currentParams);
                _fieldTracker.RunSuppressed(SyncParamsToUi);
                _fieldTracker.ScheduleFinalizeSources(parseResult.ExtractedFieldKeys);
            }
            txtConclusion.Text = "";
            RefreshRecordTable();
        }

        private void SyncParamsToUi()
        {
            txtCompactionMethod.Text = _currentParams.CompactionMethod;
            txtDesignRequirement.Text = _currentParams.DesignRequirementText;
            txtMaxDryDensity.Text = _currentParams.MaxDryDensity?.ToString() ?? string.Empty;
            txtOptimalMoisture.Text = _currentParams.OptimalMoisture?.ToString() ?? string.Empty;
            txtTestLocation.Text = _currentParams.TestLocation;
            txtMaterialType.Text = _currentParams.MaterialType;
        }

        private void ResultType_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _currentParams.ResultType = rbCompactionPercent.IsChecked == true ? "compaction_percent" : "compaction_coeff";
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
            fieldEntrustDate.SetDate(string.Empty);
            txtProjectSection.Text = "";
            fieldReportDate.SetDate(string.Empty);
            txtSampleName.Text = "回填土";
            txtMaterialType.Text = "";
            txtRingSpec.Text = "200cm³";
            txtCompactionMethod.Text = ReportDefaults.MissingFieldPlaceholder;
            txtDesignRequirement.Text = "";
            txtMaxDryDensity.Text = "";
            txtTestLocation.Text = "";
            txtOptimalMoisture.Text = "";
            _currentParams.TestBasisFull = string.Empty;
            _currentParams.UseFullBasisName = false;
            _currentParams.JudgeBasis = ReportDefaults.DefaultJudgeBasis;
            ApplyBasisDisplay();
            txtJudgeBasis.Text = _currentParams.JudgeBasis;
            txtConclusion.Text = "";
            remarkViewer.Text = "";
            var savedRemarks = _settingsService.LoadSettings().DefaultReportRemarks;
            txtReportRemarks.Text = string.IsNullOrWhiteSpace(savedRemarks)
                ? ReportDefaults.DefaultReportRemarks
                : savedRemarks;
            rbCompactionCoeff.IsChecked = true;

            _currentSamples.Clear();
            InitializeDefaultValues();
            txtStatus.Text = "已清空所有数据";
        }

        // ========== Calculate ==========
        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateCurrentParams();
                UpdateCurrentSamples();
                SyncSampleDatesFromFirst(_currentSamples);
                foreach (var sample in _currentSamples)
                {
                    sample.SamplingDate = DateHelper.Normalize(sample.SamplingDate);
                    sample.TestDate = DateHelper.EnsureRangeFormat(sample.TestDate);
                }

                var request = new CalcRequest { Params = _currentParams, Samples = _currentSamples };
                var response = _calculationService.CalculateAll(request);
                _currentResults = response.Results;
                SyncResultSharedFieldsFromFirstSample();
                RefreshResultsGrid();
                txtConclusion.Text = response.OverallConclusion;
                RefreshRecordTable();
                txtStatus.Text = $"计算完成，共{response.Results.Count}个测点";
                PersistExportDefaults();
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
            _currentProject.EntrustDate = GetDateFieldValue(fieldEntrustDate);
            _currentProject.SupervisionUnit = txtSupervisionUnit.Text;
            _currentProject.ConstructionUnit = txtConstructionUnit.Text;
            _currentProject.UnitAddress = txtUnitAddress.Text;
            _currentProject.ProjectAddress = txtProjectAddress.Text;
            _currentProject.ProjectSection = txtProjectSection.Text;
            _currentProject.ReportDate = GetDateFieldValue(fieldReportDate);

            _currentParams.SampleName = txtSampleName.Text;
            _currentParams.MaterialType = txtMaterialType.Text;
            _currentParams.RingSpec = txtRingSpec.Text;
            _currentParams.CompactionMethod = txtCompactionMethod.Text;
            _currentParams.TestLocation = txtTestLocation.Text;
            _currentParams.UseFullBasisName = tglFullBasisName.IsChecked == true;
            _currentParams.TestBasis = txtTestBasis.Text;
            _currentParams.JudgeBasis = txtJudgeBasis.Text;
            _currentParams.RecordTemplate = GetRingsPerBlock() == 3 ? "group3" : "group2";
            _currentParams.LimisRemark = remarkViewer.Text;

            if (decimal.TryParse(txtMaxDryDensity.Text, out var maxDD)) _currentParams.MaxDryDensity = maxDD;
            else _currentParams.MaxDryDensity = null;

            var designInput = ExtractDesignNumber(txtDesignRequirement.Text);
            if (decimal.TryParse(designInput, out var dr)) _currentParams.DesignRequirement = dr;
            else _currentParams.DesignRequirement = null;

            var rawDesignText = txtDesignRequirement.Text.Trim();
            if (rawDesignText.Contains("压实度", StringComparison.Ordinal))
                _currentParams.ResultType = "compaction_percent";
            else if (rawDesignText.Contains("压实系数", StringComparison.Ordinal)
                     || rawDesignText.Contains("固体体积率", StringComparison.Ordinal))
                _currentParams.ResultType = "compaction_coeff";
            else
                _currentParams.ResultType = rbCompactionPercent.IsChecked == true ? "compaction_percent" : "compaction_coeff";

            if (!string.IsNullOrWhiteSpace(designInput))
            {
                var isPercent = _currentParams.ResultType == "compaction_percent";
                _currentParams.DesignRequirementText = isPercent
                    ? $"≥{designInput.TrimEnd('%', '％')}%"
                    : $"≥{designInput}";
            }
            else if (string.IsNullOrWhiteSpace(txtDesignRequirement.Text))
            {
                _currentParams.DesignRequirementText = string.Empty;
            }

            ResultTypeHelper.SyncFromDesignText(_currentParams);
            rbCompactionPercent.IsChecked = _currentParams.ResultType == "compaction_percent";
            rbCompactionCoeff.IsChecked = _currentParams.ResultType != "compaction_percent";

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

        private string GetExportRecordFileName()
        {
            var reportNo = txtReportNo.Text?.Trim();
            if (!string.IsNullOrEmpty(reportNo))
                return $"{reportNo}原始记录.docx";
            var entrustNo = txtEntrustNo.Text?.Trim();
            if (!string.IsNullOrEmpty(entrustNo))
                return $"{entrustNo}原始记录.docx";
            return $"环刀法原始记录_{DateTime.Now:yyyyMMddHHmmss}.docx";
        }

        // ========== Export Record Word ==========
        private void BtnExportRecord_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSamples.Count == 0)
            {
                MessageBox.Show("暂无原始记录数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Word文件|*.docx",
                Title = "导出原始记录",
                FileName = GetExportRecordFileName()
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                UpdateCurrentParams();
                txtStatus.Text = "正在导出原始记录...";
                var inspector = txtRecordInspector.Text.Trim();
                var reviewer = txtReviewer.Text.Trim();
                var reminders = WordExportReminderService.CollectReminders(
                    _currentProject, _currentParams, _currentResults, txtConclusion.Text);
                var savedPath = _recordWordExportService.ExportRecord(
                    _currentSamples, _currentParams, _currentResults,
                    inspector, reviewer, dlg.FileName);
                PersistExportDefaults();
                txtStatus.Text = "原始记录导出成功";
                ShowExportSuccessMessage(savedPath, dlg.FileName, reminders);
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
                    var inspector = txtRecordInspector.Text.Trim();
                    var conclusion = txtConclusion.Text;
                    var reminders = WordExportReminderService.CollectReminders(
                        _currentProject, _currentParams, _currentResults, conclusion);
                    var savedPath = _wordExportService.ExportToWord(
                        _currentProject, _currentParams, _currentResults, conclusion,
                        txtReportRemarks.Text,
                        txtApprover.Text.Trim(),
                        txtReviewer.Text.Trim(),
                        inspector,
                        dlg.FileName);
                    PersistExportDefaults();
                    txtStatus.Text = "导出成功";
                    ShowExportSuccessMessage(savedPath, dlg.FileName, reminders);
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

        private static void ShowExportSuccessMessage(
            string savedPath,
            string requestedPath,
            IReadOnlyList<string> reminders)
        {
            var message = new System.Text.StringBuilder();
            if (!string.Equals(savedPath, requestedPath, StringComparison.OrdinalIgnoreCase))
                message.AppendLine("目标文件正在被其他程序使用，已另存为：");
            else
                message.AppendLine("导出成功");

            message.AppendLine();
            message.AppendLine(savedPath);

            if (reminders.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("【导出提醒】（不影响导出）");
                foreach (var item in reminders)
                    message.AppendLine($"• {item}");
            }

            var icon = reminders.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
            MessageBox.Show(message.ToString(), "导出成功", MessageBoxButton.OK, icon);
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
                var inspector = txtRecordInspector.Text.Trim();
                var draft = new DraftSaveRequest
                {
                    Project = _currentProject,
                    Params = _currentParams,
                    Samples = _currentSamples,
                    CalcResults = _currentResults,
                    OverallConclusion = txtConclusion.Text,
                    ReportRemarks = txtReportRemarks.Text,
                    SavedByInspector = inspector,
                    SavedApprover = txtApprover.Text.Trim(),
                    SavedReviewer = txtReviewer.Text.Trim()
                };
                var result = _draftService.SaveDraft(_currentEntrustNo, draft);
                if (result.Success)
                {
                    settings.DefaultReportRemarks = txtReportRemarks.Text;
                    _settingsService.SaveSettings(settings);
                    PersistExportDefaults();
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