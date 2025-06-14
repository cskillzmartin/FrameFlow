using FrameFlow.Forms;
using FrameFlow.Utilities;
using FrameFlow.Models;
using System.Text.Json;

namespace FrameFlow;

public partial class Form1 : BaseForm
{
    private const string DEFAULT_TITLE = "FrameFlow";
    private bool _isModelLoading = false;
    private ToolTip weightTooltip;
    
    public Form1()
    {
        components = new System.ComponentModel.Container();
        InitializeComponent();
        var rnd = new Random().NextInt64();

        // Set random seed value        
        randomSeedInput.Value = rnd;

        // Subscribe to ProjectHandler events
        App.ProjectHandler.Instance.ProjectOpened += ProjectHandler_ProjectStateChanged;
        App.ProjectHandler.Instance.ProjectClosed += ProjectHandler_ProjectStateChanged;

        // Wire up event handlers
        newProjectToolStripMenuItem.Click += NewProject_Click;
        openProjectToolStripMenuItem.Click += OpenProject_Click;
        importMediaToolStripMenuItem.Click += ImportMedia_Click;
        projectToolStripMenuItem.Click += Project_Click;
        btnImportMedia.Click += ImportMedia_Click;
        settingsToolStripMenuItem.Click += Settings_Click;
        exitToolStripMenuItem.Click += Exit_Click;
        aboutToolStripMenuItem.Click += About_Click;
        mediaListView.MouseClick += MediaListView_Click;
        generateButton.Click += GenerateButton_Click;

        // Enable drag and drop for the ListView
        mediaListView.AllowDrop = true;
        mediaListView.DragEnter += MediaListView_DragEnter;
        mediaListView.DragDrop += MediaListView_DragDrop;

        // Add handler for text changes
        promptTextBox.TextChanged += PromptTextBox_TextChanged;

        // Load model asynchronously
        LoadModelAsync();

        // Initialize tooltips
        weightTooltip = new ToolTip(components);
        weightTooltip.AutoPopDelay = 5000;
        weightTooltip.InitialDelay = 500;
        weightTooltip.ReshowDelay = 100;

        // Set tooltips for weight controls
        weightTooltip.SetToolTip(relevanceWeightInput, "How closely the content matches your prompt (0-100)");
        weightTooltip.SetToolTip(sentimentWeightInput, "Emotional tone of the content (0-100)");
        weightTooltip.SetToolTip(noveltyWeightInput, "How unique or surprising the content is (0-100)");
        weightTooltip.SetToolTip(energyWeightInput, "Energy level and intensity of the content (0-100)");
        weightTooltip.SetToolTip(lengthInput, "Target length of the final video in minutes");

        // Set tooltips for GenAI controls
        weightTooltip.SetToolTip(temperatureInput, "Controls randomness in generation (0.0-2.0, higher = more random)");
        weightTooltip.SetToolTip(topPInput, "Controls diversity of token selection (0.0-1.0)");
        weightTooltip.SetToolTip(repetitionPenaltyInput, "Penalizes repetition of tokens (1.0-2.0, higher = less repetition)");
        weightTooltip.SetToolTip(randomSeedInput, "Seed for reproducible generation (0-999999, 0 = random)");
    }

    private async Task LoadModelAsync()
    {
        try
        {
            _isModelLoading = true;
            generateButton.Enabled = false;
            generateButton.Text = "Loading Model...";

            // Create model directories if they don't exist
            Directory.CreateDirectory(App.Settings.Instance.OnnxCpuModelDirectory);
            Directory.CreateDirectory(App.Settings.Instance.OnnxCudaModelDirectory);
            Directory.CreateDirectory(App.Settings.Instance.OnnxDirectMLModelDirectory);

            // Initialize the model
            await Task.Run(() => GenAIManager.Instance);

            if (GenAIManager.Instance.IsModelLoaded)
            {
                generateButton.Text = "Generate";
                generateButton.Enabled = true;
            }
            else
            {
                debugTextBox.AppendText("Model Not Found");
                debugTextBox.AppendText($"No model files found. Please add model files to one of these directories:\n" +
                $"CUDA: {App.Settings.Instance.OnnxCudaModelDirectory}\n" +
                $"DirectML: {App.Settings.Instance.OnnxDirectMLModelDirectory}\n" +
                $"CPU: {App.Settings.Instance.OnnxCpuModelDirectory}");
                debugTextBox.AppendText("----------------------------------------\r\n");
            }
        }
        catch (Exception ex)
        {
            debugTextBox.AppendText("Model Load Failed");
            debugTextBox.AppendText($"Error: {ex.Message}\r\n");
            debugTextBox.AppendText("----------------------------------------\r\n");
        }
        finally
        {
            _isModelLoading = false;
        }
    }

    private async void GenerateButton_Click(object? sender, EventArgs e)
    {
        try
        {
            // Check if model is loaded first
            if (!GenAIManager.Instance.IsModelLoaded)
            {
                MessageBox.Show("Model not loaded. Please check model settings.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Check if we have an open project
            if (App.ProjectHandler.Instance.CurrentProject == null)
            {
                MessageBox.Show("Please open a project first.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Get the prompt text
            string prompt = promptTextBox.Text.Trim();
            if (string.IsNullOrEmpty(prompt))
            {
                MessageBox.Show("Please enter a prompt first.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedLength = int.Parse(lengthInput.Value.ToString());
            // Disable UI elements
            generateButton.Enabled = false;
            generateButton.ForeColor = Color.White; //keep it readable
            generateButton.Text = "Analyzing...";
            promptTextBox.Enabled = false;
            lengthInput.Enabled = false;
            relevanceWeightInput.Enabled = false;
            sentimentWeightInput.Enabled = false;
            noveltyWeightInput.Enabled = false;
            energyWeightInput.Enabled = false;
            btnImportMedia.Enabled = false;
            temperatureInput.Enabled = false;
            topPInput.Enabled = false;
            repetitionPenaltyInput.Enabled = false;
            randomSeedInput.Enabled = false;

            //Save the story settings to a file
            var storySettings = new StorySettings();
            storySettings.Prompt = prompt;
            storySettings.Length = selectedLength;
            storySettings.Relevance = (float)relevanceWeightInput.Value;
            storySettings.Sentiment = (float)sentimentWeightInput.Value;
            storySettings.Novelty = (float)noveltyWeightInput.Value;
            storySettings.Energy = (float)energyWeightInput.Value;
            storySettings.GenAISettings.Temperature = (float)temperatureInput.Value;
            storySettings.GenAISettings.TopP = (float)topPInput.Value;
            storySettings.GenAISettings.RepetitionPenalty = (float)repetitionPenaltyInput.Value;
            storySettings.GenAISettings.RandomSeed = (long)randomSeedInput.Value;

            // Create new render directory
            var renderDir = GetNextRenderDirectory();
            
            // Save the story settings to the new render directory
            var storySettingsFile = Path.Combine(renderDir, "story_settings.json");
            File.WriteAllText(storySettingsFile, JsonSerializer.Serialize(storySettings));
            debugTextBox.AppendText($"Story settings saved to {storySettingsFile}\r\n");

            try
            {
                await Task.Run(async () =>
                {
                    // Step 1: Ranking transcripts
                    this.Invoke(() => {
                        debugTextBox.AppendText("Step 1/4: Analyzing transcripts...\r\n");
                        generateButton.Text = "Analyzing (1/4)";
                    });
                    await StoryManager.Instance.RankProjectTranscriptsAsync(
                        App.ProjectHandler.Instance.CurrentProject,
                        storySettings,
                        renderDir  // Pass the render directory
                    );

                    // Step 2: Ranking order
                    this.Invoke(() => {
                        debugTextBox.AppendText("Step 2/4: Ranking segments...\r\n");
                        generateButton.Text = "Ranking (2/4)";
                    });
                    await StoryManager.Instance.RankOrder(
                        App.ProjectHandler.Instance.CurrentProject.Name,
                        new StoryManager.RankingWeights(
                            (float)relevanceWeightInput.Value,
                            (float)sentimentWeightInput.Value,
                            (float)noveltyWeightInput.Value,
                            (float)energyWeightInput.Value
                        ),
                        renderDir  // Pass the render directory
                    );

                    // Step 3: Trimming to length
                    this.Invoke(() => {
                        debugTextBox.AppendText("Step 3/4: Trimming to length...\r\n");
                        generateButton.Text = "Trimming (3/4)";
                    });
                    await StoryManager.Instance.TrimRankOrder(
                        App.ProjectHandler.Instance.CurrentProject.Name,
                        selectedLength,
                        renderDir  // Pass the render directory
                    );

                    // Step 4: Rendering video
                    this.Invoke(() => {
                        debugTextBox.AppendText("Step 4/4: Rendering final video...\r\n");
                        generateButton.Text = "Rendering (4/4)";
                    });
                    await RenderManager.Instance.RenderVideoAsync(
                        App.ProjectHandler.Instance.CurrentProject.Name,
                        Path.Combine(renderDir, $"{App.ProjectHandler.Instance.CurrentProject.Name}.mp4"),
                        renderDir  // Pass the render directory
                    );

                    this.Invoke(() => {
                        debugTextBox.AppendText("✓ Video generation complete!\r\n");
                        debugTextBox.AppendText("----------------------------------------\r\n");
                    });
                });
            }
            catch (Exception ex)
            {
                debugTextBox.AppendText($"Error: {ex.Message}\r\n");
                debugTextBox.AppendText("----------------------------------------\r\n");
            }
        }
        catch (Exception ex)
        {
            debugTextBox.AppendText($"ex: {ex.Message}\r\n");
            debugTextBox.AppendText("----------------------------------------\r\n");
        }
        finally
        {
            // Re-enable UI elements
            generateButton.Enabled = true;
            promptTextBox.Enabled = true;
            generateButton.Text = "Generate";
            promptTextBox.Enabled = true;
            lengthInput.Enabled = true;
            relevanceWeightInput.Enabled = true;
            sentimentWeightInput.Enabled = true;
            noveltyWeightInput.Enabled = true;
            energyWeightInput.Enabled = true;
            btnImportMedia.Enabled = true;
            temperatureInput.Enabled = true;
            topPInput.Enabled = true;
            repetitionPenaltyInput.Enabled = true;
            randomSeedInput.Enabled = true;
        }
    }

    private void ProjectHandler_ProjectStateChanged(object? sender, EventArgs e)
    {
        UpdateFormTitle();
        UpdateMediaList();
    }

    private void UpdateFormTitle()
    {
        string projectPath = App.ProjectHandler.Instance.CurrentProjectPath;
        Text = string.IsNullOrEmpty(projectPath) 
            ? DEFAULT_TITLE 
            : $"{DEFAULT_TITLE} - {Path.GetFileName(projectPath)}";
    }

    private void NewProject_Click(object? sender, EventArgs e)
    {
        using (var folderDialog = new FolderBrowserDialog())
        {
            folderDialog.Description = "Select Project Location";
            folderDialog.InitialDirectory = App.Settings.Instance.DefaultProjectLocation;
            folderDialog.UseDescriptionForTitle = true;

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                string projectPath = folderDialog.SelectedPath;
                if (!App.ProjectHandler.Instance.CreateProject(projectPath))
                {
                    MessageBox.Show("Failed to create project.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private void OpenProject_Click(object? sender, EventArgs e)
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "FrameFlow Projects (*.ffproj)|*.ffproj|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Title = "Open Project";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string projectPath = Path.GetDirectoryName(openFileDialog.FileName)!;
                
                if (!App.ProjectHandler.Instance.OpenProject(projectPath))
                {
                    MessageBox.Show("Failed to open project.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private async void ImportMedia_Click(object? sender, EventArgs e)
    {
        // Check if we have an open project
        if (string.IsNullOrEmpty(App.ProjectHandler.Instance.CurrentProjectPath))
        {
            var result = MessageBox.Show(
                "No project is currently open. Would you like to create a new project?",
                "Create Project",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                NewProject_Click(sender, e);
                // If project creation was cancelled or failed, return
                if (string.IsNullOrEmpty(App.ProjectHandler.Instance.CurrentProjectPath))
                    return;
            }
            else
            {
                return;
            }
        }

        // At this point we should have an open project
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "Video Files|" + string.Join(";", 
                App.Settings.Instance.SupportedVideoFormats.Select(x => $"*{x}"));
            openFileDialog.Multiselect = true;
            openFileDialog.Title = "Import Media Files";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                btnImportMedia.Enabled = false;
                try
                {
                    foreach (string file in openFileDialog.FileNames)
                    {
                        debugTextBox.AppendText($"Importing {Path.GetFileName(file)}...\r\n");

                        if (!await App.ProjectHandler.Instance.AddMediaToProject(file))
                        {
                            debugTextBox.AppendText($"❌ Failed to import {Path.GetFileName(file)}\r\n");
                            continue;
                        }

                        if (App.Settings.Instance.AutoAnalyzeOnImport)
                        {
                            debugTextBox.AppendText($"Extracting audio from {Path.GetFileName(file)}...\r\n");
                            var audioFile = await App.ProjectHandler.Instance.ExtractAudio(file);
                            debugTextBox.AppendText($"✓ Successfully extracted audio from {Path.GetFileName(file)}\r\n");
                            debugTextBox.AppendText($"Transcribing audio (this may take a few minutes)...\r\n");
                            await App.ProjectHandler.Instance.TranscribeAudio(file, audioFile);
                            debugTextBox.AppendText($"✓ Successfully transcribed {Path.GetFileName(file)}\r\n");

                        }

                        debugTextBox.AppendText($"✓ Successfully imported {Path.GetFileName(file)}\r\n");
                    }
                    debugTextBox.AppendText("----------------------------------------\r\n");

                    // Update the ListView after importing files
                    UpdateMediaList();
                }
                finally{
                    btnImportMedia.Enabled = true;
                }
            }
        }
    }

    private void Settings_Click(object? sender, EventArgs e)
    {
        using (var settingsDialog = new SettingsDialog())
        {
            if (settingsDialog.ShowDialog(this) == DialogResult.OK)
            {
                // Apply theme if dark mode setting changed
                App.ThemeManager.ApplyTheme(this, _settings.DarkMode);
            }
        }
    }

    private void Exit_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void About_Click(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "FrameFlow\nVersion 1.0\n\n© 2024 FrameFlow",
            "About FrameFlow",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void PromptTextBox_TextChanged(object? sender, EventArgs e)
    {
        // Here you can add logic to handle prompt text changes
        // For example, saving to the project file or updating other UI elements
    }

    private void UpdateMediaList()
    {
        mediaListView.Items.Clear();

        if (App.ProjectHandler.Instance.CurrentProject?.MediaFiles != null)
        {
            foreach (var mediaFile in App.ProjectHandler.Instance.CurrentProject.MediaFiles)
            {
                mediaListView.Items.Add($"🗑️ {mediaFile.FileName}");
            }
        }

        // Clear the prompt text when no project is open
        if (string.IsNullOrEmpty(App.ProjectHandler.Instance.CurrentProjectPath))
        {
            promptTextBox.Text = string.Empty;
            promptTextBox.Enabled = false;
        }
        else
        {
            promptTextBox.Enabled = true;
            // Here you can load the saved prompt from your project if you have one
        }
    }

    private void MediaListView_Click(object sender, MouseEventArgs e)
    {
        // Get the clicked item
        ListViewItem? item = mediaListView.GetItemAt(e.X, e.Y);
        if (item == null) return;

        // Check if click was on the emoji (first ~20 pixels)
        if (e.X <= 20)
        {
            string fileName = item.Text.Substring(3).Trim(); // Remove emoji and space
            var result = MessageBox.Show(
                $"Are you sure you want to delete '{fileName}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (App.ProjectHandler.Instance.RemoveMediaFromProject(fileName))
                {
                    UpdateMediaList();
                }
                else
                {
                    MessageBox.Show("Failed to delete media file.", 
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private void MediaListView_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Any(file => App.Settings.Instance.SupportedVideoFormats.Any(format => 
                file.EndsWith(format, StringComparison.OrdinalIgnoreCase))))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }
    }

    private async void MediaListView_DragDrop(object sender, DragEventArgs e)
    {
        // Check if we have an open project
        if (string.IsNullOrEmpty(App.ProjectHandler.Instance.CurrentProjectPath))
        {
            var result = MessageBox.Show(
                "No project is currently open. Would you like to create a new project?",
                "Create Project",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                NewProject_Click(this, EventArgs.Empty);
                // If project creation was cancelled or failed, return
                if (string.IsNullOrEmpty(App.ProjectHandler.Instance.CurrentProjectPath))
                    return;
            }
            else
            {
                return;
            }
        }

        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            btnImportMedia.Enabled = false;
            try
            {
                foreach (string file in files)
                {
                    // Check if the file is a supported video format
                    if (App.Settings.Instance.SupportedVideoFormats.Any(format => 
                        file.EndsWith(format, StringComparison.OrdinalIgnoreCase)))
                    {
                        debugTextBox.AppendText($"Importing {Path.GetFileName(file)}...\r\n");

                        if (!await App.ProjectHandler.Instance.AddMediaToProject(file))
                        {
                            debugTextBox.AppendText($"❌ Failed to import {Path.GetFileName(file)}\r\n");
                            continue;
                        }

                        if (App.Settings.Instance.AutoAnalyzeOnImport)
                        {
                            debugTextBox.AppendText($"Extracting audio from {Path.GetFileName(file)}...\r\n");
                            var audioFile = await App.ProjectHandler.Instance.ExtractAudio(file);
                            debugTextBox.AppendText($"✓ Successfully extracted audio from {Path.GetFileName(file)}\r\n");
                            debugTextBox.AppendText($"Transcribing audio (this may take a few minutes)...\r\n");
                            await App.ProjectHandler.Instance.TranscribeAudio(file, audioFile);
                            debugTextBox.AppendText($"✓ Successfully transcribed {Path.GetFileName(file)}\r\n");
                        }

                        debugTextBox.AppendText($"✓ Successfully imported {Path.GetFileName(file)}\r\n");
                    }   
                }
                debugTextBox.AppendText("----------------------------------------\r\n");

                // Update the ListView after importing files
                UpdateMediaList();
            }
            finally
            {
                btnImportMedia.Enabled = true;
            }
        }
    }

    private void Project_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(App.ProjectHandler.Instance.CurrentProjectPath) && 
            Directory.Exists(App.ProjectHandler.Instance.CurrentProjectPath))
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", App.ProjectHandler.Instance.CurrentProjectPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open project directory: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            MessageBox.Show("No project is currently open.", "Information",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    public async Task ReloadModelAsync()
    {
        if (_isModelLoading) return;
        await LoadModelAsync();
    }

    private string GetNextRenderDirectory()
    {
        var rendersPath = Path.Combine(App.ProjectHandler.Instance.CurrentProjectPath, "Renders");
        
        // Get all existing numbered directories
        var directories = Directory.GetDirectories(rendersPath)
            .Select(d => Path.GetFileName(d))
            .Where(name => int.TryParse(name, out _))
            .Select(name => int.Parse(name))
            .ToList();
        
        // Get next number (1 if no directories exist)
        int nextNumber = directories.Any() ? directories.Max() + 1 : 1;
        
        // Create the new directory
        var newRenderDir = Path.Combine(rendersPath, nextNumber.ToString());
        Directory.CreateDirectory(newRenderDir);
        
        return newRenderDir;
    }
}
