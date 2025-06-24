using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrameFlow.App
{
    public class Settings
    {
        private static Settings? _instance;
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FrameFlow",
            "settings.json"
        );

        // Required for JSON deserialization
        [JsonConstructor]
        public Settings() 
        {
            try
            {
                Directory.CreateDirectory(DefaultProjectLocation);
                Directory.CreateDirectory(DefaultImportLocation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create default directories: {ex.Message}");
            }
        }

        // FFmpeg settings
        public string FfmpegPath { get; set; } = string.Empty;
        public string FfprobePath { get; set; } = string.Empty;

        // Project settings
        public string DefaultProjectLocation { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)+@"\FrameFlow-Projects";
        public string LastOpenedProject { get; set; } = string.Empty;
        public List<string> RecentProjects { get; set; } = new();
        public int MaxRecentProjects { get; set; } = 10;

        // Import settings
        public string DefaultImportLocation { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)+@"\FrameFlow-Imports";
        public List<string> SupportedVideoFormats { get; set; } = new()
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"
        };
        public bool AutoAnalyzeOnImport { get; set; } = true;
        public bool CreateThumbnails { get; set; } = true;
        public int ThumbnailInterval { get; set; } = 10; // seconds

        // UI settings
        public bool DarkMode { get; set; } = false;
        public string UILanguage { get; set; } = "en-US";
        public bool ShowThumbnails { get; set; } = true;
        public int ThumbnailSize { get; set; } = 120; // pixels

        // AI Model settings
        public string ModelPath { get; set; } = string.Empty;
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 2048;
        public string WhisperModelPath { get; set; } = string.Empty;
  
        // ONNX Model Directories
        public string OnnxTextCpuModelDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FrameFlow",
            "models",
            "cpu");

        public string OnnxTextCudaModelDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FrameFlow",
            "models",
            "cuda");

        public string OnnxTextDirectMLModelDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FrameFlow",
            "models",
            "directml");

        // Preferred compute provider
        public string PreferredComputeProvider { get; set; } = "CUDA"; // Options: "CPU", "CUDA", "DirectML"

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }

        private static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string jsonString = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<Settings>(jsonString);
                    return settings ?? new Settings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                string settingsDirectory = Path.GetDirectoryName(SettingsFilePath)!;
                if (!Directory.Exists(settingsDirectory))
                {
                    Directory.CreateDirectory(settingsDirectory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonString = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, jsonString);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save settings: {ex.Message}", ex);
            }
        }

        public void AddRecentProject(string projectPath)
        {
            // Remove if already exists (to move it to top)
            RecentProjects.Remove(projectPath);
            
            // Add to beginning of list
            RecentProjects.Insert(0, projectPath);
            
            // Trim list to maximum size
            while (RecentProjects.Count > MaxRecentProjects)
            {
                RecentProjects.RemoveAt(RecentProjects.Count - 1);
            }
            
            Save();
        }

        public void ClearRecentProjects()
        {
            RecentProjects.Clear();
            Save();
        }

        public void ResetToDefaults()
        {
            // FFmpeg settings
            FfmpegPath = string.Empty;
            FfprobePath = string.Empty;

            // Project settings
            DefaultProjectLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            LastOpenedProject = string.Empty;
            RecentProjects.Clear();
            MaxRecentProjects = 10;

            // Import settings
            DefaultImportLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            SupportedVideoFormats = new List<string>
            {
                ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"
            };
            AutoAnalyzeOnImport = true;
            CreateThumbnails = true;
            ThumbnailInterval = 10;

            // UI settings
            DarkMode = false;
            UILanguage = "en-US";
            ShowThumbnails = true;
            ThumbnailSize = 120;

            // AI Model settings
            ModelPath = string.Empty;
            Temperature = 0.7f;
            MaxTokens = 2048;
            WhisperModelPath = string.Empty;

            // ONNX Model Directories - Reset to default paths
            OnnxTextCpuModelDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FrameFlow",
                "models",
                "Text",
                "cpu");

            OnnxTextCudaModelDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FrameFlow",
                "models",
                "Text",
                "cuda");

            OnnxTextDirectMLModelDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FrameFlow",
                "models",
                "Text",
                "directml");

            PreferredComputeProvider = "CUDA";

            Save();
        }

        public bool ValidateFFmpegPaths()
        {
            return File.Exists(FfmpegPath) && File.Exists(FfprobePath);
        }

        // Helper method to get the appropriate model directory based on compute provider
        public string GetModelDirectory(string? computeProvider = "CUDA")
        {
            computeProvider ??= PreferredComputeProvider;
            
            return computeProvider.ToUpper() switch
            {
                "CUDA" => OnnxTextCudaModelDirectory,
                "DIRECTML" => OnnxTextDirectMLModelDirectory,
                _ => OnnxTextCpuModelDirectory
            };
        }
    }
} 