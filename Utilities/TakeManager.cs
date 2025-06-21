using FrameFlow.Models;
using FrameFlow.App;
using System.Text;
using System.Text.RegularExpressions;

namespace FrameFlow.Utilities
{
    public class TakeManager
    {
        private static TakeManager? _instance;
        private readonly object _lock = new object();
        private readonly QualityScorer _qualityScorer;

        // Common filler words for flub detection
        private readonly HashSet<string> _fillerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "um", "uh", "ah", "eh", "oh", "like", "you know", "actually", "basically", "literally",
            "sort of", "kind of", "i mean", "well", "so", "right", "okay", "alright", "yeah"
        };

        private TakeManager() 
        { 
            _qualityScorer = new QualityScorer();
        }

        public static TakeManager Instance
        {
            get
            {
                _instance ??= new TakeManager();
                return _instance;
            }
        }

        // Data structure for SRT segments
        public class SrtSegment
        {
            public int Number { get; set; }
            public string Text { get; set; } = string.Empty;
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
            public string FileName { get; set; } = string.Empty;
            public float QualityScore { get; set; }
            public QualityScorer.QualityVector? QualityVector { get; set; }
        }

        // Data structure for segment clusters
        public class SegmentCluster
        {
            public List<SrtSegment> Segments { get; set; } = new List<SrtSegment>();
            public SrtSegment? BestSegment { get; set; }
        }

        /// <summary>
        /// Main entry point for take layer processing
        /// Processes each SRT file individually and creates output files with the same names containing qualified segments
        /// </summary>
        /// <param name="settings">Story settings containing take layer configuration</param>
        /// <param name="renderDir">Directory where output files should be written</param>
        /// <returns>True if processing succeeded, false otherwise</returns>
        public async Task<bool> ProcessTakeLayerAsync(StorySettings settings, string renderDir)
        {
            try
            {
                // Check if take layer is enabled
                if (!settings.TakeLayerSettings.EnableTakeLayer)
                {
                    System.Diagnostics.Debug.WriteLine("Take layer processing is disabled. Skipping...");
                    return false;
                }

                var projectPath = ProjectHandler.Instance.CurrentProjectPath;
                if (string.IsNullOrEmpty(projectPath))
                {
                    throw new InvalidOperationException("No project is currently open");
                }

                var transcriptionsDir = Path.Combine(projectPath, "Transcriptions");
                if (!Directory.Exists(transcriptionsDir))
                {
                    System.Diagnostics.Debug.WriteLine("No transcriptions directory found. Skipping take layer processing.");
                    return false;
                }

                // Get all SRT files
                var srtFiles = Directory.GetFiles(transcriptionsDir, "*.srt");
                if (!srtFiles.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No SRT files found in transcriptions directory.");
                    return false;
                }

                // ================================================
                // 1) Parse segments for ALL files up-front
                // ================================================
                var parseTasks = srtFiles.Select(async srtFile =>
                {
                    var fileName = Path.GetFileName(srtFile);
                    var segments = await ParseSrtFileAsync(srtFile, fileName, settings);
                    return (fileName, segments);
                });

                var parsedResults = await Task.WhenAll(parseTasks);

                // Flatten to a master list for cross-file clustering
                var allSegments = parsedResults.SelectMany(r => r.Item2).ToList();
                if (!allSegments.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No segments found in any SRT file. Nothing to process.");
                    return true;
                }

                // ================================================
                // 2) Detect and select best takes ACROSS files
                // ================================================
                var canonicalSegments = await DetectAndSelectBestTakesAsync(allSegments, settings.TakeLayerSettings);

                // Group canonical segments by their originating file
                var segmentsByFile = canonicalSegments.GroupBy(seg => seg.FileName);

                // Ensure output directory exists
                Directory.CreateDirectory(renderDir);

                // ================================================
                // 3) Write per-file outputs containing only canonical segments
                // ================================================
                foreach (var group in segmentsByFile)
                {
                    // Convert video filename back to SRT filename for output
                    var videoFileName = group.Key;
                    var baseFileName = Path.GetFileNameWithoutExtension(videoFileName);
                    var srtFileName = $"{baseFileName}.srt";
                    var outputPath = Path.Combine(renderDir, srtFileName);
                    
                    await WriteCanonicalSegmentsAsync(group.OrderBy(s => s.Start).ToList(), outputPath);
                    System.Diagnostics.Debug.WriteLine($"Take layer processing completed for {videoFileName}. Output: {outputPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in take layer processing: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parse SRT file and return segments with filename attached
        /// </summary>
        private async Task<List<SrtSegment>> ParseSrtFileAsync(string filePath, string fileName, StorySettings settings)
        {
            var segments = new List<SrtSegment>();
            var lines = await File.ReadAllLinesAsync(filePath);
            
            // Convert SRT filename to actual video filename
            var srtFileName = Path.GetFileNameWithoutExtension(fileName);
            var videoFileName = FindMatchingVideoFile(srtFileName);
            
            for (int i = 0; i < lines.Length;)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                if (int.TryParse(lines[i], out int number))
                {
                    i++;
                    if (i >= lines.Length) break;

                    var timeParts = lines[i].Split(" --> ");
                    if (timeParts.Length == 2)
                    {
                        var start = TimeSpan.Parse(timeParts[0].Replace(',', '.'));
                        var end = TimeSpan.Parse(timeParts[1].Replace(',', '.'));
                        
                        i++;
                        var textBuilder = new StringBuilder();
                        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                        {
                            textBuilder.AppendLine(lines[i]);
                            i++;
                        }

                        var segment = new SrtSegment
                        {
                            Number = number,
                            Text = textBuilder.ToString().Trim(),
                            Start = start,
                            End = end,
                            FileName = videoFileName
                        };

                        // Calculate comprehensive quality vector for this segment
                        segment.QualityVector = await _qualityScorer.ScoreSegmentAsync(segment.Text, settings.Prompt, settings.TakeLayerSettings);
                        segment.QualityScore = segment.QualityVector.Value.CompositeScore;
                        
                        segments.Add(segment);
                    }
                }
                else
                {
                    i++;
                }
            }

            return segments;
        }

        /// <summary>
        /// Core clustering and selection logic
        /// </summary>
        private async Task<List<SrtSegment>> DetectAndSelectBestTakesAsync(List<SrtSegment> segments, TakeLayerSettings settings)
        {
            // Cluster similar segments
            var clusters = ClusterSimilarSegments(segments, settings);
            
            // Select best segment from each cluster
            var canonicalSegments = new List<SrtSegment>();
            foreach (var cluster in clusters)
            {
                var bestSegment = SelectBestSegmentFromCluster(cluster, settings);
                if (bestSegment != null)
                {
                    canonicalSegments.Add(bestSegment);
                }
            }

            // Sort by start time for consistent output
            canonicalSegments.Sort((a, b) => a.Start.CompareTo(b.Start));
            
            return canonicalSegments;
        }

        /// <summary>
        /// Groups similar segments together using text similarity
        /// </summary>
        private List<SegmentCluster> ClusterSimilarSegments(List<SrtSegment> segments, TakeLayerSettings settings)
        {
            var clusters = new List<SegmentCluster>();
            var processedSegments = new bool[segments.Count];

            for (int i = 0; i < segments.Count; i++)
            {
                if (processedSegments[i]) continue;

                var cluster = new SegmentCluster();
                cluster.Segments.Add(segments[i]);
                processedSegments[i] = true;

                // Find similar segments
                for (int j = i + 1; j < segments.Count; j++)
                {
                    if (processedSegments[j]) continue;

                    if (AreSegmentsSimilar(segments[i], segments[j], settings))
                    {
                        cluster.Segments.Add(segments[j]);
                        processedSegments[j] = true;
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        /// <summary>
        /// Check if two segments are similar based on text content
        /// </summary>
        private bool AreSegmentsSimilar(SrtSegment segment1, SrtSegment segment2, TakeLayerSettings settings)
        {
            var text1 = PreprocessText(segment1.Text);
            var text2 = PreprocessText(segment2.Text);

            // Handle very short segments with stricter thresholds
            if (text1.Length < 10 || text2.Length < 10)
            {
                return text1.Equals(text2, StringComparison.OrdinalIgnoreCase);
            }

            // Calculate Levenshtein distance and normalise by length
            int levenshteinDistance = CalculateLevenshteinDistance(text1, text2);
            float maxLen = Math.Max(text1.Length, text2.Length);
            float normalizedDistance = maxLen == 0 ? 0 : levenshteinDistance / maxLen; // 0-1 range

            // Calculate cosine similarity (simplified word-level)
            float cosineSimilarity = CalculateCosineSimilarity(text1, text2);

            // Segments are similar if either metric indicates similarity
            bool levenshteinMatch = normalizedDistance <= (settings.LevenshteinThreshold / 100f); // convert percentage threshold
            bool cosineMatch = cosineSimilarity >= settings.CosineSimilarityThreshold;

            //returns true if either metric indicates similarity
            return levenshteinMatch || cosineMatch;
        }

        /// <summary>
        /// Preprocess text for similarity comparison
        /// </summary>
        private string PreprocessText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Remove punctuation, normalize whitespace, convert to lowercase
            var processed = Regex.Replace(text, @"[^\w\s]", "");
            processed = Regex.Replace(processed, @"\s+", " ");
            return processed.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Calculate Levenshtein distance between two strings
        /// </summary>
        private int CalculateLevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            int[,] matrix = new int[s1.Length + 1, s2.Length + 1];

            // Initialize first row and column
            for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;

            // Fill matrix
            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[s1.Length, s2.Length];
        }

        /// <summary>
        /// Calculate cosine similarity between two texts (simplified word-level)
        /// </summary>
        private float CalculateCosineSimilarity(string text1, string text2)
        {
            var words1 = text1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words2 = text2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (!words1.Any() || !words2.Any()) return 0f;

            var allWords = words1.Union(words2).ToHashSet();
            var vector1 = allWords.Select(word => words1.Count(w => w == word)).ToArray();
            var vector2 = allWords.Select(word => words2.Count(w => w == word)).ToArray();

            // Calculate dot product and magnitudes
            float dotProduct = 0f;
            float magnitude1 = 0f;
            float magnitude2 = 0f;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            if (magnitude1 == 0f || magnitude2 == 0f) return 0f;

            return dotProduct / (float)(Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2));
        }

        /// <summary>
        /// Select the best segment from a cluster based on quality scoring
        /// </summary>
        private SrtSegment? SelectBestSegmentFromCluster(SegmentCluster cluster, TakeLayerSettings settings)
        {
            if (!cluster.Segments.Any()) return null;

            // Use existing quality scores (already calculated with comprehensive vector)
            // No need to recalculate since they were computed with the same settings

            // Return segment with highest quality score
            return cluster.Segments.OrderByDescending(s => s.QualityScore).First();
        }

        // Legacy quality scoring methods removed - now handled by QualityScorer class

        /// <summary>
        /// Find the matching video file for a given base filename
        /// </summary>
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
                    System.Diagnostics.Debug.WriteLine($"Found matching video file: {videoFileName}");
                    return videoFileName;
                }
            }

            // If no matching file found, log warning and return mp4 assumption
            System.Diagnostics.Debug.WriteLine($"No matching video file found for {baseFileName} in supported formats: {string.Join(", ", App.Settings.Instance.SupportedVideoFormats)}");
            return $"{baseFileName}.mp4"; // Fallback assumption
        }

        /// <summary>
        /// Write canonical segments to output SRT file with enhanced format including quality vector data
        /// </summary>
        private async Task WriteCanonicalSegmentsAsync(List<SrtSegment> segments, string outputPath)
        {
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                
                // Write segment number
                await writer.WriteLineAsync((i + 1).ToString());
                
                // Write timestamp
                await writer.WriteLineAsync($"{FormatTimeSpan(segment.Start)} --> {FormatTimeSpan(segment.End)}");
                
                // Write filename (for compatibility with downstream processors)
                await writer.WriteLineAsync(segment.FileName);
                
                // Write quality vector scores (if available)
                if (segment.QualityVector.HasValue)
                {
                    var qv = segment.QualityVector.Value;
                    await writer.WriteLineAsync($"Relevance: {qv.Relevance:F1}");
                    await writer.WriteLineAsync($"Sentiment: {qv.Sentiment:F1}");
                    await writer.WriteLineAsync($"Novelty: {qv.Novelty:F1}");
                    await writer.WriteLineAsync($"Energy: {qv.Energy:F1}");
                    await writer.WriteLineAsync($"Focus: {qv.Focus:F1}");
                    await writer.WriteLineAsync($"Clarity: {qv.Clarity:F1}");
                    await writer.WriteLineAsync($"Emotion: {qv.Emotion:F1}");
                    await writer.WriteLineAsync($"FlubScore: {qv.FlubScore:F1}");
                    await writer.WriteLineAsync($"CompositeScore: {qv.CompositeScore:F1}");
                }
                
                // Write text
                await writer.WriteLineAsync(segment.Text);
                await writer.WriteLineAsync(); // Empty line between segments
            }
        }

        /// <summary>
        /// Format TimeSpan for SRT format
        /// </summary>
        private string FormatTimeSpan(TimeSpan ts)
        {
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
        }
    }
}