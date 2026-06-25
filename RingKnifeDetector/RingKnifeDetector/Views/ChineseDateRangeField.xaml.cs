using System.Windows.Controls;
using RingKnifeDetector.Helpers;

namespace RingKnifeDetector.Views
{
    public partial class ChineseDateRangeField : UserControl
    {
        public ChineseDateRangeField()
        {
            InitializeComponent();
            fieldStart.ValueChanged += (_, _) => RaiseChanged();
            fieldEnd.ValueChanged += (_, _) => RaiseChanged();
        }

        public event EventHandler? ValueChanged;

        public string RangeValue => DateHelper.FormatRange(fieldStart.NormalizedValue, fieldEnd.NormalizedValue);

        public void SetRange(string? value)
        {
            var (start, end) = DateHelper.ParseRange(value);
            fieldStart.SetDate(start);
            fieldEnd.SetDate(end);
        }

        public void RegisterTabOrder(Action<Control, int, int> register, int col, int startRow, int endRow)
        {
            register(fieldStart, col, startRow);
            register(fieldEnd, col, endRow);
        }

        private void RaiseChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);

        public void ApplyCompactStyle()
        {
            fieldStart.ApplyCompactStyle();
            fieldEnd.ApplyCompactStyle();
        }
    }
}
