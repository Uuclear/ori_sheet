using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector.Views
{
    public partial class RecordTableControl : UserControl
    {
        private const int ColCount = 21;
        private static readonly double[] ColWidths =
        {
            140, 60, 96, 96,
            68, 60, 60, 60, 60, 68,
            52, 60, 34, 34, 60, 56, 56, 68,
            60, 68, 60
        };
        private readonly List<(int col, int row, Control control)> _tabStops = new();
        private List<RingKnifeSample> _samples = new();
        private List<SamplePointResult> _results = new();
        private RecordParams _params = new();
        private int _ringsPerBlock = 2;
        private string _resultType = "compaction_coeff";
        private string _globalSamplingDate = "";
        private string _globalTestDate = "";

        public event EventHandler? SamplesChanged;
        public event EventHandler<int>? DeleteBlockRequested;

        public RecordTableControl()
        {
            InitializeComponent();
            var menu = new ContextMenu();
            var deleteItem = new MenuItem { Header = "删除测点" };
            deleteItem.Click += (_, _) =>
            {
                if (_contextBlockIndex >= 0)
                    DeleteBlockRequested?.Invoke(this, _contextBlockIndex);
            };
            menu.Items.Add(deleteItem);
            ContextMenu = menu;
            MouseRightButtonUp += OnMouseRightButtonUp;
        }

        private int _contextBlockIndex = -1;

        private void OnMouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(TableHost);
            var row = GetRowAtPosition(pos);
            if (row < 2) { _contextBlockIndex = -1; return; }
            var dataRow = row - 2;
            _contextBlockIndex = dataRow / RowsPerBlock;
        }

        private int GetRowAtPosition(System.Windows.Point pos)
        {
            double y = 0;
            for (int i = 0; i < TableHost.RowDefinitions.Count; i++)
            {
                var h = TableHost.RowDefinitions[i].ActualHeight;
                if (pos.Y >= y && pos.Y < y + h) return i;
                y += h;
            }
            return TableHost.RowDefinitions.Count - 1;
        }

        public void Configure(
            List<RingKnifeSample> samples,
            RecordParams parameters,
            List<SamplePointResult> results,
            int ringsPerBlock,
            string resultType,
            string globalSamplingDate,
            string globalTestDate)
        {
            _samples = samples;
            _params = parameters;
            _results = results;
            _ringsPerBlock = ringsPerBlock;
            _resultType = resultType;
            _globalSamplingDate = globalSamplingDate;
            _globalTestDate = globalTestDate;
            Rebuild();
        }

        public void RefreshResults(List<SamplePointResult> results)
        {
            _results = results;
            Rebuild();
        }

        private int RowsPerBlock => _ringsPerBlock * 2;

        private void Rebuild()
        {
            TableHost.Children.Clear();
            TableHost.RowDefinitions.Clear();
            TableHost.ColumnDefinitions.Clear();
            _tabStops.Clear();

            KeyboardNavigation.SetTabNavigation(TableHost, KeyboardNavigationMode.Local);

            for (int i = 0; i < ColCount; i++)
                TableHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColWidths[i]) });

            int row = 0;
            BuildHeader(ref row);

            int dataStart = row;
            int totalDataRows = Math.Max(1, _samples.Count) * RowsPerBlock;
            if (_samples.Count == 0) totalDataRows = RowsPerBlock;

            bool datesPlaced = false;

            for (int bi = 0; bi < Math.Max(1, _samples.Count); bi++)
            {
                var sample = bi < _samples.Count ? _samples[bi] : null;
                var result = bi < _results.Count ? _results[bi] : null;
                EnsureRings(sample);
                int blockStart = row;

                for (int ri = 0; ri < _ringsPerBlock; ri++)
                {
                    var ring = sample?.Rings.ElementAtOrDefault(ri);
                    var ringResult = result?.Rings.ElementAtOrDefault(ri);
                    bool isFirstRing = ri == 0;
                    bool ringPairNext = ri > 0;

                    // Ring row
                    TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    if (isFirstRing)
                    {
                        AddCell(blockStart, 0, row, 0, RowsPerBlock, MakeReadOnlyText(sample?.SampleNo ?? ""), true);
                        var elev = MakeEditableText(sample, s => s.Elevation, (s, v) => s.Elevation = v);
                        RegisterTab(elev, 1, blockStart);
                        AddCell(blockStart, 1, row, 1, RowsPerBlock, elev, false);

                        if (!datesPlaced)
                        {
                            var sampling = MakeGlobalDate(_globalSamplingDate, v =>
                            {
                                _globalSamplingDate = v;
                                SyncGlobalDates(v, true);
                            });
                            RegisterTab(sampling, 2, dataStart);
                            AddCell(dataStart, 2, row, 2, totalDataRows, sampling, false);

                            var testDate = MakeGlobalDate(_globalTestDate, v =>
                            {
                                _globalTestDate = v;
                                SyncGlobalDates(v, false);
                            });
                            RegisterTab(testDate, 3, dataStart);
                            AddCell(dataStart, 3, row, 3, totalDataRows, testDate, false);
                            datesPlaced = true;
                        }
                    }

                    if (ring != null)
                    {
                        var ringSampleMass = MakeRingDecimal(ring, r => r.RingSampleMass, (r, v) => r.RingSampleMass = v);
                        RegisterTab(ringSampleMass, 4, row);
                        AddCell(row, 4, row, 4, 2, ringSampleMass, false, ringPairNext);

                        var ringMass = MakeRingDecimal(ring, r => r.RingMass, (r, v) => r.RingMass = v);
                        RegisterTab(ringMass, 5, row);
                        AddCell(row, 5, row, 5, 2, ringMass, false, ringPairNext);
                        AddCell(row, 6, row, 6, 2, MakeReadOnlyText((ring.RingVolume ?? 200).ToString()), true, ringPairNext);
                        AddCell(row, 7, row, 7, 2, MakeReadOnlyText(Format(ringResult?.WetMass)), true, ringPairNext);
                        AddCell(row, 8, row, 8, 2, MakeReadOnlyText(Format(ringResult?.WetDensity)), true, ringPairNext);
                    }

                    if (isFirstRing)
                    {
                        AddCell(blockStart, 9, row, 9, RowsPerBlock, MakeReadOnlyText(Format(result?.AvgWetDensity ?? result?.WetDensity)), true);
                    }

                    if (ring != null)
                    {
                        var box1 = ring.Boxes.ElementAtOrDefault(0) ?? new AluminumBox();
                        var box1No = MakeBoxText(box1, b => b.BoxNo, (b, v) => b.BoxNo = v);
                        RegisterTab(box1No, 10, row);
                        AddCell(row, 10, box1No, false, ringPairNext);

                        var box1Mass = MakeBoxDecimal(box1, b => b.BoxMass, (b, v) => b.BoxMass = v);
                        RegisterTab(box1Mass, 11, row);
                        AddCell(row, 11, box1Mass, false, ringPairNext);

                        var box1Wet = MakeBoxDecimal(box1, b => b.WetSampleMass, (b, v) => b.WetSampleMass = v);
                        RegisterTab(box1Wet, 12, row);
                        AddCell(row, 12, box1Wet, false, ringPairNext, colspan: 2);

                        var box1Dry = MakeBoxDecimal(box1, b => b.DrySampleMass, (b, v) => b.DrySampleMass = v);
                        RegisterTab(box1Dry, 14, row);
                        AddCell(row, 14, box1Dry, false, ringPairNext);
                        AddCell(row, 15, MakeReadOnlyText(Format(ringResult?.MoistureRates.ElementAtOrDefault(0))), true, ringPairNext);
                        AddCell(row, 16, row, 16, 2, MakeReadOnlyText(Format(ringResult?.AvgMoisture)), true, ringPairNext);
                    }

                    if (isFirstRing)
                    {
                        AddCell(blockStart, 17, row, 17, RowsPerBlock, MakeReadOnlyText(Format(result?.AvgMoisture)), true);
                    }

                    if (ring != null)
                        AddCell(row, 18, row, 18, 2, MakeReadOnlyText(Format(ringResult?.DryDensity)), true, ringPairNext);

                    if (isFirstRing)
                    {
                        AddCell(blockStart, 19, row, 19, RowsPerBlock, MakeReadOnlyText(Format(result?.AvgDryDensity ?? result?.DryDensity)), true);
                        var compaction = _resultType == "compaction_percent" ? Format(result?.CompactionPercent) : Format(result?.CompactionCoeff);
                        AddCell(blockStart, 20, row, 20, RowsPerBlock, MakeReadOnlyText(compaction), true);
                    }

                    row++;

                    // Box row
                    TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    if (ring != null)
                    {
                        var box2 = ring.Boxes.ElementAtOrDefault(1) ?? new AluminumBox();
                        var box2No = MakeBoxText(box2, b => b.BoxNo, (b, v) => b.BoxNo = v);
                        RegisterTab(box2No, 10, row);
                        AddCell(row, 10, box2No, false);

                        var box2Mass = MakeBoxDecimal(box2, b => b.BoxMass, (b, v) => b.BoxMass = v);
                        RegisterTab(box2Mass, 11, row);
                        AddCell(row, 11, box2Mass, false);

                        var box2Wet = MakeBoxDecimal(box2, b => b.WetSampleMass, (b, v) => b.WetSampleMass = v);
                        RegisterTab(box2Wet, 12, row);
                        AddCell(row, 12, box2Wet, false, colspan: 2);

                        var box2Dry = MakeBoxDecimal(box2, b => b.DrySampleMass, (b, v) => b.DrySampleMass = v);
                        RegisterTab(box2Dry, 14, row);
                        AddCell(row, 14, box2Dry, false);
                        AddCell(row, 15, MakeReadOnlyText(Format(ringResult?.MoistureRates.ElementAtOrDefault(1))), true);
                    }
                    row++;
                }
            }

            ApplyTabOrder();
        }

        private void RegisterTab(Control control, int col, int row)
        {
            control.IsTabStop = true;
            control.Focusable = true;
            _tabStops.Add((col, row, control));
        }

        private void ApplyTabOrder()
        {
            var idx = 1;
            foreach (var (_, _, control) in _tabStops.OrderBy(t => t.col).ThenBy(t => t.row))
                control.TabIndex = idx++;
        }

        private void EnsureRings(RingKnifeSample? sample)
        {
            if (sample == null) return;
            while (sample.Rings.Count < _ringsPerBlock)
            {
                sample.Rings.Add(new RingMeasurement
                {
                    RingVolume = 200,
                    Boxes = new List<AluminumBox> { new(), new() }
                });
            }
            while (sample.Rings.Count > _ringsPerBlock)
                sample.Rings.RemoveAt(sample.Rings.Count - 1);
        }

        private void SyncGlobalDates(string value, bool isSampling)
        {
            foreach (var s in _samples)
            {
                if (isSampling) s.SamplingDate = value;
                else s.TestDate = value;
            }
            SamplesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BuildHeader(ref int row)
        {
            TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var h1 = new (string text, int colspan, int rowspan)[]
            {
                ("样品编号", 1, 2), ("测点标高\n(mm)", 1, 2), ("日期", 2, 1),
                ("土样湿密度(g/cm³)", 6, 1),
                ("含水率(%)", 8, 1),
                ("土样干密度\n(g/cm³)", 1, 2), ("干密度平均值\n(g/cm³)", 1, 2),
                (_resultType == "compaction_percent" ? "压实度%" : "压实系数", 1, 2)
            };

            int col = 0;
            foreach (var (text, colspan, rowspan) in h1)
            {
                AddHeaderCell(row, col, text, colspan, rowspan);
                col += colspan;
            }

            row++;
            var h2 = new (string text, int colspan)[]
            {
                ("取样日期", 1), ("检测日期", 1),
                ("环刀和样\n质量(g)", 1), ("环刀质量\n(g)", 1), ("环刀容积\n(cm³)", 1),
                ("湿土质量\n(g)", 1), ("湿密度\n(g/cm³)", 1), ("平均湿密度\n(g/cm³)", 1),
                ("铝盒号", 1), ("铝盒质量\n(g)", 1), ("湿样+铝盒\n质量(g)", 2),
                ("干样+铝盒\n质量(g)", 1), ("含水率\n(%)", 1), ("平均值\n(%)", 1), ("含水率\n平均值(%)", 1)
            };
            col = 2;
            foreach (var (text, colspan) in h2)
            {
                AddHeaderCell(row, col, text, colspan, 1);
                col += colspan;
            }
            row++;
        }

        private void AddHeaderCell(int row, int col, string text, int colspan, int rowspan)
        {
            var tb = new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Padding = new Thickness(4, 4, 4, 4),
                TextWrapping = TextWrapping.Wrap
            };
            var border = WrapBorder(tb, true);
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            if (colspan > 1) Grid.SetColumnSpan(border, colspan);
            if (rowspan > 1) Grid.SetRowSpan(border, rowspan);
            TableHost.Children.Add(border);
        }

        private void AddCell(int row, int col, FrameworkElement content, bool readOnly, bool emphasizeTop = false, int colspan = 1)
        {
            var border = WrapBorder(content, readOnly, emphasizeTop);
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            if (colspan > 1) Grid.SetColumnSpan(border, colspan);
            TableHost.Children.Add(border);
        }

        private void AddCell(int rowStart, int col, int placeRow, int placeCol, int rowSpan, FrameworkElement content, bool readOnly, bool emphasizeTop = false)
        {
            var border = WrapBorder(content, readOnly, emphasizeTop);
            Grid.SetRow(border, placeRow);
            Grid.SetColumn(border, placeCol);
            Grid.SetRowSpan(border, rowSpan);
            TableHost.Children.Add(border);
        }

        private static Border WrapBorder(FrameworkElement content, bool readOnly, bool emphasizeTop = false)
        {
            var thickness = emphasizeTop
                ? new Thickness(0.5, 1.5, 0.5, 0.5)
                : new Thickness(0.5);
            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                BorderThickness = thickness,
                Background = readOnly ? new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)) : Brushes.White,
                Child = content,
                MinHeight = 26
            };
        }

        private TextBox MakeReadOnlyText(string text)
        {
            var tb = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Padding = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                IsTabStop = false,
                Focusable = false
            };
            return tb;
        }

        private TextBox MakeEditableText(RingKnifeSample? sample, Func<RingKnifeSample, string> get, Action<RingKnifeSample, string> set)
        {
            var tb = new TextBox
            {
                Text = sample != null ? get(sample) : "",
                BorderThickness = new Thickness(0),
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Padding = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            if (sample != null)
            {
                tb.TextChanged += (_, _) => { set(sample, tb.Text); SamplesChanged?.Invoke(this, EventArgs.Empty); };
            }
            return tb;
        }

        private DatePicker MakeGlobalDate(string field, Action<string> onChange)
        {
            var dp = new DatePicker
            {
                SelectedDate = DateHelper.TryParse(field) ?? DateTime.Today,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Padding = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 92,
                IsTabStop = true,
                Focusable = true
            };
            dp.SelectedDateChanged += (_, _) => onChange(DateHelper.Format(dp.SelectedDate));
            return dp;
        }

        private TextBox MakeRingDecimal(RingMeasurement ring, Func<RingMeasurement, decimal?> get, Action<RingMeasurement, decimal?> set)
        {
            var tb = new TextBox
            {
                Text = get(ring)?.ToString() ?? "",
                BorderThickness = new Thickness(0),
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Padding = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            tb.TextChanged += (_, _) =>
            {
                set(ring, decimal.TryParse(tb.Text, out var v) ? v : null);
                SamplesChanged?.Invoke(this, EventArgs.Empty);
            };
            return tb;
        }

        private TextBox MakeBoxText(AluminumBox box, Func<AluminumBox, string> get, Action<AluminumBox, string> set)
        {
            var tb = new TextBox
            {
                Text = get(box),
                BorderThickness = new Thickness(0),
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Padding = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            tb.TextChanged += (_, _) => { set(box, tb.Text); SamplesChanged?.Invoke(this, EventArgs.Empty); };
            return tb;
        }

        private TextBox MakeBoxDecimal(AluminumBox box, Func<AluminumBox, decimal?> get, Action<AluminumBox, decimal?> set)
        {
            var tb = new TextBox
            {
                Text = get(box)?.ToString() ?? "",
                BorderThickness = new Thickness(0),
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Padding = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            tb.TextChanged += (_, _) =>
            {
                set(box, decimal.TryParse(tb.Text, out var v) ? v : null);
                SamplesChanged?.Invoke(this, EventArgs.Empty);
            };
            return tb;
        }

        private static string Format(decimal? v) => v.HasValue ? v.Value.ToString("F2") : "";
    }
}
