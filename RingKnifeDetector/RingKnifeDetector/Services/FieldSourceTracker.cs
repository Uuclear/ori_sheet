using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RingKnifeDetector.Models;
using RingKnifeDetector.Views;

namespace RingKnifeDetector.Services
{
    public class FieldSourceTracker
    {
        private static readonly SolidColorBrush Green = Freeze(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly SolidColorBrush Blue = Freeze(Color.FromRgb(0x21, 0x96, 0xF3));
        private static readonly SolidColorBrush Yellow = Freeze(Color.FromRgb(0xFF, 0xC1, 0x07));
        private static readonly SolidColorBrush Transparent = Brushes.Transparent;

        private readonly Dictionary<string, FieldSource> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _systemReferences = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _remarkReferences = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (FrameworkElement input, Border indicator)> _bindings = new(StringComparer.Ordinal);
        private bool _suppressManual;

        private static SolidColorBrush Freeze(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public void Register(string key, FrameworkElement input, Border indicator)
        {
            _bindings[key] = (input, indicator);
            if (input is TextBox tb)
                tb.TextChanged += (_, _) => OnInputChanged(key);
            else if (input is DatePicker dp)
                dp.SelectedDateChanged += (_, _) => OnInputChanged(key);
            else if (input is ChineseDateField dateField)
                dateField.ValueChanged += (_, _) => OnInputChanged(key);
        }

        public void AttachIndicatorToGridCell(TextBox textBox, string key, Panel parentGrid)
        {
            if (textBox.Parent is Grid inner && ReferenceEquals(inner.Parent, parentGrid))
                AttachIndicatorToContainer(inner, key, parentGrid, textBox);
            else
                AttachIndicatorToContainer(textBox, key, parentGrid, textBox);
        }

        public void AttachIndicatorToGridCell(FrameworkElement content, string key, Panel parentGrid, FrameworkElement inputForManual)
        {
            if (content.Parent is Grid inner && ReferenceEquals(inner.Parent, parentGrid))
                AttachIndicatorToContainer(inner, key, parentGrid, inputForManual);
            else
                AttachIndicatorToContainer(content, key, parentGrid, inputForManual);
        }

        public void AttachIndicatorToDatePicker(DatePicker picker, string key, Panel parentGrid) =>
            AttachIndicatorToContainer(picker, key, parentGrid, picker);

        public void AttachSideIndicator(
            FrameworkElement content,
            string key,
            Panel parentGrid,
            int indicatorColumn,
            FrameworkElement inputForManual)
        {
            var row = Grid.GetRow(content);
            var indicator = new Border
            {
                Width = 8,
                MinHeight = 18,
                Margin = new Thickness(1, 2, 0, 2),
                Background = Transparent,
                ToolTip = "未填写"
            };
            Grid.SetRow(indicator, row);
            Grid.SetColumn(indicator, indicatorColumn);
            parentGrid.Children.Add(indicator);
            Register(key, inputForManual, indicator);
        }

        private void AttachIndicatorToContainer(
            FrameworkElement content,
            string key,
            Panel parentGrid,
            FrameworkElement inputForManual)
        {
            var row = Grid.GetRow(content);
            var col = Grid.GetColumn(content);
            var colspan = Grid.GetColumnSpan(content);
            parentGrid.Children.Remove(content);

            var host = new Grid();
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            Grid.SetRow(host, row);
            Grid.SetColumn(host, col);
            if (colspan > 1) Grid.SetColumnSpan(host, colspan);

            Grid.SetColumn(content, 0);
            host.Children.Add(content);

            var indicator = new Border
            {
                Width = 8,
                MinHeight = 18,
                Margin = new Thickness(2, 2, 0, 2),
                Background = Transparent,
                ToolTip = "未填写"
            };
            Grid.SetColumn(indicator, 1);
            host.Children.Add(indicator);

            parentGrid.Children.Add(host);
            Register(key, inputForManual, indicator);
        }

        public void ForceSource(string key, FieldSource source)
        {
            if (_bindings.TryGetValue(key, out var pair))
            {
                var value = ReadFieldValue(pair.input);
                if (source == FieldSource.System)
                {
                    _systemReferences[key] = value;
                    _remarkReferences.Remove(key);
                }
                else if (source == FieldSource.Remark && !_systemReferences.ContainsKey(key))
                    _remarkReferences[key] = value;
            }

            SetSource(key, source);
        }

        public void MarkSystem(IEnumerable<string> keys)
        {
            _suppressManual = true;
            try
            {
                foreach (var key in keys)
                {
                    if (!_bindings.TryGetValue(key, out var pair)) continue;
                    _systemReferences[key] = ReadFieldValue(pair.input);
                    _remarkReferences.Remove(key);
                    SetSource(key, FieldSource.System);
                }
            }
            finally
            {
                _suppressManual = false;
            }
        }

        public void MarkRemark(IEnumerable<string> keys)
        {
            _suppressManual = true;
            try
            {
                foreach (var key in keys)
                {
                    if (!_bindings.TryGetValue(key, out var pair)) continue;
                    if (_systemReferences.ContainsKey(key)) continue;
                    _remarkReferences[key] = ReadFieldValue(pair.input);
                    SetSource(key, FieldSource.Remark);
                }
            }
            finally
            {
                _suppressManual = false;
            }
        }

        public void MarkUnmarkedAsManual()
        {
            _suppressManual = true;
            try
            {
                foreach (var key in _bindings.Keys)
                {
                    var source = _sources.GetValueOrDefault(key, FieldSource.None);
                    if (source is FieldSource.System or FieldSource.Remark) continue;
                    if (_systemReferences.ContainsKey(key) || _remarkReferences.ContainsKey(key))
                    {
                        ReconcileSource(key);
                        continue;
                    }

                    SetSource(key, FieldSource.Manual);
                }
            }
            finally
            {
                _suppressManual = false;
            }
        }

        public void ScheduleFinalizeSources(
            IEnumerable<string> remarkKeys,
            Action? markSystem = null,
            Action? afterFinalize = null)
        {
            var list = remarkKeys.Where(k => _bindings.ContainsKey(k)).ToList();

            void Apply()
            {
                markSystem?.Invoke();
                MarkRemark(list);
                MarkUnmarkedAsManual();
                afterFinalize?.Invoke();
            }

            if (Application.Current?.Dispatcher is { } dispatcher)
                dispatcher.BeginInvoke(Apply, DispatcherPriority.ApplicationIdle);
            else
                Apply();
        }

        public void RunSuppressed(Action action)
        {
            _suppressManual = true;
            try
            {
                action();
            }
            finally
            {
                _suppressManual = false;
            }
        }

        public void ResetAll()
        {
            _sources.Clear();
            _systemReferences.Clear();
            _remarkReferences.Clear();
            foreach (var key in _bindings.Keys)
                RefreshIndicator(key);
        }

        private void OnInputChanged(string key)
        {
            if (_suppressManual) return;
            ReconcileSource(key);
        }

        private void ReconcileSource(string key)
        {
            if (!_bindings.TryGetValue(key, out var pair)) return;

            var current = ReadFieldValue(pair.input);
            if (_systemReferences.TryGetValue(key, out var systemValue)
                && string.Equals(current, systemValue, StringComparison.Ordinal))
            {
                SetSource(key, FieldSource.System);
                return;
            }

            if (_remarkReferences.TryGetValue(key, out var remarkValue)
                && string.Equals(current, remarkValue, StringComparison.Ordinal))
            {
                SetSource(key, FieldSource.Remark);
                return;
            }

            if (_systemReferences.ContainsKey(key) || _remarkReferences.ContainsKey(key))
                SetSource(key, FieldSource.Manual);
            else if (_sources.GetValueOrDefault(key) == FieldSource.Manual)
                SetSource(key, FieldSource.Manual);
        }

        private void SetSource(string key, FieldSource source)
        {
            if (!_bindings.ContainsKey(key)) return;
            _sources[key] = source;
            RefreshIndicator(key);
        }

        private static string ReadFieldValue(FrameworkElement input) => input switch
        {
            TextBox tb => tb.Text ?? string.Empty,
            DatePicker dp => dp.SelectedDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ChineseDateField dateField => dateField.NormalizedValue ?? string.Empty,
            _ => string.Empty
        };

        private void RefreshIndicator(string key)
        {
            if (!_bindings.TryGetValue(key, out var pair)) return;
            var source = _sources.GetValueOrDefault(key, FieldSource.None);
            pair.indicator.Background = source switch
            {
                FieldSource.System => Green,
                FieldSource.Remark => Blue,
                FieldSource.Manual => Yellow,
                _ => Transparent
            };
            pair.indicator.ToolTip = source switch
            {
                FieldSource.System => $"{FieldLabels.Get(key)}：来自 LIMIS 系统",
                FieldSource.Remark => $"{FieldLabels.Get(key)}：从备注正则提取",
                FieldSource.Manual => $"{FieldLabels.Get(key)}：未从备注提取或手动修改",
                _ => $"{FieldLabels.Get(key)}：未标记"
            };
        }
    }

    internal static class FieldLabels
    {
        private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
        {
            ["project.testNature"] = "检测性质",
            ["project.entrustNo"] = "委托编号",
            ["project.reportNo"] = "报告编号",
            ["project.entrustUnit"] = "委托单位",
            ["project.contact"] = "联系方式",
            ["project.projectName"] = "工程名称",
            ["project.unitAddress"] = "单位地址",
            ["project.supervisionUnit"] = "监理单位",
            ["project.constructionUnit"] = "施工单位",
            ["project.projectAddress"] = "工程地址",
            ["project.entrustDate"] = "委托日期",
            ["project.projectSection"] = "工程部位",
            ["project.reportDate"] = "报告日期",
            ["params.sampleName"] = "样品名称",
            ["params.materialType"] = "材料种类",
            ["params.ringSpec"] = "环刀规格",
            ["params.compactionMethod"] = "夯实方式",
            ["params.designRequirement"] = "设计要求",
            ["params.maxDryDensity"] = "最大干密度",
            ["params.testLocation"] = "取样部位",
            ["params.optimalMoisture"] = "最优含水率",
            ["params.testBasis"] = "检测依据",
            ["params.judgeBasis"] = "判定依据",
        };

        public static string Get(string key) =>
            Map.TryGetValue(key, out var label) ? label : key;

        public static void SetWitnessSamplingMode(bool isWitness)
        {
            Map["project.supervisionUnit"] = isWitness ? "工程见证" : "监理单位";
            Map["project.constructionUnit"] = isWitness ? "样品取样" : "施工单位";
            Map["params.ringSpec"] = isWitness ? "规格型号" : "环刀规格";
        }
    }
}
