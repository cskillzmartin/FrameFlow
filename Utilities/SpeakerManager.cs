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
            public List<DetectedFace> DetectedFaces { get; set; } = new List<DetectedFace>();
        }

        public class DetectedFace
        {
            public string FaceId { get; set; } = string.Empty;
            public float[] Embedding { get; set; } = Array.Empty<float>();
            public float Confidence { get; set; } = 0.0f;
            public FaceRectangle BoundingBox { get; set; } = new FaceRectangle();
            public FacePoint[] Landmarks { get; set; } = Array.Empty<FacePoint>();
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
            public List<float> VoiceEmbedding { get; set; } = new List<float>();
            public List<float> FaceEmbedding { get; set; } = new List<float>();
            public int SegmentCount { get; set; } = 0;
            public float Confidence { get; set; } = 0.0f;
            public List<string> LinkedFaceIds { get; set; } = new List<string>();
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
                return JsonSerializer.Deserialize<SpeakerMetadata>(jsonContent);
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

            // Load from the first available SRT file
            foreach (var srtFile in possibleSrtFiles)
            {
                if (File.Exists(srtFile))
                {
                    segments = await ParseSrtFileAsync(srtFile);
                    if (segments.Any())
                    {
                        Debug.WriteLine($"Loaded {segments.Count} segments from {Path.GetFileName(srtFile)}");
                        break;
                    }
                }
            }

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

            // Use parallel processing for face detection across all segments
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount // Use all available cores
            };

            var faceIdCounter = 1;
            var lockObject = new object();

            await Task.Run(() => 
            {
                Parallel.ForEach(segments, parallelOptions, segment =>
                {
                    try
                    {
                        Debug.WriteLine($"Processing segment: {segment.SegmentId} for file: {segment.FileName}");
                        
                        if (string.IsNullOrEmpty(segment.FileName))
                        {
                            Debug.WriteLine("❌ No filename for segment");
                            return;
                        }

                        var videoPath = Path.Combine(mediaDir, segment.FileName);

                        if (!File.Exists(videoPath))
                        {
                            Debug.WriteLine($"❌ Video file not found: {segment.FileName}");
                            return;
                        }

                        // Extract keyframe from middle of segment for face analysis
                        var keyframeTime = segment.StartTime + TimeSpan.FromMilliseconds((segment.EndTime - segment.StartTime).TotalMilliseconds / 2);
                        
                        // Synchronous keyframe extraction (ffmpeg handles this efficiently)
                        var keyframeImage = ExtractKeyframeSync(videoPath, keyframeTime);
                        
                        if (keyframeImage == null)
                        {
                            Debug.WriteLine($"❌ Failed to extract keyframe for {segment.SegmentId}");
                            return;
                        }

                        Debug.WriteLine($"✅ Keyframe extracted for {segment.SegmentId}, size: {keyframeImage.Width}x{keyframeImage.Height}");
                      
                        var detectedFaces = FaceDetector.DetectFaces(keyframeImage);
                        var segmentFaces = new List<DetectedFace>();
                        
                        foreach (var face in detectedFaces.Take(metadata.Settings.MaxFacesPerSegment))
                        {
                            if (face.Confidence < metadata.Settings.FaceDetectionThreshold)
                                continue;

                            try
                            {
                                // Generate face embedding
                                using var faceImage = keyframeImage.Clone();
                                
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
                            segment.DetectedFaces.AddRange(segmentFaces);
                            foreach (var face in segmentFaces)
                            {
                                metadata.FaceDatabase[face.FaceId] = face;
                            }
                        }

                        keyframeImage.Dispose();
                        Debug.WriteLine($"✅ Completed processing {segment.SegmentId} - found {segmentFaces.Count} valid faces");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ Error processing segment {segment.SegmentId}: {ex.Message}");
                    }
                });
            });

            Debug.WriteLine($"Face detection completed: {metadata.FaceDatabase.Count} faces detected across {segments.Count} segments");
        }

        private Image<Rgb24>? ExtractKeyframeSync(string videoPath, TimeSpan timestamp)
        {
            var tempDir = Path.GetTempPath();
            var tempImagePath = Path.Combine(tempDir, $"keyframe_{Guid.NewGuid():N}.jpg");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = App.Settings.Instance.FfmpegPath,
                    Arguments = $"-ss {timestamp:hh\\:mm\\:ss\\.fff} -i \"{videoPath}\" -vframes 1 -q:v 2 -y \"{tempImagePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                process.WaitForExit(10000); // 10 second timeout

                if (process.ExitCode != 0 || !File.Exists(tempImagePath))
                {
                    return null;
                }

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

                    segment.DetectedFaces.Add(detectedFace);
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
                        var detectedFace = segment.DetectedFaces.FirstOrDefault(df => df.FaceId == face.FaceId);
                        if (detectedFace != null && !segment.FaceIds.Contains(clusterId))
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
                if (segment.DetectedFaces.Any())
                {
                    var faceCount = segment.DetectedFaces.Count;
                    var avgFaceSize = segment.DetectedFaces.Average(f => f.BoundingBox.Width * f.BoundingBox.Height);
                    
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
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var jsonContent = JsonSerializer.Serialize(metadata, options);
                await File.WriteAllTextAsync(cacheFilePath, jsonContent);
                
                Debug.WriteLine($"Speaker analysis cache saved to: {Path.GetFileName(cacheFilePath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving speaker analysis cache: {ex.Message}");
                throw;
            }
        }

    }
} 