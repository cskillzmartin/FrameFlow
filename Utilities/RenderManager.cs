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

                        // Skip additional quality vector fields from enhanced SRT format
                        while (i < lines.Length && lines[i].Contains(":") && 
                               (lines[i].StartsWith("Focus:") || lines[i].StartsWith("Clarity:") || 
                                lines[i].StartsWith("Emotion:") || lines[i].StartsWith("FlubScore:") || 
                                lines[i].StartsWith("CompositeScore:")))
                        {
                            i++;
                        }

                        // Now at text line
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

            var currentProject = ProjectHandler.Instance.CurrentProject;
            if (currentProject == null)
            {
                throw new InvalidOperationException("No project is currently loaded");
            }

            var projectPath = ProjectHandler.Instance.CurrentProjectPath;
            var mediaDir = Path.Combine(projectPath, "media");
            var tempDir = Path.Combine(renderDir, "temp");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Determine optimal encoding properties from project metadata
                var encodingProperties = DetermineOptimalEncodingProperties(clips, currentProject);

                // Create a temporary file list for FFmpeg concat
                var listPath = Path.Combine(tempDir, "filelist.txt");
                var segmentFiles = new List<string>();

                // First pass: Extract segments from each video with consistent parameters
                for (int i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    var inputPath = Path.Combine(mediaDir, clip.FileName);
                    var segmentPath = Path.Combine(tempDir, $"segment_{i:D4}.mp4");
                    segmentFiles.Add(segmentPath);

                    // Get specific metadata for this clip
                    var clipMetadata = GetClipMetadata(clip.FileName, currentProject);
                    await ExtractSegment(inputPath, segmentPath, clip.StartTime, clip.EndTime, encodingProperties, clipMetadata);
                    
                    // Verify segment was created successfully
                    if (!File.Exists(segmentPath) || new FileInfo(segmentPath).Length == 0)
                    {
                        throw new Exception($"Failed to create segment {i}: {segmentPath}");
                    }
                }

                // Create concat file with proper escaping
                var concatLines = segmentFiles.Select(f => $"file '{Path.GetFileName(f)}'");
                await File.WriteAllLinesAsync(listPath, concatLines);

                // Second pass: Concatenate all segments with stream copying for stability
                await ConcatenateSegments(listPath, outputPath, tempDir, encodingProperties);

                // Verify final output
                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                {
                    throw new Exception("Final output file was not created or is empty");
                }

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to render video: {ex.Message}", ex);
            }
            finally
            {
                // Cleanup temp files
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Warning: Failed to cleanup temp directory: {ex.Message}");
                }
            }
        }

        private VideoEncodingProperties DetermineOptimalEncodingProperties(List<ClipInfo> clips, ProjectModel project)
        {
            // Analyze all source videos to determine best compatible encoding properties
            var sourceMetadata = new List<VideoMetaData>();
            
            foreach (var clip in clips)
            {
                var mediaFile = project.MediaFiles.FirstOrDefault(m => m.FileName == clip.FileName);
                if (mediaFile?.VideoMetaData != null)
                {
                    sourceMetadata.Add(mediaFile.VideoMetaData);
                }
            }

            if (!sourceMetadata.Any())
            {
                // Fallback to safe defaults if no metadata available
                Debug.WriteLine("No video metadata found in project, using safe defaults");
                return new VideoEncodingProperties();
            }

            // Find the most common/compatible properties across all source videos
            var frameRates = sourceMetadata.Where(m => m.VideoStreamInfo != null)
                                          .Select(m => m.VideoStreamInfo!.FrameRate)
                                          .Where(fr => fr > 0)
                                          .ToList();
            
            var pixelFormats = sourceMetadata.Where(m => m.VideoStreamInfo != null)
                                           .Select(m => m.VideoStreamInfo!.PixelFormat)
                                           .Where(pf => !string.IsNullOrEmpty(pf))
                                           .ToList();

            var audioSampleRates = sourceMetadata.Where(m => m.AudioStreamInfo != null)
                                                .Select(m => m.AudioStreamInfo!.SampleRate)
                                                .Where(sr => sr > 0)
                                                .ToList();

            var audioChannels = sourceMetadata.Where(m => m.AudioStreamInfo != null)
                                             .Select(m => m.AudioStreamInfo!.Channels)
                                             .Where(ch => ch > 0)
                                             .ToList();

            return new VideoEncodingProperties
            {
                FrameRate = frameRates.Any() ? frameRates.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key.ToString("F2") : "30.00",
                PixelFormat = pixelFormats.Any() ? pixelFormats.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key : "yuv420p",
                AudioSampleRate = audioSampleRates.Any() ? audioSampleRates.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key.ToString() : "48000",
                AudioChannels = audioChannels.Any() ? audioChannels.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key.ToString() : "2"
            };
        }

        private VideoMetaData? GetClipMetadata(string fileName, ProjectModel project)
        {
            return project.MediaFiles.FirstOrDefault(m => m.FileName == fileName)?.VideoMetaData;
        }

        private async Task ExtractSegment(string inputPath, string outputPath, TimeSpan start, TimeSpan end, VideoEncodingProperties encodingProps, VideoMetaData? clipMetadata)
        {
            var duration = end - start;
            var startStr = start.ToString(@"hh\:mm\:ss\.fff");
            var durationStr = duration.ToString(@"hh\:mm\:ss\.fff");

            // Determine if we can use stream copy for better performance and quality
            bool canUseStreamCopy = CanUseStreamCopy(clipMetadata, encodingProps);
            
            string videoCodecArgs;
            string audioCodecArgs;

            if (canUseStreamCopy)
            {
                // Use stream copy when source is already compatible
                videoCodecArgs = "-c:v copy";
                audioCodecArgs = "-c:a copy";
                Debug.WriteLine($"Using stream copy for {Path.GetFileName(inputPath)} - compatible format detected");
            }
            else
            {
                // Re-encode with consistent parameters when necessary
                videoCodecArgs = $"-c:v libx264 -preset slow -crf 18 " +
                               $"-profile:v high -level 4.1 " +
                               $"-pix_fmt {encodingProps.PixelFormat} " +
                               $"-r {encodingProps.FrameRate} " +
                               $"-g 60 -keyint_min 30 " +
                               $"-sc_threshold 0";

                audioCodecArgs = $"-c:a aac -b:a 192k -ar {encodingProps.AudioSampleRate} -ac {encodingProps.AudioChannels}";
                Debug.WriteLine($"Re-encoding {Path.GetFileName(inputPath)} for compatibility");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = Settings.Instance.FfmpegPath,
                Arguments = $"-ss {startStr} -i \"{inputPath}\" -t {durationStr} " +
                          $"{videoCodecArgs} " +
                          $"{audioCodecArgs} " +
                          $"-fflags +genpts " +                    // Generate presentation timestamps
                          $"-avoid_negative_ts make_zero " +       // Handle negative timestamps
                          $"-max_muxing_queue_size 1024 " +        // Increase muxing buffer
                          $"-y \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("Failed to start FFmpeg process for segment extraction");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            string error = await errorTask;
            string output = await outputTask;

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg segment extraction failed for {Path.GetFileName(inputPath)} [{startStr}-{durationStr}]: {error}");
            }

            Debug.WriteLine($"Extracted segment: {Path.GetFileName(outputPath)} - Duration: {durationStr}");
        }

        private bool CanUseStreamCopy(VideoMetaData? clipMetadata, VideoEncodingProperties targetProps)
        {
            if (clipMetadata?.VideoStreamInfo == null || clipMetadata?.AudioStreamInfo == null)
                return false;

            var video = clipMetadata.VideoStreamInfo;
            var audio = clipMetadata.AudioStreamInfo;

            // Check if source properties match target encoding properties
            bool videoCompatible = video.Codec.Equals("h264", StringComparison.OrdinalIgnoreCase) &&
                                 video.PixelFormat.Equals(targetProps.PixelFormat, StringComparison.OrdinalIgnoreCase) &&
                                 Math.Abs(video.FrameRate - float.Parse(targetProps.FrameRate)) < 0.1f;

            bool audioCompatible = audio.Codec.Equals("aac", StringComparison.OrdinalIgnoreCase) &&
                                 audio.SampleRate.ToString() == targetProps.AudioSampleRate &&
                                 audio.Channels.ToString() == targetProps.AudioChannels;

            return videoCompatible && audioCompatible;
        }

        private async Task ConcatenateSegments(string listPath, string outputPath, string workingDir, VideoEncodingProperties encodingProps)
        {
            // Use concat demuxer for better stability with different source formats
            var startInfo = new ProcessStartInfo
            {
                FileName = Settings.Instance.FfmpegPath,
                Arguments = $"-f concat -safe 0 -i \"{Path.GetFileName(listPath)}\" " +  // Use relative path
                          $"-c:v libx264 -preset slow -crf 18 " +        // Consistent encoding
                          $"-profile:v high -level 4.1 " +               // H.264 high profile
                          $"-c:a aac -b:a 192k -ar {encodingProps.AudioSampleRate} -ac {encodingProps.AudioChannels} " +
                          $"-pix_fmt {encodingProps.PixelFormat} " +
                          $"-r {encodingProps.FrameRate} " +             // Consistent frame rate
                          $"-g 60 -keyint_min 30 " +                     // GOP structure
                          $"-sc_threshold 0 " +                          // Disable scene change detection
                          $"-fflags +genpts " +                          // Generate presentation timestamps
                          $"-avoid_negative_ts make_zero " +             // Handle negative timestamps
                          $"-max_muxing_queue_size 2048 " +              // Larger buffer for concatenation
                          $"-movflags +faststart " +                     // Optimize for streaming
                          $"-y \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir  // Set working directory for relative paths
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("Failed to start FFmpeg process for concatenation");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            string error = await errorTask;
            string output = await outputTask;

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg concatenation failed: {error}");
            }

            Debug.WriteLine($"Successfully concatenated {Path.GetFileName(outputPath)}");
        }

        private class VideoEncodingProperties
        {
            public string FrameRate { get; set; } = "30.00";
            public string PixelFormat { get; set; } = "yuv420p";
            public string AudioSampleRate { get; set; } = "48000";
            public string AudioChannels { get; set; } = "2";
        }
    }
} 