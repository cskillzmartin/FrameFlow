using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace FrameFlow.Utilities
{
    /// <summary>
    /// Manages dialogue processing and operations for the video editor
    /// </summary>
    public class DialogueManager
    {
        private static DialogueManager? _instance;
        private readonly object _lock = new object();

        private DialogueManager()
        {
            
        }
        
        public static DialogueManager Instance
        {
            get
            {
                _instance ??= new DialogueManager();
                return _instance;
            }
        }

        private record Segment(
            string FileName,
            TimeSpan Start,
            TimeSpan End,
            string Text,
            (float relevance, float sentiment, float novelty, float energy) Scores,
            string SpeakerId,
            SpeakerManager.ShotType ShotLabel);

        /// <summary>
        /// Arrange the order of segments in {projectName}.novelty.srt into conversational flow and overwrite the same file.
        /// </summary>
        /// <param name="projectName">Name of the current project (used to build filenames).</param>
        /// <param name="lambda">Weight trading off intrinsic score vs reply-score (0-1).</param>
        /// <param name="renderDir">Current render directory containing intermediate SRT files.</param>
        public async Task<bool> SequenceDialogueAsync(string projectName, float lambda, string renderDir)
        {
            try
            {
                // Clamp lambda to sane range
                lambda = Math.Clamp(lambda, 0f, 1f);

                var noveltyPath = Path.Combine(renderDir, $"{projectName}.novelty.srt");
                if (!File.Exists(noveltyPath))
                    throw new FileNotFoundException("Novelty-reranked SRT not found", noveltyPath);

                // Output file (will be created anew)
                var dialoguePath = Path.Combine(renderDir, $"{projectName}.dialogue.srt");

                // 1. Load speaker metadata (speaker & shot labels)
                var speakerMeta = await SpeakerManager.Instance.LoadSpeakerAnalysisAsync(projectName, renderDir);
                if (speakerMeta == null)
                    throw new InvalidOperationException("Speaker analysis metadata missing – run SpeakerManager first.");

                // Index meta segments by (text) for quick lookup (fallback if duplicates)
                var metaLookup = speakerMeta.Segments
                                            .GroupBy(s => s.Text)
                                            .ToDictionary(g => g.Key, g => g.First());

                // 2. Parse input SRT
                var rawLines = await File.ReadAllLinesAsync(noveltyPath);
                var parsed = new List<Segment>();

                for (int i = 0; i < rawLines.Length;)
                {
                    if (string.IsNullOrWhiteSpace(rawLines[i])) { i++; continue; }
                    if (!int.TryParse(rawLines[i], out int counter)) { i++; continue; }

                    // Timestamps
                    i++; if (i >= rawLines.Length) break;
                    var times = rawLines[i].Split(" --> ");
                    var start = TimeSpan.Parse(times[0].Replace(',', '.'));
                    var end = TimeSpan.Parse(times[1].Replace(',', '.'));

                    // FileName
                    i++; if (i >= rawLines.Length) break;
                    var fileName = rawLines[i];

                    // Scores
                    i++; var relevance = ParseScore(rawLines[i]);
                    i++; var sentiment = ParseScore(rawLines[i]);
                    i++; var novelty = ParseScore(rawLines[i]);
                    i++; var energy = ParseScore(rawLines[i]);

                    // Text lines
                    i++;
                    var textBuilder = new StringBuilder();
                    while (i < rawLines.Length && !string.IsNullOrWhiteSpace(rawLines[i]))
                    {
                        textBuilder.AppendLine(rawLines[i]);
                        i++;
                    }
                    var text = textBuilder.ToString().Trim();

                    // Speaker / shot lookup
                    if (!metaLookup.TryGetValue(text, out var metaSeg))
                    {
                        metaSeg = new SpeakerManager.SpeakerSegment { SpeakerId = "UNK", ShotLabel = SpeakerManager.ShotType.UNK };
                    }

                    parsed.Add(new Segment(fileName, start, end, text, (relevance, sentiment, novelty, energy), metaSeg.SpeakerId, metaSeg.ShotLabel));
                }

                if (parsed.Count == 0) return false;

                // Pre-compute base (scalar) scores 0-100
                var baseScores = parsed.Select(s => (s.Scores.relevance + s.Scores.sentiment + s.Scores.novelty + s.Scores.energy) / 4f).ToArray();

                // Reply score cache – lazily filled
                var replyCache = new Dictionary<(int, int), float>();

                var remaining = new HashSet<int>(Enumerable.Range(0, parsed.Count));
                var sequence = new List<int>();

                // Pick initial segment with highest scalar score
                int first = Array.IndexOf(baseScores, baseScores.Max());
                sequence.Add(first);
                remaining.Remove(first);

                while (remaining.Count > 0)
                {
                    int lastIdx = sequence[^1];
                    int bestIdx = -1; float bestScore = float.NegativeInfinity;

                    foreach (int idx in remaining)
                    {
                        // Enforce speaker alternation
                        if (parsed[idx].SpeakerId == parsed[lastIdx].SpeakerId)
                            continue;

                        float reply = GetReplyScoreAsync(lastIdx, idx).Result; // synchronous wait inside loop OK small dataset
                        float combined = lambda * baseScores[idx] + (1 - lambda) * reply;
                        if (combined > bestScore)
                        {
                            bestScore = combined;
                            bestIdx = idx;
                        }
                    }

                    // If no suitable alternate speaker found (all remaining same speaker), relax constraint
                    if (bestIdx == -1)
                    {
                        bestIdx = remaining.First();
                    }

                    sequence.Add(bestIdx);
                    remaining.Remove(bestIdx);
                }

                // 3. Write reordered segments back to file (overwrite)
                await File.WriteAllTextAsync(dialoguePath, string.Empty);
                int counterOut = 1;
                foreach (var idx in sequence)
                {
                    var seg = parsed[idx];
                    await AppendSegmentAsync(dialoguePath, seg, counterOut++);
                }

                return true;

                // local helper – compute reply score with caching
                async Task<float> GetReplyScoreAsync(int a, int b)
                {
                    if (replyCache.TryGetValue((a, b), out var cached)) return cached;

                    // Same speaker penalty handled earlier
                    var prompt = $"You are assisting in sequencing dialogue clips.\nSegment A: \"{parsed[a].Text}\"\nSegment B: \"{parsed[b].Text}\"\nOn a scale from 0 to 100, where 0 means Segment B does not answer or build on Segment A at all, and 100 means it is an excellent, natural reply, output ONLY the integer score.";

                    float score;
                    try
                    {
                        var response = await GenAIManager.Instance.GenerateTextAsync(prompt, false);
                        if (!float.TryParse(new string(response.Where(char.IsDigit).ToArray()), out score))
                            score = 50f; // fallback neutral
                    }
                    catch
                    {
                        score = 50f; // fallback
                    }

                    replyCache[(a, b)] = score;
                    return score;
                }

                // Local helper – score line e.g., "Relevance: 75"
                static float ParseScore(string line)
                {
                    var parts = line.Split(": ");
                    return parts.Length == 2 && float.TryParse(parts[1], out var v) ? v : 0f;
                }

                // Local helper – append segment in SRT format
                async Task AppendSegmentAsync(string filePath, Segment seg, int number)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(number.ToString());
                    sb.AppendLine($"{FormatTime(seg.Start)} --> {FormatTime(seg.End)}");
                    sb.AppendLine(seg.FileName);
                    sb.AppendLine($"Relevance: {seg.Scores.relevance}");
                    sb.AppendLine($"Sentiment: {seg.Scores.sentiment}");
                    sb.AppendLine($"Novelty: {seg.Scores.novelty}");
                    sb.AppendLine($"Energy: {seg.Scores.energy}");
                    sb.AppendLine(seg.Text);
                    sb.AppendLine();
                    await File.AppendAllTextAsync(filePath, sb.ToString());
                }

                static string FormatTime(TimeSpan ts) => ts.ToString(@"hh\:mm\:ss\,fff");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dialogue sequencing failed: {ex.Message}");
                return false;
            }
        }
    }
} 