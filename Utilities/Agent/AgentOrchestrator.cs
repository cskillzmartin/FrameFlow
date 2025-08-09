 using System;
 using System.Collections.Generic;
 using System.Diagnostics;
 using System.Globalization;
 using System.Text.Json;
 using System.Threading.Tasks;

namespace FrameFlow.Utilities.Agent
{
    public sealed class AgentOrchestrator
    {
        private readonly ToolRegistry _tools;
        private readonly Planner _planner = new Planner();
        private readonly Evaluator _evaluator = new Evaluator();

        public AgentOrchestrator(ToolRegistry tools)
        {
            _tools = tools;
        }

        public async Task<AgentResult> StartAsync(
            AgentRequest request,
            Action<AgentProgress>? onProgress = null,
            AgentMemory? memory = null)
        {
            var result = new AgentResult();
            // Phase 2: ask Planner for steps; fall back handled inside Planner
            var plan = await _planner.ProposePlanAsync(request);
            var steps = new List<(string id, string tool, string ui, string log)>();
            int idx = 1;
            foreach (var s in plan.Steps)
            {
                string ui = idx switch
                {
                    1 => "Analyzing (1/9)",
                    2 => "Speaker Analysis (2/9)",
                    3 => "Analyzing (3/9)",
                    4 => "Ranking (4/9)",
                    5 => "Reranking (5/9)",
                    6 => "Dialogue (6/9)",
                    7 => "Expanding (7/9)",
                    8 => "Trimming (8/9)",
                    _ => "Rendering (9/9)"
                };
                string log = idx switch
                {
                    1 => "Step 1/9: Analyzing takes...",
                    2 => "Step 2/9: Analyzing speakers and shots...",
                    3 => "Step 3/9: Analyzing transcripts...",
                    4 => "Step 4/9: Ranking segments...",
                    5 => "Step 5/9: Novelty rerank...",
                    6 => "Step 6/9: Sequencing dialogue...",
                    7 => "Step 7/9: Temporal expansion...",
                    8 => "Step 8/9: Trimming to length...",
                    _ => "Step 9/9: Rendering final video..."
                };
                steps.Add((s.Id, s.Tool, ui, log));
                idx++;
            }

            var total = steps.Count;
            memory?.Append(new RunEvent { Event = "run_start", StepIndex = null, Message = "Starting agent run" });

            // Phase 2: planning
            memory?.Append(new RunEvent { Event = "plan_start", StepIndex = null, Message = "Planning steps" });
            plan = await _planner.ProposePlanAsync(request);
            var planJson = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
            memory?.SaveText("plan.json", planJson);
            memory?.Append(new RunEvent { Event = "plan_ready", StepIndex = null, Message = "Plan prepared" });

            var stepReports = new List<object>();
            var artifacts = new List<object>();
            var runStopwatch = Stopwatch.StartNew();

            // Pre-known artifacts
            artifacts.Add(new { kind = "story_settings", path = System.IO.Path.Combine(request.RenderDirectory, "story_settings.json") });
            artifacts.Add(new { kind = "plan", path = System.IO.Path.Combine(request.RenderDirectory, "plan.json") });

            for (var i = 0; i < steps.Count; i++)
            {
                var (id, toolName, ui, log) = steps[i];
                var stepIdx = i + 1;
                var stepStart = DateTime.UtcNow;
                var sw = Stopwatch.StartNew();

                onProgress?.Invoke(new AgentProgress
                {
                    StepIndex = stepIdx,
                    TotalSteps = total,
                    UiText = ui,
                    LogMessage = log
                });

                memory?.Append(new RunEvent { Event = "step_start", StepIndex = stepIdx, Message = log });

                try
                {
                    var tool = _tools.Get(toolName);
                    await tool.ExecuteAsync(request);
                    sw.Stop();
                    var durationMs = sw.ElapsedMilliseconds;
                    memory?.Append(new RunEvent { Event = "step_complete", StepIndex = stepIdx, Message = id, Data = new Dictionary<string, object> { ["durationMs"] = durationMs } });

                    stepReports.Add(new
                    {
                        id,
                        tool = toolName,
                        startedAtUtc = stepStart,
                        completedAtUtc = DateTime.UtcNow,
                        durationMs,
                        success = true
                    });

                    // Notify UI of completion with timing
                    var secs = (durationMs / 1000.0).ToString("N1", CultureInfo.InvariantCulture);
                    onProgress?.Invoke(new AgentProgress
                    {
                        StepIndex = stepIdx,
                        TotalSteps = total,
                        UiText = ui + " ✓",
                        LogMessage = $"✓ {log} ({secs}s)"
                    });

                    // Minimal evaluation gate: after trim, ensure prerequisites for render
                    if (string.Equals(id, "trim", StringComparison.OrdinalIgnoreCase))
                    {
                        var eval = await _evaluator.EvaluateAsync(request);
                        if (!eval.Pass)
                        {
                            var reason = eval.Reason ?? "evaluation failed";
                            var emsg = $"Evaluation did not pass before render: {reason}";
                            memory?.Append(new RunEvent { Event = "evaluation_fail", StepIndex = stepIdx, Message = emsg });
                            result.Errors.Add(emsg);
                            // Write report before returning
                            runStopwatch.Stop();
                            var reportFail = new
                            {
                                version = 1,
                                project = request.ProjectName,
                                renderDirectory = request.RenderDirectory,
                                outputPath = request.OutputVideoPath,
                                success = false,
                                totalDurationMs = runStopwatch.ElapsedMilliseconds,
                                objectives = new
                                {
                                    targetMinutes = request.TargetMinutes,
                                    relevance = request.StorySettings.Relevance,
                                    sentiment = request.StorySettings.Sentiment,
                                    novelty = request.StorySettings.Novelty,
                                    energy = request.StorySettings.Energy,
                                    temporalExpansion = request.StorySettings.TemporalExpansion,
                                    genai = new
                                    {
                                        temperature = request.StorySettings.GenAISettings.Temperature,
                                        topP = request.StorySettings.GenAISettings.TopP,
                                        repetitionPenalty = request.StorySettings.GenAISettings.RepetitionPenalty,
                                        seed = request.StorySettings.GenAISettings.RandomSeed
                                    }
                                },
                                steps = stepReports,
                                artifacts
                            };
                            var jsonFail = JsonSerializer.Serialize(reportFail, new JsonSerializerOptions { WriteIndented = true });
                            memory?.SaveText("run_report.json", jsonFail);
                            var artifactsJsonFail = JsonSerializer.Serialize(artifacts, new JsonSerializerOptions { WriteIndented = true });
                            memory?.SaveText("artifacts.json", artifactsJsonFail);
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    var durationMs = sw.ElapsedMilliseconds;
                    var msg = $"Step '{id}' failed: {ex.Message}";
                    memory?.Append(new RunEvent { Event = "step_error", StepIndex = stepIdx, Message = msg, Data = new Dictionary<string, object> { ["durationMs"] = durationMs } });
                    result.Errors.Add(msg);

                    stepReports.Add(new
                    {
                        id,
                        tool = toolName,
                        startedAtUtc = stepStart,
                        completedAtUtc = DateTime.UtcNow,
                        durationMs,
                        success = false,
                        error = ex.Message
                    });

                    var secs = (durationMs / 1000.0).ToString("N1", CultureInfo.InvariantCulture);
                    onProgress?.Invoke(new AgentProgress
                    {
                        StepIndex = stepIdx,
                        TotalSteps = total,
                        UiText = ui + " ✗",
                        LogMessage = $"✗ {log} failed ({secs}s): {ex.Message}"
                    });

                    // Write report before returning
                    runStopwatch.Stop();
                    var reportFail = new
                    {
                        version = 1,
                        project = request.ProjectName,
                        renderDirectory = request.RenderDirectory,
                        outputPath = request.OutputVideoPath,
                        success = false,
                        totalDurationMs = runStopwatch.ElapsedMilliseconds,
                        objectives = new
                        {
                            targetMinutes = request.TargetMinutes,
                            relevance = request.StorySettings.Relevance,
                            sentiment = request.StorySettings.Sentiment,
                            novelty = request.StorySettings.Novelty,
                            energy = request.StorySettings.Energy,
                            temporalExpansion = request.StorySettings.TemporalExpansion,
                            genai = new
                            {
                                temperature = request.StorySettings.GenAISettings.Temperature,
                                topP = request.StorySettings.GenAISettings.TopP,
                                repetitionPenalty = request.StorySettings.GenAISettings.RepetitionPenalty,
                                seed = request.StorySettings.GenAISettings.RandomSeed
                            }
                        },
                        steps = stepReports,
                        artifacts
                    };
                    var jsonFail = JsonSerializer.Serialize(reportFail, new JsonSerializerOptions { WriteIndented = true });
                    memory?.SaveText("run_report.json", jsonFail);
                    // also persist artifacts manifest
                    var artifactsJsonFail = JsonSerializer.Serialize(artifacts, new JsonSerializerOptions { WriteIndented = true });
                    memory?.SaveText("artifacts.json", artifactsJsonFail);
                    return result;
                }
            }

            runStopwatch.Stop();
            memory?.Append(new RunEvent { Event = "run_complete", StepIndex = total, Message = request.OutputVideoPath });

            // Add final render artifact
            artifacts.Add(new { kind = "render_output", path = request.OutputVideoPath });

            var report = new
            {
                version = 1,
                project = request.ProjectName,
                renderDirectory = request.RenderDirectory,
                outputPath = request.OutputVideoPath,
                success = true,
                totalDurationMs = runStopwatch.ElapsedMilliseconds,
                objectives = new
                {
                    targetMinutes = request.TargetMinutes,
                    relevance = request.StorySettings.Relevance,
                    sentiment = request.StorySettings.Sentiment,
                    novelty = request.StorySettings.Novelty,
                    energy = request.StorySettings.Energy,
                    temporalExpansion = request.StorySettings.TemporalExpansion,
                    genai = new
                    {
                        temperature = request.StorySettings.GenAISettings.Temperature,
                        topP = request.StorySettings.GenAISettings.TopP,
                        repetitionPenalty = request.StorySettings.GenAISettings.RepetitionPenalty,
                        seed = request.StorySettings.GenAISettings.RandomSeed
                    }
                },
                steps = stepReports,
                artifacts
            };
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            memory?.SaveText("run_report.json", json);
            var artifactsJson = JsonSerializer.Serialize(artifacts, new JsonSerializerOptions { WriteIndented = true });
            memory?.SaveText("artifacts.json", artifactsJson);
            return new AgentResult { Success = true, OutputPath = request.OutputVideoPath };
        }
    }
}


