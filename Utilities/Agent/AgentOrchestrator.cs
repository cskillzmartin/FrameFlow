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

        // Tracks artifacts with de-duplication by absolute path
        private sealed class ArtifactTracker
        {
            private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
            private readonly List<object> _items = new();

            public void Add(string kind, string path)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                if (_paths.Add(path))
                {
                    _items.Add(new { kind, path });
                }
            }

            public IReadOnlyList<object> Items => _items;
        }

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
            // Validate/repair plan ordering and constraints
            var validator = new PlanValidator();
            var validated = validator.ValidateAndRepair(plan);
            var steps = new List<(string id, string tool, string ui, string log)>();
            int idx = 1;
            foreach (var s in validated.Steps)
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
            validated = validator.ValidateAndRepair(plan);
            var planJson = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
            var planValidatedJson = JsonSerializer.Serialize(validated, new JsonSerializerOptions { WriteIndented = true });
            memory?.SaveText("plan.json", planJson);
            memory?.SaveText("plan_validated.json", planValidatedJson);
            if (validated.WasRepaired && validated.Messages.Count > 0)
            {
                memory?.Append(new RunEvent { Event = "plan_repaired", StepIndex = null, Message = string.Join("; ", validated.Messages) });
            }
            memory?.Append(new RunEvent { Event = "plan_ready", StepIndex = null, Message = "Plan prepared" });

            var stepReports = new List<object>();
            var artifactsTracker = new ArtifactTracker();
            var runStopwatch = Stopwatch.StartNew();

            // Pre-known artifacts
            artifactsTracker.Add("story_settings", System.IO.Path.Combine(request.RenderDirectory, "story_settings.json") );
            artifactsTracker.Add("plan", System.IO.Path.Combine(request.RenderDirectory, "plan.json") );
            artifactsTracker.Add("plan_validated", System.IO.Path.Combine(request.RenderDirectory, "plan_validated.json") );

            // Local helpers moved to class methods

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

                    // After each step, validate step-specific outputs
                    var stepEval = await _evaluator.EvaluateStepAsync(toolName, request);
                    if (!stepEval.Pass)
                    {
                        var emsg = $"Output validation failed after {toolName}: {stepEval.Reason}";
                        memory?.Append(new RunEvent { Event = "step_output_fail", StepIndex = stepIdx, Message = emsg });

                        // Special handling for TrimToLength using existing bounded repair flow
                        if (string.Equals(id, "trim", StringComparison.OrdinalIgnoreCase))
                        {
                            var repaired = await TryRepairAfterTrimAsync(request, memory, artifactsTracker, stepReports, stepIdx);
                            if (!repaired)
                            {
                                result.Errors.Add(emsg);
                                // Write report before returning
                                runStopwatch.Stop();
                                WriteFailReport(request, stepReports, artifactsTracker.Items, runStopwatch.ElapsedMilliseconds, memory);
                                return result;
                            }
                        }
                        else
                        {
                            // For other steps, fail fast
                            result.Errors.Add(emsg);
                            runStopwatch.Stop();
                            WriteFailReport(request, stepReports, artifactsTracker.Items, runStopwatch.ElapsedMilliseconds, memory);
                            return result;
                        }
                    }

                    // Record artifacts for this step
                    RecordArtifactsForTool(toolName, request, artifactsTracker);

                    // Minimal evaluation gate remains for trim (redundant, but keeps prior behavior)
                    if (string.Equals(id, "trim", StringComparison.OrdinalIgnoreCase))
                    {
                        var eval = await _evaluator.EvaluateAsync(request);
                        if (!eval.Pass)
                        {
                            var reason = eval.Reason ?? "evaluation failed";
                            var emsg = $"Evaluation did not pass before render: {reason}";
                            memory?.Append(new RunEvent { Event = "evaluation_fail", StepIndex = stepIdx, Message = emsg });
                            var repaired = await TryRepairAfterTrimAsync(request, memory, artifactsTracker, stepReports, stepIdx);
                            if (!repaired)
                            {
                                result.Errors.Add(emsg);
                                runStopwatch.Stop();
                                WriteFailReport(request, stepReports, artifactsTracker.Items, runStopwatch.ElapsedMilliseconds, memory);
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
                    WriteFailReport(request, stepReports, artifactsTracker.Items, runStopwatch.ElapsedMilliseconds, memory);
                    return result;
                }
            }

            runStopwatch.Stop();
            memory?.Append(new RunEvent { Event = "run_complete", StepIndex = total, Message = request.OutputVideoPath });

            // Add final render artifact
            artifactsTracker.Add("render_output", request.OutputVideoPath);

            WriteSuccessReport(request, stepReports, artifactsTracker.Items, runStopwatch.ElapsedMilliseconds, memory);
            return new AgentResult { Success = true, OutputPath = request.OutputVideoPath };
        }

        private string UiForIndex(int idx) => idx switch
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

        private string LogForIndex(int idx) => idx switch
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

        private void RecordArtifactsForTool(string toolName, AgentRequest request, ArtifactTracker artifacts)
        {
            switch (toolName)
            {
                case "ProcessTakeLayer":
                {
                    var srts = System.IO.Directory.Exists(request.RenderDirectory)
                        ? System.IO.Directory.GetFiles(request.RenderDirectory, "*.srt")
                        : Array.Empty<string>();
                    foreach (var srt in srts) artifacts.Add("take_srt", srt);
                    break;
                }
                case "ProcessSpeakerAnalysis":
                {
                    var metaPath = System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.speaker.meta.json");
                    artifacts.Add("speaker_meta", metaPath);
                    var embPath = System.IO.Path.ChangeExtension(metaPath, ".embeddings.bin");
                    if (System.IO.File.Exists(embPath)) artifacts.Add("speaker_embeddings", embPath);
                    break;
                }
                case "RankTranscripts":
                {
                    artifacts.Add("ranked_srt", System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.ranked.srt"));
                    break;
                }
                case "RankOrder":
                {
                    artifacts.Add("ordered_srt", System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.ordered.srt"));
                    break;
                }
                case "NoveltyReRank":
                case "SequenceDialogue":
                {
                    artifacts.Add("novelty_srt", System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.novelty.srt"));
                    break;
                }
                case "TemporalExpansion":
                {
                    artifacts.Add("expanded_srt", System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.expanded.srt"));
                    break;
                }
                case "TrimToLength":
                {
                    artifacts.Add("trim_srt", System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.trim.srt"));
                    break;
                }
                case "RenderVideo":
                {
                    artifacts.Add("render_output", request.OutputVideoPath);
                    break;
                }
            }
        }

        private void WriteFailReport(AgentRequest request, List<object> stepReports, IReadOnlyList<object> artifacts, long totalDurationMs, AgentMemory? memory)
        {
            var reportFail = new
            {
                version = 1,
                project = request.ProjectName,
                renderDirectory = request.RenderDirectory,
                outputPath = request.OutputVideoPath,
                success = false,
                totalDurationMs,
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
        }

        private void WriteSuccessReport(AgentRequest request, List<object> stepReports, IReadOnlyList<object> artifacts, long totalDurationMs, AgentMemory? memory)
        {
            var report = new
            {
                version = 1,
                project = request.ProjectName,
                renderDirectory = request.RenderDirectory,
                outputPath = request.OutputVideoPath,
                success = true,
                totalDurationMs,
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
        }

        private async Task<bool> TryRepairAfterTrimAsync(AgentRequest request, AgentMemory? memory, ArtifactTracker artifacts, List<object> stepReportsLocal, int stepIdx)
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
                    artifacts.Add("trim_srt", System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.trim.srt"));
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
                    artifacts.Add("trim_srt", System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.trim.srt"));
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
                        artifacts.Add("trim_srt", System.IO.Path.Combine(request.RenderDirectory, $"{request.ProjectName}.trim.srt"));
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
    }
}


