using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FrameFlow.App;
using FrameFlow.Models;
using FaceAiSharp;
using FaceAiSharp.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FrameFlow.Utilities
{
    public class SpeakerManager
    {
        private static SpeakerManager? _instance;
        private readonly object _lock = new object();

        // FaceAiSharp components - lazy loaded for performance
        private IFaceDetectorWithLandmarks? _faceDetector;
        private IFaceEmbeddingsGenerator? _faceEmbeddingsGenerator;


        private SpeakerManager() { }

        public static SpeakerManager Instance
        {
            get
            {
                _instance ??= new SpeakerManager();
                return _instance;
            }
        }

        // Initialize FaceAiSharp components
        private IFaceDetectorWithLandmarks FaceDetector
        {
            get
            {
                if (_faceDetector == null)
                {
                    lock (_lock)
                    {
                        _faceDetector ??= FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
                    }
                }
                return _faceDetector;
            }
        }

        private IFaceEmbeddingsGenerator FaceEmbeddingsGenerator
        {
            get
            {
                if (_faceEmbeddingsGenerator == null)
                {
                    lock (_lock)
                    {
                        _faceEmbeddingsGenerator ??= FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
                    }
                }
                return _faceEmbeddingsGenerator;
            }
        }

        // Data structures for speaker and shot analysis
        public class SpeakerSegment
        {
            public string SegmentId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string Text { get; set; } = string.Empty;
            public string SpeakerId { get; set; } = "UNK";
            public float SpeakerConf { get; set; } = 0.0f;
            public List<string> FaceIds { get; set; } = new List<string>();
            public ShotType ShotLabel { get; set; } = ShotType.UNK;
            public float ShotConf { get; set; } = 0.0f;
            public List<string> DetectedFaceIds { get; set; } = new List<string>();
        }

        public class DetectedFace
        {
            public string FaceId { get; set; } = string.Empty;
            [JsonIgnore]
            public float[] Embedding { get; set; } = Array.Empty<float>();
            public float Confidence { get; set; } = 0.0f;
            public FaceRectangle BoundingBox { get; set; } = new FaceRectangle();
            public FacePoint[] Landmarks { get; set; } = Array.Empty<FacePoint>();
            public string? EmbeddingHash { get; set; }
            public bool HasEmbedding => !string.IsNullOrEmpty(EmbeddingHash);
        }

        // Simple structs to replace ImageSharp types when not available
        public struct FaceRectangle
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        public struct FacePoint
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public enum ShotType
        {
            CU,        // Close-up
            MS,        // Medium shot
            OTS_CU,    // Over-the-shoulder close-up
            WS,        // Wide shot
            INSERT,    // Insert shot
            UNK        // Unknown
        }

        public class SpeakerCluster
        {
            public string ClusterId { get; set; } = string.Empty;
            [JsonIgnore]
            public List<float> VoiceEmbedding { get; set; } = new List<float>();
            [JsonIgnore]
            public List<float> FaceEmbedding { get; set; } = new List<float>();
            public int SegmentCount { get; set; } = 0;
            public float Confidence { get; set; } = 0.0f;
            public List<string> LinkedFaceIds { get; set; } = new List<string>();
            public string? VoiceEmbeddingHash { get; set; }
            public string? FaceEmbeddingHash { get; set; }
            public int EmbeddingDimensions { get; set; } = 512;
        }

        public class SpeakerMetadata
        {
            public string ProjectName { get; set; } = string.Empty;
            public DateTime ProcessedDate { get; set; } = DateTime.Now;
            public List<SpeakerSegment> Segments { get; set; } = new List<SpeakerSegment>();
            public List<SpeakerCluster> Clusters { get; set; } = new List<SpeakerCluster>();
            public Dictionary<string, DetectedFace> FaceDatabase { get; set; } = new Dictionary<string, DetectedFace>();
            public SpeakerProcessingSettings Settings { get; set; } = new SpeakerProcessingSettings();
            public bool FaceAiSharpAvailable { get; set; } = false;
            public EmbeddingCompressionInfo CompressionInfo { get; set; } = new EmbeddingCompressionInfo();
        }

        public class SpeakerProcessingSettings
        {
            public float SpeakerConfidenceThreshold { get; set; } = 0.6f;
            public float ShotConfidenceThreshold { get; set; } = 0.5f;
            public float FaceDetectionThreshold { get; set; } = 0.3f;
            public float FaceSimilarityThreshold { get; set; } = 0.42f; // FaceAiSharp recommended threshold
            public bool EnableFaceDetection { get; set; } = true;
            public bool EnableShotClassification { get; set; } = true;
            public float FaceVoiceLinkingThreshold { get; set; } = 0.8f;
            public int MaxFacesPerSegment { get; set; } = 5;
        }

        // New: Track compression and file organization
        public class EmbeddingCompressionInfo
        {
            public bool UseExternalEmbeddings { get; set; } = true;
            public string EmbeddingFileFormat { get; set; } = "binary"; // "binary" or "hdf5"
            public int TotalFaces { get; set; } = 0;
            public int TotalClusters { get; set; } = 0;
            public long OriginalSizeBytes { get; set; } = 0;
            public long CompressedSizeBytes { get; set; } = 0;
            public float CompressionRatio => OriginalSizeBytes > 0 ? (float)CompressedSizeBytes / OriginalSizeBytes : 1.0f;
        }

        // New: In-memory embedding cache
        private static readonly Dictionary<string, float[]> _embeddingCache = new Dictionary<string, float[]>();
        private static readonly object _embeddingCacheLock = new object();

        /// <summary>
        /// Main entry point for speaker and shot analysis pipeline
        /// </summary>
        public async Task<bool> ProcessSpeakerAnalysisAsync(string projectName, string renderDir)
        {
            try
            {
                var currentProject = ProjectHandler.Instance.CurrentProject;
                if (currentProject == null)
                {
                    throw new InvalidOperationException("No project is currently loaded");
                }

                Debug.WriteLine($"Starting speaker analysis for project: {projectName}");
                Debug.WriteLine($"FaceAiSharp available: {true}");

                // Check if analysis already exists and is up to date
                var cacheFilePath = GetCacheFilePath(renderDir, projectName);
                if (await IsCacheValidAsync(cacheFilePath, renderDir, projectName))
                {
                    Debug.WriteLine("Speaker analysis cache is valid, skipping processing");
                    return true;
                }

                // Load segments from SRT files
                var segments = await LoadSegmentsFromSrtAsync(projectName, renderDir);
                if (!segments.Any())
                {
                    Debug.WriteLine("No segments found for speaker analysis");
                    return false;
                }

                // Create processing settings
                var settings = CreateProcessingSettings();

                // Execute speaker analysis pipeline
                var metadata = new SpeakerMetadata
                {
                    ProjectName = projectName,
                    ProcessedDate = DateTime.Now,
                    Settings = settings,
                    FaceAiSharpAvailable = true
                };

                // Step 1: Speaker Diarisation (placeholder for now)
                await PerformSpeakerDiarisationAsync(segments, metadata, currentProject);

                // Step 2: Face Detection & Embedding (if FaceAiSharp available)
                if (settings.EnableFaceDetection)
                {
                    await PerformFaceDetectionWithFaceAiSharpAsync(segments, metadata, currentProject);
                }
                else if (settings.EnableFaceDetection)
                {
                    Debug.WriteLine("Face detection requested but FaceAiSharp not available. Install FaceAiSharp.Bundle package to enable face detection.");
                    await PerformMockFaceDetectionAsync(segments, metadata);
                }

                // Step 3: Face Clustering and Speaker Association
                if (settings.EnableFaceDetection)
                {
                    await PerformFaceClusteringAsync(segments, metadata);
                }

                // Step 4: Speaker↔Face Linking
                if (settings.EnableFaceDetection)
                {
                    await LinkSpeakersToFacesAsync(segments, metadata);
                }

                // Step 5: Shot-Type Classification
                if (settings.EnableShotClassification)
                {
                    await PerformShotClassificationAsync(segments, metadata, currentProject);
                }

                // Step 6: Confidence Filtering
                ApplyConfidenceFiltering(segments, settings);

                // Step 7: Persist & Cache
                metadata.Segments = segments;
                await SaveCacheAsync(cacheFilePath, metadata);

                Debug.WriteLine($"Speaker analysis completed for {segments.Count} segments with {metadata.FaceDatabase.Count} unique faces");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing speaker analysis: {ex.Message}");
                throw new Exception($"Failed to process speaker analysis: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load existing speaker analysis results
        /// </summary>
        public async Task<SpeakerMetadata?> LoadSpeakerAnalysisAsync(string projectName, string renderDir)
        {
            try
            {
                var cacheFilePath = GetCacheFilePath(renderDir, projectName);
                if (!File.Exists(cacheFilePath))
                {
                    return null;
                }

                var jsonContent = await File.ReadAllTextAsync(cacheFilePath);
                var metadata = JsonSerializer.Deserialize<SpeakerMetadata>(jsonContent);
                
                if (metadata == null)
                {
                    return null;
                }

                // Load external embeddings if they exist
                if (metadata.CompressionInfo.UseExternalEmbeddings)
                {
                    await LoadEmbeddingsExternallyAsync(cacheFilePath, metadata);
                    Debug.WriteLine($"Loaded compressed speaker analysis: {metadata.CompressionInfo.CompressionRatio:P1} compression ratio");
                }

                return metadata;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading speaker analysis: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get speaker statistics for the current analysis
        /// </summary>
        public Dictionary<string, int> GetSpeakerStatistics(SpeakerMetadata metadata)
        {
            var stats = new Dictionary<string, int>();
            foreach (var segment in metadata.Segments)
            {
                if (stats.ContainsKey(segment.SpeakerId))
                {
                    stats[segment.SpeakerId]++;
                }
                else
                {
                    stats[segment.SpeakerId] = 1;
                }
            }
            return stats;
        }

        /// <summary>
        /// Get shot type distribution for the current analysis
        /// </summary>
        public Dictionary<ShotType, int> GetShotTypeStatistics(SpeakerMetadata metadata)
        {
            var stats = new Dictionary<ShotType, int>();
            foreach (var segment in metadata.Segments)
            {
                if (stats.ContainsKey(segment.ShotLabel))
                {
                    stats[segment.ShotLabel]++;
                }
                else
                {
                    stats[segment.ShotLabel] = 1;
                }
            }
            return stats;
        }

        private async Task<List<SpeakerSegment>> LoadSegmentsFromSrtAsync(string projectName, string renderDir)
        {
            var segments = new List<SpeakerSegment>();
            
            // At this point, only TakeManager output exists in renderDir
            // Look for TakeManager output files first, then fall back to original transcriptions
            var possibleSrtFiles = new List<string>();
            
            // 1. Check for TakeManager output in render directory
            var takeManagerFiles = Directory.GetFiles(renderDir, "*.srt");
            possibleSrtFiles.AddRange(takeManagerFiles);
            
            // 2. Fall back to original transcription files in project folder
            if (!possibleSrtFiles.Any())
            {
                var currentProjectPath = ProjectHandler.Instance.CurrentProjectPath;
                var transcriptionsDir = Path.Combine(currentProjectPath, "Transcriptions");
                if (Directory.Exists(transcriptionsDir))
                {
                    var originalTranscriptions = Directory.GetFiles(transcriptionsDir, "*.srt");
                    possibleSrtFiles.AddRange(originalTranscriptions);
                }
            }

            // Aggregate segments from ALL available SRT files
            int totalSegments = 0;
            foreach (var srtFile in possibleSrtFiles.Distinct())
            {
                if (!File.Exists(srtFile)) continue;

                var parsed = await ParseSrtFileAsync(srtFile);
                if (parsed.Any())
                {
                    segments.AddRange(parsed);
                    totalSegments += parsed.Count;
                    Debug.WriteLine($"Loaded {parsed.Count} segments from {Path.GetFileName(srtFile)}");
                }
            }

            Debug.WriteLine($"Total segments loaded across SRT files: {totalSegments}");
            return segments;
        }

        private async Task<List<SpeakerSegment>> ParseSrtFileAsync(string srtFilePath)
        {
            var segments = new List<SpeakerSegment>();
            var lines = await File.ReadAllLinesAsync(srtFilePath);
            
            // Derive video filename from SRT filepath by looking in media directory
            var srtFileName = Path.GetFileNameWithoutExtension(srtFilePath);
            var videoFileName = FindMatchingVideoFile(srtFileName);

            for (int i = 0; i < lines.Length;)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                if (int.TryParse(lines[i], out int segmentNumber))
                {
                    try
                    {
                        i++; // Move to timestamp line
                        if (i >= lines.Length) break;

                        var timeParts = lines[i].Split(" --> ");
                        var start = TimeSpan.Parse(timeParts[0].Replace(',', '.'));
                        var end = TimeSpan.Parse(timeParts[1].Replace(',', '.'));

                        i++; // Move to text content
                        
                        // Collect all text lines until we hit an empty line or next segment number
                        var textBuilder = new StringBuilder();
                        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !int.TryParse(lines[i], out _))
                        {
                            textBuilder.AppendLine(lines[i]);
                            i++;
                        }
                        
                        var text = textBuilder.ToString().Trim();

                        segments.Add(new SpeakerSegment
                        {
                            SegmentId = $"SEG_{segmentNumber:D4}",
                            FileName = videoFileName,
                            StartTime = start,
                            EndTime = end,
                            Text = text
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error parsing SRT segment {segmentNumber}: {ex.Message}");
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }

            return segments;
        }

        private string FindMatchingVideoFile(string baseFileName)
        {
            // Get the current project's media directory
            var mediaDir = Path.Combine(App.ProjectHandler.Instance.CurrentProjectPath, "media");
            
            // Search for matching video files using supported formats
            foreach (var format in App.Settings.Instance.SupportedVideoFormats)
            {
                var videoFileName = $"{baseFileName}{format}";
                var videoFilePath = Path.Combine(mediaDir, videoFileName);
                
                if (File.Exists(videoFilePath))
                {
                    Debug.WriteLine($"Found matching video file: {videoFileName}");
                    return videoFileName;
                }
            }

            // If no matching file found, log warning and return mp4 assumption
            Debug.WriteLine($"No matching video file found for {baseFileName} in supported formats: {string.Join(", ", App.Settings.Instance.SupportedVideoFormats)}");
            return $"{baseFileName}.mp4"; // Fallback assumption
        }

        private SpeakerProcessingSettings CreateProcessingSettings()
        {
            return new SpeakerProcessingSettings
            {
                SpeakerConfidenceThreshold = 0.6f,
                ShotConfidenceThreshold = 0.5f,
                FaceDetectionThreshold = 0.3f,
                FaceSimilarityThreshold = 0.42f,
                EnableFaceDetection = true,
                EnableShotClassification = true,
                FaceVoiceLinkingThreshold = 0.8f,
                MaxFacesPerSegment = 5
            };
        }

        private async Task PerformSpeakerDiarisationAsync(List<SpeakerSegment> segments, SpeakerMetadata metadata, ProjectModel project)
        {
            Debug.WriteLine("Performing speaker diarisation using existing Whisper transcriptions...");
            
            // Use existing Whisper transcriptions + speaker change detection
            var currentProjectPath = ProjectHandler.Instance.CurrentProjectPath;
            var transcriptionsDir = Path.Combine(currentProjectPath, "Transcriptions");
            
            var speakerCounter = 0;
            
            // Group segments by video file for consistent speaker IDs per video
            var segmentsByFile = segments.GroupBy(s => s.FileName).ToList();
            
            foreach (var fileGroup in segmentsByFile)
            {
                var fileName = fileGroup.Key;
                var fileSegments = fileGroup.OrderBy(s => s.StartTime).ToList();
                
                if (string.IsNullOrEmpty(fileName))
                {
                    // Assign default speaker for segments without filename
                    foreach (var segment in fileSegments)
                    {
                        segment.SpeakerId = "S0";
                        segment.SpeakerConf = 0.5f;
                    }
                    continue;
                }
                
                // Use text-based speaker change detection
                AssignSpeakersBasedOnTextAnalysis(fileSegments, ref speakerCounter);
            }

            Debug.WriteLine($"Speaker diarisation completed - identified {speakerCounter} speakers across {segmentsByFile.Count} files");
        }

        private void AssignSpeakersBasedOnTextAnalysis(List<SpeakerSegment> segments, ref int speakerCounter)
        {
            // Text-based speaker assignment using linguistic patterns
            var currentSpeakerId = $"S{speakerCounter}";
            speakerCounter++;
            
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var prevSegment = i > 0 ? segments[i - 1] : null;
                
                if (prevSegment != null && ShouldChangeSpeaker(prevSegment, segment))
                {
                    currentSpeakerId = $"S{speakerCounter}";
                    speakerCounter++;
                }
                
                segment.SpeakerId = currentSpeakerId;
                segment.SpeakerConf = CalculateSpeakerConfidence(segment, prevSegment);
            }
        }

        private bool ShouldChangeSpeaker(SpeakerSegment prevSegment, SpeakerSegment currentSegment)
        {
            // 1. Time gap threshold
            var timeGap = currentSegment.StartTime - prevSegment.EndTime;
            if (timeGap.TotalSeconds > 4.0) return true;
            
            // 2. Question/response pattern
            var prevEndsWithQuestion = prevSegment.Text.TrimEnd().EndsWith('?');
            var currentStartsWithAnswer = currentSegment.Text.TrimStart().StartsWith("Yes") || 
                                         currentSegment.Text.TrimStart().StartsWith("No") ||
                                         currentSegment.Text.TrimStart().StartsWith("Well");
            
            if (prevEndsWithQuestion && currentStartsWithAnswer) return true;
            
            return false;
        }

        private float CalculateSpeakerConfidence(SpeakerSegment segment, SpeakerSegment? prevSegment)
        {
            float confidence = 0.7f; // Base confidence
            
            // Higher confidence for longer segments
            var duration = segment.EndTime - segment.StartTime;
            if (duration.TotalSeconds > 3) confidence += 0.1f;
            
            // Lower confidence for very short segments
            if (duration.TotalSeconds < 1) confidence -= 0.2f;
            
            return Math.Max(0.3f, Math.Min(0.95f, confidence));
        }

        private async Task PerformFaceDetectionWithFaceAiSharpAsync(List<SpeakerSegment> segments, SpeakerMetadata metadata, ProjectModel project)
        {
            Debug.WriteLine("Performing face detection with FaceAiSharp...");
            
            var currentProjectPath = ProjectHandler.Instance.CurrentProjectPath;
            var mediaDir = Path.Combine(currentProjectPath, "media");

            var faceIdCounter = 1;
            var lockObject = new object();

            // Step 1: Extract all keyframes sequentially to avoid ffmpeg conflicts
            Debug.WriteLine("Step 1: Extracting keyframes sequentially...");
            var segmentKeyframes = new List<(SpeakerSegment segment, Image<Rgb24>? keyframe)>();
            
            foreach (var segment in segments)
            {
                try
                {
                    Debug.WriteLine($"Extracting keyframe for segment: {segment.SegmentId}");
                    
                    if (string.IsNullOrEmpty(segment.FileName))
                    {
                        Debug.WriteLine("❌ No filename for segment");
                        segmentKeyframes.Add((segment, null));
                        continue;
                    }

                    var videoPath = Path.Combine(mediaDir, segment.FileName);
                    if (!File.Exists(videoPath))
                    {
                        Debug.WriteLine($"❌ Video file not found: {segment.FileName}");
                        segmentKeyframes.Add((segment, null));
                        continue;
                    }

                    var keyframeTime = segment.StartTime + TimeSpan.FromMilliseconds((segment.EndTime - segment.StartTime).TotalMilliseconds / 2);
                    var keyframeImage = ExtractKeyframeSync(videoPath, keyframeTime);
                    
                    if (keyframeImage != null)
                    {
                        Debug.WriteLine($"✅ Keyframe extracted for {segment.SegmentId}, size: {keyframeImage.Width}x{keyframeImage.Height}");
                    }
                    else
                    {
                        Debug.WriteLine($"❌ Failed to extract keyframe for {segment.SegmentId}");
                    }
                    
                    segmentKeyframes.Add((segment, keyframeImage));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Error extracting keyframe for {segment.SegmentId}: {ex.Message}");
                    segmentKeyframes.Add((segment, null));
                }
            }

            // Step 2: Process faces in parallel on extracted keyframes
            Debug.WriteLine("Step 2: Processing faces in parallel...");
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount // Safe to use all cores for face detection
            };

            await Task.Run(() => 
            {
                Parallel.ForEach(segmentKeyframes.Where(sk => sk.keyframe != null), parallelOptions, segmentKeyframe =>
                {
                    var (segment, keyframeImage) = segmentKeyframe;
                    
                    try
                    {
                        Debug.WriteLine($"Processing faces for segment: {segment.SegmentId}");
                      
                        var detectedFaces = FaceDetector.DetectFaces(keyframeImage!);
                        var segmentFaces = new List<DetectedFace>();
                        
                        foreach (var face in detectedFaces.Take(metadata.Settings.MaxFacesPerSegment))
                        {
                            if (face.Confidence < metadata.Settings.FaceDetectionThreshold)
                                continue;

                            try
                            {
                                // Generate face embedding
                                using var faceImage = keyframeImage!.Clone();
                                
                                // Align face using landmarks if available
                                if (face.Landmarks != null && face.Landmarks.Any())
                                {
                                    FaceEmbeddingsGenerator.AlignFaceUsingLandmarks(faceImage, face.Landmarks);
                                }

                                var embedding = FaceEmbeddingsGenerator.GenerateEmbedding(faceImage);
                                
                                // Thread-safe face ID generation
                                int currentFaceId;
                                lock (lockObject)
                                {
                                    currentFaceId = faceIdCounter++;
                                }
                                
                                var detectedFace = new DetectedFace
                                {
                                    FaceId = $"F{currentFaceId:D4}",
                                    Embedding = embedding,
                                    Confidence = face.Confidence ?? 0.0f,
                                    BoundingBox = new FaceRectangle 
                                    { 
                                        X = (int)face.Box.X, 
                                        Y = (int)face.Box.Y, 
                                        Width = (int)face.Box.Width, 
                                        Height = (int)face.Box.Height 
                                    },
                                    Landmarks = face.Landmarks?.Select(p => new FacePoint { X = (int)p.X, Y = (int)p.Y }).ToArray() ?? Array.Empty<FacePoint>()
                                };

                                segmentFaces.Add(detectedFace);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error processing face in segment {segment.SegmentId}: {ex.Message}");
                            }
                        }

                        // Thread-safe update of shared collections
                        lock (lockObject)
                        {
                            segment.DetectedFaceIds.AddRange(segmentFaces.Select(f => f.FaceId));
                            foreach (var face in segmentFaces)
                            {
                                metadata.FaceDatabase[face.FaceId] = face;
                            }
                        }

                        Debug.WriteLine($"✅ Completed processing {segment.SegmentId} - found {segmentFaces.Count} valid faces");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ Error processing faces for segment {segment.SegmentId}: {ex.Message}");
                    }
                });
            });

            // Step 3: Cleanup keyframe images
            Debug.WriteLine("Step 3: Cleaning up keyframe images...");
            foreach (var (_, keyframe) in segmentKeyframes.Where(sk => sk.keyframe != null))
            {
                keyframe?.Dispose();
            }

            Debug.WriteLine($"Face detection completed: {metadata.FaceDatabase.Count} faces detected across {segments.Count} segments");
        }

        private Image<Rgb24>? ExtractKeyframeSync(string videoPath, TimeSpan timestamp)
        {
            var currentProjectPath = ProjectHandler.Instance.CurrentProjectPath;
            var tempDir = Path.Combine(currentProjectPath, "temp");
            
            // Ensure temp directory exists
            Directory.CreateDirectory(tempDir);
            
            var tempImagePath = Path.Combine(tempDir, $"keyframe_{Guid.NewGuid():N}.jpg");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = App.Settings.Instance.FfmpegPath,
                    Arguments = $"-ss {timestamp:hh\\:mm\\:ss\\.fff} -i \"{videoPath}\" -vframes 1 -update 1 -q:v 2 -y \"{tempImagePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                // Wait for process to complete naturally (with a reasonable timeout)
                bool completed = process.WaitForExit(10000); // 10 second timeout
                
                if (!completed)
                {
                    Debug.WriteLine("⚠️ FFmpeg timed out, killing process");
                    try
                    {
                        process.Kill();
                        process.WaitForExit(1000); // Wait for kill to complete
                    }
                    catch { }
                }
                else
                {
                    Debug.WriteLine($"✅ FFmpeg completed with exit code: {process.ExitCode}");
                }

                if (!File.Exists(tempImagePath))
                {
                    Debug.WriteLine($"❌ Keyframe file not created: {tempImagePath}");
                    return null;
                }

                // Verify file has content
                var fileInfo = new FileInfo(tempImagePath);
                if (fileInfo.Length == 0)
                {
                    Debug.WriteLine($"❌ Keyframe file is empty");
                    return null;
                }

                Debug.WriteLine($"✅ Keyframe ready: {fileInfo.Length} bytes");

                var image = SixLabors.ImageSharp.Image.Load<Rgb24>(tempImagePath);
                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting keyframe: {ex.Message}");
                return null;
            }
            finally
            {
                // Cleanup temp file
                try
                {
                    if (File.Exists(tempImagePath))
                        File.Delete(tempImagePath);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        private async Task PerformMockFaceDetectionAsync(List<SpeakerSegment> segments, SpeakerMetadata metadata)
        {
            Debug.WriteLine("Performing mock face detection (FaceAiSharp not available)...");
            
            await Task.Delay(50); // Simulate processing time
            
            var faceIdCounter = 0;
            
            // Create mock face detections for demo purposes
            foreach (var segment in segments)
            {
                // 70% chance of detecting a face
                if (Random.Shared.NextDouble() > 0.3)
                {
                    var detectedFace = new DetectedFace
                    {
                        FaceId = $"MOCK_F{faceIdCounter:D4}",
                        Embedding = GenerateMockEmbedding(512), // Mock 512-dim embedding
                        Confidence = 0.7f + (float)(Random.Shared.NextDouble() * 0.3), // 0.7-1.0
                        BoundingBox = new FaceRectangle 
                        { 
                            X = Random.Shared.Next(50, 200), 
                            Y = Random.Shared.Next(50, 200), 
                            Width = Random.Shared.Next(100, 300), 
                            Height = Random.Shared.Next(100, 300) 
                        }
                    };

                    segment.DetectedFaceIds.Add(detectedFace.FaceId);
                    metadata.FaceDatabase[detectedFace.FaceId] = detectedFace;
                    faceIdCounter++;
                }
            }

            Debug.WriteLine($"Mock face detection completed: {metadata.FaceDatabase.Count} mock faces created");
        }

        private async Task PerformFaceClusteringAsync(List<SpeakerSegment> segments, SpeakerMetadata metadata)
        {
            Debug.WriteLine("Performing face clustering...");
            
            await Task.Delay(100); // Simulate processing time

            var allFaces = metadata.FaceDatabase.Values.ToList();
            var faceClusters = new List<List<DetectedFace>>();
            var processed = new HashSet<string>();

            // Simple clustering based on face similarity
            foreach (var face in allFaces)
            {
                if (processed.Contains(face.FaceId))
                    continue;

                var cluster = new List<DetectedFace> { face };
                processed.Add(face.FaceId);

                // Find similar faces
                foreach (var otherFace in allFaces)
                {
                    if (processed.Contains(otherFace.FaceId))
                        continue;

                    var similarity = CalculateFaceSimilarity(face.Embedding, otherFace.Embedding);
                    if (similarity >= metadata.Settings.FaceSimilarityThreshold)
                    {
                        cluster.Add(otherFace);
                        processed.Add(otherFace.FaceId);
                    }
                }

                faceClusters.Add(cluster);
            }

            // Create speaker clusters with face information
            for (int i = 0; i < faceClusters.Count; i++)
            {
                var cluster = faceClusters[i];
                var clusterId = $"FC{i:D2}";

                // Calculate average embedding for the cluster
                var avgEmbedding = CalculateAverageEmbedding(cluster.Select(f => f.Embedding).ToList());

                metadata.Clusters.Add(new SpeakerCluster
                {
                    ClusterId = clusterId,
                    FaceEmbedding = avgEmbedding.ToList(),
                    SegmentCount = cluster.Count,
                    Confidence = cluster.Average(f => f.Confidence),
                    LinkedFaceIds = cluster.Select(f => f.FaceId).ToList()
                });

                // Update segments with cluster information
                foreach (var face in cluster)
                {
                    foreach (var segment in segments)
                    {
                        var detectedFace = segment.DetectedFaceIds.FirstOrDefault(df => df == face.FaceId);
                        if (!string.IsNullOrEmpty(detectedFace) && !segment.FaceIds.Contains(clusterId))
                        {
                            segment.FaceIds.Add(clusterId);
                        }
                    }
                }
            }

            Debug.WriteLine($"Face clustering completed: {faceClusters.Count} face clusters created");
        }

        private float CalculateFaceSimilarity(float[] embedding1, float[] embedding2)
        {
            if (embedding1.Length != embedding2.Length)
                return 0.0f;

            // Calculate cosine similarity (dot product for normalized embeddings)
            float dotProduct = 0.0f;
            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
            }

            return dotProduct;
        }

        private float[] CalculateAverageEmbedding(List<float[]> embeddings)
        {
            if (!embeddings.Any())
                return Array.Empty<float>();

            var dimensions = embeddings.First().Length;
            var avgEmbedding = new float[dimensions];

            for (int i = 0; i < dimensions; i++)
            {
                avgEmbedding[i] = embeddings.Average(e => e[i]);
            }

            return avgEmbedding;
        }

        private float[] GenerateMockEmbedding(int dimensions)
        {
            var embedding = new float[dimensions];
            for (int i = 0; i < dimensions; i++)
            {
                embedding[i] = (float)(Random.Shared.NextDouble() * 2.0 - 1.0); // Random values between -1 and 1
            }
            return embedding;
        }

        private async Task LinkSpeakersToFacesAsync(List<SpeakerSegment> segments, SpeakerMetadata metadata)
        {
            Debug.WriteLine("Linking speakers to faces...");
            
            await Task.Delay(50); // Simulate processing time
            
            // Simple linking based on temporal co-occurrence
            // In a real implementation, this would use audio-visual correlation
            foreach (var segment in segments)
            {
                if (segment.FaceIds.Any() && segment.SpeakerId != "UNK")
                {
                    // Link the most confident face cluster to the speaker
                    var bestCluster = metadata.Clusters
                        .Where(c => segment.FaceIds.Contains(c.ClusterId))
                        .OrderByDescending(c => c.Confidence)
                        .FirstOrDefault();

                    if (bestCluster != null)
                    {
                        // Update speaker cluster with face information
                        var speakerCluster = metadata.Clusters.FirstOrDefault(c => c.ClusterId == segment.SpeakerId);
                        if (speakerCluster != null)
                        {
                            speakerCluster.LinkedFaceIds.AddRange(bestCluster.LinkedFaceIds);
                            speakerCluster.FaceEmbedding = bestCluster.FaceEmbedding;
                        }
                    }
                }
            }
            
            Debug.WriteLine("Speaker-face linking completed");
        }

        private async Task PerformShotClassificationAsync(List<SpeakerSegment> segments, SpeakerMetadata metadata, ProjectModel project)
        {
            // Placeholder for shot classification implementation
            // This would use CLIP embeddings + k-NN/SVM for shot type classification
            Debug.WriteLine("Performing shot classification...");
            
            await Task.Delay(150); // Simulate processing time
            
            // Mock shot type assignment based on face detection results
            foreach (var segment in segments)
            {
                if (segment.DetectedFaceIds.Any())
                {
                    var faceCount = segment.DetectedFaceIds.Count;
                    var avgFaceSize = segment.DetectedFaceIds
                        .Where(faceId => metadata.FaceDatabase.ContainsKey(faceId))
                        .Average(faceId => metadata.FaceDatabase[faceId].BoundingBox.Width * metadata.FaceDatabase[faceId].BoundingBox.Height);
                    
                    // Simple heuristic based on face size and count
                    if (faceCount == 1 && avgFaceSize > 10000) // Large single face
                        segment.ShotLabel = ShotType.CU;
                    else if (faceCount == 1 && avgFaceSize > 5000) // Medium single face
                        segment.ShotLabel = ShotType.MS;
                    else if (faceCount > 1) // Multiple faces
                        segment.ShotLabel = ShotType.WS;
                    else
                        segment.ShotLabel = ShotType.MS;
                }
                else
                {
                    // No faces detected - could be insert or wide shot
                    segment.ShotLabel = Random.Shared.NextDouble() > 0.5 ? ShotType.INSERT : ShotType.WS;
                }
                
                segment.ShotConf = 0.7f + (float)(Random.Shared.NextDouble() * 0.3); // 0.7-1.0 confidence
            }

            Debug.WriteLine("Shot classification completed");
        }

        private void ApplyConfidenceFiltering(List<SpeakerSegment> segments, SpeakerProcessingSettings settings)
        {
            foreach (var segment in segments)
            {
                if (segment.SpeakerConf < settings.SpeakerConfidenceThreshold)
                {
                    segment.SpeakerId = "UNK";
                }

                if (segment.ShotConf < settings.ShotConfidenceThreshold)
                {
                    segment.ShotLabel = ShotType.UNK;
                }
            }

            var filteredSpeakers = segments.Count(s => s.SpeakerId == "UNK");
            var filteredShots = segments.Count(s => s.ShotLabel == ShotType.UNK);
            
            Debug.WriteLine($"Confidence filtering: {filteredSpeakers} speakers, {filteredShots} shots marked as UNK");
        }

        private async Task<bool> IsCacheValidAsync(string cacheFilePath, string renderDir, string projectName)
        {
            if (!File.Exists(cacheFilePath))
                return false;

            try
            {
                var cacheInfo = new FileInfo(cacheFilePath);
                
                // Check if any SRT files are newer than the cache
                var srtFiles = Directory.GetFiles(renderDir, "*.srt");
                foreach (var srtFile in srtFiles)
                {
                    var srtInfo = new FileInfo(srtFile);
                    if (srtInfo.LastWriteTime > cacheInfo.LastWriteTime)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GetCacheFilePath(string renderDir, string projectName)
        {
            return Path.Combine(renderDir, $"{projectName}.speaker.meta.json");
        }

        private async Task SaveCacheAsync(string cacheFilePath, SpeakerMetadata metadata)
        {
            try
            {
                // Step 1: Save embeddings to external binary file (if enabled)
                if (metadata.CompressionInfo.UseExternalEmbeddings)
                {
                    await SaveEmbeddingsExternallyAsync(cacheFilePath, metadata);
                }

                // Step 2: Calculate compression stats
                var originalSize = EstimateOriginalJsonSize(metadata);
                metadata.CompressionInfo.OriginalSizeBytes = originalSize;
                metadata.CompressionInfo.TotalFaces = metadata.FaceDatabase.Count;
                metadata.CompressionInfo.TotalClusters = metadata.Clusters.Count;

                // Step 3: Save compressed JSON (without embeddings)
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var jsonContent = JsonSerializer.Serialize(metadata, options);
                await File.WriteAllTextAsync(cacheFilePath, jsonContent);
                
                var compressedSize = new FileInfo(cacheFilePath).Length;
                metadata.CompressionInfo.CompressedSizeBytes = compressedSize;

                Debug.WriteLine($"Speaker analysis cache saved to: {Path.GetFileName(cacheFilePath)}");
                Debug.WriteLine($"Compression ratio: {metadata.CompressionInfo.CompressionRatio:P1} " +
                               $"({originalSize:N0} → {compressedSize:N0} bytes)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving speaker analysis cache: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Save embeddings to external binary file for efficient storage
        /// </summary>
        private async Task SaveEmbeddingsExternallyAsync(string cacheFilePath, SpeakerMetadata metadata)
        {
            var embeddingFilePath = GetEmbeddingFilePath(cacheFilePath);
            
            using var fileStream = new FileStream(embeddingFilePath, FileMode.Create);
            using var writer = new BinaryWriter(fileStream);

            // Header: version, counts
            writer.Write((uint)1); // Version
            writer.Write(metadata.FaceDatabase.Count);
            writer.Write(metadata.Clusters.Count);

            // Write face embeddings
            foreach (var kvp in metadata.FaceDatabase)
            {
                var face = kvp.Value;
                writer.Write(face.FaceId);
                writer.Write(face.Embedding.Length);
                foreach (var value in face.Embedding)
                {
                    writer.Write(value);
                }
                
                // Update hash for verification
                face.EmbeddingHash = ComputeEmbeddingHash(face.Embedding);
            }

            // Write cluster embeddings
            foreach (var cluster in metadata.Clusters)
            {
                writer.Write(cluster.ClusterId);
                
                // Voice embedding
                writer.Write(cluster.VoiceEmbedding.Count);
                foreach (var value in cluster.VoiceEmbedding)
                {
                    writer.Write(value);
                }
                
                // Face embedding
                writer.Write(cluster.FaceEmbedding.Count);
                foreach (var value in cluster.FaceEmbedding)
                {
                    writer.Write(value);
                }

                // Update hashes
                cluster.VoiceEmbeddingHash = ComputeEmbeddingHash(cluster.VoiceEmbedding.ToArray());
                cluster.FaceEmbeddingHash = ComputeEmbeddingHash(cluster.FaceEmbedding.ToArray());
            }

            Debug.WriteLine($"Embeddings saved to external file: {Path.GetFileName(embeddingFilePath)} " +
                           $"({new FileInfo(embeddingFilePath).Length:N0} bytes)");
        }

        /// <summary>
        /// Load embeddings from external binary file
        /// </summary>
        private async Task LoadEmbeddingsExternallyAsync(string cacheFilePath, SpeakerMetadata metadata)
        {
            var embeddingFilePath = GetEmbeddingFilePath(cacheFilePath);
            
            if (!File.Exists(embeddingFilePath))
            {
                Debug.WriteLine("External embedding file not found, embeddings will be empty");
                return;
            }

            using var fileStream = new FileStream(embeddingFilePath, FileMode.Open);
            using var reader = new BinaryReader(fileStream);

            // Read header
            var version = reader.ReadUInt32();
            var faceCount = reader.ReadInt32();
            var clusterCount = reader.ReadInt32();

            // Read face embeddings
            for (int i = 0; i < faceCount; i++)
            {
                var faceId = reader.ReadString();
                var embeddingLength = reader.ReadInt32();
                var embedding = new float[embeddingLength];
                
                for (int j = 0; j < embeddingLength; j++)
                {
                    embedding[j] = reader.ReadSingle();
                }

                if (metadata.FaceDatabase.ContainsKey(faceId))
                {
                    metadata.FaceDatabase[faceId].Embedding = embedding;
                    
                    // Cache in memory for faster access
                    lock (_embeddingCacheLock)
                    {
                        _embeddingCache[faceId] = embedding;
                    }
                }
            }

            // Read cluster embeddings
            for (int i = 0; i < clusterCount; i++)
            {
                var clusterId = reader.ReadString();
                
                // Voice embedding
                var voiceLength = reader.ReadInt32();
                var voiceEmbedding = new List<float>();
                for (int j = 0; j < voiceLength; j++)
                {
                    voiceEmbedding.Add(reader.ReadSingle());
                }

                // Face embedding
                var faceLength = reader.ReadInt32();
                var faceEmbedding = new List<float>();
                for (int j = 0; j < faceLength; j++)
                {
                    faceEmbedding.Add(reader.ReadSingle());
                }

                var cluster = metadata.Clusters.FirstOrDefault(c => c.ClusterId == clusterId);
                if (cluster != null)
                {
                    cluster.VoiceEmbedding = voiceEmbedding;
                    cluster.FaceEmbedding = faceEmbedding;
                }
            }

            Debug.WriteLine($"Loaded embeddings from external file: {faceCount} faces, {clusterCount} clusters");
        }

        /// <summary>
        /// Get face embedding on-demand with caching
        /// </summary>
        public float[] GetFaceEmbedding(string faceId)
        {
            lock (_embeddingCacheLock)
            {
                if (_embeddingCache.ContainsKey(faceId))
                {
                    return _embeddingCache[faceId];
                }
            }

            // If not in cache, return empty array (embeddings should be loaded via LoadEmbeddingsExternallyAsync)
            return Array.Empty<float>();
        }

        /// <summary>
        /// Convert an existing uncompressed speaker.meta.json file to compressed format
        /// </summary>
        public async Task<bool> CompressExistingCacheAsync(string projectName, string renderDir)
        {
            try
            {
                var cacheFilePath = GetCacheFilePath(renderDir, projectName);
                if (!File.Exists(cacheFilePath))
                {
                    Debug.WriteLine("No existing cache file found to compress");
                    return false;
                }

                // Load existing file
                var jsonContent = await File.ReadAllTextAsync(cacheFilePath);
                var metadata = JsonSerializer.Deserialize<SpeakerMetadata>(jsonContent);
                
                if (metadata == null)
                {
                    Debug.WriteLine("Failed to deserialize existing cache file");
                    return false;
                }

                // Check if already compressed
                if (metadata.CompressionInfo.UseExternalEmbeddings)
                {
                    Debug.WriteLine("Cache file is already compressed");
                    return true;
                }

                // Enable compression
                metadata.CompressionInfo.UseExternalEmbeddings = true;

                // Create backup of original
                var backupPath = cacheFilePath + ".backup";
                File.Copy(cacheFilePath, backupPath, true);

                // Save compressed version
                await SaveCacheAsync(cacheFilePath, metadata);

                Debug.WriteLine($"Successfully compressed cache file. Backup saved to: {Path.GetFileName(backupPath)}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error compressing existing cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get file size information for the current cache
        /// </summary>
        public async Task<CacheFileInfo?> GetCacheFileInfoAsync(string projectName, string renderDir)
        {
            try
            {
                var cacheFilePath = GetCacheFilePath(renderDir, projectName);
                var embeddingFilePath = GetEmbeddingFilePath(cacheFilePath);

                if (!File.Exists(cacheFilePath))
                {
                    return null;
                }

                var jsonInfo = new FileInfo(cacheFilePath);
                var embeddingInfo = File.Exists(embeddingFilePath) ? new FileInfo(embeddingFilePath) : null;

                var metadata = await LoadSpeakerAnalysisAsync(projectName, renderDir);
                
                return new CacheFileInfo
                {
                    JsonFilePath = cacheFilePath,
                    JsonFileSize = jsonInfo.Length,
                    EmbeddingFilePath = embeddingFilePath,
                    EmbeddingFileSize = embeddingInfo?.Length ?? 0,
                    TotalSize = jsonInfo.Length + (embeddingInfo?.Length ?? 0),
                    IsCompressed = metadata?.CompressionInfo.UseExternalEmbeddings ?? false,
                    CompressionRatio = metadata?.CompressionInfo.CompressionRatio ?? 1.0f,
                    FaceCount = metadata?.CompressionInfo.TotalFaces ?? 0,
                    ClusterCount = metadata?.CompressionInfo.TotalClusters ?? 0
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting cache file info: {ex.Message}");
                return null;
            }
        }

        public class CacheFileInfo
        {
            public string JsonFilePath { get; set; } = string.Empty;
            public long JsonFileSize { get; set; }
            public string EmbeddingFilePath { get; set; } = string.Empty;
            public long EmbeddingFileSize { get; set; }
            public long TotalSize { get; set; }
            public bool IsCompressed { get; set; }
            public float CompressionRatio { get; set; }
            public int FaceCount { get; set; }
            public int ClusterCount { get; set; }
            
            public string GetSizeDescription()
            {
                if (IsCompressed)
                {
                    return $"JSON: {JsonFileSize:N0} bytes, Embeddings: {EmbeddingFileSize:N0} bytes " +
                           $"(Total: {TotalSize:N0} bytes, {CompressionRatio:P1} of original)";
                }
                else
                {
                    return $"Uncompressed: {TotalSize:N0} bytes";
                }
            }
        }

        private string GetEmbeddingFilePath(string cacheFilePath)
        {
            return Path.ChangeExtension(cacheFilePath, ".embeddings.bin");
        }

        private string ComputeEmbeddingHash(float[] embedding)
        {
            if (embedding.Length == 0) return string.Empty;
            
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = new byte[embedding.Length * sizeof(float)];
            Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash)[..16]; // First 16 chars for compactness
        }

        private long EstimateOriginalJsonSize(SpeakerMetadata metadata)
        {
            // Rough estimate of JSON size if embeddings were included
            long embeddingSize = 0;
            
            foreach (var face in metadata.FaceDatabase.Values)
            {
                embeddingSize += face.Embedding.Length * 8; // ~8 bytes per float in JSON
            }
            
            foreach (var cluster in metadata.Clusters)
            {
                embeddingSize += cluster.VoiceEmbedding.Count * 8;
                embeddingSize += cluster.FaceEmbedding.Count * 8;
            }

            return embeddingSize;
        }

    }
} 