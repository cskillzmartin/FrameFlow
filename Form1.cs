// This is the main form class for the FrameFlow video editing application
// It handles all the UI interactions and coordinates the video processing workflow
namespace FrameFlow;
using System.Windows.Forms;
using FrameFlow.Utilities;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;

public partial class Form1 : Form
{
    // This inner class represents a media file in the application
    // It keeps track of both the file's name and its full path
    private class MediaFileItem
    {
        public string FileName { get; }  // Just the name of the file (e.g., "video.mp4")
        public string FilePath { get; }  // The complete path to the file
        
        // Constructor: Creates a new MediaFileItem from a file path
        public MediaFileItem(string filePath)
        {
            FilePath = filePath;
            FileName = System.IO.Path.GetFileName(filePath);  // Extracts just the filename from the path
        }
        
        // When we need to display this item, show just the filename
        public override string ToString() => FileName;
    }

    // Important class-level variables that keep track of the application's state
    private string? projectName;  // The name of the current project
    private string? projectWorkingDir;  // The directory where project files are stored
    private Dictionary<string, ProgressBar> transcriptionProgressBars = new();  // Tracks progress bars for each file being transcribed
    private Dictionary<string, Label> transcriptionStatusLabels = new();  // Tracks status labels for each file being transcribed
    private Settings settings;  // Application settings (like working directory)

    // Constructor: Sets up the main form and initializes everything
    public Form1()
    {
        InitializeComponent();
        settings = Settings.Load();
        
        // Do not prompt for project on startup
        panelTranscriptionProgress.Resize += (s, e) =>
        {
            int margin = 20;
            foreach (var pb in transcriptionProgressBars.Values)
            {
                pb.Width = panelTranscriptionProgress.Width - margin;
            }
        };
        this.importMediaButton.Click += new System.EventHandler(this.importMediaButton_Click);
        this.btnNewProject.Click += new System.EventHandler(this.btnNewProject_Click);
        this.btnOpenProject.Click += new System.EventHandler(this.btnOpenProject_Click);
        this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
        this.btnAnalyzeAndEditSrt.Click += new System.EventHandler(this.btnAnalyzeAndEditSrt_Click);
        this.importMediaButton.Text = "Import Media";
        this.btnNewProject.Text = "New Project";
        this.btnOpenProject.Text = "Open Project";
        this.btnAnalyzeAndEditSrt.Text = "Analyze & Edit SRT";
    }

    // Handles clicking the Settings button
    // Opens a settings dialog where users can change the working directory
    private void btnSettings_Click(object sender, EventArgs e)
    {
        using (var settingsForm = new SettingsForm(settings.WorkingFilesPath))
        {
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                settings.WorkingFilesPath = settingsForm.WorkingFilesPath;
                settings.Save();

                // If we have an active project, update its path
                if (!string.IsNullOrEmpty(projectName))
                {
                    projectWorkingDir = Path.Combine(settings.WorkingFilesPath, projectName);
                    // Ensure the directory exists at the new location
                    Directory.CreateDirectory(projectWorkingDir);
                }
            }
        }
    }

    // Shows a dialog to get a project name from the user
    // Creates a new project directory if it doesn't exist
    private void PromptForProjectName()
    {
        string? input = null;
        while (string.IsNullOrWhiteSpace(input))
        {
            input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a project name:",
                "Project Name",
                "MyProject");
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Project name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        projectName = input.Trim();
        projectWorkingDir = Path.Combine(settings.WorkingFilesPath, projectName);
        Directory.CreateDirectory(projectWorkingDir);
        this.Text = $"FrameFlow - Project: {projectName}";
    }

    // Creates and shows the progress panel that appears during transcription
    // Each file gets its own progress bar and status label
    private void ShowTranscriptionProgressPanel(List<MediaFileItem> files)
    {
        panelTranscriptionProgress.Controls.Clear();
        transcriptionProgressBars.Clear();
        transcriptionStatusLabels.Clear();
        
        int y = 10;
        int margin = 20;
        
        // Create progress bars for all files upfront
        foreach (var file in files)
        {
            var label = new Label
            {
                Text = file.FileName + " - Queued",
                ForeColor = System.Drawing.Color.White,
                Location = new System.Drawing.Point(10, y),
                AutoSize = true
            };
            var progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Width = panelTranscriptionProgress.Width - margin,
                Location = new System.Drawing.Point(10, y + 20),
                Value = 0,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            panelTranscriptionProgress.Controls.Add(label);
            panelTranscriptionProgress.Controls.Add(progressBar);
            transcriptionProgressBars[file.FilePath] = progressBar;
            transcriptionStatusLabels[file.FilePath] = label;
            y += 50;
        }
        
        panelTranscriptionProgress.Visible = true;
    }

    // Updates the progress bar and status label for a specific file during transcription
    private void UpdateTranscriptionProgress(string filePath, string message, int percent)
    {
           if (transcriptionProgressBars.TryGetValue(filePath, out var progressBar) && transcriptionStatusLabels.TryGetValue(filePath, out var label))
            {
                if (progressBar.InvokeRequired || label.InvokeRequired)
                {
                    this.Invoke(() => UpdateTranscriptionProgress(filePath, message, percent));
                    return;
                }

                progressBar.Value = Math.Clamp(percent, 0, 100);
                label.Text = System.IO.Path.GetFileName(filePath) + $" - {message} ({percent}%)";
            }
    }

    // Marks a file's transcription as complete in the UI
    private void MarkTranscriptionDone(string filePath)
    {
        if (transcriptionProgressBars.TryGetValue(filePath, out var progressBar) &&
        transcriptionStatusLabels.TryGetValue(filePath, out var label))
        {
            if (progressBar.InvokeRequired || label.InvokeRequired)
            {
                this.Invoke(() => MarkTranscriptionDone(filePath));
                return;
            }

            progressBar.Value = 100;
            label.Text = System.IO.Path.GetFileName(filePath) + " - Done";
        }
    }

    // Handles importing media files into the project using a queue system
    // maintaining 10 concurrent operations at all times
    private async void importMediaButton_Click(object sender, EventArgs e)
    {
        // Prompt for project if not set
        if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(projectWorkingDir))
        {
            PromptForProjectName();
        }
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Multiselect = true;
            openFileDialog.Title = "Import Media";
            openFileDialog.Filter = "Audio/Video/Images (*.mp4;)|*.mp4;";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                mediaListBox.Items.Clear();
                var importedFiles = openFileDialog.FileNames.Select(f => new MediaFileItem(f)).ToList();
                foreach (var file in importedFiles)
                {
                    mediaListBox.Items.Add(file);
                }

                // Automatically transcribe video files
                var videoExtensions = new[] { ".mp4" };
                var videoFiles = importedFiles
                    .Where(f => videoExtensions.Contains(System.IO.Path.GetExtension(f.FilePath).ToLowerInvariant()))
                    .ToList();

                if (videoFiles.Any())
                {
                    const int maxConcurrent = 3;
                    var processingQueue = new Queue<MediaFileItem>(videoFiles);
                    var currentlyProcessing = new HashSet<string>(); // Track files being processed
                    var completedCount = 0;
                    var totalFiles = videoFiles.Count;
                    var activeTasks = new List<Task>();
                    string whisperModelPath = GetWhisperModelPath();

                    this.Text = $"FrameFlow - Project: {projectName} - Transcribed Files (0/{totalFiles})...";
                    ShowTranscriptionProgressPanel(videoFiles);

                    // Process queue until empty
                    while (processingQueue.Count > 0 || activeTasks.Count > 0)
                    {
                        // Start new tasks if we have capacity and files to process
                        while (activeTasks.Count < maxConcurrent && processingQueue.Count > 0)
                        {
                            var video = processingQueue.Dequeue();
                            var filePath = video.FilePath;
                            currentlyProcessing.Add(filePath);

                            var progress = new Progress<FrameFlow.Utilities.TranscriptionProgress>(p =>
                            {
                                UpdateTranscriptionProgress(filePath, p.Message, p.PercentComplete);
                            });

                            var transcriptionUtility = new FrameFlow.Utilities.VideoTranscriptionUtility(projectWorkingDir, progress);
                            var task = Task.Run(async () =>
                            {
                                try
                                {
                                    await transcriptionUtility.TranscribeVideoAsync(filePath, whisperModelPath);
                                    
                                    // Update UI to show completion and remove progress bar
                                    this.Invoke((Action)(() =>
                                    {
                                        if (transcriptionProgressBars.TryGetValue(filePath, out var progressBar) &&
                                            transcriptionStatusLabels.TryGetValue(filePath, out var label))
                                        {
                                            // Fade out completed progress bar and label
                                            FadeOutAndRemoveControls(progressBar, label);
                                            
                                            // Remove from dictionaries
                                            transcriptionProgressBars.Remove(filePath);
                                            transcriptionStatusLabels.Remove(filePath);
                                            
                                            // Reposition remaining progress bars
                                            RepositionProgressBars();
                                        }
                                    }));
                                    
                                    // Update completion status
                                    Interlocked.Increment(ref completedCount);
                                    this.Invoke((Action)(() =>
                                    {
                                        this.Text = $"FrameFlow - Project: {projectName} - Transcribed Files ({completedCount}/{totalFiles})...";
                                    }));
                                }
                                catch (Exception ex)
                                {
                                    this.Invoke((Action)(() =>
                                    {
                                        MessageBox.Show($"Error processing {Path.GetFileName(filePath)}: {ex.Message}", 
                                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }));
                                }
                                finally
                                {
                                    currentlyProcessing.Remove(filePath);
                                }
                            });

                            activeTasks.Add(task);
                        }

                        // Wait for at least one task to complete before checking queue again
                        if (activeTasks.Count > 0)
                        {
                            var completedTask = await Task.WhenAny(activeTasks);
                            activeTasks.Remove(completedTask);
                        }
                    }

                    // Clean up UI
                    panelTranscriptionProgress.Visible = false;
                    this.Text = $"FrameFlow - Project: {projectName}";
                    transcriptionProgressBars.Clear();
                    transcriptionStatusLabels.Clear();
                }
            }
        }
    }

    // Modified helper method to fade out and remove completed progress bars
    private async void FadeOutAndRemoveControls(ProgressBar progressBar, Label label)
    {
        try
        {
            // Animate fade out for label only (since ProgressBar doesn't support transparency)
            for (double opacity = 1.0; opacity > 0; opacity -= 0.1)
            {
                // Only fade the label
                label.ForeColor = Color.FromArgb((int)(opacity * 255), label.ForeColor);
                
                // For ProgressBar, we'll gradually change its value to 0
                progressBar.Value = (int)(opacity * progressBar.Value);
                
                await Task.Delay(50);
            }
            
            // Remove controls
            this.Invoke((Action)(() =>
            {
                if (!panelTranscriptionProgress.IsDisposed && !progressBar.IsDisposed && !label.IsDisposed)
                {
                    panelTranscriptionProgress.Controls.Remove(progressBar);
                    panelTranscriptionProgress.Controls.Remove(label);
                    progressBar.Dispose();
                    label.Dispose();
                }
            }));
        }
        catch (ObjectDisposedException)
        {
            // Handle case where controls might have been disposed
        }
        catch (InvalidOperationException)
        {
            // Handle case where form might be closing
        }
    }

    // Modified helper method to reposition remaining progress bars with safety checks
    private void RepositionProgressBars()
    {
        try
        {
            this.Invoke((Action)(() =>
            {
                int y = 10;
                foreach (var kvp in transcriptionProgressBars.ToList()) // Create a copy to avoid modification during enumeration
                {
                    if (!kvp.Value.IsDisposed && transcriptionStatusLabels.TryGetValue(kvp.Key, out var label) && !label.IsDisposed)
                    {
                        var progressBar = kvp.Value;
                        
                        label.Location = new Point(10, y);
                        progressBar.Location = new Point(10, y + 20);
                        y += 50;
                    }
                }
            }));
        }
        catch (ObjectDisposedException)
        {
            // Handle case where controls might have been disposed
        }
        catch (InvalidOperationException)
        {
            // Handle case where form might be closing
        }
    }

    // Creates a new project:
    // 1. Gets a name from the user
    // 2. Creates a new directory
    // 3. Clears the media list
    private void btnNewProject_Click(object sender, EventArgs e)
    {
        PromptForProjectName();
        mediaListBox.Items.Clear();
    }

    // Opens an existing project:
    // 1. Shows a list of available projects
    // 2. Loads the selected project
    // 3. Updates the UI to show the project name
    private void btnOpenProject_Click(object sender, EventArgs e)
    {
        // List all subfolders in WorkingFiles
        if (!Directory.Exists(settings.WorkingFilesPath))
        {
            MessageBox.Show("No projects found.", "Open Project", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var projects = Directory.GetDirectories(settings.WorkingFilesPath)
            .Select(Path.GetFileName)
            .ToList();
        if (projects.Count == 0)
        {
            MessageBox.Show("No projects found.", "Open Project", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var selectForm = new Form { Width = 350, Height = 200, Text = "Select Project" };
        var listBox = new ListBox { Dock = DockStyle.Fill };
        listBox.Items.AddRange(projects.ToArray());
        selectForm.Controls.Add(listBox);
        var okButton = new Button { Text = "OK", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
        selectForm.Controls.Add(okButton);
        selectForm.AcceptButton = okButton;
        if (selectForm.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
        {
            projectName = listBox.SelectedItem.ToString();
            projectWorkingDir = Path.Combine(settings.WorkingFilesPath, projectName);
            this.Text = $"FrameFlow - Project: {projectName}";
            mediaListBox.Items.Clear();

            // Load all video files from the project directory
            var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv" };
            var videoFiles = Directory.GetFiles(projectWorkingDir)
                .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => new MediaFileItem(f))
                .ToList();

            // Add videos to the media list box
            foreach (var video in videoFiles)
            {
                mediaListBox.Items.Add(video);
            }

            // If no videos found, show a message
            if (videoFiles.Count == 0)
            {
                MessageBox.Show("No video files found in this project.", "Project Opened", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    // Handles changes to the duration dropdown
    // Shows/hides the custom duration controls as needed
    private void durationComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        bool isCustom = durationComboBox.SelectedItem != null && durationComboBox.SelectedItem.ToString() == "Custom";
        customDurationComboBox.Visible = isCustom;
        minLabel.Visible = isCustom;
    }

    // Helper function that processes a batch of segments:
    // 1. Scores each segment against the subject
    // 2. Writes them to the edit.srt file
    // 3. Updates the progress bar
    // Returns: The next segment index and count of processed segments
    private async Task<(int newSegmentIndex, int newProcessedSegments)> ProcessSegmentBatch(
        List<SrtSegment> batch,          // The batch of segments to process
        PhiChatModel phi,                // The AI model for scoring
        string subject,                  // The subject we're looking for
        string editSrtPath,              // Where to write the scored segments
        int segmentIndex,                // Current segment number
        int processedSegments,           // How many segments we've processed so far
        int totalSegments,               // Total number of segments to process
        ProgressBar progressBar,         // Progress bar to update
        List<(SrtSegment Segment, float Score)> scoredSegments)  // List to store scored segments
    {
        // Score all segments in the batch
        foreach (var seg in batch)
        {
            // Truncate very long segments to prevent memory issues
            string truncatedText = seg.Text;
            if (truncatedText.Length > 1000) // Limit segment text length
            {
                truncatedText = truncatedText.Substring(0, 1000) + "...";
            }

            // Use the AI model to score how relevant this segment is to our subject
            float score = phi.ScoreRelevance(truncatedText, subject);
            
            // Write the segment and its score to our edit.srt file
            using (var writer = new StreamWriter(editSrtPath, true))
            {
                await writer.WriteLineAsync(segmentIndex.ToString());
                await writer.WriteLineAsync($"{FrameFlow.Utilities.SrtUtils.FormatTime(seg.Start)} --> {FrameFlow.Utilities.SrtUtils.FormatTime(seg.End)}");
                await writer.WriteLineAsync($"[Source: {seg.SourceFile}]");
                await writer.WriteLineAsync($"[Score: {score:F1}]");
                await writer.WriteLineAsync(seg.Text);
                await writer.WriteLineAsync();
            }

            // Keep track of the segment and its score for later
            scoredSegments.Add((seg, score));
            segmentIndex++;
            processedSegments++;

            // Update the progress bar to show how far along we are
            int progress = (int)(100.0 * processedSegments / totalSegments);
            progressBar.Invoke((Action)(() => progressBar.Value = Math.Min(progress, 100)));
        }

        // Return updated counters
        return (segmentIndex, processedSegments);
    }

    // Helper function to write a list of segments to an SRT file
    // Makes sure the format is correct and includes all necessary information
    private async Task WriteSrtFileAsync(string filePath, List<SrtSegment> segments)
    {
        // First clear the file
        File.WriteAllText(filePath, "");
        
        // Then append each segment individually
        for (int i = 0; i < segments.Count; i++)
        {
            using (var writer = new StreamWriter(filePath, append: true))
            {
                await writer.WriteLineAsync((i + 1).ToString());
                await writer.WriteLineAsync($"{FrameFlow.Utilities.SrtUtils.FormatTime(segments[i].Start)} --> {FrameFlow.Utilities.SrtUtils.FormatTime(segments[i].End)}");
                await writer.WriteLineAsync($"[Source: {segments[i].SourceFile}]");
                await writer.WriteLineAsync(segments[i].Text);
                await writer.WriteLineAsync();
            }
        }
    }

    // Detects which AI model backend to use based on available hardware
    private string DetectBestPhiModelBackend()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string cudaPath = Path.Combine(baseDir, "Models", "cuda");
        string dmlPath = Path.Combine(baseDir, "Models", "directml");
        string cpuPath = Path.Combine(baseDir, "Models", "cpu");

        // Try CUDA
        try
        {          
            using (var phi = new PhiChatModel(cudaPath))
                return cudaPath;
        }
        catch (Exception)
        { }

        // Try DirectML
        try
        {
            using (var phi = new PhiChatModel(dmlPath))
                return dmlPath;
        }
        catch (Exception)
        { }

        return cpuPath;
    }

    // Gets the path to the Whisper model for speech recognition
    private string GetWhisperModelPath()
    {
        // Look for the model in the application's directory
        string appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        string modelPath = Path.Combine(appPath, "Models", "whisper", "ggml-base.bin");
        
        // If we don't find it, check common locations
        if (!File.Exists(modelPath))
        {
            modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                   "FrameFlow", "Models", "whisper" , "ggml-base.bin");
        }
        
        // Make sure the model exists
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Could not find Whisper model file. Please ensure it is installed correctly.");
        }
        
        return modelPath;
    }

    // The main function that analyzes and edits SRT files
    // This is where the magic happens:
    // 1. Scores each segment based on relevance to the subject
    // 2. Selects the best segments within the time limit
    // 3. Creates a chronological and story-ordered version
    // 4. Generates the final edited video
    private async void btnAnalyzeAndEditSrt_Click(object sender, EventArgs e)
    {
        // Basic error checking to make sure everything is set up correctly
        if (progressBarAnalysis == null)
        {
            MessageBox.Show("progressBarAnalysis is not initialized. Please check the designer file.");
            return;
        }
        if (btnAnalyzeAndEditSrt == null)
        {
            MessageBox.Show("btnAnalyzeAndEditSrt is not initialized. Please check the designer file.");
            return;
        }
        if (string.IsNullOrWhiteSpace(projectWorkingDir) || string.IsNullOrWhiteSpace(projectName))
        {
            MessageBox.Show("No project selected.");
            return;
        }

        // Get the subject (topic) that we're looking for in the videos
        string subject = subjectTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            MessageBox.Show("Please enter a subject for analysis.");
            return;
        }

        // Find all SRT files (transcripts) in the project directory
        var srtFiles = Directory.GetFiles(projectWorkingDir, "*.srt");
        if (srtFiles.Length == 0)
        {
            MessageBox.Show("No SRT files found in project.");
            return;
        }

        // Disable the analyze button and show progress bar while we work
        btnAnalyzeAndEditSrt.Enabled = false;
        progressBarAnalysis.Invoke((Action)(() => {
            progressBarAnalysis.Visible = true;
            progressBarAnalysis.Value = 0;
        }));

        // Set up our output files:
        // 1. edit.srt - Contains ALL segments with their scores
        // 2. working.edit.srt - Contains SELECTED segments in chronological order
        // 3. working.edit.reorder.srt - Contains SELECTED segments in story order
        string editSrtPath = Path.Combine(projectWorkingDir, $"{projectName}.edit.srt");
        string workingSrtPath = Path.Combine(projectWorkingDir, $"{projectName}.working.edit.srt");
        string reorderedSrtPath = Path.Combine(projectWorkingDir, $"{projectName}.working.edit.reorder.srt");
        
        // Start with empty files
        File.WriteAllText(editSrtPath, "");
        File.WriteAllText(workingSrtPath, "");
        File.WriteAllText(reorderedSrtPath, "");

        // Get the AI model we'll use for scoring segments
        string modelPath = DetectBestPhiModelBackend();

        // Count total segments to track progress
        int totalSegments = 0;
        foreach (var srtFile in srtFiles)
            totalSegments += FrameFlow.Utilities.SrtUtils.ParseSrt(srtFile).Count;
        int processedSegments = 0;

        // Get the maximum duration for our final video from the UI
        TimeSpan maxDuration;
        string durationStr = "";
        durationComboBox.Invoke((Action)(() => durationStr = durationComboBox.SelectedItem?.ToString() ?? ""));
        
        // Handle custom duration if selected
        if (durationStr == "Custom")
        {
            string customDurationStr = "";
            customDurationComboBox.Invoke((Action)(() => customDurationStr = customDurationComboBox.SelectedItem?.ToString() ?? ""));
            maxDuration = FrameFlow.Utilities.SrtUtils.ParseDurationString(customDurationStr);
        }
        else
        {
            maxDuration = FrameFlow.Utilities.SrtUtils.ParseDurationString(durationStr);
        }

        // This will hold all our segments and their relevance scores
        var scoredSegments = new List<(SrtSegment Segment, float Score)>();

        // Main processing happens in this task
        await Task.Run(async () =>
        {
            // Initialize our AI model for scoring segments
            using var phi = new FrameFlow.Utilities.PhiChatModel(modelPath);
            int segmentIndex = 1;
            var batchSize = 50; // Process 50 segments at a time to manage memory
            var currentBatch = new List<SrtSegment>();

            // STEP 1: Score all segments and write them to edit.srt
            foreach (var srtFile in srtFiles)
            {
                // Parse each SRT file
                var segments = FrameFlow.Utilities.SrtUtils.ParseSrt(srtFile);
                
                // Process segments in batches
                foreach (var seg in segments)
                {
                    currentBatch.Add(seg);
                    
                    // When we have enough segments, process the batch
                    if (currentBatch.Count >= batchSize)
                    {
                        var result = await ProcessSegmentBatch(currentBatch, phi, subject, editSrtPath, segmentIndex, processedSegments, totalSegments, progressBarAnalysis, scoredSegments);
                        segmentIndex = result.newSegmentIndex;
                        processedSegments = result.newProcessedSegments;
                        currentBatch.Clear();
                        GC.Collect(); // Clean up memory after each batch
                    }
                }
            }

            // Process any remaining segments
            if (currentBatch.Count > 0)
            {
                await ProcessSegmentBatch(currentBatch, phi, subject, editSrtPath, segmentIndex, processedSegments, totalSegments, progressBarAnalysis, scoredSegments);
                currentBatch.Clear();
                GC.Collect();
            }

            // STEP 2: Select the best segments that fit within our time limit
            var allScoredSegments = scoredSegments
                .OrderByDescending(x => x.Score)  // Sort by score, highest first
                .ToList();

            // Keep track of what we've selected
            var selectedSegments = new List<SrtSegment>();
            var totalDuration = TimeSpan.Zero;

            // Take highest scoring segments that fit in our time limit
            foreach (var scored in allScoredSegments)
            {
                if (totalDuration >= maxDuration) break;
                
                var segmentDuration = scored.Segment.End - scored.Segment.Start;
                // Take segment if score >= 50 or if we have room for it
                if (scored.Score >= 50 || totalDuration + segmentDuration <= maxDuration)
                {
                    selectedSegments.Add(scored.Segment);
                    totalDuration += segmentDuration;
                }
            }

            // Write selected segments to working.edit.srt in chronological order
            var chronologicalSegments = selectedSegments.OrderBy(s => s.Start).ToList();
            await WriteSrtFileAsync(workingSrtPath, chronologicalSegments);

            // STEP 3: Create a story-ordered version of the segments
            var selectedSegmentsForReorder = SrtUtils.ParseSrt(workingSrtPath);
            var reorderedSegments = await SrtUtils.ReorderSegmentsForStoryAsync(
                selectedSegmentsForReorder,
                subject,
                phi,
                new Progress<TranscriptionProgress>(p => {
                    progressBarAnalysis.Invoke((Action)(() => progressBarAnalysis.Value = Math.Min(p.PercentComplete, 100)));
                }));

            // Write the story-ordered segments
            await WriteSrtFileAsync(reorderedSrtPath, reorderedSegments);

            // Clean up memory
            scoredSegments.Clear();
            selectedSegments.Clear();
            GC.Collect();

            // Re-enable the analyze button and update progress
            btnAnalyzeAndEditSrt.Invoke((Action)(() => btnAnalyzeAndEditSrt.Enabled = true));
            progressBarAnalysis.Invoke((Action)(() => progressBarAnalysis.Visible = false));
            progressBarAnalysis.Invoke((Action)(() => {
                progressBarAnalysis.Value = 70;  // Previous steps use 0-70%
            }));

            // STEP 4: Create the final edited video
            try
            {
                // Initialize video utilities with progress tracking (70-100%)
                var videoUtils = new VideoUtils(projectWorkingDir, new Progress<TranscriptionProgress>(p => {
                    int videoProgress = (int)(70 + (p.PercentComplete * 0.3));
                    progressBarAnalysis.Invoke((Action)(() => progressBarAnalysis.Value = videoProgress));
                }));
                
                // Create the final video
                await videoUtils.CreateEditedVideoAsync(reorderedSrtPath);
                
                // Show success message with file locations
                MessageBox.Show(
                    "Analysis and video creation complete!\n\n" +
                    "- Chronological segments in working.edit.srt\n" +
                    "- Story-ordered segments in working.edit.reorder.srt\n" +
                    "- Edited video created in project directory",
                    "Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                // Handle video creation errors
                var errorMessage = ex.Message;
                if (ex.Message.Contains("Source video not found"))
                {
                    errorMessage = $"Video creation failed:\n\n{ex.Message}\n\n" +
                                  "Please ensure all source videos are in the project directory.";
                }
                
                MessageBox.Show(
                    errorMessage,
                    "Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        });
    }
}
