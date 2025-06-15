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
            TemporalExpansion = 2;  // Default 10
            GenAISettings = new GenAISettings();
            TakeLayerSettings = new TakeLayerSettings();
        }

        public string Prompt { get; set; }           // Main story prompt or creative direction
        public float Relevance { get; set; }         // How closely to follow the prompt (0.0 - 1.0)
        public float Sentiment { get; set; }         // Emotional tone (-1.0 negative to 1.0 positive)
        public float Novelty { get; set; }           // Preference for unique/unexpected content (0.0 - 1.0)
        public float Energy { get; set; }            // Pacing and intensity level (0.0 calm to 1.0 energetic)
        public int Length { get; set; }              // Target duration in seconds
        public int TemporalExpansion { get; set; }   // Temporal expansion factor (0.0 - 10.0)
        public GenAISettings GenAISettings { get; set; } // AI settings for generating the story
        public TakeLayerSettings TakeLayerSettings { get; set; } // Take layer settings for duplicate detection
    }

    public class GenAISettings
    {
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        public float RepetitionPenalty { get; set; } = 1.1f;
        public long? RandomSeed { get; set; } = null;
    }

    public class TakeLayerSettings
    {
        [JsonConstructor]
        public TakeLayerSettings()
        {
            EnableTakeLayer = true;
            LevenshteinThreshold = 10;
            CosineSimilarityThreshold = 0.9f;
            RelevanceWeight = 0.4f;
            FlubWeight = 0.3f;
            FocusWeight = 0.2f;
            EnergyWeight = 0.1f;
        }

        public bool EnableTakeLayer { get; set; }
        public int LevenshteinThreshold { get; set; }      // Maximum edit distance for clustering
        public float CosineSimilarityThreshold { get; set; } // Minimum cosine similarity for clustering
        public float RelevanceWeight { get; set; }         // Weight for relevance scoring
        public float FlubWeight { get; set; }              // Weight for flub detection scoring
        public float FocusWeight { get; set; }             // Weight for focus scoring (placeholder)
        public float EnergyWeight { get; set; }            // Weight for energy scoring (placeholder)
    }
} 