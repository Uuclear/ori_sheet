using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector.Views
{
    public class RemarkHighlightViewer : UserControl
    {
        private static readonly SolidColorBrush HighlightBackground = new(Color.FromRgb(0xBB, 0xDE, 0xFB));

        private readonly RichTextBox _editor;
        private bool _suppressChange;
        private string _plainText = string.Empty;
        private List<RemarkHighlight> _highlights = new();

        public RemarkHighlightViewer()
        {
            _editor = new RichTextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 48,
                FontSize = 12,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2)
            };
            _editor.TextChanged += (_, _) =>
            {
                if (_suppressChange) return;
                _plainText = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd).Text.TrimEnd('\r', '\n');
                TextChanged?.Invoke(this, EventArgs.Empty);
            };
            _editor.LostFocus += (_, _) =>
            {
                if (_suppressChange) return;
                var text = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd).Text.TrimEnd('\r', '\n');
                _plainText = text;
                var highlights = RemarkParser.AnalyzeHighlights(text).Highlights;
                RenderDocument(text, highlights);
            };
            _editor.PreviewMouseMove += OnPreviewMouseMove;
            Content = _editor;
        }

        public event EventHandler? TextChanged;

        public string Text
        {
            get => _plainText;
            set
            {
                _plainText = value ?? string.Empty;
                RenderDocument(_plainText, null);
            }
        }

        public void ApplyHighlights(string text, IEnumerable<RemarkHighlight>? highlights)
        {
            _plainText = text ?? string.Empty;
            RenderDocument(_plainText, highlights);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_highlights.Count == 0)
            {
                _editor.ToolTip = null;
                return;
            }

            var pointer = _editor.GetPositionFromPoint(e.GetPosition(_editor), snapToText: true);
            if (pointer == null)
            {
                _editor.ToolTip = null;
                return;
            }

            var offset = new TextRange(_editor.Document.ContentStart, pointer).Text.Length;
            var hit = _highlights.FirstOrDefault(h => offset >= h.Start && offset < h.Start + h.Length);
            _editor.ToolTip = hit == null ? null : $"{hit.Label}（从备注提取）";
        }

        private void RenderDocument(string text, IEnumerable<RemarkHighlight>? highlights)
        {
            _suppressChange = true;
            try
            {
                _editor.Document.Blocks.Clear();
                _highlights = (highlights ?? Enumerable.Empty<RemarkHighlight>())
                    .Where(h => h.Start >= 0 && h.Length > 0 && h.Start < text.Length)
                    .OrderBy(h => h.Start)
                    .ToList();

                var paragraph = new Paragraph { Margin = new Thickness(0) };
                if (string.IsNullOrEmpty(text))
                {
                    _editor.Document.Blocks.Add(paragraph);
                    return;
                }

                var index = 0;
                foreach (var item in _highlights)
                {
                    var start = Math.Min(item.Start, text.Length);
                    var end = Math.Min(start + item.Length, text.Length);
                    if (start > index)
                        paragraph.Inlines.Add(new Run(text[index..start]));

                    if (end > start)
                    {
                        var tip = $"{item.Label}（从备注提取）";
                        var link = new Hyperlink(new Run(text[start..end]))
                        {
                            Background = HighlightBackground,
                            TextDecorations = null,
                            Foreground = Brushes.Black,
                            Cursor = Cursors.IBeam
                        };
                        ToolTipService.SetToolTip(link, tip);
                        ToolTipService.SetInitialShowDelay(link, 200);
                        ToolTipService.SetShowDuration(link, 8000);
                        link.Click += (_, args) => args.Handled = true;
                        paragraph.Inlines.Add(link);
                    }
                    index = Math.Max(index, end);
                }

                if (index < text.Length)
                    paragraph.Inlines.Add(new Run(text[index..]));

                _editor.Document.Blocks.Add(paragraph);
            }
            finally
            {
                _suppressChange = false;
                _plainText = text;
            }
        }
    }
}
