public class ClipInfo
{
    public string FileName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Relevance { get; set; }
    public float Sentiment { get; set; }
    public float Novelty { get; set; }
    public float Energy { get; set; }
}