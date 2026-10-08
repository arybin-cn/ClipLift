using System;
using System.Drawing;

namespace ClipLift
{
    internal static class Ui
    {
        /// <summary>Converts a size designed for a 15 px font (Segoe UI 9pt at 96 DPI) to the given font.</summary>
        public static int Px(Font font, int logical) => (int)Math.Round(logical * font.Height / 15.0);
    }
}
