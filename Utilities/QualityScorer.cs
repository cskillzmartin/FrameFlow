using FrameFlow.Models;
using System.Text.RegularExpressions;

namespace FrameFlow.Utilities
{
    /// <summary>
    /// Comprehensive quality scoring for video segments using multiple dimensions
    /// </summary>
    public class QualityScorer
    {
        private readonly GenAIManager _aiManager;

        public QualityScorer()
        {
            _aiManager = GenAIManager.Instance;
        }

        /// <summary>
        /// Complete quality vector for a segment
        /// </summary>
        public struct QualityVector
        {
            public float Relevance { get; set; }      // 0-100: How well it matches the prompt
            public float Sentiment { get; set; }      // 0-100: Normalized from -100 to +100 range
            public float Novelty { get; set; }        // 0-100: Uniqueness/surprise factor
            public float Energy { get; set; }         // 0-100: Speaker energy/intensity
            public float Focus { get; set; }          // 0-100: Visual sharpness (placeholder)
            public float Clarity { get; set; }        // 0-100: Audio clarity (placeholder)
            public float Emotion { get; set; }        // 0-100: Emotional expressiveness (placeholder)
            public float FlubScore { get; set; }      // 0-100: Filler word ratio (inverted)
            public float CompositeScore { get; set; } // Weighted combination
        }

        /// <summary>
        /// Score a segment using all available quality dimensions
        /// </summary>
        public async Task<QualityVector> ScoreSegmentAsync(string segmentText, string prompt, TakeLayerSettings settings)
        {
            var vector = new QualityVector();

            // 1. Get LLM vector scores (relevance, sentiment, novelty, energy)
            var llmScores = await GetLLMVectorScoresAsync(segmentText, prompt, settings);
            vector.Relevance = llmScores.relevance;
            vector.Sentiment = llmScores.sentiment;
            vector.Novelty = llmScores.novelty;
            vector.Energy = llmScores.energy;

            // 2. Calculate flub score (existing logic)
            vector.FlubScore = CalculateFlubScore(segmentText);

            // 3. Placeholder scores for future computer vision/audio analysis
            vector.Focus = 75f;    // TODO: Implement CV sharpness detection
            vector.Clarity = 80f;  // TODO: Implement audio SNR analysis
            vector.Emotion = 70f;  // TODO: Implement emotion detection

            // 4. Calculate composite score using weights
            vector.CompositeScore = CalculateCompositeScore(vector, settings);

            return vector;
        }

        /// <summary>
        /// Get LLM-based vector scores for a segment
        /// </summary>
        private async Task<(float relevance, float sentiment, float novelty, float energy)> GetLLMVectorScoresAsync(
            string segmentText, string prompt, TakeLayerSettings settings)
        {
            try
            {
                // Set up system prompt for vector scoring
                var originalPrompt = _aiManager.SystemPrompt;
                _aiManager.SystemPrompt = @"You are an expert at analyzing video transcript segments across multiple dimensions. 
For each segment, analyze and return 4 scores with these specific ranges:
1. Relevance: 0-100 (0=irrelevant, 100=highly relevant)
2. Sentiment: -100 to +100 (-100=very negative, 0=neutral, +100=very positive)
3. Novelty: 0-10 (0=completely common, 10=extremely unique/surprising)
4. Energy: 1-5 (1=very low energy, 5=very high energy)

Format: '##,##,#.#,#.#' 
You MUST respond with ONLY the numbers in the specified format. NO other text, NO explanations, just the comma-separated numbers.
Example: '80,-34,4.3,3.2'";

                var rankingPrompt = $"""
                    Analyze this transcript segment across multiple dimensions.

                    Prompt: {prompt}

                    Transcript segment:
                    {segmentText}
                    """;

                var response = await _aiManager.GenerateTextAsync(rankingPrompt, saveHistory: false);
                
                // Restore original system prompt
                _aiManager.SystemPrompt = originalPrompt;
                
                // Parse the comma-separated response
                var scores = response.Split(',', StringSplitOptions.TrimEntries);
                if (scores.Length == 4 && 
                    float.TryParse(scores[0], out float rawRelevance) &&
                    float.TryParse(scores[1], out float rawSentiment) &&
                    float.TryParse(scores[2], out float rawNovelty) &&
                    float.TryParse(scores[3], out float rawEnergy))
                {
                    // Normalize each score to 0-100 range
                    float relevance = Math.Clamp(rawRelevance, 0f, 100f);
                    float sentiment = Math.Clamp((rawSentiment + 100f) / 2f, 0f, 100f); // Convert -100,+100 to 0-100
                    float novelty = Math.Clamp(rawNovelty * 10f, 0f, 100f); // Convert 0-10 to 0-100
                    float energy = Math.Clamp((rawEnergy - 1f) * 25f, 0f, 100f); // Convert 1-5 to 0-100

                    return (relevance, sentiment, novelty, energy);
                }

                // If parsing fails, return default scores
                System.Diagnostics.Debug.WriteLine($"Failed to parse vector scores from response: {response}");
                return (50f, 50f, 50f, 50f);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get LLM vector scores: {ex.Message}");
                return (50f, 50f, 50f, 50f);
            }
        }

        /// <summary>
        /// Calculate flub score (ratio of filler words to total words, inverted to 0-100 scale)
        /// </summary>
        private float CalculateFlubScore(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 50f;

            // Common filler words
            var fillerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "um", "uh", "ah", "eh", "oh", "like", "you know", "actually", "basically", "literally",
                "sort of", "kind of", "i mean", "well", "so", "right", "okay", "alright", "yeah"
            };

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!words.Any()) return 50f;

            int fillerWordCount = words.Count(word => 
                fillerWords.Contains(word.Trim().ToLowerInvariant()));

            float flubRatio = (float)fillerWordCount / words.Length;
            
            // Invert so higher score = better (less flubs)
            return Math.Clamp((1f - flubRatio) * 100f, 0f, 100f);
        }

        /// <summary>
        /// Calculate weighted composite score from all quality dimensions
        /// </summary>
        private float CalculateCompositeScore(QualityVector vector, TakeLayerSettings settings)
        {
            // Get total weight to normalize
            float totalWeight = settings.RelevanceWeight + settings.FlubWeight + 
                               settings.FocusWeight + settings.EnergyWeight;
            
            if (totalWeight == 0) return 50f;

            // Calculate weighted score
            float compositeScore = (
                vector.Relevance * settings.RelevanceWeight +
                vector.FlubScore * settings.FlubWeight +
                vector.Focus * settings.FocusWeight +
                vector.Energy * settings.EnergyWeight
            ) / totalWeight;

            return Math.Clamp(compositeScore, 0f, 100f);
        }

        /// <summary>
        /// Synchronous version for compatibility with existing code
        /// </summary>
        public QualityVector ScoreSegment(string segmentText, string prompt, TakeLayerSettings settings)
        {
            return ScoreSegmentAsync(segmentText, prompt, settings).GetAwaiter().GetResult();
        }
    }
} 