using System;
using System.IO;
using System.Text.Json;
using FrameFlow.Models;
using System.Linq;
using System.Threading.Tasks;

namespace FrameFlow.App
{
    public class ProjectHandler
    {
        private static ProjectHandler? _instance;
        private readonly Utilities.ImportManager _importManager;

        private ProjectHandler()
        {
            try
            {
                _importManager = new Utilities.ImportManager();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize ImportManager: {ex.Message}");
                // Allow creation even if ImportManager fails
            }
        }

        public static ProjectHandler Instance
        {
            get
            {
                _instance ??= new ProjectHandler();
                return _instance;
            }
        }

        private static string? _currentProjectPath;
        private static bool _isProjectModified;
        private ProjectModel? _currentProject;
        private const string PROJECT_FILE_NAME = "project.ffproj";

        public string CurrentProjectPath 
        { 
            get => _currentProjectPath ?? string.Empty;
            private set
            {
                _currentProjectPath = value;
                Settings.Instance.LastOpenedProject = value;
                Settings.Instance.Save();
            }
        }

        public bool IsProjectModified
        {
            get => _isProjectModified;
            private set
            {
                if (_isProjectModified != value)
                {
                    _isProjectModified = value;
                    ProjectModified?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ProjectModel? CurrentProject => _currentProject;

        // Event handlers for project state changes
        public event EventHandler? ProjectOpened;
        public event EventHandler? ProjectClosed;
        public event EventHandler? ProjectModified;

        public bool CreateProject(string projectPath)
        {
            try
            {
                if (string.IsNullOrEmpty(projectPath))
                    throw new ArgumentException("Project path cannot be empty");

                // Create project directory if it doesn't exist
                Directory.CreateDirectory(projectPath);

                // Create initial project structure
                Directory.CreateDirectory(Path.Combine(projectPath, "media"));
                Directory.CreateDirectory(Path.Combine(projectPath, "thumbnails"));
                Directory.CreateDirectory(Path.Combine(projectPath, "Transcriptions"));
                Directory.CreateDirectory(Path.Combine(projectPath, "Renders"));

                // Create new project model
                _currentProject = new ProjectModel
                {
                    Name = Path.GetFileName(projectPath),
                    CreatedDate = DateTime.Now,
                    LastModifiedDate = DateTime.Now
                };

                // Save project file
                SaveProject(projectPath);

                CurrentProjectPath = projectPath;
                IsProjectModified = false;

                // Update recent projects in settings
                Settings.Instance.AddRecentProject(projectPath);
                Settings.Instance.LastOpenedProject = projectPath;
                Settings.Instance.Save();

                OnProjectOpened();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create project: {ex.Message}");
                return false;
            }
        }

        public bool OpenProject(string projectPath)
        {
            try
            {
                if (!Directory.Exists(projectPath))
                    throw new DirectoryNotFoundException("Project directory not found");

                string projectFilePath = Path.Combine(projectPath, PROJECT_FILE_NAME);
                if (!File.Exists(projectFilePath))
                    throw new FileNotFoundException("Project file not found", projectFilePath);

                // Load project file
                string jsonContent = File.ReadAllText(projectFilePath);
                _currentProject = JsonSerializer.Deserialize<ProjectModel>(jsonContent);

                if (_currentProject == null)
                    throw new InvalidOperationException("Failed to load project file");

                CurrentProjectPath = projectPath;
                IsProjectModified = false;

                // Update recent projects in settings
                Settings.Instance.AddRecentProject(projectPath);
                Settings.Instance.LastOpenedProject = projectPath;
                Settings.Instance.Save();

                OnProjectOpened();
                System.Diagnostics.Debug.WriteLine("Project Opened Event Fired");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open project: {ex.Message}");
                return false;
            }
        }

        private void SaveProject(string projectPath)
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is currently loaded");

            string projectFilePath = Path.Combine(projectPath, PROJECT_FILE_NAME);
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonContent = JsonSerializer.Serialize(_currentProject, options);
            File.WriteAllText(projectFilePath, jsonContent);
        }

        public void CloseProject()
        {
            if (CurrentProjectPath != null)
            {
                if (IsProjectModified && _currentProject != null)
                {
                    SaveProject(CurrentProjectPath);
                }
                
                CurrentProjectPath = null;
                _currentProject = null;
                IsProjectModified = false;
                OnProjectClosed();
            }
        }

        public void MarkAsModified()
        {
            IsProjectModified = true;
        }

        public async Task<bool> AddMediaToProject(string sourceFilePath)
        {
            try
            {
                if (_currentProject == null)
                    throw new InvalidOperationException("No project is currently loaded");

                // Copy file to project's media directory
                string targetPath = Path.Combine(
                    CurrentProjectPath,
                    "media",
                    Path.GetFileName(sourceFilePath));

                File.Copy(sourceFilePath, targetPath, true);

                // Add to project manifest
                var mediaFile = new Models.MediaFileInfo
                {
                    FileName = Path.GetFileName(sourceFilePath),
                    RelativePath = Path.Combine("media", Path.GetFileName(sourceFilePath)),
                    FileSize = new FileInfo(sourceFilePath).Length,
                    ImportDate = DateTime.Now,
                    FileHash = string.Empty, // TODO: Implement file hash calculation
                    HasThumbnails = false
                };

                // Extract metadata using ImportManager
                await Task.Run(() => {
                    try
                    {
                        mediaFile.VideoMetaData = _importManager.ExtractMetaData(targetPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to extract metadata: {ex.Message}");
                        // Continue even if metadata extraction fails
                    }
                });

                _currentProject.MediaFiles.Add(mediaFile);
                SaveProject(CurrentProjectPath);
                MarkAsModified();
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add media to project: {ex.Message}");
                return false;
            }
        }

       public async Task<string> ExtractAudio(string sourceFilePath)
       {  
            var rtnValue = string.Empty;
            try
            {
                rtnValue = await _importManager.ExtractAudioAsync(sourceFilePath);                
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - we don't want to block the metadata return
                Console.WriteLine($"Auto transcription failed: {ex.Message}");
            }
            return rtnValue;
        }
        public async Task<bool> TranscribeAudio(string mediafile, string addiofile)
        {
                await _importManager.TranscribeAudioToSrtAsync(addiofile);

                // Update the media file info in the project
                var fileName = Path.GetFileName(mediafile);
                var mediaFile = ProjectHandler.Instance.CurrentProject?.MediaFiles.FirstOrDefault(m => m.FileName == fileName);
                if (mediaFile != null)
                {
                    mediaFile.HasTranscription = true;
                    ProjectHandler.Instance.MarkAsModified();
                    ProjectHandler.Instance.SaveCurrentProject();
                }
                // Clean up the temporary audio file
                if (File.Exists(addiofile))
                    File.Delete(addiofile);
                return true;
        }
        public bool RemoveMediaFromProject(string fileName)
        {
            try
            {
                if (_currentProject == null)
                    throw new InvalidOperationException("No project is currently loaded");

                // Find the media file in the manifest
                var mediaFile = _currentProject.MediaFiles.FirstOrDefault(m => m.FileName == fileName);
                if (mediaFile == null)
                    throw new FileNotFoundException($"Media file '{fileName}' not found in project manifest");

                // Remove the physical file
                string filePath = Path.Combine(CurrentProjectPath, mediaFile.RelativePath);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Add this section to remove the transcription file
                if (mediaFile.HasTranscription)
                {
                    string transcriptionFile = Path.Combine(CurrentProjectPath, "Transcriptions", $"{Path.GetFileNameWithoutExtension(fileName)}.srt");
                    if (File.Exists(transcriptionFile))
                    {
                        File.Delete(transcriptionFile);
                    }
                }

                // Remove from manifest
                _currentProject.MediaFiles.Remove(mediaFile);
                
                // Save project and mark as modified
                SaveProject(CurrentProjectPath);
                MarkAsModified();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to remove media from project: {ex.Message}");
                return false;
            }
        }

        protected virtual void OnProjectOpened()
        {
            ProjectOpened?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnProjectClosed()
        {
            ProjectClosed?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnProjectModified()
        {
            ProjectModified?.Invoke(this, EventArgs.Empty);
        }

        public void SaveCurrentProject()
        {
            if (!string.IsNullOrEmpty(CurrentProjectPath))
            {
                SaveProject(CurrentProjectPath);
            }
        }
    }
} 