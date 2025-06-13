namespace FrameFlow.Forms
{
    public class BaseForm : Form
    {
        protected readonly App.Settings _settings;

        public BaseForm()
        {
            _settings = App.Settings.Instance;
            Load += BaseForm_Load;
        }

        private void BaseForm_Load(object? sender, EventArgs e)
        {
            App.ThemeManager.ApplyTheme(this, _settings.DarkMode);
        }
    }
} 