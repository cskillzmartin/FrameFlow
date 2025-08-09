using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using FrameFlow.Utilities;

namespace FrameFlow.Utilities.Agent
{
    public sealed class Evaluator
    {
        public sealed class EvalOutcome
        {
            public bool Pass { get; init; }
            public string? Reason { get; init; }
            public Dictionary<string, object>? Metrics { get; init; }
        }

        public Task<EvalOutcome> EvaluateAsync(AgentRequest request)
        {
            try
            {
                var renderDir = request.RenderDirectory;
                var trimmedSrt = Path.Combine(renderDir, $"{request.ProjectName}.trim.srt");

                var exists = File.Exists(trimmedSrt);
                long size = exists ? new FileInfo(trimmedSrt).Length : 0L;

                var metrics = new Dictionary<string, object>
                {
                    ["trimmedSrtPath"] = trimmedSrt,
                    ["exists"] = exists,
                    ["sizeBytes"] = size
                };

                var pass = exists && size > 0;
                return Task.FromResult(new EvalOutcome { Pass = pass, Metrics = metrics, Reason = pass ? null : "Missing or empty trimmed SRT" });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new EvalOutcome { Pass = false, Reason = ex.Message });
            }
        }

        // Validate the expected output(s) for a specific tool step
        public Task<EvalOutcome> EvaluateStepAsync(string toolName, AgentRequest request)
        {
            try
            {
                var renderDir = request.RenderDirectory;
                var metrics = new Dictionary<string, object>();
                bool pass;
                string? reason = null;

                switch (toolName)
                {
                    case "ProcessTakeLayer":
                    {
                        var srts = Directory.Exists(renderDir)
                            ? Directory.GetFiles(renderDir, "*.srt")
                            : Array.Empty<string>();
                        pass = srts.Length > 0;
                        metrics["srtCount"] = srts.Length;
                        if (!pass) reason = "No SRT files produced by TakeManager";
                        break;
                    }
                    case "ProcessSpeakerAnalysis":
                    {
                        var metaPath = Path.Combine(renderDir, $"{request.ProjectName}.speaker.meta.json");
                        pass = File.Exists(metaPath) && new FileInfo(metaPath).Length > 0;
                        metrics["metaPath"] = metaPath;
                        if (!pass) reason = "Missing or empty speaker.meta.json";
                        break;
                    }
                    case "RankTranscripts":
                    {
                        var ranked = Path.Combine(renderDir, $"{request.ProjectName}.ranked.srt");
                        pass = File.Exists(ranked) && new FileInfo(ranked).Length > 0;
                        metrics["rankedPath"] = ranked;
                        if (!pass) reason = "Missing or empty ranked.srt";
                        break;
                    }
                    case "RankOrder":
                    {
                        var ordered = Path.Combine(renderDir, $"{request.ProjectName}.ordered.srt");
                        pass = File.Exists(ordered) && new FileInfo(ordered).Length > 0;
                        metrics["orderedPath"] = ordered;
                        if (!pass) reason = "Missing or empty ordered.srt";
                        break;
                    }
                    case "NoveltyReRank":
                    case "SequenceDialogue":
                    {
                        var novelty = Path.Combine(renderDir, $"{request.ProjectName}.novelty.srt");
                        pass = File.Exists(novelty) && new FileInfo(novelty).Length > 0;
                        metrics["noveltyPath"] = novelty;
                        if (!pass) reason = "Missing or empty novelty.srt";
                        break;
                    }
                    case "TemporalExpansion":
                    {
                        var expanded = Path.Combine(renderDir, $"{request.ProjectName}.expanded.srt");
                        pass = File.Exists(expanded) && new FileInfo(expanded).Length > 0;
                        metrics["expandedPath"] = expanded;
                        if (!pass) reason = "Missing or empty expanded.srt";
                        break;
                    }
                    case "TrimToLength":
                    {
                        var trimmed = Path.Combine(renderDir, $"{request.ProjectName}.trim.srt");
                        pass = File.Exists(trimmed) && new FileInfo(trimmed).Length > 0;
                        metrics["trimmedPath"] = trimmed;
                        if (!pass) reason = "Missing or empty trim.srt";
                        break;
                    }
                    case "RenderVideo":
                    {
                        var output = request.OutputVideoPath;
                        pass = !string.IsNullOrWhiteSpace(output) && File.Exists(output) && new FileInfo(output).Length > 0;
                        metrics["renderPath"] = output ?? string.Empty;
                        if (!pass) reason = "Missing or empty rendered video";
                        break;
                    }
                    default:
                        pass = true; // Unknown tool: do not block
                        break;
                }

                return Task.FromResult(new EvalOutcome
                {
                    Pass = pass,
                    Reason = pass ? null : reason,
                    Metrics = metrics
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new EvalOutcome { Pass = false, Reason = ex.Message });
            }
        }

        // Semantic alignment of final script with user prompt
        public async Task<EvalOutcome> EvaluatePromptAlignmentAsync(AgentRequest request)
        {
            try
            {
                var renderDir = request.RenderDirectory;
                var prompt = request.StorySettings?.Prompt ?? string.Empty;
                var trimmed = Path.Combine(renderDir, $"{request.ProjectName}.trim.srt");

                if (!File.Exists(trimmed))
                {
                    return new EvalOutcome { Pass = false, Reason = "Trimmed script not found" };
                }

                var lines = await File.ReadAllLinesAsync(trimmed);
                var textBuilder = new System.Text.StringBuilder();
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (int.TryParse(line, out _)) continue; // index line
                    if (line.Contains("-->", StringComparison.Ordinal)) continue; // timestamp
                    if (Regex.IsMatch(line, "^(Relevance|Sentiment|Novelty|Energy|Focus|Clarity|Emotion|FlubScore|CompositeScore):\\s", RegexOptions.IgnoreCase)) continue;
                    textBuilder.AppendLine(line);
                }
                var scriptText = textBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(scriptText))
                {
                    return new EvalOutcome { Pass = false, Reason = "Trimmed script is empty" };
                }

                // Keyword coverage heuristic
                var keywords = ExtractKeywords(prompt);
                var coverage = ComputeCoverage(scriptText, keywords);

                // Optional: LLM alignment score when model is available
                int? llmScore = null;
                if (GenAIManager.Instance.IsModelLoaded && !string.IsNullOrWhiteSpace(prompt))
                {
                    var prev = GenAIManager.Instance.SystemPrompt;
                    try
                    {
                        GenAIManager.Instance.SystemPrompt = "You are a strict evaluator. Reply with ONLY an integer 0-100 for alignment of the script to the goal. No words.";
                        var evalPrompt = $"Goal: {prompt}\n---\nScript:\n{scriptText}\n---\nScore:";
                        var raw = await GenAIManager.Instance.GenerateTextAsync(evalPrompt, saveHistory: false);
                        if (int.TryParse(ExtractDigits(raw), out var s) && s >= 0 && s <= 100)
                        {
                            llmScore = s;
                        }
                    }
                    catch { }
                    finally
                    {
                        GenAIManager.Instance.SystemPrompt = prev;
                    }
                }

                var pass = (llmScore.HasValue && llmScore.Value >= 70) || coverage >= 0.5;
                var metrics = new Dictionary<string, object>
                {
                    ["keywordCoverage"] = coverage,
                    ["keywords"] = keywords,
                    ["llmScore"] = llmScore ?? -1
                };
                return new EvalOutcome { Pass = pass, Reason = pass ? null : "Prompt alignment below threshold", Metrics = metrics };
            }
            catch (Exception ex)
            {
                return new EvalOutcome { Pass = false, Reason = ex.Message };
            }
        }

        private static List<string> ExtractKeywords(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return new List<string>();
            var tokens = Regex.Matches(prompt.ToLowerInvariant(), "[a-z0-9]{4,}")
                .Select(m => m.Value)
                .Where(w => !_stopwords.Contains(w))
                .Distinct()
                .ToList();
            return tokens;
        }

        private static double ComputeCoverage(string text, List<string> keywords)
        {
            if (keywords.Count == 0) return 1.0; // nothing to cover
            var lower = text.ToLowerInvariant();
            var hits = keywords.Count(k => lower.Contains(k));
            return hits / (double)keywords.Count;
        }

        private static string ExtractDigits(string input)
        {
            var m = Regex.Match(input ?? string.Empty, @"\d+");
            return m.Success ? m.Value : string.Empty;
        }

        private static readonly HashSet<string> _stopwords = new(StringComparer.OrdinalIgnoreCase)
        {
            "this","that","with","from","about","into","after","before","over","under","between","while","where","when","what","which","whose","your","yours","their","theirs","ours","our","have","has","had","will","would","should","could","can","just","like","also","only","very","more","most","some","such","other","than","then","them","they","there","here","make","made","using","use","for","and","the","was","were","are","is","you","how","why","who","whom","a","an","in","on","to","of","as","by","at","it","be","or"
        };
    }
}


