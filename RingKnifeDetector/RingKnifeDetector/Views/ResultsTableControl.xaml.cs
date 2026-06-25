using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Views
{
    public partial class ResultsTableControl : UserControl
    {
        private static readonly double[] ColWidths =
        {
            150, 100, 100, 128, 200, 100, 80, 100, 80, 120
        };

        private static readonly string[] Headers =
        {
            "样品编号", "取样点标高/(mm)", "取样点厚度/(mm)", "取样日期", "检测日期",
            "湿密度/(g/cm³)", "含水率/%", "干密度/(g/cm³)", "压实系数", "结论"
        };

        public ResultsTableControl()
        {
            InitializeComponent();
        }

        public void Configure(List<SamplePointResult> results, RecordParams parameters)
        {
            Rebuild(results, parameters);
        }

        private void Rebuild(List<SamplePointResult> results, RecordParams parameters)
        {
            TableHost.Children.Clear();
            TableHost.RowDefinitions.Clear();
            TableHost.ColumnDefinitions.Clear();

            for (var i = 0; i < ColWidths.Length; i++)
                TableHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColWidths[i]) });

            var row = 0;
            TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var col = 0; col < Headers.Length; col++)
            {
                var compactionHeader = col == 8
                    ? parameters.ResultType == "compaction_percent" ? "压实度/%" : "压实系数"
                    : Headers[col];
                AddHeaderCell(row, col, compactionHeader);
            }

            row++;
            var isGroup3 = parameters.RecordTemplate == "group3";
            var isPercent = parameters.ResultType == "compaction_percent";

            if (results.Count == 0)
            {
                TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddCell(row, 0, MakeReadOnlyText("暂无数据"), true, colspan: Headers.Length);
                return;
            }

            var globalSampling = results[0].SamplingDateDisplay;
            var globalTest = results[0].TestDateDisplay;
            var globalThickness = results[0].Thickness;
            var sharedRowStart = row;
            var totalDataRows = isGroup3 ? results.Count * 3 : results.Count;
            var sharedPlaced = false;

            foreach (var sample in results)
            {
                if (!isGroup3)
                {
                    TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    AddSummaryRow(row, sample, isPercent, sharedRowStart, totalDataRows, ref sharedPlaced, globalThickness, globalSampling, globalTest);
                    row++;
                    continue;
                }

                var blockStart = row;
                for (var ringIndex = 0; ringIndex < 3; ringIndex++)
                {
                    var ring = sample.Rings.ElementAtOrDefault(ringIndex);
                    TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    if (ringIndex == 0)
                    {
                        AddCell(blockStart, 0, row, 0, 3, MakeReadOnlyText(sample.SampleNo), true);
                        AddCell(blockStart, 1, row, 1, 3, MakeReadOnlyText(sample.Elevation), true);
                        if (!sharedPlaced)
                        {
                            AddCell(sharedRowStart, 2, row, 2, totalDataRows, MakeReadOnlyText(globalThickness), true);
                            AddCell(sharedRowStart, 3, row, 3, totalDataRows, MakeReadOnlyText(globalSampling), true);
                            AddCell(sharedRowStart, 4, row, 4, totalDataRows, MakeReadOnlyText(globalTest), true);
                            sharedPlaced = true;
                        }
                    }

                    AddCell(row, 5, MakeReadOnlyText(CompactionFormat.FormatDensity(ring?.WetDensity)), true);
                    AddCell(row, 6, MakeReadOnlyText(CompactionFormat.FormatMoisture(ring?.AvgMoisture)), true);
                    AddCell(row, 7, MakeReadOnlyText(CompactionFormat.FormatDensity(ring?.DryDensity)), true);
                    var compaction = isPercent
                        ? CompactionFormat.FormatPercent(ring?.CompactionPercent)
                        : CompactionFormat.FormatCoeff(ring?.CompactionCoeff);
                    AddCell(row, 8, MakeReadOnlyText(compaction), true);
                    AddCell(row, 9, MakeReadOnlyText(ring?.Conclusion ?? string.Empty), true);
                    row++;
                }
            }
        }

        private void AddSummaryRow(
            int row,
            SamplePointResult sample,
            bool isPercent,
            int sharedRowStart,
            int totalDataRows,
            ref bool sharedPlaced,
            string globalThickness,
            string globalSampling,
            string globalTest)
        {
            AddCell(row, 0, MakeReadOnlyText(sample.SampleNo), true);
            AddCell(row, 1, MakeReadOnlyText(sample.Elevation), true);
            if (!sharedPlaced)
            {
                AddCell(sharedRowStart, 2, row, 2, totalDataRows, MakeReadOnlyText(globalThickness), true);
                AddCell(sharedRowStart, 3, row, 3, totalDataRows, MakeReadOnlyText(globalSampling), true);
                AddCell(sharedRowStart, 4, row, 4, totalDataRows, MakeReadOnlyText(globalTest), true);
                sharedPlaced = true;
            }
            AddCell(row, 5, MakeReadOnlyText(CompactionFormat.FormatDensity(sample.AvgWetDensity ?? sample.WetDensity)), true);
            AddCell(row, 6, MakeReadOnlyText(CompactionFormat.FormatMoisture(sample.AvgMoisture)), true);
            AddCell(row, 7, MakeReadOnlyText(CompactionFormat.FormatDensity(sample.AvgDryDensity ?? sample.DryDensity)), true);
            var compaction = isPercent
                ? CompactionFormat.FormatPercent(sample.CompactionPercent)
                : CompactionFormat.FormatCoeff(sample.CompactionCoeff);
            AddCell(row, 8, MakeReadOnlyText(compaction), true);
            AddCell(row, 9, MakeReadOnlyText(sample.Conclusion), true);
        }

        private void AddHeaderCell(int row, int col, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Padding = new Thickness(4),
                TextWrapping = TextWrapping.Wrap
            };
            TableHost.Children.Add(WrapBorder(tb, true, row, col));
        }

        private void AddCell(int row, int col, FrameworkElement content, bool readOnly, int colspan = 1) =>
            TableHost.Children.Add(WrapBorder(content, readOnly, row, col, colspan));

        private void AddCell(int rowStart, int col, int placeRow, int placeCol, int rowSpan, FrameworkElement content, bool readOnly)
        {
            var border = WrapBorder(content, readOnly, placeRow, placeCol);
            Grid.SetRowSpan(border, rowSpan);
            TableHost.Children.Add(border);
        }

        private static Border WrapBorder(FrameworkElement content, bool readOnly, int row, int col, int colspan = 1)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                BorderThickness = new Thickness(0.5),
                Background = readOnly ? new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)) : Brushes.White,
                Child = content,
                MinHeight = 26
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            if (colspan > 1)
                Grid.SetColumnSpan(border, colspan);
            return border;
        }

        private static TextBox MakeReadOnlyText(string text) =>
            new()
            {
                Text = text,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Padding = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                IsTabStop = false,
                Focusable = false
            };
    }
}
