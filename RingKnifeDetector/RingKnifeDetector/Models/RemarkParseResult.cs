namespace RingKnifeDetector.Models
{
    public class RemarkHighlight
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public string Label { get; set; } = string.Empty;
        public string FieldKey { get; set; } = string.Empty;
    }

    public class RemarkParseResult
    {
        public List<RemarkHighlight> Highlights { get; set; } = new();
        public HashSet<string> ExtractedFieldKeys { get; set; } = new(StringComparer.Ordinal);
    }
}
