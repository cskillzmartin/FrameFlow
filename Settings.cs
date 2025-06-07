using System.IO;
using System.Text.Json;

namespace FrameFlow
{
    public class Settings
    {
        private const string SettingsFileName = "settings.json";
        private static string SettingsFilePath => Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
            SettingsFileName);

        public string WorkingFilesPath { get; set; }

        public Settings()
        {
            // Default to the application directory
            WorkingFilesPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "WorkingFiles");
        }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string jsonString = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<Settings>(jsonString);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
                // If there's any error loading settings, return defaults
            }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, jsonString);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 