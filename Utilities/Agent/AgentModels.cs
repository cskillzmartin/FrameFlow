using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FrameFlow.Utilities.Agent
{
    public enum AgentRunMode
    {
        Full,
        Resume,
        FromStepId
    }

    public sealed class AgentRequest
    {
        public required string ProjectName { get; init; }
        public required string ProjectPath { get; init; }
        public required string RenderDirectory { get; init; }
        public required Models.StorySettings StorySettings { get; init; }
        public int TargetMinutes { get; set; }
        public AgentRunMode RunMode { get; set; } = AgentRunMode.Full;
        public string? FromStepId { get; set; }

        // Derived convenience values
        public string OutputVideoPath => System.IO.Path.Combine(RenderDirectory, $"{ProjectName}.mp4");
    }

    public sealed class AgentResult
    {
        public bool Success { get; init; }
        public string? OutputPath { get; init; }
        public List<string> Errors { get; } = new();
    }

    public sealed class AgentProgress
    {
        public int StepIndex { get; init; }
        public int TotalSteps { get; init; }
        public required string UiText { get; init; }
        public required string LogMessage { get; init; }
    }

    public sealed class RunEvent
    {
        [JsonPropertyName("ts")] public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        [JsonPropertyName("level")] public string Level { get; init; } = "info";
        [JsonPropertyName("event")] public required string Event { get; init; }
        [JsonPropertyName("step")] public int? StepIndex { get; init; }
        [JsonPropertyName("message")] public string? Message { get; init; }
        [JsonPropertyName("data")] public Dictionary<string, object>? Data { get; init; }
    }
}


