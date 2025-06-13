namespace FrameFlow.Models
{
    internal class FfprobeOutput
    {
        public FormatInfo? format { get; set; }
        public List<StreamInfo>? streams { get; set; }
    }

    internal class FormatInfo
    {
        public string? filename { get; set; }
        public string? format_name { get; set; }
        public string? duration { get; set; }
        public string? size { get; set; }
        public string? bit_rate { get; set; }
        public Tags? tags { get; set; }
    }

    internal class StreamInfo
    {
        public string? codec_type { get; set; }
        public string? codec_name { get; set; }
        public int? width { get; set; }
        public int? height { get; set; }
        public string? pix_fmt { get; set; }
        public string? profile { get; set; }
        public int? level { get; set; }
        public string? r_frame_rate { get; set; }
        public string? sample_rate { get; set; }
        public int? channels { get; set; }
        public string? bit_rate { get; set; }
        public Tags? tags { get; set; }
    }

    internal class Tags
    {
        public string? title { get; set; }
        public string? language { get; set; }
    }
} 