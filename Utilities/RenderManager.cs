using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FrameFlow.App;
using FrameFlow.Models;

namespace FrameFlow.Utilities
{
    public class RenderManager
    {
        private static RenderManager? _instance;
        private readonly object _lock = new object();

        private RenderManager()
        {
            if (string.IsNullOrEmpty(Settings.Instance.FfmpegPath) || !File.Exists(Settings.Instance.FfmpegPath))
            {
                throw new FileNotFoundException("ffmpeg not found. Please configure ffmpeg path in settings", Settings.Instance.FfmpegPath);
            }
        }

        public static RenderManager Instance
        {
            get
            {
                _instance ??= new RenderManager();
                return _instance;
            }
        }

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

        public async Task<List<ClipInfo>> ParseTrimmedSrtFile(string projectName, string renderDir)
        {
            var clips = new List<ClipInfo>();
            var trimmedFilePath = Path.Combine(renderDir, $"{projectName}.trim.srt");

            if (!File.Exists(trimmedFilePath))
            {
                throw new FileNotFoundException("Trimmed SRT file not found", trimmedFilePath);
            }

            var lines = await File.ReadAllLinesAsync(trimmedFilePath);
            for (int i = 0; i < lines.Length;)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                if (int.TryParse(lines[i], out _))
                {
                    try
                    {
                        i++; // Move to timestamp line
                        if (i >= lines.Length) break;

                        var timeParts = lines[i].Split(" --> ");
                        var start = TimeSpan.Parse(timeParts[0].Replace(',', '.'));
                        var end = TimeSpan.Parse(timeParts[1].Replace(',', '.'));

                        i++; // Move to filename line
                        var fileName = lines[i];

                        i++; // Move to relevance score line
                        var relevance = float.Parse(lines[i].Split(": ")[1]);
                        i++; // Move to sentiment score line
                        var sentiment = float.Parse(lines[i].Split(": ")[1]);
                        i++; // Move to novelty score line
                        var novelty = float.Parse(lines[i].Split(": ")[1]);
                        i++; // Move to energy score line
                        var energy = float.Parse(lines[i].Split(": ")[1]);

                        i++; // Move to text line
                        var textBuilder = new StringBuilder();
                        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                        {
                            textBuilder.AppendLine(lines[i]);
                            i++;
                        }

                        clips.Add(new ClipInfo
                        {
                            FileName = fileName,
                            StartTime = start,
                            EndTime = end,
                            Text = textBuilder.ToString().Trim(),
                            Relevance = relevance,
                            Sentiment = sentiment,
                            Novelty = novelty,
                            Energy = energy
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error parsing segment: {ex.Message}");
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }

            return clips;
        }

        public async Task<string> RenderVideoAsync(string projectName, string outputPath, string renderDir)
        {
            var clips = await ParseTrimmedSrtFile(projectName, renderDir);
            if (!clips.Any())
            {
                throw new InvalidOperationException("No clips found to render");
            }

            var projectPath = ProjectHandler.Instance.CurrentProjectPath;
            var mediaDir = Path.Combine(projectPath, "media");
            var tempDir = Path.Combine(renderDir, "temp");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create a temporary file list for FFmpeg concat
                var listPath = Path.Combine(tempDir, "filelist.txt");
                var segmentFiles = new List<string>();

                // First pass: Extract segments from each video
                for (int i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    var inputPath = Path.Combine(mediaDir, clip.FileName);
                    var segmentPath = Path.Combine(tempDir, $"segment_{i}.mp4");
                    segmentFiles.Add(segmentPath);

                    await ExtractSegment(inputPath, segmentPath, clip.StartTime, clip.EndTime);
                }

                // Create concat file
                await File.WriteAllLinesAsync(listPath, segmentFiles.Select(f => $"file '{f}'"));

                // Second pass: Concatenate all segments
                await ConcatenateSegments(listPath, outputPath);

                // Cleanup temp files
                foreach (var file in segmentFiles)
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                if (File.Exists(listPath)) File.Delete(listPath);

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to render video: {ex.Message}", ex);
            }
            finally
            {
                // Ensure temp directory is cleaned up
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private async Task ExtractSegment(string inputPath, string outputPath, TimeSpan start, TimeSpan end)
        {
            var duration = end - start;
            var startStr = start.ToString(@"hh\:mm\:ss\.fff");
            var durationStr = duration.ToString(@"hh\:mm\:ss\.fff");

            var startInfo = new ProcessStartInfo
            {
                FileName = Settings.Instance.FfmpegPath,
                Arguments = $"-ss {startStr} -i \"{inputPath}\" -t {durationStr} " +
                          $"-c:v libx264 -preset fast -crf 22 " +
                          $"-c:a aac -b:a 128k " +
                          $"-force_key_frames \"expr:gte(t,0)\" " +
                          $"-vsync cfr -r 30 " +
                          $"-refs 4 -g 30 " +
                          $"-keyint_min 1 " +
                          $"-bf 2 " +
                          $"-pix_fmt yuv420p " +
                          $"\"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("Failed to start FFmpeg process");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg segment extraction failed: {error}");
            }
        }

        private async Task ConcatenateSegments(string listPath, string outputPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Settings.Instance.FfmpegPath,
                Arguments = $"-f concat -safe 0 -i \"{listPath}\" " +
                          $"-c:v libx264 -preset medium -crf 22 " +
                          $"-c:a aac -b:a 128k " +
                          $"-vsync cfr -r 30 " +
                          $"-refs 4 -g 30 " +
                          $"-keyint_min 1 " +
                          $"-bf 2 " +
                          $"-pix_fmt yuv420p " +
                          $"-movflags +faststart " +
                          $"\"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("Failed to start FFmpeg process");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg concatenation failed: {error}");
            }
        }
    }
} 