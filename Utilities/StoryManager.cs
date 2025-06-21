using FrameFlow.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Configuration;

namespace FrameFlow.Utilities
{
    public class StoryManager
    {
        private static StoryManager? _instance;
        private readonly GenAIManager _aiManager;
        private readonly object _lock = new object();

        private StoryManager()
        {
            _aiManager = GenAIManager.Instance;
            _aiManager.SystemPrompt = "You are an expert at analyzing video transcripts and rating content relevance. Rate only with a number between 0 and 100.";
        }

        public static StoryManager Instance
        {
            get
            {
                _instance ??= new StoryManager();
                return _instance;
            }
        }

        public void UpdateSystemPrompt(string newPrompt)
        {
            _aiManager.SystemPrompt = newPrompt;
        }

        public void ResetSystemPrompt()
        {
            _aiManager.SystemPrompt = "You are an expert at analyzing video transcripts and rating content relevance. You MUST rate only with a number between 1 and 100. No other text, no explanations, just the number.";
        }

        // Enhanced version with all quality metrics
        private async Task AppendSegmentToFileAsync(string filePath, (string fileName, int segmentNumber, string text, (float relevance, float sentiment, float novelty, float energy, float focus, float clarity, float emotion, float flubScore, float compositeScore) scores, TimeSpan start, TimeSpan end) segment, int counter)
        {
            using var writer = new StreamWriter(filePath, append: true, encoding: Encoding.UTF8);
            
            await writer.WriteLineAsync(counter.ToString());
            await writer.WriteLineAsync($"{FormatTimeSpan(segment.start)} --> {FormatTimeSpan(segment.end)}");
            await writer.WriteLineAsync($"{segment.fileName}");
            await writer.WriteLineAsync($"Relevance: {segment.scores.relevance:F1}");
            await writer.WriteLineAsync($"Sentiment: {segment.scores.sentiment:F1}");
            await writer.WriteLineAsync($"Novelty: {segment.scores.novelty:F1}");
            await writer.WriteLineAsync($"Energy: {segment.scores.energy:F1}");
            await writer.WriteLineAsync($"Focus: {segment.scores.focus:F1}");
            await writer.WriteLineAsync($"Clarity: {segment.scores.clarity:F1}");
            await writer.WriteLineAsync($"Emotion: {segment.scores.emotion:F1}");
            await writer.WriteLineAsync($"FlubScore: {segment.scores.flubScore:F1}");
            await writer.WriteLineAsync($"CompositeScore: {segment.scores.compositeScore:F1}");
            await writer.WriteLineAsync(segment.text);
            await writer.WriteLineAsync(); // Empty line between segments
        }

        // Legacy version with 4 metrics for backward compatibility
        private async Task AppendSegmentToFileAsync(string filePath, (string fileName, int segmentNumber, string text, (float relevance, float sentiment, float novelty, float speakerEnergy) scores, TimeSpan start, TimeSpan end) segment, int counter)
        {
            using var writer = new StreamWriter(filePath, append: true, encoding: Encoding.UTF8);
            
            await writer.WriteLineAsync(counter.ToString());
            await writer.WriteLineAsync($"{FormatTimeSpan(segment.start)} --> {FormatTimeSpan(segment.end)}");
            await writer.WriteLineAsync($"{segment.fileName}");
            await writer.WriteLineAsync($"Relevance: {segment.scores.relevance:F1}");
            await writer.WriteLineAsync($"Sentiment: {segment.scores.sentiment:F1}");
            await writer.WriteLineAsync($"Novelty: {segment.scores.novelty:F1}");
            await writer.WriteLineAsync($"Energy: {segment.scores.speakerEnergy:F1}");
            await writer.WriteLineAsync(segment.text);
            await writer.WriteLineAsync(); // Empty line between segments
        }

        private async Task<List<(int number, string text, TimeSpan start, TimeSpan end)>> ParseSrtFileAsync(string filePath)
        {
            var segments = new List<(int number, string text, TimeSpan start, TimeSpan end)>();
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

                        segments.Add((number, textBuilder.ToString().Trim(), start, end));
                    }
                }
                else
                {
                    i++;
                }
            }

            return segments;
        }

        private async Task<List<(int number, string fileName, string text, (float relevance, float sentiment, float novelty, float energy, float focus, float clarity, float emotion, float flubScore, float compositeScore) scores, TimeSpan start, TimeSpan end)>> ParseEnhancedSrtFileAsync(string filePath)
        {
            var segments = new List<(int number, string fileName, string text, (float relevance, float sentiment, float novelty, float energy, float focus, float clarity, float emotion, float flubScore, float compositeScore) scores, TimeSpan start, TimeSpan end)>();
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
                        i++; // Move to focus score line
                        var focus = float.Parse(lines[i].Split(": ")[1]);
                        i++; // Move to clarity score line
                        var clarity = float.Parse(lines[i].Split(": ")[1]);
                        i++; // Move to emotion score line
                        var emotion = float.Parse(lines[i].Split(": ")[1]);
                        i++; // Move to flubScore score line
                        var flubScore = float.Parse(lines[i].Split(": ")[1]);
                        i++; // Move to compositeScore score line
                        var compositeScore = float.Parse(lines[i].Split(": ")[1]);
                        
                        // Move to text content
                        var textBuilder = new StringBuilder();
                        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                        {
                            textBuilder.AppendLine(lines[i]);
                            i++;
                        }

                        segments.Add((number, fileName, textBuilder.ToString().Trim(), (relevance, sentiment, novelty, energy, focus, clarity, emotion, flubScore, compositeScore), start, end));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing enhanced SRT segment: {ex.Message}");
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

        private string FormatTimeSpan(TimeSpan ts)
        {
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
        }

        public struct RankingWeights
        {
            public float Relevance { get; set; }
            public float Sentiment { get; set; }
            public float Novelty { get; set; }
            public float Energy { get; set; }
            public float Focus { get; set; }
            public float Clarity { get; set; }
            public float Emotion { get; set; }
            public float FlubScore { get; set; }
            public float CompositeScore { get; set; }

            public RankingWeights(float relevance, float sentiment, float novelty, float energy, 
                                float focus = 0f, float clarity = 0f, float emotion = 0f, 
                                float flubScore = 0f, float compositeScore = 0f)
            {
                Relevance = relevance;
                Sentiment = sentiment;
                Novelty = novelty;
                Energy = energy;
                Focus = focus;
                Clarity = clarity;
                Emotion = emotion;
                FlubScore = flubScore;
                CompositeScore = compositeScore;
            }
        }

        public async Task RankProjectTranscriptsAsync(ProjectModel project, StorySettings settings, string renderDir)
        {
            if (project == null || string.IsNullOrEmpty(settings.Prompt))
                throw new ArgumentException("Project and prompt are required");

            var rankedFilePath = Path.Combine(renderDir, $"{project.Name}.ranked.srt");
            var transcriptionDir = Path.Combine(renderDir);
            
            // Check if TakeManager has already processed files in renderDir
            var takeManagerFiles = Directory.GetFiles(renderDir, "*.srt");
            if (takeManagerFiles.Any())
            {
                // Use TakeManager output which already has quality vectors
                await ProcessTakeManagerOutputAsync(takeManagerFiles, rankedFilePath, project);
                return;
            }
            
            // Fallback: process original transcriptions (legacy mode)
            await ProcessOriginalTranscriptionsAsync(project, settings, renderDir, rankedFilePath);
        }

        private async Task ProcessTakeManagerOutputAsync(string[] srtFiles, string rankedFilePath, ProjectModel project)
        {
            // Create or clear the output file
            await File.WriteAllTextAsync(rankedFilePath, string.Empty);
            int segmentCounter = 1;

            foreach (var srtFile in srtFiles)
            {
                var segments = await ParseEnhancedSrtFileAsync(srtFile);
                foreach (var segment in segments)
                {
                    // Write this segment immediately to the file (scores already included)
                    await AppendSegmentToFileAsync(
                        rankedFilePath,
                        (
                            segment.fileName,
                            segment.number,
                            segment.text,
                            segment.scores,
                            segment.start,
                            segment.end
                        ),
                        segmentCounter++
                    );
                }
            }
        }

        private async Task ProcessOriginalTranscriptionsAsync(ProjectModel project, StorySettings settings, string renderDir, string rankedFilePath)
        {
            // Create or clear the output file
            await File.WriteAllTextAsync(rankedFilePath, string.Empty);
            int segmentCounter = 1;

            foreach (var mediaFile in project.MediaFiles.Where(m => m.HasTranscription))
            {               
                var srtPath = Path.Combine(renderDir, $"{Path.GetFileNameWithoutExtension(mediaFile.FileName)}.srt");
                if (!File.Exists(srtPath)) continue;

                var segments = await ParseSrtFileAsync(srtPath);
                foreach (var segment in segments)
                {
                    var scores = await VectorRankSegmentAsync(segment.text, settings);
                    
                     // Write this segment immediately to the file
                     await AppendSegmentToFileAsync(
                        rankedFilePath,
                        (
                            mediaFile.FileName,
                            segment.number,
                            segment.text,
                            scores,  // Now passing the full vector of scores
                            segment.start,
                            segment.end
                        ),
                        segmentCounter++
                    );
                }
            }
        }

        // Enhanced version with all quality metrics
        private float CalculateWeightedScore((float relevance, float sentiment, float novelty, float energy, float focus, float clarity, float emotion, float flubScore, float compositeScore) scores, RankingWeights weights)
        {
            float totalWeight = weights.Relevance + weights.Sentiment + weights.Novelty + weights.Energy + 
                               weights.Focus + weights.Clarity + weights.Emotion + weights.FlubScore + weights.CompositeScore;
            if (totalWeight == 0) return 0;

            return (scores.relevance * weights.Relevance +
                    scores.sentiment * weights.Sentiment +
                    scores.novelty * weights.Novelty +
                    scores.energy * weights.Energy +
                    scores.focus * weights.Focus +
                    scores.clarity * weights.Clarity +
                    scores.emotion * weights.Emotion +
                    scores.flubScore * weights.FlubScore +
                    scores.compositeScore * weights.CompositeScore) / totalWeight;
        }

        // Legacy version with 4 metrics for backward compatibility
        private float CalculateWeightedScore((float relevance, float sentiment, float novelty, float speakerEnergy) scores, RankingWeights weights)
        {
            float totalWeight = weights.Relevance + weights.Sentiment + weights.Novelty + weights.Energy;
            if (totalWeight == 0) return 0;

            return (scores.relevance * weights.Relevance +
                    scores.sentiment * weights.Sentiment +
                    scores.novelty * weights.Novelty +
                    scores.speakerEnergy * weights.Energy) / totalWeight;
        }

        public async Task RankOrder(string projectName, RankingWeights weights, string renderDir)
        {
            var rankedFilePath = Path.Combine(renderDir, $"{projectName}.ranked.srt");
            var orderedFilePath = Path.Combine(renderDir, $"{projectName}.ordered.srt");

            if (!File.Exists(rankedFilePath))
            {
                throw new FileNotFoundException("Ranked segments file not found", rankedFilePath);
            }

            // Read and parse all segments
            var segments = new List<(
                string fileName, 
                (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
                TimeSpan start,
                TimeSpan end,
                string text
            )>();
            
            var lines = await File.ReadAllLinesAsync(rankedFilePath);
            
            for (int i = 0; i < lines.Length;)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                if (int.TryParse(lines[i], out int counter))
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

                        segments.Add((
                            fileName,
                            (relevance, sentiment, novelty, energy),
                            start,
                            end,
                            textBuilder.ToString().Trim()
                        ));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing segment: {ex.Message}");
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }

            // Modify the sort to use weighted scoring
            segments.Sort((a, b) =>
            {
                float scoreA = CalculateWeightedScore(a.scores, weights);
                float scoreB = CalculateWeightedScore(b.scores, weights);
                return scoreB.CompareTo(scoreA);
            });

            // Create or clear the output file
            await File.WriteAllTextAsync(orderedFilePath, string.Empty);
            
            // Write ordered segments
            int newCounter = 1;
            foreach (var segment in segments)
            {
              await AppendSegmentToFileAsync(
                    orderedFilePath,
                    (
                        segment.fileName,
                        newCounter,
                        segment.text,
                        segment.scores,
                        segment.start,
                        segment.end
                    ),
                    newCounter++
                );
            }
        }

        public async Task NoveltyReRank(string projectName, float lambda, string renderDir)
        {
            var orderedFilePath = Path.Combine(renderDir, $"{projectName}.ordered.srt");
            var noveltyFilePath = Path.Combine(renderDir, $"{projectName}.novelty.srt");

            if (!File.Exists(orderedFilePath))
            {
                throw new FileNotFoundException("Ordered segments file not found", orderedFilePath);
            }

            var segments = new List<(
                string fileName,
                (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
                TimeSpan start,
                TimeSpan end,
                string text
            )>();

            // Read all segments from the ordered file
            var lines = await File.ReadAllLinesAsync(orderedFilePath);
            
            // Parse segments using the same pattern as before
            for (int i = 0; i < lines.Length;)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                if (int.TryParse(lines[i], out int counter))
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

                        segments.Add((
                            fileName,
                            (relevance, sentiment, novelty, energy),
                            start,
                            end,
                            textBuilder.ToString().Trim()
                        ));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing segment: {ex.Message}");
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }

            // Apply MMR-style reranking using pre-computed novelty scores
            var remainingSegments = new List<(
                string fileName,
                (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
                TimeSpan start,
                TimeSpan end,
                string text
            )>(segments);

            var rerankedSegments = new List<(
                string fileName,
                (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
                TimeSpan start,
                TimeSpan end,
                string text
            )>();

            while (remainingSegments.Count > 0)
            {
                // Find segment that maximizes λ·score(c) − (1−λ)·novelty(c)
                var bestScore = float.MinValue;
                var bestIndex = -1;

                for (int i = 0; i < remainingSegments.Count; i++)
                {
                    var segment = remainingSegments[i];
                    
                    // Calculate weighted score using relevance and novelty
                    var weightedScore = lambda * (segment.scores.relevance / 100f) - 
                                      (1 - lambda) * (1 - segment.scores.novelty / 100f);
                    
                    if (weightedScore > bestScore)
                    {
                        bestScore = weightedScore;
                        bestIndex = i;
                    }
                }

                // Add best segment to reranked list and remove from remaining
                if (bestIndex >= 0)
                {
                    rerankedSegments.Add(remainingSegments[bestIndex]);
                    remainingSegments.RemoveAt(bestIndex);
                }
            }

            // Write reranked segments to file
            await File.WriteAllTextAsync(noveltyFilePath, string.Empty);
            
            int newCounter = 1;
            foreach (var segment in rerankedSegments)
            {
                await AppendSegmentToFileAsync(
                    noveltyFilePath,
                    (
                        segment.fileName,
                        newCounter,
                        segment.text,
                        segment.scores,
                        segment.start,
                        segment.end
                    ),
                    newCounter++
                );
            }
        }

        private async Task<(float relevance, float sentiment, float novelty, float speakerEnergy)> VectorRankSegmentAsync(string segmentText, StorySettings settings)
        {
            try
            {
                UpdateSystemPrompt(@"You are an expert at analyzing video transcript segments across multiple dimensions. 
For each segment, analyze and return 4 scores with these specific ranges:
1. Relevance: 0-100 (0=irrelevant, 100=highly relevant)
2. Sentiment: -100 to +100 (-100=very negative, 0=neutral, +100=very positive)
3. Novelty: 0-10 (0=completely common, 10=extremely unique/surprising)
4. Energy: 1-5 (1=very low energy, 5=very high energy)

Format: '##,##,#.#,#.#' 
You MUST respond with ONLY the numbers in the speficied format. NO other text, NO explanations, just the comma-separated numbes.
Example: '80,-34,4.3,3.2'");

                var rankingPrompt = $""" 
                    Analyze this transcript segment across multiple dimensions.

                    Prompt: {settings.Prompt}

                    Transcript segment:
                    {segmentText}
                    """;
                _aiManager.UpdateSettings(settings.GenAISettings.Temperature, 
                                          settings.GenAISettings.TopP, 
                                          settings.GenAISettings.RepetitionPenalty, 
                                          null, 
                                          null, 
                                          settings.GenAISettings.RandomSeed);
                var response = await _aiManager.GenerateTextAsync(rankingPrompt, saveHistory: false);
                
                // Parse the comma-separated response
                var scores = response.Split(',', StringSplitOptions.TrimEntries);
                if (scores.Length == 4 && 
                    float.TryParse(scores[0], out float rawRelevance) &&
                    float.TryParse(scores[1], out float rawSentiment) &&
                    float.TryParse(scores[2], out float rawNovelty) &&
                    float.TryParse(scores[3], out float rawEnergy))
                {
                    // Normalize each score to 0-1 range
                    float relevance = Math.Clamp(rawRelevance / 100f, 0f, 1f);
                    float sentiment = Math.Clamp((rawSentiment + 100f) / 200f, 0f, 1f); // Convert -100,+100 to 0,1
                    float novelty = Math.Clamp(rawNovelty / 10f, 0f, 1f);
                    float energy = Math.Clamp((rawEnergy - 1f) / 4f, 0f, 1f); // Convert 1-5 to 0-1

                    // Convert back to 0-100 range for consistency with existing code
                    return (
                        relevance * 100f,
                        sentiment * 100f,
                        novelty * 100f,
                        energy * 100f
                    );
                }

                // If parsing fails, return default scores
                System.Diagnostics.Debug.WriteLine($"Failed to parse vector scores from response: {response}");
                return (0, 50, 0, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to vector rank segment: {ex.Message}");
                return (0, 50, 0, 0);
            }
        }

        public async Task TemporalExpansion(string projectName, float baseWindowSeconds, string renderDir)
        {
            var orderedFilePath = Path.Combine(renderDir, $"{projectName}.dialogue.srt");
            var expandedFilePath = Path.Combine(renderDir, $"{projectName}.expanded.srt");

            if (!File.Exists(orderedFilePath))
            {
                throw new FileNotFoundException("Dialogue segments file not found", orderedFilePath);
            }

            var segments = new List<(
                string fileName,
                (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
                TimeSpan start,
                TimeSpan end,
                string text
            )>();

            // Read all segments from the trimmed file
            var lines = await File.ReadAllLinesAsync(orderedFilePath);
            
            // Parse segments using the same pattern as in TrimRankOrder
            for (int i = 0; i < lines.Length;)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                if (int.TryParse(lines[i], out int counter))
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

                        segments.Add((
                            fileName,
                            (relevance, sentiment, novelty, energy),
                            start,
                            end,
                            textBuilder.ToString().Trim()
                        ));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing segment: {ex.Message}");
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }

            // Process each segment with energy-based temporal expansion
            var expandedSegments = new List<(
                string fileName,
                (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
                TimeSpan start,
                TimeSpan end,
                string text
            )>();

            foreach (var segment in segments)
            {
                // Normalize energy score from 0-100 to 0-1
                float energyNorm = segment.scores.speakerEnergy / 100f;
                
                // Calculate dynamic window size using linear interpolation
                // Lower energy → 0.8 * baseWindow
                // Higher energy → 1.3 * baseWindow
                float deltaSeconds = baseWindowSeconds * (0.8f + (energyNorm * 0.5f));
                
                // Expand the time window
                var expandedStart = segment.start - TimeSpan.FromSeconds(deltaSeconds);
                var expandedEnd = segment.end + TimeSpan.FromSeconds(deltaSeconds);
                
                // Ensure we don't go below 0
                expandedStart = expandedStart < TimeSpan.Zero ? TimeSpan.Zero : expandedStart;
                
                expandedSegments.Add((
                    segment.fileName,
                    segment.scores,
                    expandedStart,
                    expandedEnd,
                    segment.text
                ));
            }

            // Write expanded segments to file
            await File.WriteAllTextAsync(expandedFilePath, string.Empty);
            
            int newCounter = 1;
            foreach (var segment in expandedSegments)
            {
                await AppendSegmentToFileAsync(
                    expandedFilePath,
                    (
                        segment.fileName,
                        newCounter,
                        segment.text,
                        segment.scores,
                        segment.start,
                        segment.end
                    ),
                    newCounter++
                );
            }
        }   
   
        public async Task TrimRankOrder(string projectName, int maxLengthSeconds, string renderDir)
        {
            var expandedFilePath = Path.Combine(renderDir, $"{projectName}.expanded.srt");
            var trimmedFilePath = Path.Combine(renderDir, $"{projectName}.trim.srt");

            if (!File.Exists(expandedFilePath))
            {
                throw new FileNotFoundException("Expanded segments file not found", expandedFilePath);
            }

            var segments = new List<(
                string fileName,
                (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
                TimeSpan start,
                TimeSpan end,
                string text
            )>();

            // Read segments from the ordered file (already sorted by score)
            var lines = await File.ReadAllLinesAsync(expandedFilePath);
            
            for (int i = 0; i < lines.Length;)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    continue;
                }

                if (int.TryParse(lines[i], out int counter))
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

                        segments.Add((
                            fileName,
                            (relevance, sentiment, novelty, energy),
                            start,
                            end,
                            textBuilder.ToString().Trim()
                        ));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing segment: {ex.Message}");
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }

            // Select segments until we reach the time limit
            var selectedSegments = new List<(
                string fileName,
                (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
                TimeSpan start,
                TimeSpan end,
                string text
            )>();

            TimeSpan totalDuration = TimeSpan.Zero;
            var len = maxLengthSeconds*60;
            foreach (var segment in segments)
            {
                var segmentDuration = segment.end - segment.start;
                if (totalDuration.TotalSeconds + segmentDuration.TotalSeconds <= len)
                {
                    selectedSegments.Add(segment);
                    totalDuration += segmentDuration;
                }
                else
                {
                    break;
                }
            }

            // Create or clear the output file
            await File.WriteAllTextAsync(trimmedFilePath, string.Empty);
            
            // Write selected segments
            int newCounter = 1;
            foreach (var segment in selectedSegments)
            {
                await AppendSegmentToFileAsync(
                    trimmedFilePath,
                    (
                        segment.fileName,
                        newCounter,
                        segment.text,
                        segment.scores,
                        segment.start,
                        segment.end
                    ),
                    newCounter++
                );
            }
        }
    }
} 