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
    }
}


