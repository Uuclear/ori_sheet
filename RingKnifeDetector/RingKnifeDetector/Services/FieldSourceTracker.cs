using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    public class FieldSourceTracker
    {
        private static readonly SolidColorBrush Green = new(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly SolidColorBrush Blue = new(Color.FromRgb(0x21, 0x96, 0xF3));
        private static readonly SolidColorBrush Yellow = new(Color.FromRgb(0xFF, 0xC1, 0x07));
        private static readonly SolidColorBrush Transparent = Brushes.Transparent;

        private readonly Dictionary<string, FieldSource> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (FrameworkElement input, Border indicator)> _bindings = new(StringComparer.Ordinal);
        private bool _suppressManual;

        public void Register(string key, FrameworkElement input, Border indicator)
        {
            _bindings[key] = (input, indicator);
            if (input is TextBox tb)
                tb.TextChanged += (_, _) => OnInputChanged(key);
            else if (input is DatePicker dp)
                dp.SelectedDateChanged += (_, _) => OnInputChanged(key);
        }

        public void AttachIndicatorToGridCell(TextBox textBox, string key, Panel parentGrid)
        {
            if (textBox.Parent is Grid inner && ReferenceEquals(inner.Parent, parentGrid))
                AttachIndicatorToContainer(inner, key, parentGrid, textBox);
            else
                AttachIndicatorToContainer(textBox, key, parentGrid, textBox);
        }

        public void AttachIndicatorToDatePicker(DatePicker picker, string key, Panel parentGrid) =>
            AttachIndicatorToContainer(picker, key, parentGrid, picker);

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
                Margin = new Thickness(2, 2, 0, 2),
                Background = Transparent,
                ToolTip = "未填写"
            };
            Grid.SetColumn(indicator, 1);
            host.Children.Add(indicator);

            parentGrid.Children.Add(host);
            Register(key, inputForManual, indicator);
        }

        public void SetSource(string key, FieldSource source)
        {
            if (!_bindings.ContainsKey(key)) return;
            _sources[key] = source;
            RefreshIndicator(key);
        }

        public void MarkSystem(IEnumerable<string> keys)
        {
            _suppressManual = true;
            try
            {
                foreach (var key in keys)
                    SetSource(key, FieldSource.System);
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
                    SetSource(key, FieldSource.Remark);
            }
            finally
            {
                _suppressManual = false;
            }
        }

        /// <summary>批量写入表单时抑制 TextChanged 触发的「手动修改」标记。</summary>
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
            foreach (var key in _bindings.Keys)
                RefreshIndicator(key);
        }

        private void OnInputChanged(string key)
        {
            if (_suppressManual) return;
            if (!_bindings.ContainsKey(key)) return;
            SetSource(key, FieldSource.Manual);
        }

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
                FieldSource.Manual => $"{FieldLabels.Get(key)}：手动修改",
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
    }
}
