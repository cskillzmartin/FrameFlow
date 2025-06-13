using System.Windows.Forms;

namespace FrameFlow.Forms
{
    public partial class SettingsDialog : BaseForm
    {
        public SettingsDialog()
        {
            InitializeComponent();
            this.Shown += (s, e) => LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                // FFmpeg Settings
                if (txtFfmpegPath != null)
                    txtFfmpegPath.Text = App.Settings.Instance.FfmpegPath;
                if (txtFfprobePath != null)
                    txtFfprobePath.Text = App.Settings.Instance.FfprobePath;

                // Project Settings
                if (txtDefaultProjectLocation != null)
                    txtDefaultProjectLocation.Text = App.Settings.Instance.DefaultProjectLocation;
                if (txtDefaultImportLocation != null)
                    txtDefaultImportLocation.Text = App.Settings.Instance.DefaultImportLocation;
                if (numMaxRecentProjects != null)
                    numMaxRecentProjects.Value = App.Settings.Instance.MaxRecentProjects;

                // Import Settings
                chkAutoAnalyzeOnImport.Checked = App.Settings.Instance.AutoAnalyzeOnImport;
                chkCreateThumbnails.Checked = App.Settings.Instance.CreateThumbnails;
                numThumbnailInterval.Value = App.Settings.Instance.ThumbnailInterval;
                lstSupportedFormats.Items.Clear();
                lstSupportedFormats.Items.AddRange(App.Settings.Instance.SupportedVideoFormats.ToArray());

                // UI Settings
                chkDarkMode.Checked = App.Settings.Instance.DarkMode;
                cmbUILanguage.Text = App.Settings.Instance.UILanguage;
                chkShowThumbnails.Checked = App.Settings.Instance.ShowThumbnails;
                numThumbnailSize.Value = App.Settings.Instance.ThumbnailSize;

                // Compute Provider
                if (cmbComputeProvider != null)
                    cmbComputeProvider.Text = App.Settings.Instance.PreferredComputeProvider;

                // AI Model Settings
                if (txtCpuModelPath != null)
                    txtCpuModelPath.Text = App.Settings.Instance.OnnxCpuModelDirectory;
                if (txtCudaModelPath != null)
                    txtCudaModelPath.Text = App.Settings.Instance.OnnxCudaModelDirectory;
                if (txtDirectMLModelPath != null)
                    txtDirectMLModelPath.Text = App.Settings.Instance.OnnxDirectMLModelDirectory;
                if (txtWhisperModelPath != null)
                    txtWhisperModelPath.Text = App.Settings.Instance.WhisperModelPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void chkDarkMode_CheckedChanged(object sender, EventArgs e)
        {
            // Apply theme preview to settings dialog
            App.ThemeManager.ApplyTheme(this, chkDarkMode.Checked);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            // FFmpeg Settings
            App.Settings.Instance.FfmpegPath = txtFfmpegPath.Text;
            App.Settings.Instance.FfprobePath = txtFfprobePath.Text;

            // Project Settings
            App.Settings.Instance.DefaultProjectLocation = txtDefaultProjectLocation.Text;
            App.Settings.Instance.DefaultImportLocation = txtDefaultImportLocation.Text;
            App.Settings.Instance.MaxRecentProjects = (int)numMaxRecentProjects.Value;

            // Import Settings
            App.Settings.Instance.AutoAnalyzeOnImport = chkAutoAnalyzeOnImport.Checked;
            App.Settings.Instance.CreateThumbnails = chkCreateThumbnails.Checked;
            App.Settings.Instance.ThumbnailInterval = (int)numThumbnailInterval.Value;
            App.Settings.Instance.SupportedVideoFormats = lstSupportedFormats.Items.Cast<string>().ToList();

            // UI Settings
            App.Settings.Instance.DarkMode = chkDarkMode.Checked;
            App.Settings.Instance.UILanguage = cmbUILanguage.Text;
            App.Settings.Instance.ShowThumbnails = chkShowThumbnails.Checked;
            App.Settings.Instance.ThumbnailSize = (int)numThumbnailSize.Value;

            // Compute Provider
            App.Settings.Instance.PreferredComputeProvider = cmbComputeProvider.Text;

            // AI Model Settings
            App.Settings.Instance.OnnxCpuModelDirectory = txtCpuModelPath.Text;
            App.Settings.Instance.OnnxCudaModelDirectory = txtCudaModelPath.Text;
            App.Settings.Instance.OnnxDirectMLModelDirectory = txtDirectMLModelPath.Text;
            App.Settings.Instance.WhisperModelPath = txtWhisperModelPath.Text;

            App.Settings.Instance.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                "Are you sure you want to reset all settings to their default values?",
                "Reset Settings",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                App.Settings.Instance.ResetToDefaults();
                LoadSettings();
            }
        }
    }
} 