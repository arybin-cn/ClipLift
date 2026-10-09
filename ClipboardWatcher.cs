using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClipLift
{
    /// <summary>Raises <see cref="Changed"/> on the UI thread whenever the clipboard content changes.</summary>
    internal sealed class ClipboardWatcher : NativeWindow, IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        /// <summary>Changes every time the clipboard content changes.</summary>
        public static uint SequenceNumber => GetClipboardSequenceNumber();

        public event EventHandler Changed;

        public ClipboardWatcher()
        {
            // Message-only window: never shown, only receives messages.
            CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
            if (!AddClipboardFormatListener(Handle))
            {
                int error = Marshal.GetLastWin32Error();
                DestroyHandle();
                throw new System.ComponentModel.Win32Exception(error);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
                Changed?.Invoke(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle == IntPtr.Zero)
                return;
            RemoveClipboardFormatListener(Handle);
            DestroyHandle();
        }
    }
}
