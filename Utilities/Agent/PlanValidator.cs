using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FrameFlow.Utilities.Agent
{
    public sealed class PlanValidator
    {
        private static readonly string[] AllowedTools = new[]
        {
            "ProcessTakeLayer","ProcessSpeakerAnalysis","RankTranscripts","RankOrder",
            "NoveltyReRank","SequenceDialogue","TemporalExpansion","TrimToLength","RenderVideo"
        };

        // Canonical safe order
        private static readonly string[] CanonicalOrder = new[]
        {
            "ProcessTakeLayer",
            "ProcessSpeakerAnalysis",
            "RankTranscripts",
            "RankOrder",
            "NoveltyReRank",
            "SequenceDialogue",
            "TemporalExpansion",
            "TrimToLength",
            "RenderVideo"
        };

        private static readonly HashSet<string> RequiredTools = CanonicalOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public ValidatedPlan ValidateAndRepair(StepPlan incoming)
        {
            var messages = new List<string>();
            var errors = new List<string>();

            // Sanitize: filter unknown tools
            var steps = (incoming.Steps ?? new List<PlanStep>())
                .Where(s => AllowedTools.Contains(s.Tool, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // De-duplicate by tool: keep first occurrence to avoid repeats
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            steps = steps.Where(s => seen.Add(s.Tool)).ToList();

            // Add missing required tools with default ids
            foreach (var tool in CanonicalOrder)
            {
                if (!steps.Any(s => string.Equals(s.Tool, tool, StringComparison.OrdinalIgnoreCase)))
                {
                    steps.Add(new PlanStep(DefaultIdFor(tool), tool));
                    messages.Add($"Inserted missing step: {tool}");
                }
            }

            // Build precedence constraints from canonical order
            var indexByTool = CanonicalOrder
                .Select((t, i) => (t, i))
                .ToDictionary(x => x.t, x => x.i, StringComparer.OrdinalIgnoreCase);

            // Stable topological sort honoring canonical dependencies and preserving incoming relative order where possible
            var originalIndex = steps.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);

            var nodes = steps.ToList();
            var edges = new List<(PlanStep from, PlanStep to)>();

            // Add edge for every pair that violates canonical order
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j) continue;
                    var a = nodes[i];
                    var b = nodes[j];
                    if (indexByTool[a.Tool] < indexByTool[b.Tool])
                    {
                        edges.Add((a, b));
                    }
                }
            }

            var incomingDegree = nodes.ToDictionary(n => n, n => 0);
            foreach (var e in edges) incomingDegree[e.to]++;

            var queue = new List<PlanStep>(nodes.Where(n => incomingDegree[n] == 0)
                .OrderBy(n => indexByTool[n.Tool])
                .ThenBy(n => originalIndex[n]));

            var ordered = new List<PlanStep>();
            while (queue.Count > 0)
            {
                var n = queue[0];
                queue.RemoveAt(0);
                ordered.Add(n);
                foreach (var e in edges.Where(e => ReferenceEquals(e.from, n)).ToList())
                {
                    incomingDegree[e.to]--;
                    if (incomingDegree[e.to] == 0)
                    {
                        queue.Add(e.to);
                        queue = queue
                            .OrderBy(x => indexByTool[x.Tool])
                            .ThenBy(x => originalIndex[x])
                            .ToList();
                    }
                }
            }

            // Detect cycle (should not happen with our complete order), fallback to canonical order if needed
            if (ordered.Count != nodes.Count)
            {
                errors.Add("Plan validation failed due to ordering cycle. Falling back to canonical order.");
                ordered = CanonicalOrder.Select(t => nodes.First(s => string.Equals(s.Tool, t, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            // Hard constraint checks (examples):
            if (IndexOfTool(ordered, "RenderVideo") < IndexOfTool(ordered, "TrimToLength"))
            {
                messages.Add("Adjusted order: RenderVideo moved after TrimToLength");
                ordered = MoveAfter(ordered, "RenderVideo", "TrimToLength");
            }

            if (IndexOfTool(ordered, "TrimToLength") < IndexOfTool(ordered, "TemporalExpansion"))
            {
                messages.Add("Adjusted order: TrimToLength moved after TemporalExpansion");
                ordered = MoveAfter(ordered, "TrimToLength", "TemporalExpansion");
            }

            var repaired = messages.Count > 0 || errors.Count > 0;
            return new ValidatedPlan
            {
                Steps = ordered,
                Messages = messages,
                Errors = errors,
                WasRepaired = repaired
            };
        }

        private static int IndexOfTool(List<PlanStep> steps, string tool)
        {
            for (int i = 0; i < steps.Count; i++)
                if (string.Equals(steps[i].Tool, tool, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static List<PlanStep> MoveAfter(List<PlanStep> steps, string moveTool, string afterTool)
        {
            var list = steps.ToList();
            var iMove = IndexOfTool(list, moveTool);
            var iAfter = IndexOfTool(list, afterTool);
            if (iMove < 0 || iAfter < 0) return list;
            var item = list[iMove];
            list.RemoveAt(iMove);
            iAfter = IndexOfTool(list, afterTool);
            list.Insert(iAfter + 1, item);
            return list;
        }

        private static string DefaultIdFor(string tool)
        {
            return tool switch
            {
                "ProcessTakeLayer" => "takes",
                "ProcessSpeakerAnalysis" => "speakers",
                "RankTranscripts" => "rank",
                "RankOrder" => "order",
                "NoveltyReRank" => "novelty",
                "SequenceDialogue" => "dialogue",
                "TemporalExpansion" => "expand",
                "TrimToLength" => "trim",
                "RenderVideo" => "render",
                _ => tool.ToLowerInvariant()
            };
        }
    }

    public sealed class ValidatedPlan
    {
        public List<PlanStep> Steps { get; set; } = new();
        public List<string> Messages { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool WasRepaired { get; set; }
    }
}


