using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ClipLift
{
    /// <summary>An image (or image files) captured from the clipboard. Must be used on the UI (STA) thread.</summary>
    internal sealed class ClipboardPayload : IDisposable
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };

        private Image _image;
        private StringCollection _fileDrop;
        private string _tempFile;

        /// <summary>Local files to upload.</summary>
        public IReadOnlyList<string> Files { get; private set; }

        public static bool Available()
        {
            if (Clipboard.ContainsImage())
                return true;
            return Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Cast<string>().Any(IsImageFile);
        }

        /// <summary>Returns null when the clipboard holds neither an image nor image files.</summary>
        public static ClipboardPayload Capture()
        {
            if (Clipboard.ContainsImage())
            {
                Image image = Clipboard.GetImage();
                if (image == null)
                    return null;

                string dir = Path.Combine(Path.GetTempPath(), "ClipLift");
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                string file = Path.Combine(dir, "shot_" + stamp + ".png");
                image.Save(file, ImageFormat.Png);

                return new ClipboardPayload { _image = image, _tempFile = file, Files = new[] { file } };
            }

            if (Clipboard.ContainsFileDropList())
            {
                StringCollection drop = Clipboard.GetFileDropList();
                string[] images = drop.Cast<string>().Where(IsImageFile).ToArray();
                if (images.Length > 0)
                    return new ClipboardPayload { _fileDrop = drop, Files = images };
            }

            return null;
        }

        /// <summary>Puts the pasted path into the clipboard, optionally next to the original content.</summary>
        public void PutBack(string text, bool keepOriginal)
        {
            var data = new DataObject();
            data.SetText(text, TextDataFormat.UnicodeText);
            if (keepOriginal)
            {
                if (_image != null)
                    data.SetImage(_image);
                if (_fileDrop != null)
                    data.SetFileDropList(_fileDrop);
            }
            Clipboard.SetDataObject(data, true, 10, 50);
        }

        public void Dispose()
        {
            _image?.Dispose();
            if (_tempFile != null)
            {
                try { File.Delete(_tempFile); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static bool IsImageFile(string path) =>
            ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }
}
