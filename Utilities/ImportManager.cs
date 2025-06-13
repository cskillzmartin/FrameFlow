using System.Diagnostics;
using System.Text.Json;
using System.Text;
using FrameFlow.Models;
using Whisper.net;
using Whisper.net.Ggml;
using FrameFlow.App;

namespace FrameFlow.Utilities
{
    public class ImportManager
    {
        private readonly string FFPROBE_PATH;
        private readonly string FFMPEG_PATH;
        private readonly string WHISPER_MODEL_PATH;

        public ImportManager()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            FFPROBE_PATH = Settings.Instance.FfprobePath;
            FFMPEG_PATH = Settings.Instance.FfmpegPath;
            WHISPER_MODEL_PATH = Settings.Instance.WhisperModelPath;

            // Verify ffprobe exists
            if (!File.Exists(FFPROBE_PATH))
            {
                throw new FileNotFoundException("ffprobe not found in the expected location", FFPROBE_PATH);
            }

            // Verify ffmpeg exists
            if (!File.Exists(FFMPEG_PATH))
            {
                throw new FileNotFoundException("ffmpeg not found in the expected location", FFMPEG_PATH);
            }

            if (!File.Exists(WHISPER_MODEL_PATH))
            {
                throw new FileNotFoundException("Whisper model not found in the expected location", WHISPER_MODEL_PATH);
            }
        }

        public VideoMetaData ExtractMetaData(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Video file not found", filePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = FFPROBE_PATH,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(FFPROBE_PATH) ?? string.Empty
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                    throw new Exception("Failed to start ffprobe process");

                string jsonOutput = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new Exception($"FFprobe failed with error: {error}");
                }

                var ffprobeData = JsonSerializer.Deserialize<FfprobeOutput>(jsonOutput);
                if (ffprobeData == null)
                    throw new Exception("Failed to parse ffprobe output");

                var metadata = MapFfprobeDataToVideoMetadata(ffprobeData);

                return metadata;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error extracting metadata: {ex.Message}", ex);
            }
        }

        public async Task<string> ExtractAudioAsync(string videoPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(videoPath);
            var audioPath = Path.Combine(GetTranscriptionsDir(), $"{fileName}.wav");

            var startInfo = new ProcessStartInfo
            {
                FileName = FFMPEG_PATH,
                Arguments = $"-i \"{videoPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{audioPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("Failed to start ffmpeg process");

            // Ensure streams are read to completion to prevent deadlocks
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new Exception($"Audio extraction failed: {error}");
            }

            // Ensure process is fully terminated
            process.Close();
            return audioPath;
        }

        private static VideoMetaData MapFfprobeDataToVideoMetadata(FfprobeOutput ffprobeData)
        {
            var metadata = new VideoMetaData();

            if (ffprobeData.format != null)
            {
                metadata.Format = ffprobeData.format.format_name ?? string.Empty;
                metadata.FileSize = long.Parse(ffprobeData.format.size ?? "0");
                metadata.Duration = TimeSpan.FromSeconds(double.Parse(ffprobeData.format.duration ?? "0"));
                metadata.OverallBitrate = int.Parse(ffprobeData.format.bit_rate ?? "0");
                metadata.Title = ffprobeData.format.tags?.title ?? string.Empty;
                metadata.FilePath = ffprobeData.format.filename ?? string.Empty; // Added this line
            }

            if (ffprobeData.streams != null)
            {
                foreach (var stream in ffprobeData.streams)
                {
                    if (stream.codec_type == "video" && metadata.VideoStreamInfo != null)
                    {
                        metadata.VideoStreamInfo.Codec = stream.codec_name ?? string.Empty;
                        metadata.VideoStreamInfo.Width = stream.width ?? 0;
                        metadata.VideoStreamInfo.Height = stream.height ?? 0;
                        metadata.VideoStreamInfo.PixelFormat = stream.pix_fmt ?? string.Empty;
                        metadata.VideoStreamInfo.Profile = stream.profile ?? string.Empty;
                        metadata.VideoStreamInfo.Level = stream.level.ToString() ?? string.Empty;
                        
                        // Parse frame rate which might be in "num/den" format
                        if (stream.r_frame_rate != null)
                        {
                            var parts = stream.r_frame_rate.Split('/');
                            if (parts.Length == 2 && float.TryParse(parts[0], out float num) && float.TryParse(parts[1], out float den))
                            {
                                metadata.VideoStreamInfo.FrameRate = num / den;
                            }
                        }

                        // Parse bitrate
                        if (!string.IsNullOrEmpty(stream.bit_rate))
                        {
                            metadata.VideoStreamInfo.Bitrate = int.Parse(stream.bit_rate);
                        }
                    }
                    else if (stream.codec_type == "audio" && metadata.AudioStreamInfo != null)
                    {
                        metadata.AudioStreamInfo.Codec = stream.codec_name ?? string.Empty;
                        metadata.AudioStreamInfo.SampleRate = int.Parse(stream.sample_rate ?? "0");
                        metadata.AudioStreamInfo.Channels = stream.channels ?? 0;
                        metadata.AudioStreamInfo.Bitrate = int.Parse(stream.bit_rate ?? "0");
                        metadata.AudioStreamInfo.Language = stream.tags?.language ?? string.Empty;
                    }
                }
            }

            return metadata;
        }

        public async Task TranscribeAudioToSrtAsync(string audioFilePath)
        {
            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException("Audio file not found", audioFilePath);

            var fileName = Path.GetFileNameWithoutExtension(audioFilePath);
            var srtFilePath = Path.Combine(GetTranscriptionsDir(), $"{fileName}.srt");
            var wavFilePath = audioFilePath;

            try
            {
                // Convert audio to WAV format if it's not already
                if (!audioFilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    wavFilePath = Path.Combine(GetTranscriptionsDir(), $"{fileName}.wav");
                    await ConvertToWavAsync(audioFilePath, wavFilePath);
                }

                // Perform transcription
                using var whisperFactory = WhisperFactory.FromPath(WHISPER_MODEL_PATH);
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                using var fileStream = File.OpenRead(wavFilePath);
                var segments = processor.ProcessAsync(fileStream);

                // Write segments to SRT file
                await WriteSrtFileAsync(segments, srtFilePath);

                // Clean up temporary WAV file if it was created
                if (audioFilePath != wavFilePath && File.Exists(wavFilePath))
                {
                    File.Delete(wavFilePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during transcription: {ex.Message}", ex);
            }
        }

        private async Task ConvertToWavAsync(string inputPath, string outputPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = FFMPEG_PATH,
                Arguments = $"-i \"{inputPath}\" -ar 16000 -ac 1 -c:a pcm_s16le \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("Failed to start ffmpeg process");

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"FFmpeg conversion failed: {error}");
            }
        }

        private async Task WriteSrtFileAsync(IAsyncEnumerable<SegmentData> segments, string srtFilePath)
        {
            using var writer = new StreamWriter(srtFilePath, false, Encoding.UTF8);
            int index = 0;

            await foreach (var segment in segments)
            {
                await writer.WriteLineAsync(index.ToString());
                await writer.WriteLineAsync($"{segment.Start} --> {segment.End}");
                await writer.WriteLineAsync(segment.Text.Trim());
                await writer.WriteLineAsync();

                index++;
            }
        }

        private string GetTranscriptionsDir()
        {
            var projectPath = ProjectHandler.Instance.CurrentProjectPath;
            if (string.IsNullOrEmpty(projectPath))
                throw new InvalidOperationException("No project is currently open");

            var transcriptionsDir = Path.Combine(projectPath, "Transcriptions");
            Directory.CreateDirectory(transcriptionsDir);
            return transcriptionsDir;
        }

        // Add methods for handling media import operations
        
    }
} 