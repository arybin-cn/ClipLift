using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipLift
{
    /// <summary>
    /// Classic balloon-style popup shown above the tray. It never takes focus, does not appear in the
    /// taskbar or Alt+Tab, bypasses the Windows notification center, and closes on its own or on click.
    /// </summary>
    internal sealed class TrayPopup : Form
    {
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int CS_DROPSHADOW = 0x00020000;

        private readonly Timer _timer = new Timer();
        private readonly Font _titleFont;

        public TrayPopup(string title, string text, bool isError, int milliseconds)
        {
            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;
            _titleFont = new Font(Font, FontStyle.Bold);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = SystemColors.Info;
            ForeColor = SystemColors.InfoText;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Ui.Px(Font, 10), Ui.Px(Font, 7), Ui.Px(Font, 10), Ui.Px(Font, 8));

            var stack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            stack.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = _titleFont,
                ForeColor = isError ? Color.Firebrick : SystemColors.InfoText,
                Margin = new Padding(0),
            });
            if (!string.IsNullOrEmpty(text))
            {
                stack.Controls.Add(new Label
                {
                    Text = text,
                    AutoSize = true,
                    UseMnemonic = false,
                    MaximumSize = new Size(Ui.Px(Font, 380), 0),
                    Margin = new Padding(0, Ui.Px(Font, 3), 0, 0),
                });
            }
            Controls.Add(stack);

            Click += (s, e) => Close();
            stack.Click += (s, e) => Close();
            foreach (Control c in stack.Controls)
                c.Click += (s, e) => Close();

            _timer.Interval = milliseconds;
            _timer.Tick += (s, e) => Close();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        /// <summary>Shows the popup at the bottom-right of the working area of the screen under the cursor.</summary>
        public void ShowNearTray()
        {
            Size size = PreferredSize;
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int gap = Ui.Px(Font, 12);
            Bounds = new Rectangle(area.Right - size.Width - gap, area.Bottom - size.Height - gap, size.Width, size.Height);
            Show();
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, SystemColors.WindowFrame, ButtonBorderStyle.Solid);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _titleFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
