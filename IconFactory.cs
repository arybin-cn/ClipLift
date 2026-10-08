using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClipLift
{
    /// <summary>
    /// Draws the tray icons at the system small-icon size: a white up arrow on a circle that fills the
    /// whole icon. Green while idle, blue while uploading.
    /// </summary>
    internal static class IconFactory
    {
        private static readonly Color IdleColor = Color.FromArgb(0x16, 0xA3, 0x4A);
        private static readonly Color BusyColor = Color.FromArgb(0x25, 0x63, 0xEB);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon CreateIdle() => Draw(IdleColor);

        public static Icon CreateBusy() => Draw(BusyColor);

        private static Icon Draw(Color background)
        {
            int s = Math.Max(16, SystemInformation.SmallIconSize.Width);
            using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    using (var fill = new SolidBrush(background))
                        g.FillEllipse(fill, 0, 0, s, s);

                    DrawArrow(g, s);
                }

                IntPtr handle = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(handle))
                        return (Icon)temp.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private static void DrawArrow(Graphics g, int s)
        {
            float cx = s / 2f;
            float tip = s * 0.27f;
            float tail = s * 0.73f;
            float wing = s * 0.18f;

            using (var arrow = new GraphicsPath())
            using (var pen = new Pen(Color.White, Math.Max(1.5f, s * 0.09f)))
            {
                arrow.AddLine(cx, tail, cx, tip);
                arrow.StartFigure();
                arrow.AddLines(new[]
                {
                    new PointF(cx - wing, tip + wing),
                    new PointF(cx, tip),
                    new PointF(cx + wing, tip + wing),
                });

                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawPath(pen, arrow);
            }
        }
    }
}
