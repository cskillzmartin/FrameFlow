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

            // Local helpers
            async Task<bool> TryRepairAfterTrimAsync(int stepIdx, List<object> stepReportsLocal)
            {
                // Attempt 1: re-run trim
                memory?.Append(new RunEvent { Event = "replan_start", StepIndex = stepIdx, Message = "Retry trim" });
                var sw1 = Stopwatch.StartNew();
                try
                {
                    var trimTool = _tools.Get("TrimToLength");
                    await trimTool.ExecuteAsync(request);
                    sw1.Stop();
                    var d1 = sw1.ElapsedMilliseconds;
                    stepReportsLocal.Add(new { id = "trim_retry_1", tool = "TrimToLength", startedAtUtc = DateTime.UtcNow.AddMilliseconds(-d1), completedAtUtc = DateTime.UtcNow, durationMs = d1, success = true });

                    var eval1 = await _evaluator.EvaluateAsync(request);
                    if (eval1.Pass)
                    {
                        // add trimmed artifact
                        artifacts.Add(new { kind = "trim_srt", path = System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.trim.srt") });
                        memory?.Append(new RunEvent { Event = "replan_success", StepIndex = stepIdx, Message = "Trim retry succeeded" });
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    sw1.Stop();
                    stepReportsLocal.Add(new { id = "trim_retry_1", tool = "TrimToLength", startedAtUtc = DateTime.UtcNow.AddMilliseconds(-sw1.ElapsedMilliseconds), completedAtUtc = DateTime.UtcNow, durationMs = sw1.ElapsedMilliseconds, success = false, error = ex.Message });
                }

                // Attempt 2: increase temporal expansion then trim
                var originalExpansion = request.StorySettings.TemporalExpansion;
                request.StorySettings.TemporalExpansion = Math.Min(originalExpansion + 2, 30);
                memory?.Append(new RunEvent { Event = "replan_adjust", StepIndex = stepIdx, Message = $"Increase temporalExpansion {originalExpansion}->{request.StorySettings.TemporalExpansion}" });
                var sw2a = Stopwatch.StartNew();
                try
                {
                    var expandTool = _tools.Get("TemporalExpansion");
                    await expandTool.ExecuteAsync(request);
                    sw2a.Stop();
                    var d2a = sw2a.ElapsedMilliseconds;
                    stepReportsLocal.Add(new { id = "expand_retry_2", tool = "TemporalExpansion", startedAtUtc = DateTime.UtcNow.AddMilliseconds(-d2a), completedAtUtc = DateTime.UtcNow, durationMs = d2a, success = true });
                }
                catch (Exception ex)
                {
                    sw2a.Stop();
                    stepReportsLocal.Add(new { id = "expand_retry_2", tool = "TemporalExpansion", startedAtUtc = DateTime.UtcNow.AddMilliseconds(-sw2a.ElapsedMilliseconds), completedAtUtc = DateTime.UtcNow, durationMs = sw2a.ElapsedMilliseconds, success = false, error = ex.Message });
                }

                var sw2b = Stopwatch.StartNew();
                try
                {
                    var trimTool2 = _tools.Get("TrimToLength");
                    await trimTool2.ExecuteAsync(request);
                    sw2b.Stop();
                    var d2b = sw2b.ElapsedMilliseconds;
                    stepReportsLocal.Add(new { id = "trim_retry_2", tool = "TrimToLength", startedAtUtc = DateTime.UtcNow.AddMilliseconds(-d2b), completedAtUtc = DateTime.UtcNow, durationMs = d2b, success = true });
                    var eval2 = await _evaluator.EvaluateAsync(request);
                    if (eval2.Pass)
                    {
                        artifacts.Add(new { kind = "trim_srt", path = System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.trim.srt") });
                        memory?.Append(new RunEvent { Event = "replan_success", StepIndex = stepIdx, Message = "Temporal expansion + trim succeeded" });
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    sw2b.Stop();
                    stepReportsLocal.Add(new { id = "trim_retry_2", tool = "TrimToLength", startedAtUtc = DateTime.UtcNow.AddMilliseconds(-sw2b.ElapsedMilliseconds), completedAtUtc = DateTime.UtcNow, durationMs = sw2b.ElapsedMilliseconds, success = false, error = ex.Message });
                }
                finally
                {
                    // restore original expansion to avoid drift for future runs
                    request.StorySettings.TemporalExpansion = originalExpansion;
                }

                // Attempt 3: reduce target minutes and trim
                var originalMinutes = request.TargetMinutes;
                var reduced = Math.Max(1, (int)Math.Floor(originalMinutes * 0.9));
                if (reduced != originalMinutes)
                {
                    request.TargetMinutes = reduced;
                    memory?.Append(new RunEvent { Event = "replan_adjust", StepIndex = stepIdx, Message = $"Reduce targetMinutes {originalMinutes}->{reduced}" });
                    var sw3 = Stopwatch.StartNew();
                    try
                    {
                        var trimTool3 = _tools.Get("TrimToLength");
                        await trimTool3.ExecuteAsync(request);
                        sw3.Stop();
                        var d3 = sw3.ElapsedMilliseconds;
                        stepReportsLocal.Add(new { id = "trim_retry_3", tool = "TrimToLength", startedAtUtc = DateTime.UtcNow.AddMilliseconds(-d3), completedAtUtc = DateTime.UtcNow, durationMs = d3, success = true });
                        var eval3 = await _evaluator.EvaluateAsync(request);
                        if (eval3.Pass)
                        {
                            artifacts.Add(new { kind = "trim_srt", path = System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.trim.srt") });
                            memory?.Append(new RunEvent { Event = "replan_success", StepIndex = stepIdx, Message = "Reduced length + trim succeeded" });
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        sw3.Stop();
                        stepReportsLocal.Add(new { id = "trim_retry_3", tool = "TrimToLength", startedAtUtc = DateTime.UtcNow.AddMilliseconds(-sw3.ElapsedMilliseconds), completedAtUtc = DateTime.UtcNow, durationMs = sw3.ElapsedMilliseconds, success = false, error = ex.Message });
                    }
                    finally
                    {
                        request.TargetMinutes = originalMinutes;
                    }
                }

                memory?.Append(new RunEvent { Event = "replan_fail", StepIndex = stepIdx, Message = "All repair attempts failed" });
                return false;
            }

            // Determine starting index based on run mode
            int startIndex = 0;
            if (request.RunMode == AgentRunMode.FromStepId && !string.IsNullOrWhiteSpace(request.FromStepId))
            {
                var idxFound = steps.FindIndex(s => string.Equals(s.id, request.FromStepId, StringComparison.OrdinalIgnoreCase));
                if (idxFound >= 0) startIndex = idxFound;
            }

            for (var i = startIndex; i < steps.Count; i++)
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

                    // Record trimmed artifact on initial success
                    if (string.Equals(id, "trim", StringComparison.OrdinalIgnoreCase))
                    {
                        artifacts.Add(new { kind = "trim_srt", path = System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.trim.srt") });
                    }

                    // Minimal evaluation gate: after trim, ensure prerequisites for render
                    if (string.Equals(id, "trim", StringComparison.OrdinalIgnoreCase))
                    {
                        var eval = await _evaluator.EvaluateAsync(request);
                        if (!eval.Pass)
                        {
                            var reason = eval.Reason ?? "evaluation failed";
                            var emsg = $"Evaluation did not pass before render: {reason}";
                            memory?.Append(new RunEvent { Event = "evaluation_fail", StepIndex = stepIdx, Message = emsg });
                            // Attempt bounded self-repair
                            var repaired = await TryRepairAfterTrimAsync(stepIdx, stepReports);
                            if (!repaired)
                            {
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


