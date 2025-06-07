using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

namespace FrameFlow.Utilities
{
    public class SrtSegment
    {
        public int Index { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Text { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
    }

    public static class SrtUtils
    {
        private static readonly Regex TimeRegex = new(
            @"(?<h1>\d{2}):(?<m1>\d{2}):(?<s1>\d{2}),(?<ms1>\d{3})\s*-->\s*(?<h2>\d{2}):(?<m2>\d{2}):(?<s2>\d{2}),(?<ms2>\d{3})",
            RegexOptions.Compiled);

        private static readonly Regex ScoreRegex = new(@"\[Score:\s*(\d+\.?\d*)\]", RegexOptions.Compiled);

        public static List<SrtSegment> FilterSegmentsByScoreAndDuration(List<SrtSegment> segments, float minScore, TimeSpan maxDuration)
        {
            // First, extract scores and sort segments by score (highest first)
            var scoredSegments = segments
                .Select(seg => {
                    var match = ScoreRegex.Match(seg.Text);
                    float score = match.Success ? float.Parse(match.Groups[1].Value) : 0f;
                    return (Segment: seg, Score: score);
                })
                .Where(x => x.Score >= minScore)
                .OrderByDescending(x => x.Score)
                .ToList();

            // Now select segments up to maxDuration
            var result = new List<SrtSegment>();
            var totalDuration = TimeSpan.Zero;

            foreach (var (segment, _) in scoredSegments)
            {
                var segmentDuration = segment.End - segment.Start;
                if (totalDuration + segmentDuration <= maxDuration)
                {
                    result.Add(segment);
                    totalDuration += segmentDuration;
                }
                else
                {
                    break;
                }
            }

            // Sort by original time order
            return result.OrderBy(s => s.Start).ToList();
        }

        public static TimeSpan ParseDurationString(string durationStr)
        {
            if (durationStr == "<= 60 Seconds")
                return TimeSpan.FromSeconds(60);
            else if (durationStr == "<=10 Min")
                return TimeSpan.FromMinutes(10);
            else if (durationStr == "<=20 Min")
                return TimeSpan.FromMinutes(20);
            else if (int.TryParse(durationStr, out int minutes))
                return TimeSpan.FromMinutes(minutes);
            else
                throw new ArgumentException($"Invalid duration format: {durationStr}");
        }

        public static List<SrtSegment> ParseSrt(string filePath)
        {
            var segments = new List<SrtSegment>();
            var lines = File.ReadAllLines(filePath);
            int i = 0;
            while (i < lines.Length)
            {
                // Parse index
                if (!int.TryParse(lines[i], out int index))
                {
                    i++;
                    continue;
                }
                i++;
                if (i >= lines.Length) break;

                // Parse time
                var match = TimeRegex.Match(lines[i]);
                if (!match.Success)
                {
                    i++;
                    continue;
                }
                var start = new TimeSpan(
                    int.Parse(match.Groups["h1"].Value),
                    int.Parse(match.Groups["m1"].Value),
                    int.Parse(match.Groups["s1"].Value))
                    .Add(TimeSpan.FromMilliseconds(int.Parse(match.Groups["ms1"].Value)));
                var end = new TimeSpan(
                    int.Parse(match.Groups["h2"].Value),
                    int.Parse(match.Groups["m2"].Value),
                    int.Parse(match.Groups["s2"].Value))
                    .Add(TimeSpan.FromMilliseconds(int.Parse(match.Groups["ms2"].Value)));
                i++;
                if (i >= lines.Length) break;

                // Parse text
                var textLines = new List<string>();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    textLines.Add(lines[i]);
                    i++;
                }
                var text = string.Join("\n", textLines);
                // Skip empty line
                while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i])) i++;

                segments.Add(new SrtSegment
                {
                    Index = index,
                    Start = start,
                    End = end,
                    Text = text,
                    SourceFile = Path.GetFileName(filePath)
                        .Replace("_audio_transcription.srt", ".mp4")
                        .Replace("_transcription.srt", ".mp4")
                });
            }
            return segments;
        }

        public static void WriteSrt(string filePath, List<SrtSegment> segments, bool includeSource = true)
        {
            using var writer = new StreamWriter(filePath);
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                writer.WriteLine(i + 1);
                writer.WriteLine($"{FormatTime(seg.Start)} --> {FormatTime(seg.End)}");
                if (includeSource && !string.IsNullOrEmpty(seg.SourceFile))
                {
                    // Convert the transcription filename back to the original video filename
                    string videoSource = seg.SourceFile.Replace("_audio_transcription.srt", ".mp4");
                    writer.WriteLine($"[Source: {videoSource}]");
                }
                writer.WriteLine(seg.Text);
                writer.WriteLine();
            }
        }

        public static string FormatTime(TimeSpan time)
        {
            return string.Format("{0:00}:{1:00}:{2:00},{3:000}",
                (int)time.TotalHours, time.Minutes, time.Seconds, time.Milliseconds);
        }

        /// <summary>
        /// Reorders SRT segments to tell a coherent story based on the subject.
        /// Uses the Phi model to evaluate segment relationships and story flow.
        /// </summary>
        public static async Task<List<SrtSegment>> ReorderSegmentsForStoryAsync(
            List<SrtSegment> segments,
            string subject,
            PhiChatModel phi,
            IProgress<TranscriptionProgress>? progress = null)
        {
            if (segments.Count == 0) return segments;

            progress?.Report(new TranscriptionProgress(Prompts.Analysis.AnalyzingSegments, 0));

            // If total text length is greater than model's context window (128K),
            // process in chunks
            const int maxChunkSize = 20; // Process 20 segments at a time
            if (segments.Count > maxChunkSize)
            {
                var orderedSegments = new List<SrtSegment>();
                progress?.Report(new TranscriptionProgress(Prompts.Analysis.ProcessingChunk(0, segments.Count), 0));
                var chunks = segments.Chunk(maxChunkSize).ToList();
                
                for (int i = 0; i < chunks.Count; i++)
                {
                    progress?.Report(new TranscriptionProgress(Prompts.Analysis.ProcessingChunk(i + 1, chunks.Count), 0));
                    var chunk = chunks[i].ToList();
                    var orderedChunk = await ReorderSegmentChunkAsync(chunk, subject, phi);
                    orderedSegments.AddRange(orderedChunk);
                }

                // Final pass to ensure global coherence
                progress?.Report(new TranscriptionProgress(Prompts.Analysis.FinalCoherence, 0));
                return await ReorderSegmentChunkAsync(orderedSegments, subject, phi);
            }

            return await ReorderSegmentChunkAsync(segments, subject, phi);
        }

        private static async Task<List<SrtSegment>> ReorderSegmentChunkAsync(
            List<SrtSegment> segments,
            string subject,
            PhiChatModel phi)
        {
            const int maxChunkSize = 10; // Reduce from 20 to 10 for better memory management
            
            if (segments.Count > maxChunkSize)
            {
                var subChunks = segments
                    .Select((s, i) => new { Segment = s, Index = i })
                    .GroupBy(x => x.Index / maxChunkSize)
                    .Select(g => g.Select(x => x.Segment).ToList())
                    .ToList();

                var orderedSegments = new List<SrtSegment>();
                foreach (var subChunk in subChunks)
                {
                    var orderedSubChunk = await ProcessChunk(subChunk, subject, phi);
                    orderedSegments.AddRange(orderedSubChunk);
                }
                return orderedSegments;
            }

            return await ProcessChunk(segments, subject, phi);
        }

        private static async Task<List<SrtSegment>> ProcessChunk(
            List<SrtSegment> chunk,
            string subject,
            PhiChatModel phi)
        {
            var orderedSegments = new List<SrtSegment>();
            var remainingSegments = new List<SrtSegment>(chunk);

            // Find best start
            var bestStartScore = -1f;
            SrtSegment? bestStart = null;

            foreach (var segment in chunk)
            {
                var startScore = await EvaluateAsStoryStart(segment, subject, phi);
                if (startScore > bestStartScore)
                {
                    bestStartScore = startScore;
                    bestStart = segment;
                }
            }

            if (bestStart != null)
            {
                orderedSegments.Add(bestStart);
                remainingSegments.Remove(bestStart);
            }

            // Build story by finding best next segment
            while (remainingSegments.Count > 0)
            {
                var currentSegment = orderedSegments[^1];
                var bestNextScore = -1f;
                SrtSegment? bestNext = null;

                foreach (var candidate in remainingSegments)
                {
                    var nextScore = await EvaluateAsNextSegment(currentSegment, candidate, subject, phi);
                    if (nextScore > bestNextScore)
                    {
                        bestNextScore = nextScore;
                        bestNext = candidate;
                    }
                }

                if (bestNext != null)
                {
                    orderedSegments.Add(bestNext);
                    remainingSegments.Remove(bestNext);
                }
                else
                {
                    // If no good next segment found, add remaining by relevance
                    orderedSegments.AddRange(remainingSegments);
                    break;
                }
            }

            return orderedSegments;
        }

        private static async Task<float> EvaluateAsStoryStart(
            SrtSegment segment,
            string subject,
            PhiChatModel phi)
        {
            // Truncate text if too long (keeping start of segment as it's most important for story start)
            string truncatedText = segment.Text;
            if (truncatedText.Length > 500)
            {
                truncatedText = truncatedText.Substring(0, 500) + "...";
            }

            phi.SystemPrompt = Prompts.System.ScoreOnly;
            string prompt = Prompts.Story.EvaluateStart(subject, truncatedText);
            return float.TryParse(phi.Chat(prompt, addToHistory: false), out float score) ? score : 0f;
        }

        private static async Task<float> EvaluateAsNextSegment(
            SrtSegment current,
            SrtSegment next,
            string subject,
            PhiChatModel phi)
        {
            // Truncate both segments to ensure we don't exceed token limit
            string truncatedCurrent = current.Text;
            string truncatedNext = next.Text;
            
            if (truncatedCurrent.Length > 300)
            {
                truncatedCurrent = truncatedCurrent.Substring(0, 300) + "...";
            }
            if (truncatedNext.Length > 300)
            {
                truncatedNext = truncatedNext.Substring(0, 300) + "...";
            }

            phi.SystemPrompt = Prompts.System.ScoreOnly;
            string prompt = Prompts.Story.EvaluateNext(subject, truncatedCurrent, truncatedNext);
            return float.TryParse(phi.Chat(prompt, addToHistory: false), out float score) ? score : 0f;
        }

        public static async Task<float> EvaluateSegmentContinuity(
            SrtSegment current,
            SrtSegment next,
            PhiChatModel phi)
        {
            // Truncate both segments to ensure we don't exceed token limit
            string truncatedCurrent = current.Text.Length > 300 
                ? current.Text.Substring(0, 300) + "..." 
                : current.Text;
            string truncatedNext = next.Text.Length > 300 
                ? next.Text.Substring(0, 300) + "..."
                : next.Text;

            phi.SystemPrompt = Prompts.System.ScoreOnly;
            string prompt = Prompts.Story.EvaluateContinuity(truncatedCurrent, truncatedNext);
            return float.TryParse(phi.Chat(prompt, addToHistory: false), out float score) ? score : 0f;
        }
    }
} 