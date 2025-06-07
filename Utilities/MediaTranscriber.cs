using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Whisper.net;
using FFMpegCore;
using System.Text.Json;
using System.IO.Compression;
using System.Net.Http;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace FrameFlow.Utilities
{
    /// <summary>
    /// Utility for transcribing video files using Whisper.net with timestamped output
    /// Enhanced to automatically handle FFmpeg without requiring PATH configuration
    /// </summary>
    public class VideoTranscriptionUtility
    {
        private readonly string _workingDirectory;
        private readonly string _ffmpegDirectory;
        private readonly IProgress<TranscriptionProgress>? _progressReporter;
        private static readonly HttpClient _httpClient = new();

        static VideoTranscriptionUtility()
        {
            // Don't configure anything initially - let the instance handle it
        }

        public VideoTranscriptionUtility(string? workingDirectory = null, IProgress<TranscriptionProgress>? progressReporter = null)
        {
            _workingDirectory = workingDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "WorkingFiles");
            _ffmpegDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Utilities", "ffmpeg");
            _progressReporter = progressReporter;

            // Ensure working directories exist
            Directory.CreateDirectory(_workingDirectory);
            Directory.CreateDirectory(_ffmpegDirectory);
        }

        /// <summary>
        /// Downloads and sets up FFmpeg if not available, without requiring PATH
        /// </summary>
        public async Task<bool> EnsureFFmpegAvailableAsync()
        {
            try
            {
                // First check if ffmpeg binaries exist in our local directory
                var ffmpegPath = GetLocalFFmpegPath();
                var ffprobePath = GetLocalFFprobePath();

                _progressReporter?.Report(new TranscriptionProgress($"Checking for FFmpeg at: {ffmpegPath}", 5));

                if (File.Exists(ffmpegPath) && File.Exists(ffprobePath))
                {
                    // Configure FFMpegCore to use our local binaries
                    GlobalFFOptions.Configure(new FFOptions { BinaryFolder = _ffmpegDirectory });
                    
                    _progressReporter?.Report(new TranscriptionProgress("FFmpeg binaries found, testing functionality...", 10));
                    
                    // Test if they work with a more reliable test
                    try
                    {
                        // Try to get FFmpeg version instead of analyzing a non-existent file
                        var ffmpegBinaryPath = GlobalFFOptions.GetFFMpegBinaryPath();
                        var ffprobeBinaryPath = GlobalFFOptions.GetFFProbeBinaryPath();
                        
                        _progressReporter?.Report(new TranscriptionProgress($"Using FFmpeg: {ffmpegBinaryPath}", 15));
                        _progressReporter?.Report(new TranscriptionProgress($"Using FFprobe: {ffprobeBinaryPath}", 20));
                        
                        // Simple test to verify FFprobe is working
                        var testResult = await TestFFmpegAsync();
                        if (testResult)
                        {
                            _progressReporter?.Report(new TranscriptionProgress("FFmpeg is working correctly", 25));
                            return true;
                        }
                        else
                        {
                            _progressReporter?.Report(new TranscriptionProgress("FFmpeg test failed", 0));
                        }
                    }
                    catch (Exception ex)
                    {
                        _progressReporter?.Report(new TranscriptionProgress($"FFmpeg test error: {ex.Message}", 0));
                        // FFmpeg binaries might be corrupted or missing dependencies, try to download again
                    }
                }
                else
                {
                    _progressReporter?.Report(new TranscriptionProgress("FFmpeg binaries not found locally", 5));
                }

                // Download and extract FFmpeg binaries
                return await DownloadFFmpegAsync();
            }
            catch (Exception ex)
            {
                _progressReporter?.Report(new TranscriptionProgress($"Failed to setup FFmpeg: {ex.Message}", 0));
                return false;
            }
        }

        /// <summary>
        /// Tests if FFmpeg is working by checking version
        /// </summary>
        private async Task<bool> TestFFmpegAsync()
        {
            try
            {
                // Create a simple test to verify FFmpeg is working
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = GetLocalFFmpegPath();
                process.StartInfo.Arguments = "-version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WorkingDirectory = _ffmpegDirectory;

                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && output.Contains("ffmpeg version"))
                {
                    return true;
                }
                else
                {
                    _progressReporter?.Report(new TranscriptionProgress($"FFmpeg test failed: {error}", 0));
                    return false;
                }
            }
            catch (Exception ex)
            {
                _progressReporter?.Report(new TranscriptionProgress($"FFmpeg test exception: {ex.Message}", 0));
                return false;
            }
        }

        /// <summary>
        /// Downloads FFmpeg binaries for the current platform
        /// </summary>
        private async Task<bool> DownloadFFmpegAsync()
        {
            try
            {
                _progressReporter?.Report(new TranscriptionProgress("Downloading FFmpeg binaries...", 5));

                string downloadUrl = GetFFmpegDownloadUrl();
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _progressReporter?.Report(new TranscriptionProgress("Unsupported platform for automatic FFmpeg download", 0));
                    return false;
                }

                await DownloadAndExtractFFmpegAsync(downloadUrl);

                // Configure FFMpegCore to use our extracted binaries
                GlobalFFOptions.Configure(new FFOptions { BinaryFolder = _ffmpegDirectory });

                _progressReporter?.Report(new TranscriptionProgress("FFmpeg setup completed", 20));
                return true;
            }
            catch (Exception ex)
            {
                _progressReporter?.Report(new TranscriptionProgress($"Failed to download FFmpeg: {ex.Message}", 0));
                return false;
            }
        }

        private async Task DownloadAndExtractFFmpegAsync(string downloadUrl)
        {
            var zipPath = Path.Combine(_workingDirectory, "ffmpeg.zip");
            await DownloadFFmpegFileAsync(downloadUrl, zipPath);
            await ExtractFFmpegAsync(zipPath);
            File.Delete(zipPath);
        }

        private async Task DownloadFFmpegFileAsync(string downloadUrl, string zipPath)
        {
            using (var response = await _httpClient.GetAsync(downloadUrl))
            {
                response.EnsureSuccessStatusCode();
                await using var fileStream = File.Create(zipPath);
                await response.Content.CopyToAsync(fileStream);
            }
        }

        /// <summary>
        /// Gets the appropriate FFmpeg download URL for the current platform
        /// </summary>
        private string GetFFmpegDownloadUrl()
        {
            if (OperatingSystem.IsWindows())
            {
                // Use a smaller, essentials-only build for Windows
                return "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl-shared.zip";
            }
            else if (OperatingSystem.IsLinux())
            {
                return "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl-shared.tar.xz";
            }
            else if (OperatingSystem.IsMacOS())
            {
                // For macOS, we'll provide instructions to use Homebrew instead
                return "";
            }
            
            return "";
        }

        /// <summary>
        /// Extracts FFmpeg binaries from the downloaded archive
        /// </summary>
        private async Task ExtractFFmpegAsync(string archivePath)
        {
            await Task.Run(() =>
            {
                if (OperatingSystem.IsWindows())
                {
                    using var archive = ZipFile.OpenRead(archivePath);
                    
                    // Extract all files from the bin directory of the archive
                    var binEntries = archive.Entries.Where(entry => 
                        entry.FullName.Contains("/bin/") && 
                        !string.IsNullOrEmpty(entry.Name) &&
                        (entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                         entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));

                    foreach (var entry in binEntries)
                    {
                        try
                        {
                            var destinationPath = Path.Combine(_ffmpegDirectory, entry.Name);
                            _progressReporter?.Report(new TranscriptionProgress($"Extracting {entry.Name}...", 16));
                            
                            // Ensure the destination directory exists
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                            
                            // Extract the file
                            entry.ExtractToFile(destinationPath, true);
                            
                            // On Windows, ensure the file is not blocked
                            if (entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                // Remove the "downloaded from internet" flag that might block execution
                                try
                                {
                                    File.SetAttributes(destinationPath, FileAttributes.Normal);
                                }
                                catch
                                {
                                    // Ignore if we can't set attributes
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _progressReporter?.Report(new TranscriptionProgress($"Failed to extract {entry.Name}: {ex.Message}", 0));
                            throw;
                        }
                    }

                    // Verify the main executables were extracted
                    if (!File.Exists(GetLocalFFmpegPath()) || !File.Exists(GetLocalFFprobePath()))
                    {
                        throw new InvalidOperationException("FFmpeg extraction completed but main executables not found");
                    }
                }
                else
                {
                    // For Linux, we would need to handle tar.xz extraction
                    // This is more complex and would require additional libraries
                    throw new NotSupportedException("Automatic FFmpeg download for Linux is not implemented. Please install FFmpeg using your package manager.");
                }
            });
        }

        /// <summary>
        /// Gets the path to the local ffmpeg executable
        /// </summary>
        private string GetLocalFFmpegPath()
        {
            return Path.Combine(_ffmpegDirectory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        }

        /// <summary>
        /// Gets the path to the local ffprobe executable
        /// </summary>
        private string GetLocalFFprobePath()
        {
            return Path.Combine(_ffmpegDirectory, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
        }

        /// <summary>
        /// Provides platform-specific instructions for installing FFmpeg
        /// </summary>
        public static string GetFFmpegInstallationInstructions()
        {
            if (OperatingSystem.IsWindows())
            {
                return """
                    FFmpeg will be automatically downloaded for Windows.
                    If automatic download fails, you can:
                    1. Download from: https://github.com/BtbN/FFmpeg-Builds/releases
                    2. Extract ffmpeg.exe and ffprobe.exe
                    3. Place them in the WorkingFiles/ffmpeg folder
                    """;
            }
            else if (OperatingSystem.IsMacOS())
            {
                return """
                    For macOS, please install FFmpeg using Homebrew:
                    
                    1. Install Homebrew: https://brew.sh/
                    2. Run: brew install ffmpeg
                    3. FFmpeg will be available system-wide
                    """;
            }
            else if (OperatingSystem.IsLinux())
            {
                return """
                    For Linux, please install FFmpeg using your package manager:
                    
                    Ubuntu/Debian: sudo apt-get install ffmpeg
                    CentOS/RHEL: sudo yum install ffmpeg
                    Fedora: sudo dnf install ffmpeg
                    Arch: sudo pacman -S ffmpeg
                    """;
            }
            else
            {
                return "Please install FFmpeg for your platform and ensure it's in your PATH.";
            }
        }

        /// <summary>
        /// Extracts audio from video file and saves to working directory
        /// </summary>
        /// <param name="videoFilePath">Path to video file</param>
        /// <param name="audioOutputPath">Optional custom audio output path</param>
        /// <returns>Path to extracted audio file</returns>
        public async Task<string> ExtractAudioAsync(string videoFilePath, string? audioOutputPath = null)
        {
            if (!File.Exists(videoFilePath))
                throw new FileNotFoundException($"Video file not found: {videoFilePath}");

            // Ensure FFmpeg is available
            if (!await EnsureFFmpegAvailableAsync())
            {
                throw new InvalidOperationException(
                    "FFmpeg is not available. " + GetFFmpegInstallationInstructions());
            }

            try
            {
                _progressReporter?.Report(new TranscriptionProgress(Prompts.Transcription.ExtractingAudio, 25));

                audioOutputPath ??= Path.Combine(_workingDirectory, $"{Path.GetFileNameWithoutExtension(videoFilePath)}_audio.wav");

                // Modified FFmpeg command to force segmentation
                await ProcessAudioAsync(videoFilePath, audioOutputPath);

                await Task.Delay(10);

                _progressReporter?.Report(new TranscriptionProgress(Prompts.Transcription.AudioExtractionComplete, 40));
                return audioOutputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to extract audio: {ex.Message}. " + GetFFmpegInstallationInstructions(), ex);
            }
        }

        private async Task<string> ProcessAudioAsync(string videoFilePath, string audioOutputPath)
        {
            await FFMpegArguments
                .FromFileInput(videoFilePath)
                .OutputToFile(audioOutputPath, true, options => options
                    .WithAudioCodec("pcm_s16le")
                    .WithAudioSamplingRate(16000)
                    .WithCustomArgument("-ac 1"))
                .ProcessAsynchronously();

            await Task.Delay(10);
            return audioOutputPath;
        }

        /// <summary>
        /// Transcribes audio file using Whisper
        /// </summary>
        /// <param name="audioFilePath">Path to audio file</param>
        /// <param name="modelPath">Path to Whisper model file (optional - will prompt for download location if not provided)</param>
        /// <returns>Transcription result with timestamps</returns>
        public async Task<TranscriptionResult> TranscribeAudioAsync(string audioFilePath, string? modelPath = null)
        {
            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException($"Audio file not found: {audioFilePath}");

            try
            {
                var factory = WhisperFactory.FromPath(modelPath ?? GetDefaultModelPath());
                _progressReporter?.Report(new TranscriptionProgress(Prompts.Transcription.StartingTranscription, 30));

                // Configure Whisper for shorter segments
                using var processor = factory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                var segmentCount = 0;
                var processedDuration = TimeSpan.Zero;
                var totalDuration = await GetAudioDurationAsync(audioFilePath);
                var lastProgressUpdate = DateTime.Now;

                // Create file paths
                var srtPath = Path.Combine(_workingDirectory, $"{Path.GetFileNameWithoutExtension(audioFilePath)}_transcription.srt");

                // Create or clear the files
                File.WriteAllText(srtPath, "");

                await foreach (var transcriptionResult in processor.ProcessAsync(File.OpenRead(audioFilePath)))
                {
                    (segmentCount, processedDuration) = await ProcessTranscriptionSegmentAsync(
                        transcriptionResult, 
                        srtPath, 
                        segmentCount,
                        processedDuration,
                        totalDuration);
                }

                _progressReporter?.Report(new TranscriptionProgress("Finalizing transcription...", 90));

                var result = new TranscriptionResult
                {
                    AudioFilePath = audioFilePath,
                    SrtFilePath = srtPath
                };

                _progressReporter?.Report(new TranscriptionProgress(
                    Prompts.Transcription.TranscriptionComplete(segmentCount, processedDuration), 100));
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Transcription failed: {ex.Message}", ex);
            }
        }

        private async Task<TimeSpan> GetAudioDurationAsync(string audioFilePath)
        {
            try
            {
                var audioInfo = await FFProbe.AnalyseAsync(audioFilePath);
                return audioInfo.Duration;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        private async Task<(int segmentCount, TimeSpan processedDuration)> ProcessTranscriptionSegmentAsync(
            SegmentData transcriptionResult,
            string srtPath,
            int currentSegmentCount,
            TimeSpan currentProcessedDuration,
            TimeSpan totalDuration)
        {
            var segment = new TranscriptionSegment
            {
                Start = transcriptionResult.Start,
                End = transcriptionResult.End,
                Text = transcriptionResult.Text?.Trim() ?? ""
            };

            await AppendSegmentToSrtFile(srtPath, segment, currentSegmentCount + 1);
            var newProcessedDuration = transcriptionResult.End;

            await UpdateTranscriptionProgressAsync(currentSegmentCount + 1, newProcessedDuration, totalDuration);
            
            return (currentSegmentCount + 1, newProcessedDuration);
        }

        private async Task UpdateTranscriptionProgressAsync(
            int segmentCount,
            TimeSpan processedDuration,
            TimeSpan totalDuration)
        {
            int progressPercent;
            string progressMessage;

            if (totalDuration > TimeSpan.Zero)
            {
                progressPercent = Math.Min(85, 40 + (int)((processedDuration.TotalSeconds / totalDuration.TotalSeconds) * 45));
                progressMessage = $"Transcribing... {processedDuration:mm\\:ss} / {totalDuration:mm\\:ss}";
            }
            else
            {
                progressPercent = Math.Min(85, 40 + (segmentCount * 2));
                progressMessage = $"Transcribing... {segmentCount} segments processed";
            }

           await Task.Run(() => _progressReporter?.Report(new TranscriptionProgress(progressMessage, progressPercent)));
        }

        private async Task AppendSegmentToSrtFile(string srtPath, TranscriptionSegment segment, int index)
        {
            var sourceFile = Path.GetFileName(srtPath)
                .Replace("_audio_transcription.srt", "")
                .Replace("_transcription.srt", "")
                + ".mp4";

            using var writer = new StreamWriter(srtPath, append: true);
            await writer.WriteLineAsync(index.ToString());
            await writer.WriteLineAsync($"{FormatTimeForSrt(segment.Start)} --> {FormatTimeForSrt(segment.End)}");
            await writer.WriteLineAsync($"[Source: {sourceFile}]");
            await writer.WriteLineAsync(segment.Text);
            await writer.WriteLineAsync();
        }

        /// <summary>
        /// Complete video transcription workflow
        /// </summary>
        /// <param name="videoFilePath">Path to video file</param>
        /// <param name="modelPath">Path to Whisper model file</param>
        /// <returns>Complete transcription result</returns>
        public async Task<TranscriptionResult> TranscribeVideoAsync(string videoFilePath, string? modelPath = null)
        {
            try
            {
                _progressReporter?.Report(new TranscriptionProgress("Starting video transcription...", 0));

                // Copy video to working directory first
                string safeFileName = Path.GetFileName(videoFilePath);
                string workingVideoPath = Path.Combine(_workingDirectory, safeFileName);
                _progressReporter?.Report(new TranscriptionProgress($"Copying video file to working directory...", 5));
                
                // If the file already exists in the working directory, append a number
                string baseFileName = Path.GetFileNameWithoutExtension(safeFileName);
                string extension = Path.GetExtension(safeFileName);
                int counter = 1;
                while (File.Exists(workingVideoPath))
                {
                    workingVideoPath = Path.Combine(_workingDirectory, $"{baseFileName}_{counter}{extension}");
                    counter++;
                }
                
                File.Copy(videoFilePath, workingVideoPath);
                _progressReporter?.Report(new TranscriptionProgress("Video file copied successfully", 10));

                // Extract audio from the working copy
                var audioPath = await ExtractAudioAsync(workingVideoPath);

                // Transcribe the audio
                var result = await TranscribeAudioAsync(audioPath, modelPath);
                
                // Update result with both original and working video paths
                result.VideoFilePath = videoFilePath;
                result.WorkingVideoPath = workingVideoPath;

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Video transcription failed: {ex.Message}", ex);
            }
        }

        private string GetDefaultModelPath()
        {
            string modelPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "Models",
                "whisper",
                "ggml-base.bin");

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException(
                    "Whisper model not found. Please download a model from: " +
                    "https://huggingface.co/ggerganov/whisper.cpp/tree/main " +
                    "and place the .bin file in the Models/whisper directory.");
            }

            return modelPath;
        }

        private static string FormatTimeForSrt(TimeSpan time)
        {
            return $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
        }

        private async Task ConfigureFFmpegAsync()
        {
            // Extract from EnsureFFmpegAvailableAsync
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = _ffmpegDirectory });
            var ffmpegBinaryPath = GlobalFFOptions.GetFFMpegBinaryPath();
            var ffprobeBinaryPath = GlobalFFOptions.GetFFProbeBinaryPath();
            
            _progressReporter?.Report(new TranscriptionProgress($"Using FFmpeg: {ffmpegBinaryPath}", 15));
            _progressReporter?.Report(new TranscriptionProgress($"Using FFprobe: {ffprobeBinaryPath}", 20));
            
            var testResult = await TestFFmpegAsync();
            // ... rest of configuration logic
        }
    }

    /// <summary>
    /// Represents a single transcription segment with timing information
    /// </summary>
    public class TranscriptionSegment
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Progress information for transcription operations
    /// </summary>
    public class TranscriptionProgress
    {
        public string Message { get; }
        public int PercentComplete { get; }

        public TranscriptionProgress(string message, int percentComplete)
        {
            Message = message;
            PercentComplete = Math.Clamp(percentComplete, 0, 100);
        }
    }

    /// <summary>
    /// Complete result of a transcription operation
    /// </summary>
    public class TranscriptionResult
    {
        public string? VideoFilePath { get; set; }
        public string? AudioFilePath { get; set; }
        public List<TranscriptionSegment> Segments { get; set; } = new();
        public string? SrtFilePath { get; set; }
        public string? WorkingVideoPath { get; set; }
    }
} 