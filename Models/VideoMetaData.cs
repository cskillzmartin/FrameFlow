namespace FrameFlow.Models;

public class VideoMetaData
{
    // General Video Information
    public string Format { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public long FileSize { get; set; }
    public int OverallBitrate { get; set; }
    public DateTime StartTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Copyright { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    // Video Stream Properties
    public class VideoStream
    {
        public string Codec { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public float FrameRate { get; set; }
        public string PixelFormat { get; set; } = string.Empty;
        public int Bitrate { get; set; }
        public string ColorSpace { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public float SampleAspectRatio { get; set; }
        public float DisplayAspectRatio { get; set; }
    }
    public VideoStream? VideoStreamInfo { get; set; }

    // Audio Stream Properties
    public class AudioStream
    {
        public string Codec { get; set; } = string.Empty;
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int Bitrate { get; set; }
        public string Language { get; set; } = string.Empty;
    }
    public AudioStream? AudioStreamInfo { get; set; }

    // Technical Metadata
    public class TechnicalInfo
    {
        public string StreamId { get; set; } = string.Empty;
        public Dictionary<string, string> CodecParameters { get; set; } = new();
        public string Timecode { get; set; } = string.Empty;
        public int GopSize { get; set; }
        public bool HasBFrames { get; set; }
        public int ReferenceFrames { get; set; }
        public Dictionary<string, string> EncodingSettings { get; set; } = new();
    }
    public TechnicalInfo? TechnicalMetadata { get; set; }

    // Container Metadata
    public class ContainerInfo
    {
        public List<ChapterInfo> Chapters { get; set; } = new();
        public Dictionary<string, string> MetadataTags { get; set; } = new();
        public string ProgramInfo { get; set; } = string.Empty;
        public Dictionary<string, string> StreamMapping { get; set; } = new();
        public DateTime CreationTime { get; set; }
        public string EncoderInfo { get; set; } = string.Empty;
    }
    public ContainerInfo? ContainerMetadata { get; set; }

    // Chapter information
    public class ChapterInfo
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    // Constructor
    public VideoMetaData()
    {
        VideoStreamInfo = new VideoStream();
        AudioStreamInfo = new AudioStream();
        TechnicalMetadata = new TechnicalInfo();
        ContainerMetadata = new ContainerInfo();
    }

    // Method to parse ffprobe output and populate this object
    public static VideoMetaData FromFfprobeOutput(string ffprobeOutput)
    {
        // TODO: Implement parsing logic for ffprobe output
        throw new NotImplementedException();
    }

    // Method to get a formatted summary of the video metadata
    public string GetSummary()
    {
        return $"Video: {Format} {VideoStreamInfo?.Width}x{VideoStreamInfo?.Height} " +
               $"@ {VideoStreamInfo?.FrameRate}fps ({VideoStreamInfo?.Codec})\n" +
               $"Audio: {AudioStreamInfo?.Codec} {AudioStreamInfo?.Channels}ch " +
               $"@ {AudioStreamInfo?.SampleRate}Hz\n" +
               $"Duration: {Duration.ToString(@"hh\:mm\:ss\.fff")}\n" +
               $"Size: {FileSize / (1024 * 1024):F2} MB";
    }
} 