using System.Windows.Forms;
using System.Drawing;

namespace FrameFlow
{
    public partial class SettingsForm : Form
    {
        private TextBox txtWorkingDir;
        private Button btnBrowse;
        private Button btnSave;
        private Button btnCancel;
        private Label lblWorkingDir;

        public string WorkingFilesPath { get; private set; }

        public SettingsForm(string currentPath)
        {
            InitializeComponent();
            WorkingFilesPath = currentPath;
            txtWorkingDir.Text = currentPath;
        }

        private void InitializeComponent()
        {
            this.Text = "Settings";
            this.Size = new Size(500, 200);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(45, 45, 45);
            this.ForeColor = Color.White;

            lblWorkingDir = new Label
            {
                Text = "Working Files Directory:",
                Location = new Point(20, 20),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };

            txtWorkingDir = new TextBox
            {
                Location = new Point(20, 45),
                Size = new Size(350, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            btnBrowse = new Button
            {
                Text = "Browse",
                Location = new Point(380, 44),
                Size = new Size(80, 25),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBrowse.Click += BtnBrowse_Click;

            btnSave = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(290, 120),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(380, 120),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            this.Controls.AddRange(new Control[] { 
                lblWorkingDir, 
                txtWorkingDir, 
                btnBrowse, 
                btnSave, 
                btnCancel 
            });

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void BtnBrowse_Click(object sender, System.EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select Working Files Directory";
                folderDialog.UseDescriptionForTitle = true;
                folderDialog.InitialDirectory = txtWorkingDir.Text;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    txtWorkingDir.Text = folderDialog.SelectedPath;
                }
            }
        }

        private void BtnSave_Click(object sender, System.EventArgs e)
        {
            string path = txtWorkingDir.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Please select a valid directory.", "Invalid Directory", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            try
            {
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }
                WorkingFilesPath = path;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating directory: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }
    }
} 