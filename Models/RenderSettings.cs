using System.Text.Json.Serialization;

namespace FrameFlow.Models
{
    public class RenderSettings
    {
        [JsonConstructor]
        public RenderSettings()
        {
            OutputFileName = string.Empty;
            OutputDirectory = string.Empty;
            VideoCodec = "h264";
            AudioCodec = "aac";
            PresetName = "medium";
        }

        public string OutputFileName { get; set; }
        public string OutputDirectory { get; set; }
        public int VideoWidth { get; set; }
        public int VideoHeight { get; set; }
        public string VideoCodec { get; set; }
        public string AudioCodec { get; set; }
        public int VideoBitrate { get; set; } = 8000;  // Default 8Mbps
        public int AudioBitrate { get; set; } = 192;   // Default 192kbps
        public float FrameRate { get; set; } = 30;     // Default 30fps
        public string PresetName { get; set; }         // encoding preset (e.g., ultrafast, medium, slow)
        public bool MaintainAspectRatio { get; set; } = true;
        public bool EnableHardwareAcceleration { get; set; } = true;
        public int AudioSampleRate { get; set; } = 48000;  // Default 48kHz
        public int AudioChannels { get; set; } = 2;        // Default stereo
        public string? CustomFFmpegOptions { get; set; }   // For advanced users
    }
} 