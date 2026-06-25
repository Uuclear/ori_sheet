using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RingKnifeDetector.Helpers;

namespace RingKnifeDetector.Views
{
    public partial class ChineseDateField : UserControl
    {
        private bool _syncing;

        public ChineseDateField()
        {
            InitializeComponent();
            Loaded += (_, _) => CollapseDatePickerTextBox();
            txtDate.TextChanged += (_, _) =>
            {
                if (_syncing) return;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            txtDate.LostFocus += (_, _) => NormalizeTextBoxDisplay();
        }

        public event EventHandler? ValueChanged;

        public string NormalizedValue => DateHelper.Normalize(txtDate.Text);

        public void SetDate(string? value)
        {
            _syncing = true;
            try
            {
                txtDate.Text = string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : DateHelper.FormatWordDate(value);
                dpDate.SelectedDate = DateHelper.TryParse(value);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void DpDate_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_syncing || !IsLoaded) return;
            if (dpDate.SelectedDate == null) return;

            _syncing = true;
            try
            {
                txtDate.Text = DateHelper.Format(dpDate.SelectedDate);
            }
            finally
            {
                _syncing = false;
            }

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NormalizeTextBoxDisplay()
        {
            if (_syncing) return;
            var normalized = DateHelper.Normalize(txtDate.Text);
            if (string.IsNullOrEmpty(normalized)) return;

            _syncing = true;
            try
            {
                txtDate.Text = normalized;
                dpDate.SelectedDate = DateHelper.TryParse(normalized);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void CollapseDatePickerTextBox()
        {
            dpDate.ApplyTemplate();
            if (dpDate.Template?.FindName("PART_TextBox", dpDate) is DatePickerTextBox textBox)
            {
                textBox.Visibility = Visibility.Collapsed;
                textBox.Width = 0;
                textBox.MinWidth = 0;
            }
        }
        public void ApplyCompactStyle()
        {
            chromeBorder.BorderThickness = new Thickness(0);
            chromeBorder.Margin = new Thickness(0);
            chromeBorder.Background = Brushes.Transparent;
            txtDate.FontSize = 10;
            txtDate.Padding = new Thickness(2, 1, 1, 1);
            txtDate.TextAlignment = TextAlignment.Center;
            dpDate.Width = 22;
            dpDate.Height = 20;
            dpDate.FontSize = 10;
        }
    }
}
