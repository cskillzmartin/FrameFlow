using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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
    }
}


