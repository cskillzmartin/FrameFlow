using System.Data;
using System.Text.Json;
using FrameFlow.Models;

namespace FrameFlow.Forms;

public partial class StorySettingsViewer : BaseForm
{
    private readonly Form1 _mainForm;
    private readonly DataGridView _gridView;
    private readonly List<(string FilePath, StorySettings Settings)> _settings = new();

    public StorySettingsViewer(Form1 mainForm)
    {
        _mainForm = mainForm;
        
        // Basic form setup
        Text = "Story Settings";
        Size = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterParent;

        // Create and configure the DataGridView
        _gridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ShowCellToolTips = true,
            BackgroundColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(60, 60, 60),
            BorderStyle = BorderStyle.None
        };

        // Configure grid view styles
        _gridView.EnableHeadersVisualStyles = false;
        _gridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 45);
        _gridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _gridView.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
        _gridView.DefaultCellStyle.ForeColor = Color.White;
        _gridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
        _gridView.DefaultCellStyle.SelectionForeColor = Color.White;
        _gridView.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 45);
        _gridView.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35);

        // Add cell formatting handler for tooltips
        _gridView.CellFormatting += GridView_CellFormatting;
        _gridView.CellMouseEnter += GridView_CellMouseEnter;

        // Create button panel
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(45, 45, 45)
        };

        // Create Load Settings button
        var loadButton = new Button
        {
            Text = "Load Settings",
            Dock = DockStyle.Right,
            Width = 120,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        loadButton.FlatAppearance.BorderSize = 0;
        loadButton.Click += LoadButton_Click;

        // Add button to panel
        buttonPanel.Controls.Add(loadButton);

        // Setup layout
        _gridView.Dock = DockStyle.Fill;
        Controls.Add(_gridView);
        Controls.Add(buttonPanel);

        // Add click handler
        _gridView.CellClick += GridView_CellClick;

        // Load settings files
        LoadStorySettings();
    }

    private void LoadButton_Click(object? sender, EventArgs e)
    {
        LoadSelectedSettings();
    }

    private void GridView_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            _gridView.Rows[e.RowIndex].Selected = true;
        }
    }

    private void LoadSelectedSettings()
    {
        if (_gridView.SelectedRows.Count == 0) return;
        
        var selectedIndex = _gridView.SelectedRows[0].Index;
        if (selectedIndex < 0 || selectedIndex >= _settings.Count) return;

        var settings = _settings[selectedIndex].Settings;
        
        // Update main form controls
        _mainForm.LoadStorySettings(settings);
        
        // Close the viewer
        DialogResult = DialogResult.OK;
        Close();
    }

    private void LoadStorySettings()
    {
        _settings.Clear();

        // Get the project's renders directory
        var rendersPath = Path.Combine(App.ProjectHandler.Instance.CurrentProjectPath, "Renders");
        if (!Directory.Exists(rendersPath)) return;

        // Find all story_settings.json files in render directories
        foreach (var renderDir in Directory.GetDirectories(rendersPath))
        {
            var settingsFile = Path.Combine(renderDir, "story_settings.json");
            if (!File.Exists(settingsFile)) continue;

            try
            {
                var json = File.ReadAllText(settingsFile);
                var settings = JsonSerializer.Deserialize<StorySettings>(json);
                if (settings != null)
                {
                    _settings.Add((settingsFile, settings));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings from {settingsFile}: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Create DataTable for the grid
        var dt = new DataTable();
        dt.Columns.Add("Render #", typeof(string));
        dt.Columns.Add("Prompt", typeof(string));
        dt.Columns.Add("Length", typeof(int));
        dt.Columns.Add("Relevance", typeof(float));
        dt.Columns.Add("Sentiment", typeof(float));
        dt.Columns.Add("Novelty", typeof(float));
        dt.Columns.Add("Energy", typeof(float));
        dt.Columns.Add("TempExp", typeof(int));
        dt.Columns.Add("Temperature", typeof(float));
        dt.Columns.Add("TopP", typeof(float));
        dt.Columns.Add("RepPenalty", typeof(float));
        dt.Columns.Add("Seed", typeof(long));

        // Add rows
        foreach (var (filePath, settings) in _settings)
        {
            var renderNum = Path.GetFileName(Path.GetDirectoryName(filePath));
            dt.Rows.Add(
                renderNum,
                settings.Prompt,
                settings.Length,
                settings.Relevance,
                settings.Sentiment,
                settings.Novelty,
                settings.Energy,
                settings.TemporalExpansion,
                settings.GenAISettings.Temperature,
                settings.GenAISettings.TopP,
                settings.GenAISettings.RepetitionPenalty,
                settings.GenAISettings.RandomSeed
            );
        }

        _gridView.DataSource = dt;

        // Auto-size the prompt column differently
        if (_gridView.Columns.Count > 1)
        {
            _gridView.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            foreach (DataGridViewColumn col in _gridView.Columns)
            {
                if (col.Index != 1) // Not the prompt column
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                }
            }
        }
    }

    private void GridView_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value != null)
        {
            var cell = _gridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.ToolTipText = e.Value.ToString();
        }
    }

    private void GridView_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
        {
            var cell = _gridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (cell.Value != null)
            {
                // Update tooltip text in case it changed
                cell.ToolTipText = cell.Value.ToString();
            }
        }
    }
} 