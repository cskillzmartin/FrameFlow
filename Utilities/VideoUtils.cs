using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace FrameFlow.Utilities
{
    public class VideoUtils
    {
        private readonly string _ffmpegPath;
        private readonly string _projectDir;
        private readonly IProgress<TranscriptionProgress>? _progress;

        public VideoUtils(string projectDir, IProgress<TranscriptionProgress>? progress = null)
        {
            _projectDir = projectDir;
            _progress = progress;
            _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Utilities", "ffmpeg", "ffmpeg.exe");
            
            if (!File.Exists(_ffmpegPath))
            {
                throw new FileNotFoundException($"FFmpeg not found at: {_ffmpegPath}");
            }
        }

        public async Task CreateEditedVideoAsync(string reorderedSrtPath)
        {
            var segments = SrtUtils.ParseSrt(reorderedSrtPath);
            Debug.WriteLine($"Found {segments.Count} segments in {reorderedSrtPath}");
            
            if (segments.Count == 0)
            {
                throw new InvalidOperationException("No segments found in the reordered SRT file.");
            }

            string tempDir = Path.Combine(_projectDir, "temp_segments");
            Directory.CreateDirectory(tempDir);

            try
            {
                var segmentFiles = new List<string>();
                int totalSegments = segments.Count;
                
                _progress?.Report(new TranscriptionProgress("Extracting video segments", 0));
                
                for (int i = 0; i < segments.Count; i++)
                {
                    var segment = segments[i];
                    
                    // Debug info
                    Debug.WriteLine($"\nProcessing segment {i + 1}:");
                    Debug.WriteLine($"Text: {segment.Text}");
                    Debug.WriteLine($"Time: {segment.Start} -> {segment.End}");
                 
                    string sourceVideoPath = Path.Combine(_projectDir, segment.SourceFile);
                    Debug.WriteLine($"Video path: {sourceVideoPath}");

                    if (!File.Exists(sourceVideoPath))
                    {
                        var availableVideos = Directory.GetFiles(_projectDir, "*.mp4")
                                                     .Select(Path.GetFileName)
                                                     .ToList();
                        Debug.WriteLine("Available videos:");
                        foreach (var video in availableVideos)
                        {
                            Debug.WriteLine($"  {video}");
                        }
                        throw new FileNotFoundException(
                            $"Source video not found for: {segment.SourceFile}\n" +
                            $"Available videos: {string.Join(", ", availableVideos)}");
                    }

                    string outputSegment = Path.Combine(tempDir, $"segment_{i:D4}.mp4");
                    await ExtractSegmentAsync(sourceVideoPath, segment.Start, segment.End, outputSegment);
                    
                    // Verify the extracted segment
                    if (new FileInfo(outputSegment).Length == 0)
                    {
                        throw new Exception($"Extracted segment file is empty: {outputSegment}");
                    }
                    
                    segmentFiles.Add(outputSegment);
                    
                    _progress?.Report(new TranscriptionProgress(
                        $"Extracted segment {i + 1} of {totalSegments}",
                        (i * 50) / totalSegments));
                }

                // Step 2: Create concat file after all segments are extracted
                _progress?.Report(new TranscriptionProgress("Preparing to combine segments", 50));
                
                var concatFilePath = Path.Combine(tempDir, "concat.txt");
                var concatLines = segmentFiles.Select(f => $"file '{f.Replace("'", "\\'")}'");
                await File.WriteAllLinesAsync(concatFilePath, concatLines);

                // Step 3: Combine all segments
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string outputPath = Path.Combine(_projectDir, $"edit_output_{timestamp}.mp4");

                _progress?.Report(new TranscriptionProgress("Combining segments into final video", 75));

                await CombineSegmentsAsync(concatFilePath, outputPath);

                // Ensure the final file exists and is complete
                if (!File.Exists(outputPath))
                {
                    throw new Exception("Failed to create final video file");
                }

                _progress?.Report(new TranscriptionProgress("Cleaning up temporary files", 90));

                // Cleanup
                foreach (var file in segmentFiles)
                {
                    try { File.Delete(file); } catch { }
                }
                try { File.Delete(concatFilePath); } catch { }
                try { Directory.Delete(tempDir); } catch { }

                _progress?.Report(new TranscriptionProgress($"Video created successfully: {Path.GetFileName(outputPath)}", 100));
            }
            catch (Exception ex)
            {
                // Cleanup on error
                try { Directory.Delete(tempDir, true); } catch { }
                throw;
            }
        }

        private async Task ExtractSegmentAsync(string sourceVideo, TimeSpan start, TimeSpan end, string outputPath)
        {
            var duration = end - start;
            var startStr = $"{start.Hours:D2}:{start.Minutes:D2}:{start.Seconds:D2}.{start.Milliseconds:D3}";
            var durationStr = $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}.{duration.Milliseconds:D3}";

            // More robust FFmpeg command with:
            // 1. Accurate seeking
            // 2. Proper keyframe alignment
            // 3. Audio sync preservation
            var arguments = $"-ss {startStr} -i \"{sourceVideo}\" -t {durationStr} " +
                           $"-c:v libx264 -preset ultrafast -c:a aac " + // Re-encode for clean cuts
                           $"-avoid_negative_ts 1 -async 1 " + // Better audio/video sync
                           $"-y \"{outputPath}\"";

            Debug.WriteLine("\nFFmpeg command:");
            Debug.WriteLine($"{_ffmpegPath} {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = new Process { StartInfo = startInfo };
            var errorBuilder = new StringBuilder();
            
            process.ErrorDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    Debug.WriteLine($"FFmpeg: {e.Data}");
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            // Ensure the process has fully completed
            await Task.Delay(50);

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg failed to extract segment:\n{errorBuilder}");
            }

            // Verify the output file
            if (!File.Exists(outputPath))
            {
                throw new Exception("FFmpeg did not create output file");
            }

            var fileInfo = new FileInfo(outputPath);
            if (fileInfo.Length == 0)
            {
                throw new Exception("FFmpeg created an empty output file");
            }

            // Verify the segment is valid video
            await VerifyVideoSegment(outputPath);

            Debug.WriteLine($"Successfully created segment: {outputPath} ({fileInfo.Length} bytes)");
        }

        private async Task VerifyVideoSegment(string videoPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-v error -i \"{videoPath}\" -f null -",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            var errorBuilder = new StringBuilder();
            
            process.ErrorDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Segment validation failed: {errorBuilder}");
            }
        }

        private async Task CombineSegmentsAsync(string concatFile, string outputPath)
        {
            // More robust combining command with:
            // 1. Proper format settings
            // 2. Error checking
            // 3. Clean metadata
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-f concat -safe 0 -i \"{concatFile}\" " +
                           $"-c copy -movflags +faststart " + // Optimize for streaming
                           $"-map_metadata -1 " + // Clean metadata
                           $"-y \"{outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = new Process { StartInfo = startInfo };
            var errorBuilder = new StringBuilder();
            
            process.ErrorDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    Debug.WriteLine($"FFmpeg: {e.Data}");
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            // Ensure the process has fully completed
            await Task.Delay(50);

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg failed to combine segments: {errorBuilder}");
            }

            // Verify the final video
            await VerifyVideoSegment(outputPath);
        }
    }
} 