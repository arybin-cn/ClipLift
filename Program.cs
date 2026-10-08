using System;
using System.Threading;
using System.Windows.Forms;

namespace ClipLift
{
    internal static class Program
    {
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
