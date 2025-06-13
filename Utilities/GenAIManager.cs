using Microsoft.ML.OnnxRuntimeGenAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Text;

namespace FrameFlow.Utilities
{
    public class GenAIManager : IDisposable
    {
        private static GenAIManager? _instance;
        private Model? _model;
        private Tokenizer? _tokenizer;
        private TokenizerStream? _tokenizerStream;
        private GeneratorParams? _genParams;
        private readonly List<(string role, string content)> _chatHistory;
        private readonly object _lock = new object();

        // Configuration properties
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        public float RepetitionPenalty { get; set; } = 1.1f;
        public int MaxLength { get; set; } = 2048;
        public int MinLength { get; set; } = 8;
        public int? RandomSeed { get; set; } = null;
        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";

        private GenAIManager()
        {
            _chatHistory = new List<(string role, string content)>();
            try
            {
                InitializeAsync().Wait();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize GenAIManager: {ex.Message}");
            }
        }

        public static GenAIManager Instance
        {
            get
            {
                _instance ??= new GenAIManager();
                return _instance;
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                string modelPath = GetModelPath();
                System.Diagnostics.Debug.WriteLine($"Loading model from: {modelPath}");

                _model = new Model(modelPath);
                _tokenizer = new Tokenizer(_model);
                _tokenizerStream = _tokenizer.CreateStream();
                _genParams = new GeneratorParams(_model);
                
                InitializeParameters();
                System.Diagnostics.Debug.WriteLine("Model loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize model: {ex.Message}");
                throw;
            }
        }

        private string GetModelPath()
        {
            // Try loading model in order of preference: Preferred -> DirectML -> CPU
            string preferredDir = App.Settings.Instance.GetModelDirectory();
            if (Directory.Exists(preferredDir) && HasModelFiles(preferredDir))
                return preferredDir;

            string directMLDir = App.Settings.Instance.OnnxDirectMLModelDirectory;
            if (Directory.Exists(directMLDir) && HasModelFiles(directMLDir))
                return directMLDir;

            string cpuDir = App.Settings.Instance.OnnxCpuModelDirectory;
            if (Directory.Exists(cpuDir) && HasModelFiles(cpuDir))
                return cpuDir;

            throw new DirectoryNotFoundException("No valid model found in any of the model directories");
        }

        private static bool HasModelFiles(string path)
        {
            return Directory.GetFiles(path, "*.onnx").Length > 0 ||
                   File.Exists(Path.Combine(path, "genai_config.json"));
        }

        private void InitializeParameters()
        {
            if (_genParams == null) return;

            RandomSeed ??= new Random().Next(int.MaxValue);
            
            _genParams.SetSearchOption("temperature", Temperature);
            _genParams.SetSearchOption("top_p", TopP);
            _genParams.SetSearchOption("repetition_penalty", RepetitionPenalty);
            _genParams.SetSearchOption("max_length", MaxLength);
            _genParams.SetSearchOption("min_length", MinLength);
            
            if (RandomSeed.HasValue)
                _genParams.SetSearchOption("random_seed", RandomSeed.Value);
        }

        public async Task<string> GenerateTextAsync(string userPrompt, bool saveHistory = true)
        {
            if (_model == null || _tokenizer == null || _genParams == null)
                throw new InvalidOperationException("Model not initialized");

            const int maxAttempts = 2;
            var attempts = 0;
            
            while (true)
            {
                try
                {
                    var prompt = BuildChatPrompt(userPrompt);
                    var sequences = _tokenizer.Encode(prompt);

                    using var generator = new Generator(_model, _genParams);
                    generator.AppendTokenSequences(sequences);

                    var response = "";
                    while (!generator.IsDone())
                    {
                        generator.GenerateNextToken();
                        var token = _tokenizerStream?.Decode(generator.GetSequence(0)[^1]) ?? "";
                        response += token;
                    }

                    response = response.Trim();
                    
                    if (saveHistory)
                    {
                        _chatHistory.Add(("user", userPrompt));
                        _chatHistory.Add(("assistant", response));
                    }

                    return response;
                }
                catch (Exception ex) when (ex.Message.Contains("input_ids size") || ex.Message.Contains("sequence length"))
                {
                    attempts++;
                    if (attempts >= maxAttempts || _chatHistory.Count == 0)
                        throw new InvalidOperationException($"Generation failed after {attempts} attempts: {ex.Message}");
                        
                    _chatHistory.Clear();
                    continue;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Generation failed: {ex.Message}");
                }
            }
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

        public void UpdateSettings(
            float? temperature = null,
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
            
            InitializeParameters();
        }

        public void Dispose()
        {
            _tokenizerStream?.Dispose();
            _tokenizer?.Dispose();
            _genParams?.Dispose();
            _model?.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool IsModelLoaded => _model != null && _tokenizer != null;
    }
} 