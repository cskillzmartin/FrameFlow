namespace FrameFlow.App
{
    public static class ThemeManager
    {
        // Base colors
        private static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
        private static readonly Color DarkSecondaryBackground = Color.FromArgb(45, 45, 45);
        private static readonly Color DarkForeground = Color.FromArgb(240, 240, 240);
        private static readonly Color DarkBorder = Color.FromArgb(70, 70, 70);
        private static readonly Color DarkTextBoxBackground = Color.FromArgb(55, 55, 55);
        private static readonly Color DarkTabBackground = Color.FromArgb(38, 38, 38);
        private static readonly Color DarkButtonBackground = Color.FromArgb(60, 60, 60);
        private static readonly Color DarkButtonHover = Color.FromArgb(75, 75, 75);
        private static readonly Color DarkAccent = Color.FromArgb(86, 156, 214); // Visual Studio blue

        // Menu-specific colors
        private static readonly Color DarkMenuBackground = Color.FromArgb(45, 45, 45);
        private static readonly Color DarkMenuHover = Color.FromArgb(65, 65, 65);
        private static readonly Color DarkMenuBorder = Color.FromArgb(70, 70, 70);

        // Light mode colors
        private static readonly Color LightBackground = SystemColors.Control;
        private static readonly Color LightForeground = SystemColors.ControlText;

        public static void ApplyTheme(Form form, bool darkMode)
        {
            ApplyThemeToControl(form, darkMode);
            foreach (Control control in GetAllControls(form))
            {
                ApplyTheme(control, darkMode);
            }

            // Apply to MenuStrip if exists
            var menuStrip = form.Controls.OfType<MenuStrip>().FirstOrDefault();
            if (menuStrip != null)
            {
                ApplyThemeToMenuStrip(menuStrip, darkMode);
            }
        }

        public static void ApplyTheme(Control control, bool darkMode)
        {
            if (darkMode)
            {
                ApplyDarkTheme(control);
            }
            else
            {
                ApplyLightTheme(control);
            }

            foreach (Control child in control.Controls)
            {
                ApplyTheme(child, darkMode);
            }
        }

        private static void ApplyDarkTheme(Control control)
        {
            switch (control)
            {
                case MenuStrip menuStrip:
                    menuStrip.BackColor = DarkBackground;
                    menuStrip.ForeColor = DarkForeground;
                    menuStrip.Renderer = new DarkMenuRenderer();
                    foreach (ToolStripItem item in menuStrip.Items)
                    {
                        if (item is ToolStripMenuItem menuItem)
                        {
                            StyleMenuItem(menuItem);
                        }
                    }
                    break;

                case Form form:
                    form.BackColor = DarkBackground;
                    form.ForeColor = DarkForeground;
                    break;

                case TextBox textBox:
                    textBox.BackColor = DarkTextBoxBackground;
                    textBox.ForeColor = DarkForeground;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case Button button:
                    ApplyDarkButtonStyle(button);
                    break;

                case Label label:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = DarkForeground;
                    break;

                case CheckBox checkBox:
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = DarkForeground;
                    break;

                case NumericUpDown numericUpDown:
                    numericUpDown.BackColor = DarkTextBoxBackground;
                    numericUpDown.ForeColor = DarkForeground;
                    break;

                case ListBox listBox:
                    listBox.BackColor = DarkTextBoxBackground;
                    listBox.ForeColor = DarkForeground;
                    listBox.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ComboBox comboBox:
                    comboBox.BackColor = DarkTextBoxBackground;
                    comboBox.ForeColor = DarkForeground;
                    break;

                case TabControl tabControl:
                    tabControl.BackColor = DarkBackground;
                    tabControl.ForeColor = DarkForeground;
                    SetTabControlStyles(tabControl);
                    break;

                case TabPage tabPage:
                    tabPage.BackColor = DarkTabBackground;
                    tabPage.ForeColor = DarkForeground;
                    break;

                case ToolStrip toolStrip:
                    toolStrip.BackColor = DarkSecondaryBackground;
                    toolStrip.ForeColor = DarkForeground;
                    foreach (ToolStripItem item in toolStrip.Items)
                    {
                        if (item is ToolStripButton button)
                        {
                            button.BackColor = DarkSecondaryBackground;
                            button.ForeColor = DarkForeground;
                        }
                        else if (item is ToolStripSeparator separator)
                        {
                            separator.BackColor = DarkSecondaryBackground;
                            separator.ForeColor = DarkBorder;
                        }
                    }
                    break;
            }
        }

        private static void ApplyDarkButtonStyle(Button button)
        {
            button.BackColor = DarkButtonBackground;
            button.ForeColor = DarkForeground;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = DarkBorder;
            button.FlatAppearance.MouseOverBackColor = DarkButtonHover;
            button.FlatAppearance.BorderSize = 1;
            
            // Add hover effect
            button.MouseEnter += (s, e) => button.BackColor = DarkButtonHover;
            button.MouseLeave += (s, e) => button.BackColor = DarkButtonBackground;
        }

        private static void StyleMenuItem(ToolStripMenuItem item)
        {
            item.BackColor = DarkBackground;
            item.ForeColor = DarkForeground;
            
            // Style the dropdown
            if (item.DropDown is ToolStripDropDown dropDown)
            {
                dropDown.BackColor = DarkMenuBackground;
                dropDown.ForeColor = DarkForeground;
                dropDown.RenderMode = ToolStripRenderMode.Professional;
            }

            // Style sub-items
            foreach (var dropDownItem in item.DropDownItems)
            {
                if (dropDownItem is ToolStripMenuItem menuItem)
                {
                    StyleMenuItem(menuItem);
                }
            }
        }

        private static void SetTabControlStyles(TabControl tabControl)
        {
            // Custom drawing for tab control
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.DrawItem += (sender, e) =>
            {
                var g = e.Graphics;
                var tabRect = tabControl.GetTabRect(e.Index);
                var selected = e.Index == tabControl.SelectedIndex;

                var tabBackColor = selected ? DarkTabBackground : DarkSecondaryBackground;
                var textColor = DarkForeground;

                using (var brush = new SolidBrush(tabBackColor))
                {
                    g.FillRectangle(brush, tabRect);
                }

                var textRect = tabRect;
                textRect.Inflate(-2, -2);

                var tabPage = tabControl.TabPages[e.Index];
                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                using (var brush = new SolidBrush(textColor))
                {
                    g.DrawString(tabPage.Text, tabControl.Font, brush, textRect, format);
                }

                if (selected)
                {
                    using (var pen = new Pen(DarkAccent, 2))
                    {
                        g.DrawLine(pen, tabRect.Left, tabRect.Bottom - 2, tabRect.Right, tabRect.Bottom - 2);
                    }
                }
            };
        }

        private static void ApplyLightTheme(Control control)
        {
            // Default system colors for light theme
            control.BackColor = SystemColors.Control;
            control.ForeColor = SystemColors.ControlText;

            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Standard;
                button.UseVisualStyleBackColor = true;
            }
        }

        private static void ApplyThemeToControl(Control control, bool darkMode)
        {
            control.BackColor = darkMode ? DarkBackground : LightBackground;
            control.ForeColor = darkMode ? DarkForeground : LightForeground;

            // Special handling for specific control types
            switch (control)
            {
                case TextBox textBox:
                    textBox.BackColor = darkMode ? Color.FromArgb(45, 45, 45) : SystemColors.Window;
                    break;
                case Button button:
                    button.FlatStyle = darkMode ? FlatStyle.Flat : FlatStyle.Standard;
                    if (darkMode)
                    {
                        button.FlatAppearance.BorderColor = Color.Gray;
                    }
                    break;
                case SplitContainer splitContainer:
                    splitContainer.BackColor = darkMode ? Color.Gray : SystemColors.Control;
                    break;
            }
        }

        private static void ApplyThemeToMenuStrip(MenuStrip menuStrip, bool darkMode)
        {
            menuStrip.BackColor = darkMode ? DarkMenuBackground : LightBackground;
            menuStrip.ForeColor = darkMode ? DarkForeground : LightForeground;
            menuStrip.Renderer = darkMode ? new DarkMenuRenderer() : new LightMenuRenderer();

            foreach (ToolStripMenuItem item in menuStrip.Items)
            {
                ApplyThemeToMenuItem(item, darkMode);
            }
        }

        private static void ApplyThemeToMenuItem(ToolStripMenuItem item, bool darkMode)
        {
            item.BackColor = darkMode ? DarkMenuBackground : LightBackground;
            item.ForeColor = darkMode ? DarkForeground : LightForeground;

            // Set the dropdown properties
            if (item.DropDown is ToolStripDropDown dropDown)
            {
                dropDown.BackColor = darkMode ? DarkMenuBackground : LightBackground;
                dropDown.ForeColor = darkMode ? DarkForeground : LightForeground;
                if (darkMode)
                {
                    dropDown.RenderMode = ToolStripRenderMode.Professional;
                }
            }

            foreach (var dropDownItem in item.DropDownItems)
            {
                if (dropDownItem is ToolStripMenuItem menuItem)
                {
                    ApplyThemeToMenuItem(menuItem, darkMode);
                }
            }
        }

        // Custom renderer for dark mode
        private class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColorTable())
            {
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected)
                {
                    base.OnRenderMenuItemBackground(e);
                    return;
                }

                var rect = new Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1);
                using (var brush = new SolidBrush(DarkMenuHover))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                var rect = new Rectangle(1, 3, e.Item.Width - 2, 1);
                using (var brush = new SolidBrush(DarkMenuBorder))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
            }
        }

        // Custom color table for dark mode
        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuBorder => DarkMenuBorder;
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuItemSelected => DarkMenuHover;
            public override Color MenuItemSelectedGradientBegin => DarkMenuHover;
            public override Color MenuItemSelectedGradientEnd => DarkMenuHover;
            public override Color MenuStripGradientBegin => DarkBackground;
            public override Color MenuStripGradientEnd => DarkBackground;
            public override Color ToolStripDropDownBackground => DarkMenuBackground;
            public override Color ImageMarginGradientBegin => DarkMenuBackground;
            public override Color ImageMarginGradientMiddle => DarkMenuBackground;
            public override Color ImageMarginGradientEnd => DarkMenuBackground;
        }

        private class LightMenuRenderer : ToolStripProfessionalRenderer
        {
            // Uses default Windows rendering for light mode
        }

        private static IEnumerable<Control> GetAllControls(Control container)
        {
            var controls = container.Controls.Cast<Control>();
            return controls.SelectMany(ctrl => GetAllControls(ctrl)).Concat(controls);
        }
    }
} 