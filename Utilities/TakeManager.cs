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

        // Common filler words for flub detection
        private readonly HashSet<string> _fillerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "um", "uh", "ah", "eh", "oh", "like", "you know", "actually", "basically", "literally",
            "sort of", "kind of", "i mean", "well", "so", "right", "okay", "alright", "yeah"
        };

        private TakeManager() { }

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
        /// <param name="projectName">Name of the project</param>
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

                bool allFilesProcessedSuccessfully = true;

                // Process each SRT file individually
                foreach (var srtFile in srtFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileName(srtFile);
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(srtFile);
                        
                        // Parse segments from this specific file
                        var segments = await ParseSrtFileAsync(srtFile, fileName);
                        
                        if (!segments.Any())
                        {
                            System.Diagnostics.Debug.WriteLine($"No segments found in {fileName}. Skipping...");
                            continue;
                        }

                        // Detect and select best takes for this file
                        var canonicalSegments = await DetectAndSelectBestTakesAsync(segments, settings.TakeLayerSettings);

                        // Create output file with the same name as source
                        var outputPath = Path.Combine(renderDir, fileName);
                        await WriteCanonicalSegmentsAsync(canonicalSegments, outputPath);

                        System.Diagnostics.Debug.WriteLine($"Take layer processing completed for {fileName}. Output: {outputPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing file {Path.GetFileName(srtFile)}: {ex.Message}");
                        allFilesProcessedSuccessfully = false;
                    }
                }

                return allFilesProcessedSuccessfully;
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
        private async Task<List<SrtSegment>> ParseSrtFileAsync(string filePath, string fileName)
        {
            var segments = new List<SrtSegment>();
            var lines = await File.ReadAllLinesAsync(filePath);
            
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
                            FileName = fileName
                        };

                        // Calculate quality score for this segment
                        segment.QualityScore = CalculateQualityScore(segment, new TakeLayerSettings());
                        
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

            // Calculate Levenshtein distance
            int levenshteinDistance = CalculateLevenshteinDistance(text1, text2);
            
            // Calculate cosine similarity (simplified word-level)
            float cosineSimilarity = CalculateCosineSimilarity(text1, text2);

            // Segments are similar if either metric indicates similarity
            return (levenshteinDistance <= settings.LevenshteinThreshold) || 
                   (cosineSimilarity >= settings.CosineSimilarityThreshold);
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

            // Recalculate quality scores with current settings
            foreach (var segment in cluster.Segments)
            {
                segment.QualityScore = CalculateQualityScore(segment, settings);
            }

            // Return segment with highest quality score
            return cluster.Segments.OrderByDescending(s => s.QualityScore).First();
        }

        /// <summary>
        /// Calculate quality score for a segment (currently FlubScore only)
        /// </summary>
        private float CalculateQualityScore(SrtSegment segment, TakeLayerSettings settings)
        {
            // Current implementation: FlubScore only
            float flubScore = CalculateFlubScore(segment.Text);
            
            // Placeholder for future scoring components
            float relevanceScore = 0f;  // Placeholder
            float focusScore = 0f;      // Placeholder  
            float energyScore = 0f;     // Placeholder

            // Calculate weighted score
            float totalWeight = settings.RelevanceWeight + settings.FlubWeight + 
                               settings.FocusWeight + settings.EnergyWeight;
            
            if (totalWeight == 0) return 0f;

            return (relevanceScore * settings.RelevanceWeight + 
                   (1 - flubScore) * settings.FlubWeight + 
                   focusScore * settings.FocusWeight + 
                   energyScore * settings.EnergyWeight) / totalWeight;
        }

        /// <summary>
        /// Calculate flub score (ratio of filler words to total words)
        /// </summary>
        private float CalculateFlubScore(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0f;

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!words.Any()) return 0f;

            int fillerWordCount = words.Count(word => 
                _fillerWords.Contains(word.Trim().ToLowerInvariant()));

            return (float)fillerWordCount / words.Length;
        }

        /// <summary>
        /// Write canonical segments to output SRT file with enhanced format
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