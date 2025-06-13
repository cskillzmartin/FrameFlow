namespace FrameFlow.Forms
{
    partial class SettingsDialog
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 400);
            this.Text = "Settings";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Padding = new Padding(0);
            this.SizeGripStyle = SizeGripStyle.Hide;

            // Create main container panel to hold content
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            // Create MenuStrip
            toolStrip = new MenuStrip
            {
                Dock = DockStyle.Top,
                Padding = new Padding(0),
                AutoSize = false,
                Height = 24
            };

            // Create content panel to hold the different setting panels
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 24, 0, 0)  // Add top padding to avoid MenuStrip overlap
            };

            // Create panels for each section
            panelFFmpeg = CreateFFmpegPanel();
            panelProject = CreateProjectPanel();
            panelImport = CreateImportPanel();
            panelUI = CreateUIPanel();
            panelAI = CreateAIPanel();

            // Set panel properties
            foreach (var panel in new[] { panelFFmpeg, panelProject, panelImport, panelUI, panelAI })
            {
                panel.Dock = DockStyle.Fill;
                panel.Visible = false;
                contentPanel.Controls.Add(panel);
            }

            // Create navigation buttons
            ffmpegMenuItem = new ToolStripMenuItem("FFmpeg");
            projectMenuItem = new ToolStripMenuItem("Project");
            importMenuItem = new ToolStripMenuItem("Import");
            uiMenuItem = new ToolStripMenuItem("UI");
            aiMenuItem = new ToolStripMenuItem("AI Models");

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                ffmpegMenuItem,
                projectMenuItem,
                importMenuItem,
                uiMenuItem,
                aiMenuItem
            });

            // Wire up click events
            ffmpegMenuItem.Click += (s, e) => ShowPanel(panelFFmpeg);
            projectMenuItem.Click += (s, e) => ShowPanel(panelProject);
            importMenuItem.Click += (s, e) => ShowPanel(panelImport);
            uiMenuItem.Click += (s, e) => ShowPanel(panelUI);
            aiMenuItem.Click += (s, e) => ShowPanel(panelAI);

            // Create bottom button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(0, 8, 12, 8)
            };

            btnSave = new Button
            {
                Text = "Save",
                Width = 80,
                Height = 30,
                Dock = DockStyle.Right
            };
            btnSave.Click += btnSave_Click;

            buttonPanel.Controls.Add(btnSave);

            // Add controls in the correct order
            mainContainer.Controls.Add(contentPanel);
            mainContainer.Controls.Add(buttonPanel);
            this.Controls.Add(mainContainer);
            this.Controls.Add(toolStrip);  // MenuStrip should be added last

            // Show initial panel
            ShowPanel(panelFFmpeg);
        }

        private Panel CreateFFmpegPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            const int LABEL_WIDTH = 100;
            const int CONTROL_HEIGHT = 23;
            const int VERTICAL_SPACING = 20;
            const int TEXT_BOX_WIDTH = 350;
            const int BUTTON_WIDTH = 75;
            var y = 10;

            // FFmpeg Path
            var lblFfmpeg = new Label
            {
                Text = "FFmpeg Path:",
                Location = new Point(0, y + 3),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtFfmpegPath = new TextBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Size = new Size(TEXT_BOX_WIDTH, CONTROL_HEIGHT)
            };

            var btnBrowseFfmpeg = new Button
            {
                Text = "Browse",
                Location = new Point(LABEL_WIDTH + TEXT_BOX_WIDTH + 5, y - 1),
                Size = new Size(BUTTON_WIDTH, CONTROL_HEIGHT + 2)
            };
            btnBrowseFfmpeg.Click += (s, e) => BrowseForFile(txtFfmpegPath);

            y += VERTICAL_SPACING;

            // FFprobe Path
            var lblFfprobe = new Label
            {
                Text = "FFprobe Path:",
                Location = new Point(0, y + 3),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtFfprobePath = new TextBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Size = new Size(TEXT_BOX_WIDTH, CONTROL_HEIGHT)
            };

            var btnBrowseFfprobe = new Button
            {
                Text = "Browse",
                Location = new Point(LABEL_WIDTH + TEXT_BOX_WIDTH + 5, y - 1),
                Size = new Size(BUTTON_WIDTH, CONTROL_HEIGHT + 2)
            };
            btnBrowseFfprobe.Click += (s, e) => BrowseForFile(txtFfprobePath);

            panel.Controls.AddRange(new Control[] {
                lblFfmpeg, txtFfmpegPath, btnBrowseFfmpeg,
                lblFfprobe, txtFfprobePath, btnBrowseFfprobe
            });

            return panel;
        }

        private Panel CreateProjectPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            const int LABEL_WIDTH = 100;
            const int CONTROL_HEIGHT = 23;
            const int VERTICAL_SPACING = 20;
            const int TEXT_BOX_WIDTH = 350;
            const int BUTTON_WIDTH = 75;
            var y = 10;

            // Project Location
            var lblProjectLocation = new Label
            {
                Text = "Default Project Location:",
                Location = new Point(0, y + 3),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtDefaultProjectLocation = new TextBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Size = new Size(TEXT_BOX_WIDTH, CONTROL_HEIGHT)
            };

            var btnBrowseProject = new Button
            {
                Text = "Browse",
                Location = new Point(LABEL_WIDTH + TEXT_BOX_WIDTH + 5, y - 1),
                Size = new Size(BUTTON_WIDTH, CONTROL_HEIGHT + 2)
            };
            btnBrowseProject.Click += (s, e) => BrowseForFolder(txtDefaultProjectLocation);

            y += VERTICAL_SPACING;

            // Import Location
            var lblImportLocation = new Label
            {
                Text = "Default Import Location:",
                Location = new Point(0, y + 3),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtDefaultImportLocation = new TextBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Size = new Size(TEXT_BOX_WIDTH, CONTROL_HEIGHT)
            };

            var btnBrowseImport = new Button
            {
                Text = "Browse",
                Location = new Point(LABEL_WIDTH + TEXT_BOX_WIDTH + 5, y - 1),
                Size = new Size(BUTTON_WIDTH, CONTROL_HEIGHT + 2)
            };
            btnBrowseImport.Click += (s, e) => BrowseForFolder(txtDefaultImportLocation);

            y += VERTICAL_SPACING;

            // Max Recent Projects
            var lblMaxRecent = new Label
            {
                Text = "Max Recent Projects:",
                Location = new Point(0, y + 3),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            numMaxRecentProjects = new NumericUpDown
            {
                Location = new Point(LABEL_WIDTH, y),
                Minimum = 1,
                Maximum = 50,
                Value = 10
            };

            panel.Controls.AddRange(new Control[] {
                lblProjectLocation, txtDefaultProjectLocation, btnBrowseProject,
                lblImportLocation, txtDefaultImportLocation, btnBrowseImport,
                lblMaxRecent, numMaxRecentProjects
            });

            return panel;
        }

        private Panel CreateImportPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            const int LABEL_WIDTH = 100;
            const int CONTROL_HEIGHT = 23;
            const int VERTICAL_SPACING = 20;
            const int TEXT_BOX_WIDTH = 350;
            const int BUTTON_WIDTH = 75;
            var y = 10;

            // Auto Analyze
            chkAutoAnalyzeOnImport = new CheckBox
            {
                Text = "Auto-analyze media on import",
                Location = new Point(0, y),
                AutoSize = true
            };

            y += VERTICAL_SPACING;

            // Create Thumbnails
            chkCreateThumbnails = new CheckBox
            {
                Text = "Create thumbnails",
                Location = new Point(0, y),
                AutoSize = true
            };

            y += VERTICAL_SPACING;

            // Thumbnail Interval
            var lblInterval = new Label
            {
                Text = "Thumbnail Interval (seconds):",
                Location = new Point(0, y),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            numThumbnailInterval = new NumericUpDown
            {
                Location = new Point(LABEL_WIDTH, y),
                Minimum = 1,
                Maximum = 300,
                Value = 10
            };

            y += VERTICAL_SPACING;

            // Supported Formats
            var lblFormats = new Label
            {
                Text = "Supported Video Formats:",
                Location = new Point(0, y),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lstSupportedFormats = new ListBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Width = TEXT_BOX_WIDTH,
                Height = 100
            };

            panel.Controls.AddRange(new Control[] {
                chkAutoAnalyzeOnImport,
                chkCreateThumbnails,
                lblInterval, numThumbnailInterval,
                lblFormats, lstSupportedFormats
            });

            return panel;
        }

        private Panel CreateUIPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            const int LABEL_WIDTH = 100;
            const int CONTROL_HEIGHT = 23;
            const int VERTICAL_SPACING = 20;
            const int TEXT_BOX_WIDTH = 350;
            const int BUTTON_WIDTH = 75;
            var y = 10;

            // Dark Mode
            chkDarkMode = new CheckBox
            {
                Text = "Dark Mode",
                Location = new Point(0, y),
                AutoSize = true
            };

            y += VERTICAL_SPACING;

            // UI Language
            var lblLanguage = new Label
            {
                Text = "UI Language:",
                Location = new Point(0, y),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            cmbUILanguage = new ComboBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Width = TEXT_BOX_WIDTH,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbUILanguage.Items.AddRange(new string[] { "en-US" }); // Add more languages as needed

            y += VERTICAL_SPACING;

            // Show Thumbnails
            chkShowThumbnails = new CheckBox
            {
                Text = "Show Thumbnails",
                Location = new Point(0, y),
                AutoSize = true
            };

            y += VERTICAL_SPACING;

            // Thumbnail Size
            var lblSize = new Label
            {
                Text = "Thumbnail Size (pixels):",
                Location = new Point(0, y),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            numThumbnailSize = new NumericUpDown
            {
                Location = new Point(LABEL_WIDTH, y),
                Minimum = 60,
                Maximum = 300,
                Value = 120,
                Increment = 20
            };

            y += VERTICAL_SPACING;

            // Add Compute Provider selection
            var lblComputeProvider = new Label
            {
                Text = "Compute Provider:",
                Location = new Point(0, y),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            cmbComputeProvider = new ComboBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Width = TEXT_BOX_WIDTH,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbComputeProvider.Items.AddRange(new string[] { "CUDA", "DirectML", "CPU" });

            panel.Controls.AddRange(new Control[] {
                chkDarkMode,
                lblLanguage, cmbUILanguage,
                chkShowThumbnails,
                lblSize, numThumbnailSize,
                lblComputeProvider, cmbComputeProvider
            });

            return panel;
        }

        private Panel CreateAIPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            const int LABEL_WIDTH = 100;
            const int CONTROL_HEIGHT = 23;
            const int VERTICAL_SPACING = 20;
            const int TEXT_BOX_WIDTH = 350;
            const int BUTTON_WIDTH = 75;
            var y = 10;

            // CPU Model Path
            var lblCpuModel = new Label
            {
                Text = "CPU Model Path:",
                Location = new Point(0, y + 3),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtCpuModelPath = new TextBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Size = new Size(TEXT_BOX_WIDTH, CONTROL_HEIGHT)
            };

            var btnBrowseCpu = new Button
            {
                Text = "Browse",
                Location = new Point(LABEL_WIDTH + TEXT_BOX_WIDTH + 5, y - 1),
                Size = new Size(BUTTON_WIDTH, CONTROL_HEIGHT + 2)
            };
            btnBrowseCpu.Click += (s, e) => BrowseForFolder(txtCpuModelPath);

            y += VERTICAL_SPACING;

            // CUDA Model Path
            var lblCudaModel = new Label
            {
                Text = "CUDA Model Path:",
                Location = new Point(0, y + 3),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtCudaModelPath = new TextBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Size = new Size(TEXT_BOX_WIDTH, CONTROL_HEIGHT)
            };

            var btnBrowseCuda = new Button
            {
                Text = "Browse",
                Location = new Point(LABEL_WIDTH + TEXT_BOX_WIDTH + 5, y - 1),
                Size = new Size(BUTTON_WIDTH, CONTROL_HEIGHT + 2)
            };
            btnBrowseCuda.Click += (s, e) => BrowseForFolder(txtCudaModelPath);

            y += VERTICAL_SPACING;

            // DirectML Model Path
            var lblDirectMLModel = new Label
            {
                Text = "DirectML Path:",
                Location = new Point(0, y + 3),
                Size = new Size(LABEL_WIDTH, CONTROL_HEIGHT),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtDirectMLModelPath = new TextBox
            {
                Location = new Point(LABEL_WIDTH, y),
                Size = new Size(TEXT_BOX_WIDTH, CONTROL_HEIGHT)
            };

            var btnBrowseDirectML = new Button
            {
                Text = "Browse",
                Location = new Point(LABEL_WIDTH + TEXT_BOX_WIDTH + 5, y - 1),
                Size = new Size(BUTTON_WIDTH, CONTROL_HEIGHT + 2)
            };
            btnBrowseDirectML.Click += (s, e) => BrowseForFolder(txtDirectMLModelPath);

            panel.Controls.AddRange(new Control[] {
                lblCpuModel, txtCpuModelPath, btnBrowseCpu,
                lblCudaModel, txtCudaModelPath, btnBrowseCuda,
                lblDirectMLModel, txtDirectMLModelPath, btnBrowseDirectML
            });

            return panel;
        }

        private void ShowPanel(Panel panelToShow)
        {
            panelFFmpeg.Visible = false;
            panelProject.Visible = false;
            panelImport.Visible = false;
            panelUI.Visible = false;
            panelAI.Visible = false;

            // Uncheck all menu items
            ffmpegMenuItem.Checked = false;
            projectMenuItem.Checked = false;
            importMenuItem.Checked = false;
            uiMenuItem.Checked = false;
            aiMenuItem.Checked = false;

            // Show selected panel and check corresponding menu item
            panelToShow.Visible = true;
            if (panelToShow == panelFFmpeg) ffmpegMenuItem.Checked = true;
            else if (panelToShow == panelProject) projectMenuItem.Checked = true;
            else if (panelToShow == panelImport) importMenuItem.Checked = true;
            else if (panelToShow == panelUI) uiMenuItem.Checked = true;
            else if (panelToShow == panelAI) aiMenuItem.Checked = true;
        }

        private void BrowseForFile(TextBox textBox)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                dialog.FilterIndex = 1;

                if (!string.IsNullOrEmpty(textBox.Text) && File.Exists(textBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(textBox.Text);
                    dialog.FileName = Path.GetFileName(textBox.Text);
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = dialog.FileName;
                }
            }
        }

        private void BrowseForFolder(TextBox textBox)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (!string.IsNullOrEmpty(textBox.Text) && Directory.Exists(textBox.Text))
                {
                    dialog.SelectedPath = textBox.Text;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = dialog.SelectedPath;
                }
            }
        }

        // Control declarations
        private Button btnSave;
        private TextBox txtFfmpegPath;
        private TextBox txtFfprobePath;
        private TextBox txtDefaultProjectLocation;
        private TextBox txtDefaultImportLocation;
        private NumericUpDown numMaxRecentProjects;
        private CheckBox chkAutoAnalyzeOnImport;
        private CheckBox chkCreateThumbnails;
        private NumericUpDown numThumbnailInterval;
        private ListBox lstSupportedFormats;
        private CheckBox chkDarkMode;
        private ComboBox cmbUILanguage;
        private CheckBox chkShowThumbnails;
        private NumericUpDown numThumbnailSize;
        private ComboBox cmbComputeProvider;
        private MenuStrip toolStrip;
        private ToolStripMenuItem ffmpegMenuItem;
        private ToolStripMenuItem projectMenuItem;
        private ToolStripMenuItem importMenuItem;
        private ToolStripMenuItem uiMenuItem;
        private ToolStripMenuItem aiMenuItem;
        private Panel panelFFmpeg;
        private Panel panelProject;
        private Panel panelImport;
        private Panel panelUI;
        private Panel panelAI;
        private TextBox txtCpuModelPath;
        private TextBox txtCudaModelPath;
        private TextBox txtDirectMLModelPath;
    }
} 