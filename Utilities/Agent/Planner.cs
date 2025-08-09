using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FrameFlow.Utilities.Agent
{
    public sealed class Planner
    {
        private static readonly string[] AllowedTools = new[]
        {
            "ProcessTakeLayer","ProcessSpeakerAnalysis","RankTranscripts","RankOrder",
            "NoveltyReRank","SequenceDialogue","TemporalExpansion","TrimToLength","RenderVideo"
        };

        public async Task<StepPlan> ProposePlanAsync(AgentRequest request)
        {
            // If GenAI is not loaded, return default plan
            if (!GenAIManager.Instance.IsModelLoaded)
            {
                return DefaultPlan();
            }

            try
            {
                var sys = "You are a planning agent. Output pure JSON only. Schema: {\"steps\":[{\"id\":string,\"tool\":string,\"inputs\":object}]}";
                GenAIManager.Instance.SystemPrompt = sys;
                var prompt = $"Create a plan to generate a {request.TargetMinutes} minute video. Use tools from this whitelist: {string.Join(',', AllowedTools)}. Include the 9 canonical steps in order. Keep inputs minimal; reference UI-provided values implicitly.";

                var raw = await GenAIManager.Instance.GenerateTextAsync(prompt, saveHistory: false);
                var json = ExtractFirstJson(raw);
                var plan = JsonSerializer.Deserialize<StepPlan>(json);
                if (plan == null || plan.Steps == null || plan.Steps.Count == 0)
                    return DefaultPlan();

                // sanitize tool names
                foreach (var s in plan.Steps)
                {
                    if (Array.IndexOf(AllowedTools, s.Tool) < 0)
                        s.Tool = MapClosestTool(s.Tool);
                }

                return plan;
            }
            catch
            {
                return DefaultPlan();
            }
        }

        private static string ExtractFirstJson(string text)
        {
            // naive extraction: find first '{' and last '}'
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                return text.Substring(start, end - start + 1);
            return "{\"steps\":[]}";
        }

        private static string MapClosestTool(string tool)
        {
            // very simple mapping by contains
            var t = tool.ToLowerInvariant();
            if (t.Contains("take")) return "ProcessTakeLayer";
            if (t.Contains("speak")) return "ProcessSpeakerAnalysis";
            if (t.Contains("rank") && t.Contains("trans")) return "RankTranscripts";
            if (t.Contains("order") || (t.Contains("rank") && t.Contains("order"))) return "RankOrder";
            if (t.Contains("novel")) return "NoveltyReRank";
            if (t.Contains("dialog")) return "SequenceDialogue";
            if (t.Contains("expand") || t.Contains("temporal")) return "TemporalExpansion";
            if (t.Contains("trim")) return "TrimToLength";
            if (t.Contains("render")) return "RenderVideo";
            return "RenderVideo";
        }

        private static StepPlan DefaultPlan()
        {
            return new StepPlan
            {
                Steps = new List<PlanStep>
                {
                    new("takes","ProcessTakeLayer"),
                    new("speakers","ProcessSpeakerAnalysis"),
                    new("rank","RankTranscripts"),
                    new("order","RankOrder"),
                    new("novelty","NoveltyReRank"),
                    new("dialogue","SequenceDialogue"),
                    new("expand","TemporalExpansion"),
                    new("trim","TrimToLength"),
                    new("render","RenderVideo"),
                }
            };
        }
    }

    public sealed class StepPlan
    {
        public List<PlanStep> Steps { get; set; } = new();
    }

    public sealed class PlanStep
    {
        public PlanStep() { }
        public PlanStep(string id, string tool)
        {
            Id = id; Tool = tool;
        }
        public string Id { get; set; } = string.Empty;
        public string Tool { get; set; } = string.Empty;
        public Dictionary<string, object>? Inputs { get; set; }
    }
}


