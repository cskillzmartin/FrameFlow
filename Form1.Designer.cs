namespace FrameFlow;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

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
        this.leftPanel = new System.Windows.Forms.Panel();
        this.centerPanel = new System.Windows.Forms.Panel();
        this.rightPanel = new System.Windows.Forms.Panel();
        this.importMediaButton = new System.Windows.Forms.Button();
        this.mediaListBox = new System.Windows.Forms.ListBox();
        this.panelTranscriptionProgress = new System.Windows.Forms.Panel();
        this.subjectLabel = new System.Windows.Forms.Label();
        this.subjectTextBox = new System.Windows.Forms.TextBox();
        this.durationComboBox = new System.Windows.Forms.ComboBox();
        this.customDurationComboBox = new System.Windows.Forms.ComboBox();
        this.lengthLabel = new System.Windows.Forms.Label();
        this.minLabel = new System.Windows.Forms.Label();
        this.btnAnalyzeAndEditSrt = new System.Windows.Forms.Button();
        this.progressBarAnalysis = new System.Windows.Forms.ProgressBar();
        this.mainToolStrip = new System.Windows.Forms.ToolStrip();
        this.btnNewProject = new System.Windows.Forms.ToolStripButton();
        this.btnOpenProject = new System.Windows.Forms.ToolStripButton();
        this.btnSettings = new System.Windows.Forms.ToolStripButton();
        this.SuspendLayout();

        // 
        // mainToolStrip
        // 
        this.mainToolStrip.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
        this.mainToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnNewProject,
            this.btnOpenProject,
            this.btnSettings
        });
        this.mainToolStrip.Location = new System.Drawing.Point(0, 0);
        this.mainToolStrip.Name = "mainToolStrip";
        this.mainToolStrip.Size = new System.Drawing.Size(800, 25);
        this.mainToolStrip.TabIndex = 0;
        this.mainToolStrip.ForeColor = System.Drawing.Color.White;

        // 
        // btnNewProject
        // 
        this.btnNewProject.Text = "New Project";
        this.btnNewProject.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnNewProject.ForeColor = System.Drawing.Color.White;

        // 
        // btnOpenProject
        // 
        this.btnOpenProject.Text = "Open Project";
        this.btnOpenProject.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnOpenProject.ForeColor = System.Drawing.Color.White;

        // 
        // btnSettings
        // 
        this.btnSettings.Text = "Settings";
        this.btnSettings.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnSettings.ForeColor = System.Drawing.Color.White;

        // 
        // leftPanel
        // 
        this.leftPanel.Dock = System.Windows.Forms.DockStyle.Left;
        this.leftPanel.Location = new System.Drawing.Point(0, 25);
        this.leftPanel.Name = "leftPanel";
        this.leftPanel.Size = new System.Drawing.Size(200, 425);
        this.leftPanel.TabIndex = 0;
        this.leftPanel.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
        // Adjust button locations and stack them vertically in leftPanel
        int buttonWidth = 180;
        int buttonHeight = 40;
        int buttonSpacing = 10;
        int y = 10;
        this.importMediaButton.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
        this.importMediaButton.Location = new System.Drawing.Point(10, y);
        y += buttonHeight + buttonSpacing;
        this.btnAnalyzeAndEditSrt.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
        this.btnAnalyzeAndEditSrt.Location = new System.Drawing.Point(10, y);
        y += buttonHeight + buttonSpacing;
        // Move mediaListBox below all buttons
        this.mediaListBox.Size = new System.Drawing.Size(buttonWidth, 350);
        this.mediaListBox.Location = new System.Drawing.Point(10, y);
        // Add all buttons to leftPanel
        this.leftPanel.Controls.Add(this.importMediaButton);
        this.leftPanel.Controls.Add(this.btnAnalyzeAndEditSrt);
        this.leftPanel.Controls.Add(this.mediaListBox);
        this.leftPanel.Controls.Add(this.panelTranscriptionProgress);
        // 
        // centerPanel
        // 
        this.centerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this.centerPanel.Location = new System.Drawing.Point(200, 25);
        this.centerPanel.Name = "centerPanel";
        this.centerPanel.Size = new System.Drawing.Size(400, 425);
        this.centerPanel.TabIndex = 1;
        this.centerPanel.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
        this.centerPanel.Controls.Add(this.panelTranscriptionProgress);
        this.centerPanel.Controls.Add(this.subjectLabel);
        this.centerPanel.Controls.Add(this.subjectTextBox);
        this.centerPanel.Controls.Add(this.durationComboBox);
        this.centerPanel.Controls.Add(this.customDurationComboBox);
        this.centerPanel.Controls.Add(this.lengthLabel);
        this.centerPanel.Controls.Add(this.minLabel);
        // 
        // rightPanel
        // 
        this.rightPanel.Dock = System.Windows.Forms.DockStyle.Right;
        this.rightPanel.Location = new System.Drawing.Point(600, 25);
        this.rightPanel.Name = "rightPanel";
        this.rightPanel.Size = new System.Drawing.Size(200, 425);
        this.rightPanel.TabIndex = 2;
        this.rightPanel.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
        // 
        // panelTranscriptionProgress
        // 
        this.panelTranscriptionProgress.Name = "panelTranscriptionProgress";
        this.panelTranscriptionProgress.Size = this.mediaListBox.Size;
        this.panelTranscriptionProgress.Location = this.mediaListBox.Location;
        this.panelTranscriptionProgress.BackColor = System.Drawing.Color.FromArgb(180, 40, 40, 40);
        this.panelTranscriptionProgress.Visible = false;
        this.panelTranscriptionProgress.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        this.leftPanel.Controls.Add(this.panelTranscriptionProgress);
        this.panelTranscriptionProgress.BringToFront();
        // 
        // subjectLabel
        // 
        this.subjectLabel.Text = "Subject:";
        this.subjectLabel.ForeColor = System.Drawing.Color.White;
        this.subjectLabel.Location = new System.Drawing.Point(20, 20);
        this.subjectLabel.Size = new System.Drawing.Size(100, 20);
        // 
        // subjectTextBox
        // 
        this.subjectTextBox.Multiline = true;
        this.subjectTextBox.Location = new System.Drawing.Point(20, 45);
        this.subjectTextBox.Size = new System.Drawing.Size(360, 60);
        this.subjectTextBox.BackColor = System.Drawing.Color.FromArgb(60, 60, 60);
        this.subjectTextBox.ForeColor = System.Drawing.Color.White;
        // 
        // durationComboBox
        // 
        this.durationComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.durationComboBox.Items.AddRange(new object[] { "<= 60 Seconds", "<=10 Min", "<=20 Min", "Custom" });
        this.durationComboBox.Location = new System.Drawing.Point(20, 140);
        this.durationComboBox.Size = new System.Drawing.Size(180, 23);
        this.durationComboBox.SelectedIndex = 0;
        this.durationComboBox.SelectedIndexChanged += new System.EventHandler(this.durationComboBox_SelectedIndexChanged);
        // 
        // customDurationComboBox
        // 
        this.customDurationComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        for (int i = 1; i <= 100; i++) this.customDurationComboBox.Items.Add(i.ToString());
        this.customDurationComboBox.Location = new System.Drawing.Point(220, 140);
        this.customDurationComboBox.Size = new System.Drawing.Size(80, 23);
        this.customDurationComboBox.Visible = false;
        // 
        // lengthLabel
        // 
        this.lengthLabel.Text = "Length:";
        this.lengthLabel.ForeColor = System.Drawing.Color.White;
        this.lengthLabel.Location = new System.Drawing.Point(20, 120);
        this.lengthLabel.Size = new System.Drawing.Size(100, 20);
        // 
        // minLabel
        // 
        this.minLabel.Text = "Min";
        this.minLabel.ForeColor = System.Drawing.Color.White;
        this.minLabel.Location = new System.Drawing.Point(220, 120);
        this.minLabel.Size = new System.Drawing.Size(80, 20);
        this.minLabel.Visible = false;
        // 
        // Form1
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(800, 450);
        this.Controls.Add(this.centerPanel);
        this.Controls.Add(this.rightPanel);
        this.Controls.Add(this.leftPanel);
        this.Controls.Add(this.mainToolStrip);
        this.Controls.Add(this.progressBarAnalysis);
        this.Name = "Form1";
        this.Text = "FrameFlow";
        // Fix button text visibility and responsive layout
        this.btnAnalyzeAndEditSrt.AutoSize = false;
        this.btnAnalyzeAndEditSrt.UseVisualStyleBackColor = true;
        // Adjust mediaListBox height to fit within leftPanel
        this.mediaListBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;
        this.mediaListBox.Height = this.leftPanel.Height - (this.btnAnalyzeAndEditSrt.Location.Y + this.btnAnalyzeAndEditSrt.Height + 20);
        // Optionally, handle leftPanel resize to keep mediaListBox within bounds
        this.leftPanel.Resize += (s, e) => {
            this.mediaListBox.Height = this.leftPanel.Height - (this.btnAnalyzeAndEditSrt.Location.Y + this.btnAnalyzeAndEditSrt.Height + 20);
        };
        // Style all project action buttons for dark background
        var buttonBackColor = System.Drawing.Color.FromArgb(45, 45, 45);
        var buttonForeColor = System.Drawing.Color.White;
        var buttonFont = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        this.importMediaButton.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
        this.importMediaButton.BackColor = buttonBackColor;
        this.importMediaButton.ForeColor = buttonForeColor;
        this.importMediaButton.Font = buttonFont;
        this.btnAnalyzeAndEditSrt.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
        this.btnAnalyzeAndEditSrt.BackColor = buttonBackColor;
        this.btnAnalyzeAndEditSrt.ForeColor = buttonForeColor;
        this.btnAnalyzeAndEditSrt.Font = buttonFont;
        // Explicitly set ForeColor to white for all buttons at the end
        this.importMediaButton.ForeColor = System.Drawing.Color.White;
        this.btnAnalyzeAndEditSrt.ForeColor = System.Drawing.Color.White;
        this.progressBarAnalysis.Name = "progressBarAnalysis";
        this.progressBarAnalysis.Dock = System.Windows.Forms.DockStyle.Top;
        this.progressBarAnalysis.Height = 8;
        this.progressBarAnalysis.Visible = false;
        this.progressBarAnalysis.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
        this.progressBarAnalysis.ForeColor = System.Drawing.Color.DeepSkyBlue;
        this.Controls.Add(this.progressBarAnalysis);
        this.progressBarAnalysis.BringToFront();
        this.mainToolStrip.ResumeLayout(false);
        this.mainToolStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Panel leftPanel;
    private System.Windows.Forms.Panel centerPanel;
    private System.Windows.Forms.Panel rightPanel;
    private System.Windows.Forms.Button importMediaButton;
    private System.Windows.Forms.ListBox mediaListBox;
    private System.Windows.Forms.Panel panelTranscriptionProgress;
    private System.Windows.Forms.Label subjectLabel;
    private System.Windows.Forms.TextBox subjectTextBox;
    private System.Windows.Forms.ComboBox durationComboBox;
    private System.Windows.Forms.ComboBox customDurationComboBox;
    private System.Windows.Forms.Label lengthLabel;
    private System.Windows.Forms.Label minLabel;
    private System.Windows.Forms.Button btnAnalyzeAndEditSrt;
    private System.Windows.Forms.ProgressBar progressBarAnalysis;
    private System.Windows.Forms.ToolStrip mainToolStrip;
    private System.Windows.Forms.ToolStripButton btnNewProject;
    private System.Windows.Forms.ToolStripButton btnOpenProject;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    private System.Windows.Forms.ToolStripButton btnSettings;
}
