using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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

        /// <summary>Content hash used to recognize an image that was already uploaded.</summary>
        public string Fingerprint { get; private set; }

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

                return new ClipboardPayload
                {
                    _image = image, _tempFile = file, Files = new[] { file }, Fingerprint = HashPixels(image),
                };
            }

            if (Clipboard.ContainsFileDropList())
            {
                StringCollection drop = Clipboard.GetFileDropList();
                string[] images = drop.Cast<string>().Where(IsImageFile).ToArray();
                if (images.Length > 0)
                    return new ClipboardPayload { _fileDrop = drop, Files = images, Fingerprint = HashFiles(images) };
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

        /// <summary>
        /// Hashes the size and RGB pixels only, so the same picture matches even when the clipboard hands it
        /// back in a different format (e.g. with or without an alpha channel).
        /// </summary>
        private static string HashPixels(Image image)
        {
            using (var bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb))
            using (var sha = SHA256.Create())
            {
                using (Graphics g = Graphics.FromImage(bmp))
                    g.DrawImageUnscaled(image, 0, 0);

                BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] size = BitConverter.GetBytes((long)bmp.Width << 32 | (uint)bmp.Height);
                    sha.TransformBlock(size, 0, size.Length, null, 0);

                    var row = new byte[bmp.Width * 4];
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                        for (int i = 3; i < row.Length; i += 4)
                            row[i] = 0;
                        sha.TransformBlock(row, 0, row.Length, null, 0);
                    }
                    sha.TransformFinalBlock(new byte[0], 0, 0);
                    return "img:" + Convert.ToBase64String(sha.Hash);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        /// <summary>Files are identified by path, size and modification time.</summary>
        private static string HashFiles(IEnumerable<string> files)
        {
            var sb = new StringBuilder("files:");
            foreach (string path in files)
            {
                var info = new FileInfo(path);
                sb.Append(info.FullName.ToLowerInvariant()).Append('|');
                if (info.Exists)
                    sb.Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static bool IsImageFile(string path) =>
            ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }
}
