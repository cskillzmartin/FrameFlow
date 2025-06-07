using System.Numerics;
using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace FrameFlow.Utilities
{
    public class PhiChatModel : IDisposable
    {
        private readonly Model _model;
        private readonly Tokenizer _tokenizer;
        private readonly TokenizerStream _tokenizerStream;
        private readonly GeneratorParams _genParams;
        private readonly List<(string role, string content)> _chatHistory;

        // Configuration properties
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        public float RepetitionPenalty { get; set; } = 1.1f;
        public int MaxLength { get; set; } = 2048;
        public int MinLength { get; set; } = 8;
        public int? RandomSeed { get; set; } = null;
        public string SystemPrompt { get; set; } = Prompts.System.DefaultAssistant;
        public string ModelPath { get; private set; }

        public PhiChatModel(string modelPath, 
                           float temperature = 1.1f,
                           float topP = 1.1f, 
                           float repetitionPenalty = 1.1f,
                           int maxLength = 2048,
                           int minLength = 8,
                           int? randomSeed = null,
                           string systemPrompt = null)
        {
            try
            {
                _model = new Model(modelPath);
                _tokenizer = new Tokenizer(_model);
                _tokenizerStream = _tokenizer.CreateStream();
                _genParams = new GeneratorParams(_model);
                _chatHistory = new List<(string role, string content)>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load model from {modelPath}: {ex.Message}", ex);
            }

            InitializeParameters(temperature, topP, repetitionPenalty, maxLength, minLength, randomSeed, systemPrompt);
        }

        private static string FindCpuModelPath(string basePath)
        {
            // If the provided path is directly a model directory, use it
            if (Directory.Exists(basePath) && HasModelFiles(basePath))
            {
                return basePath;
            }
            
            // Try to find CPU model in standard structure
            var cpuPath = Path.Combine(basePath, "cpu");
            if (Directory.Exists(cpuPath) && HasModelFiles(cpuPath))
            {
                return cpuPath;
            }

            throw new DirectoryNotFoundException($"No valid CPU model found at: {basePath}");
        }

        private static bool HasModelFiles(string path)
        {
            return Directory.GetFiles(path, "*.onnx").Length > 0 ||
                   File.Exists(Path.Combine(path, "genai_config.json"));
        }

        private void UpdateGeneratorParams()
        {
            _genParams.SetSearchOption("temperature", Temperature);
            _genParams.SetSearchOption("top_p", TopP);
            _genParams.SetSearchOption("repetition_penalty", RepetitionPenalty);
            _genParams.SetSearchOption("max_length", MaxLength);
            _genParams.SetSearchOption("min_length", MinLength);
            
            if (RandomSeed.HasValue)
                _genParams.SetSearchOption("random_seed", RandomSeed.Value);
        }

        public string Chat(string userInput, bool addToHistory = true)
        {
            var prompt = BuildChatPrompt(userInput);
            using var generator = CreateGenerator(prompt);
            var response = ProcessGeneratorResponse(generator, addToHistory);
            
            if (addToHistory)
            {
                _chatHistory.Add(("user", userInput));
                _chatHistory.Add(("assistant", response));
            }
            
            return response;
        }

        public string ChatWithStreaming(string userInput, Action<string> onTokenReceived, bool addToHistory = true)
        {
            if (addToHistory)
                _chatHistory.Add(("user", userInput));

            var prompt = BuildChatPrompt(userInput);
            var sequences = _tokenizer.Encode(prompt);

            using var generator = new Generator(_model, _genParams);
            generator.AppendTokenSequences(sequences);

            var response = "";
            while (!generator.IsDone())
            {
                generator.GenerateNextToken();
                var token = _tokenizerStream.Decode(generator.GetSequence(0)[^1]);
                response += token;
                onTokenReceived?.Invoke(token);
            }

            response = response.Trim();
            
            if (addToHistory)
                _chatHistory.Add(("assistant", response));

            return response;
        }

        private string BuildChatPrompt(string currentInput)
        {
            var prompt = $"<|system|>{SystemPrompt}<|end|>";
            
            foreach (var (role, content) in _chatHistory)
            {
                prompt += $"<|{role}|>{content}<|end|>";
            }
            
            prompt += $"<|user|>{currentInput}<|end|><|assistant|>";
            return prompt;
        }

        public void ClearHistory()
        {
            _chatHistory.Clear();
        }

        public void UpdateSettings(float? temperature = null, 
                                 float? topP = null, 
                                 float? repetitionPenalty = null,
                                 int? maxLength = null, 
                                 int? minLength = null,
                                 int? randomSeed = null)
        {
            if (temperature.HasValue) Temperature = temperature.Value;
            if (topP.HasValue) TopP = topP.Value;
            if (repetitionPenalty.HasValue) RepetitionPenalty = repetitionPenalty.Value;
            if (maxLength.HasValue) MaxLength = maxLength.Value;
            if (minLength.HasValue) MinLength = minLength.Value;
            if (randomSeed.HasValue) RandomSeed = randomSeed.Value;
            
            UpdateGeneratorParams();
        }

        /// <summary>
        /// Scores the relevance of a text segment to a subject using the model.
        /// </summary>
        /// <param name="segmentText">The text to score.</param>
        /// <param name="subject">The subject to compare against.</param>
        /// <returns>A float score from 0 to 100.</returns>
        public float ScoreRelevance(string segmentText, string subject)
        {
            // Fixed scoring system prompt
            SystemPrompt = Prompts.System.RelevanceScoring;
            string userPrompt = Prompts.Relevance.ScoreSegment(subject, segmentText);
            
            // Build prompt without using chat history
            string prompt = $"<|system|>{SystemPrompt}<|end|><|user|>{userPrompt}<|end|><|assistant|>";
            
            var sequences = _tokenizer.Encode(prompt);
            using var generator = new Generator(_model, _genParams);
            generator.AppendTokenSequences(sequences);

            var response = "";
            while (!generator.IsDone())
            {
                generator.GenerateNextToken();
                var token = _tokenizerStream.Decode(generator.GetSequence(0)[^1]);
                response += token;
            }

            response = response.Trim();
            
            return ParseScoreResponse(response);
        }

        public void Dispose()
        {
            _tokenizerStream?.Dispose();
            _tokenizer?.Dispose();
            _genParams?.Dispose();
            _model?.Dispose();
        }

        private void InitializeParameters(
            float temperature,
            float topP,
            float repetitionPenalty,
            int maxLength,
            int minLength,
            int? randomSeed,
            string? systemPrompt)
        {
            Temperature = temperature;
            TopP = topP;
            RepetitionPenalty = repetitionPenalty;
            MaxLength = maxLength;
            MinLength = minLength;
            RandomSeed = randomSeed ?? RandomNumberGenerator.GetInt32(0, int.MaxValue);
            SystemPrompt = systemPrompt ?? Prompts.System.DefaultAssistant;
            UpdateGeneratorParams();
        }

        private Generator CreateGenerator(string prompt)
        {
            var sequences = _tokenizer.Encode(prompt);
            var generator = new Generator(_model, _genParams);
            generator.AppendTokenSequences(sequences);
            return generator;
        }

        private string ProcessGeneratorResponse(Generator generator, bool addToHistory = true)
        {
            var response = "";
            while (!generator.IsDone())
            {
                generator.GenerateNextToken();
                var token = _tokenizerStream.Decode(generator.GetSequence(0)[^1]);
                response += token;
            }

            response = response.Trim();
            if (addToHistory)
                _chatHistory.Add(("assistant", response));
            
            return response;
        }

        private float ParseScoreResponse(string response)
        {
            if (float.TryParse(
                new string(response.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray()).Replace(',', '.'), 
                System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, 
                out float score))
            {
                return Math.Clamp(score, 0, 100);
            }
            return 0f;
        }
    }
} 