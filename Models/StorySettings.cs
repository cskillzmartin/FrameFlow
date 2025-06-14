using System.Text.Json.Serialization;

namespace FrameFlow.Models
{
    public class StorySettings
    {
        [JsonConstructor]
        public StorySettings()
        {
            Prompt = string.Empty;
            Relevance = 100f;
            Sentiment = 25f;
            Novelty = 25f;
            Energy = 25f;
            Length = 60;  // Default 1 minutes in seconds
            GenAISettings = new GenAISettings();
        }

        public string Prompt { get; set; }           // Main story prompt or creative direction
        public float Relevance { get; set; }         // How closely to follow the prompt (0.0 - 1.0)
        public float Sentiment { get; set; }         // Emotional tone (-1.0 negative to 1.0 positive)
        public float Novelty { get; set; }           // Preference for unique/unexpected content (0.0 - 1.0)
        public float Energy { get; set; }            // Pacing and intensity level (0.0 calm to 1.0 energetic)
        public int Length { get; set; }              // Target duration in seconds
        public GenAISettings GenAISettings { get; set; } // AI settings for generating the story
    }

    public class GenAISettings
    {
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        public float RepetitionPenalty { get; set; } = 1.1f;
        public long? RandomSeed { get; set; } = null;
    }
} 