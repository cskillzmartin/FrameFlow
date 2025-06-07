using System;

namespace FrameFlow.Utilities
{
    /// <summary>
    /// Centralizes all prompts used in the application for consistency and easy maintenance
    /// </summary>
    public static class Prompts
    {
        // System prompts
        public static class System
        {
            public const string DefaultAssistant = "You are a helpful and friendly AI assistant.";
            public const string ScoreOnly = "You are a scoring assistant. You MUST respond with ONLY a number between 0 and 100. No other text, no explanations, just the number.";
            public const string RelevanceScoring = "You are a scoring assistant. You MUST respond with ONLY a number between 0 and 100. No other text, no explanations, just the number.";
        }

        // Relevance scoring prompts
        public static class Relevance
        {
            public static string ScoreSegment(string subject, string text) =>
                $"Score from 0 to 100 how relevant this text is to the subject. Subject: '{subject}'. Text: '{text}'";
        }

        // Story ordering prompts
        public static class Story
        {
            public static string EvaluateStart(string subject, string text) =>
                $"Rate 0-100 how good this is as story start about '{subject}': '{text}'";

            public static string EvaluateNext(string subject, string currentText, string nextText) =>
                $"Rate 0-100 how well this follows in story about '{subject}'. Current: '{currentText}'. Next: '{nextText}'";

            public static string EvaluateContinuity(string currentText, string nextText) =>
                $"Rate 0-100 how much this second segment completes or continues the thought from the first segment. First: '{currentText}'. Second: '{nextText}'";
        }

        // Transcription prompts
        public static class Transcription
        {
            public static string ExtractingAudio = "Extracting audio...";
            public static string AudioExtractionComplete = "Audio extraction completed";
            public static string StartingTranscription = "Starting transcription...";
            public static string TranscriptionComplete(int segments, TimeSpan duration) =>
                $"Transcription completed! {segments} segments, {duration:mm\\:ss} total";
            public static string ProcessingChunk(int current, int total) =>
                $"Processing chunk {current} of {total}...";
        }

        // Analysis prompts
        public static class Analysis
        {
            public static string StartingAnalysis = "Starting analysis...";
            public static string AnalyzingSegments = "Analyzing segments for story flow...";
            public static string ProcessingChunk(int current, int total) =>
                $"Processing chunk {current} of {total}...";
            public static string FinalCoherence = "Performing final coherence check...";
            public static string AnalysisComplete = "Analysis complete!";
        }
    }
} 