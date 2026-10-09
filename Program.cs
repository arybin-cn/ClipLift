using System;
using System.Threading;
using System.Windows.Forms;

namespace ClipLift
{
    internal static class Program
    {
        /// <summary>The product version (from &lt;Version&gt; in the project), without build metadata.</summary>
        public static string Version => Application.ProductVersion.Split('+')[0];

        [STAThread]
        private static void Main()
        {
            using (new Mutex(true, @"Local\ClipLift", out bool createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("ClipLift is already running.", "ClipLift",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
