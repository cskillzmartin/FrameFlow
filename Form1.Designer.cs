namespace FrameFlow;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.SplitContainer splitContainer1;
    private System.Windows.Forms.TableLayoutPanel leftTableLayout;
    private System.Windows.Forms.Button btnImportMedia;
    private System.Windows.Forms.MenuStrip menuStrip1;
    private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem newProjectToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem projectToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem openProjectToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem importMediaToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
    private System.Windows.Forms.Label topLabel;
    private System.Windows.Forms.ListView mediaListView;
    private System.Windows.Forms.Label promptLabel;
    private System.Windows.Forms.TextBox promptTextBox;
    private System.Windows.Forms.TableLayoutPanel rightTableLayout;
    private System.Windows.Forms.Button generateButton;
    private System.Windows.Forms.TextBox debugTextBox;
    private NumericUpDown relevanceWeightInput;
    private NumericUpDown sentimentWeightInput;
    private NumericUpDown noveltyWeightInput;
    private NumericUpDown energyWeightInput;
    private TableLayoutPanel weightPanel;
    private NumericUpDown lengthInput;
    // GenAI controls
    private NumericUpDown temperatureInput;
    private NumericUpDown topPInput;
    private NumericUpDown repetitionPenaltyInput;
    private NumericUpDown randomSeedInput;
    private NumericUpDown temporalExpansionInput;
    private Button newSeedButton;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        menuStrip1 = new MenuStrip();
        fileToolStripMenuItem = new ToolStripMenuItem();
        newProjectToolStripMenuItem = new ToolStripMenuItem();
        projectToolStripMenuItem = new ToolStripMenuItem();
        openProjectToolStripMenuItem = new ToolStripMenuItem();
        importMediaToolStripMenuItem = new ToolStripMenuItem();
        settingsToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator1 = new ToolStripSeparator();
        exitToolStripMenuItem = new ToolStripMenuItem();
        helpToolStripMenuItem = new ToolStripMenuItem();
        aboutToolStripMenuItem = new ToolStripMenuItem();
        splitContainer1 = new SplitContainer();
        btnImportMedia = new Button();
        leftTableLayout = new TableLayoutPanel();

        // MenuStrip setup
        menuStrip1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
        splitContainer1.Panel1.SuspendLayout();
        splitContainer1.SuspendLayout();
        SuspendLayout();

        // menuStrip1
        menuStrip1.Items.AddRange(new ToolStripItem[] {
            fileToolStripMenuItem,
            projectToolStripMenuItem,
            helpToolStripMenuItem});
        menuStrip1.Location = new Point(0, 0);
        menuStrip1.Name = "menuStrip1";
        menuStrip1.Size = new Size(800, 24);
        menuStrip1.TabIndex = 0;
        menuStrip1.Text = "menuStrip1";

        // fileToolStripMenuItem
        fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
            newProjectToolStripMenuItem,
            openProjectToolStripMenuItem,
            new ToolStripSeparator(),
            importMediaToolStripMenuItem,
            new ToolStripSeparator(),
            settingsToolStripMenuItem,
            toolStripSeparator1,
            exitToolStripMenuItem});
        fileToolStripMenuItem.Name = "fileToolStripMenuItem";
        fileToolStripMenuItem.Size = new Size(37, 20);
        fileToolStripMenuItem.Text = "&File";

        // newProjectToolStripMenuItem
        newProjectToolStripMenuItem.Name = "newProjectToolStripMenuItem";
        newProjectToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
        newProjectToolStripMenuItem.Size = new Size(180, 22);
        newProjectToolStripMenuItem.Text = "&New Project...";

        // projectToolStripMenuItem
        projectToolStripMenuItem.Name = "projectToolStripMenuItem";
        projectToolStripMenuItem.Size = new Size(61, 20);
        projectToolStripMenuItem.Text = "&Project";

        // openProjectToolStripMenuItem
        openProjectToolStripMenuItem.Name = "openProjectToolStripMenuItem";
        openProjectToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.P;
        openProjectToolStripMenuItem.Size = new Size(180, 22);
        openProjectToolStripMenuItem.Text = "&Open Project...";

        // importMediaToolStripMenuItem
        importMediaToolStripMenuItem.Name = "importMediaToolStripMenuItem";
        importMediaToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
        importMediaToolStripMenuItem.Size = new Size(180, 22);
        importMediaToolStripMenuItem.Text = "&Import Media...";

        // settingsToolStripMenuItem
        settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
        settingsToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
        settingsToolStripMenuItem.Size = new Size(180, 22);
        settingsToolStripMenuItem.Text = "&Settings...";

        // toolStripSeparator1
        toolStripSeparator1.Name = "toolStripSeparator1";
        toolStripSeparator1.Size = new Size(177, 6);

        // exitToolStripMenuItem
        exitToolStripMenuItem.Name = "exitToolStripMenuItem";
        exitToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;
        exitToolStripMenuItem.Size = new Size(180, 22);
        exitToolStripMenuItem.Text = "E&xit";

        // helpToolStripMenuItem
        helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
            aboutToolStripMenuItem});
        helpToolStripMenuItem.Name = "helpToolStripMenuItem";
        helpToolStripMenuItem.Size = new Size(44, 20);
        helpToolStripMenuItem.Text = "&Help";

        // aboutToolStripMenuItem
        aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
        aboutToolStripMenuItem.Size = new Size(180, 22);
        aboutToolStripMenuItem.Text = "&About";

        // splitContainer1 left / right layout
        splitContainer1.Dock = DockStyle.Fill;
        splitContainer1.Location = new Point(0, 24);
        splitContainer1.Name = "splitContainer1";
        splitContainer1.Panel1MinSize = 200;
        splitContainer1.Panel2MinSize = 200;
        splitContainer1.Size = new Size(800, 426);
        splitContainer1.SplitterDistance = 266;
        splitContainer1.TabIndex = 1;

        // Create TableLayoutPanel for left side
        leftTableLayout.Dock = DockStyle.Fill;
        leftTableLayout.ColumnCount = 1;
        leftTableLayout.RowCount = 2;
        
        // Set each row to take up 33.33% of the space
        leftTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 39.5F));
        leftTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60.5F));

        
        // Add the TableLayoutPanel to the left panel
        splitContainer1.Panel1.Controls.Add(leftTableLayout);

        // Create placeholder labels
        topLabel = new Label();
        topLabel.Text = "Holder... 😉";
        topLabel.Dock = DockStyle.Fill;
        topLabel.TextAlign = ContentAlignment.MiddleCenter;

        // Create and setup media ListView
        mediaListView = new ListView();
        mediaListView.Dock = DockStyle.Fill;
        mediaListView.View = View.List;
        mediaListView.FullRowSelect = true;

        // Create a Panel to hold both the button and ListView
        Panel mediaPanel = new Panel();
        mediaPanel.Dock = DockStyle.Fill;

        // Configure Import Media button
        btnImportMedia.Dock = DockStyle.Top;
        btnImportMedia.Height = 30;

        // Add controls in correct order (first added is at the bottom)
        mediaPanel.Controls.Add(mediaListView);    // ListView will be on top
        mediaPanel.Controls.Add(btnImportMedia);   // Button will be at bottom

        // Update the table layout controls
        leftTableLayout.Controls.Add(mediaPanel, 0, 0);
        leftTableLayout.Controls.Add(topLabel, 0, 1);
        
        // btnImportMedia
        btnImportMedia.Location = new Point(12, 12);
        btnImportMedia.Name = "btnImportMedia";
        btnImportMedia.AutoSize = true;
        btnImportMedia.TabIndex = 0;
        btnImportMedia.Text = "Import Media +";
        btnImportMedia.UseVisualStyleBackColor = true;

        // Create right panel layout
        rightTableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };

        // Setup prompt label
        promptLabel = new Label
        {
            Text = "Prompt",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 5)
        };

        // Setup prompt textbox
        promptTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            AcceptsReturn = true,
            AcceptsTab = true,
            Font = new Font("Segoe UI", 10F),
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 10),
            Height = Font.Height * 5
        };

        // Create weight panel
        weightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,  // Changed from 5 to 6 to add the new control
            RowCount = 4,     
            Margin = new Padding(0, 10, 0, 10),
            AutoSize = true
        };

        // Set column widths to be equal
        for (int i = 0; i < 6; i++)  // Changed from 5 to 6
        {
            weightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));  // Changed from 20F to 16.66F
        }

        // Set row heights
        weightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));  // First row labels
        weightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));  // First row controls
        weightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));  // Second row labels
        weightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));  // Second row controls

        // Create weight controls
        relevanceWeightInput = CreateWeightInput("Relevance", 100);
        sentimentWeightInput = CreateWeightInput("Sentiment", 25);
        noveltyWeightInput = CreateWeightInput("Novelty", 25);
        energyWeightInput = CreateWeightInput("Energy", 25);        
        temporalExpansionInput = CreateWeightInput("TemporalExpansion", 5); 

        // Create GenAI controls
        temperatureInput = CreateGenAIInput("Temperature", 0.7m, 0.0m, 2.0m, 0.1m);
        topPInput = CreateGenAIInput("TopP", 0.9m, 0.0m, 1.0m, 0.1m);
        repetitionPenaltyInput = CreateGenAIInput("RepetitionPenalty", 1.1m, 0.0m, 2.0m, 0.1m);
        randomSeedInput = CreateGenAIInput("RandomSeed", 0m, 0m, 9223372036854775807m, 1m);

        // Create length dropdown
        lengthInput = new NumericUpDown
        {
            Name = "lengthInput",
            Minimum = 1,
            Maximum = 100,
            Value = 1,
            DecimalPlaces = 0,
            Width = 60,
            Dock = DockStyle.Fill,
            Margin = new Padding(5)
        };

        // Create new seed button
        newSeedButton = CreateControlButton("New Seed");

        // Add first row labels (weights)
        weightPanel.Controls.Add(CreateWeightLabel("Relevance"), 0, 0);
        weightPanel.Controls.Add(CreateWeightLabel("Sentiment"), 1, 0);
        weightPanel.Controls.Add(CreateWeightLabel("Novelty"), 2, 0);
        weightPanel.Controls.Add(CreateWeightLabel("Energy"), 3, 0);
        weightPanel.Controls.Add(CreateWeightLabel("Length"), 4, 0);
        weightPanel.Controls.Add(CreateWeightLabel("TempExp"), 5, 0);  // Add new label

        // Add first row controls (weights)
        weightPanel.Controls.Add(relevanceWeightInput, 0, 1);
        weightPanel.Controls.Add(sentimentWeightInput, 1, 1);
        weightPanel.Controls.Add(noveltyWeightInput, 2, 1);
        weightPanel.Controls.Add(energyWeightInput, 3, 1);
        weightPanel.Controls.Add(lengthInput, 4, 1);
        weightPanel.Controls.Add(temporalExpansionInput, 5, 1);  // Add new control

        // Add second row labels (GenAI)
        weightPanel.Controls.Add(CreateWeightLabel("Temperature"), 0, 2);
        weightPanel.Controls.Add(CreateWeightLabel("TopP"), 1, 2);
        weightPanel.Controls.Add(CreateWeightLabel("RepPenalty"), 2, 2);
        weightPanel.Controls.Add(CreateWeightLabel("RandSeed"), 3, 2);

        // Add second row controls (GenAI)
        weightPanel.Controls.Add(temperatureInput, 0, 3);
        weightPanel.Controls.Add(topPInput, 1, 3);
        weightPanel.Controls.Add(repetitionPenaltyInput, 2, 3);
        weightPanel.Controls.Add(randomSeedInput, 3, 3);
        weightPanel.Controls.Add(newSeedButton, 4, 3);

        // Create generate button
        generateButton = new Button
        {
            Text = "Generate",
            Dock = DockStyle.Fill,
            Height = 35,
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 10)
        };

        // Create debug textbox
        debugTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            AcceptsReturn = true,
            AcceptsTab = true,
            Font = new Font("Consolas", 9F),
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true
        };

        // Add all controls to right layout
        rightTableLayout.Controls.Add(promptLabel, 0, 0);
        rightTableLayout.Controls.Add(promptTextBox, 0, 1);
        rightTableLayout.Controls.Add(weightPanel, 0, 2);
        rightTableLayout.Controls.Add(generateButton, 0, 3);
        rightTableLayout.Controls.Add(debugTextBox, 0, 4);

        // Add right layout to split container
        splitContainer1.Panel2.Controls.Add(rightTableLayout);

        // Form1
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 450);
        Controls.Add(splitContainer1);
        Controls.Add(menuStrip1);
        MainMenuStrip = menuStrip1;
        Name = "Form1";
        Text = "FrameFlow";
        menuStrip1.ResumeLayout(false);
        menuStrip1.PerformLayout();
        splitContainer1.Panel1.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
        splitContainer1.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    // Add these helper methods to Form1:
    private NumericUpDown CreateWeightInput(string name, int defaultValue)
    {
        return new NumericUpDown
        {
            Name = $"{name.ToLower()}WeightInput",
            Minimum = 0,
            Maximum = 100,
            Value = defaultValue,
            DecimalPlaces = 0,
            Width = 60,
            Dock = DockStyle.Fill,
            Margin = new Padding(5)
        };
    }

    private Label CreateWeightLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            TextAlign = ContentAlignment.BottomCenter,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 2)
        };
    }

    private NumericUpDown CreateGenAIInput(string name, decimal defaultValue, decimal min, decimal max, decimal increment)
    {
        return new NumericUpDown
        {
            Name = $"{name.ToLower()}Input",
            Minimum = min,
            Maximum = max,
            Value = defaultValue,
            DecimalPlaces = 1,
            Width = 60,
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            Increment = increment
        };
    }

    private Button CreateControlButton(string text)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control,
            ForeColor = SystemColors.ControlText,
            Font = DefaultFont
        };
    }
}
