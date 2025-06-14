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

        public async Task<string> GenerateStoryFromMediaAsync(MediaFileInfo mediaFile)
        {
            var prompt = BuildMediaPrompt(mediaFile);
            return await _aiManager.GenerateTextAsync(prompt, saveHistory: false);
        }

        public async Task<string> GenerateStoryFromProjectAsync(ProjectModel project)
        {
            var prompt = BuildProjectPrompt(project);
            return await _aiManager.GenerateTextAsync(prompt, saveHistory: false);
        }

        private string BuildMediaPrompt(MediaFileInfo mediaFile)
        {
            return $"""
                Create a creative description for this video content:
                Filename: {mediaFile.FileName}
                Duration: {mediaFile.Duration}
                Import Date: {mediaFile.ImportDate}
                Video Details: {mediaFile.VideoMetaData?.ToString() ?? "No metadata available"}
                """;
        }

        private string BuildProjectPrompt(ProjectModel project)
        {
            return $"""
                Create a story that connects these video files into a coherent narrative:
                Project Name: {project.Name}
                Project Description: {project.Description}
                Number of Media Files: {project.MediaFiles.Count}
                Project Created: {project.CreatedDate}
                Custom Prompt: {project.Prompt}

                Media Files:
                {string.Join("\n", project.MediaFiles.Select(m => $"- {m.FileName} ({m.Duration})"))}
                """;
        }

        public void UpdateSystemPrompt(string newPrompt)
        {
            _aiManager.SystemPrompt = newPrompt;
        }

        public void ResetSystemPrompt()
        {
            _aiManager.SystemPrompt = "You are an expert at analyzing video transcripts and rating content relevance. You MUST rate only with a number between 1 and 100. No other text, no explanations, just the number.";
        }

        public async Task RankProjectTranscriptsAsync(ProjectModel project, StorySettings settings, string renderDir)
        {
            if (project == null || string.IsNullOrEmpty(settings.Prompt))
                throw new ArgumentException("Project and prompt are required");

            var rankedFilePath = Path.Combine(renderDir, $"{project.Name}.ranked.srt");
            var transcriptionDir = Path.Combine(App.ProjectHandler.Instance.CurrentProjectPath, "Transcriptions");
            // Create or clear the output file
            await File.WriteAllTextAsync(rankedFilePath, string.Empty);
            int segmentCounter = 1;

            foreach (var mediaFile in project.MediaFiles.Where(m => m.HasTranscription))
            {               
                var srtPath = Path.Combine(transcriptionDir, $"{Path.GetFileNameWithoutExtension(mediaFile.FileName)}.srt");
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

        private async Task AppendSegmentToFileAsync(
            string filePath,
            (string fileName, int segmentNumber, string text, (float relevance, float sentiment, float novelty, float speakerEnergy) scores, TimeSpan start, TimeSpan end) segment,
            int counter)
        {
            // Use FileMode.Append to add to the existing file
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

        private string FormatTimeSpan(TimeSpan ts)
        {
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
        }

        // Add a new struct for weights
        public struct RankingWeights
        {
            public float Relevance { get; set; }
            public float Sentiment { get; set; }
            public float Novelty { get; set; }
            public float Energy { get; set; }

            public RankingWeights(float relevance, float sentiment, float novelty, float energy)
            {
                Relevance = relevance;
                Sentiment = sentiment;
                Novelty = novelty;
                Energy = energy;
            }
        }

        // Modify RankOrder to accept weights
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

        private float CalculateWeightedScore(
            (float relevance, float sentiment, float novelty, float speakerEnergy) scores,
            RankingWeights weights)
        {
            float totalWeight = weights.Relevance + weights.Sentiment + weights.Novelty + weights.Energy;
            if (totalWeight == 0) return 0;

            return (scores.relevance * weights.Relevance +
                    scores.sentiment * weights.Sentiment +
                    scores.novelty * weights.Novelty +
                    scores.speakerEnergy * weights.Energy) / totalWeight;
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

        public async Task TrimRankOrder(string projectName, int maxLengthSeconds, string renderDir)
        {
            var orderedFilePath = Path.Combine(renderDir, $"{projectName}.ordered.srt");
            var trimmedFilePath = Path.Combine(renderDir, $"{projectName}.trim.ordered.srt");

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

            // Read segments from the ordered file (already sorted by score)
            var lines = await File.ReadAllLinesAsync(orderedFilePath);
            
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