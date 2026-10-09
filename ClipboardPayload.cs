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
        private IReadOnlyList<string> _tempFiles;

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

                string file = TempPath(NewBaseName() + ".png");
                image.Save(file, ImageFormat.Png);

                return new ClipboardPayload
                {
                    _image = image, _tempFiles = new[] { file }, Files = new[] { file }, Fingerprint = HashPixels(image),
                };
            }

            if (Clipboard.ContainsFileDropList())
            {
                StringCollection drop = Clipboard.GetFileDropList();
                string[] images = drop.Cast<string>().Where(IsImageFile).ToArray();
                if (images.Length > 0)
                {
                    // Upload copies under ClipLift's own names, so old uploads can be recognized and pruned.
                    string baseName = NewBaseName();
                    var copies = new List<string>();
                    var payload = new ClipboardPayload
                    {
                        _fileDrop = drop, _tempFiles = copies, Files = copies, Fingerprint = HashFiles(images),
                    };
                    try
                    {
                        for (int i = 0; i < images.Length; i++)
                        {
                            string suffix = images.Length > 1 ? "_" + (i + 1).ToString(CultureInfo.InvariantCulture) : "";
                            string copy = TempPath(baseName + suffix + Path.GetExtension(images[i]).ToLowerInvariant());
                            File.Copy(images[i], copy, true);
                            copies.Add(copy);
                        }
                    }
                    catch
                    {
                        payload.Dispose();
                        throw;
                    }
                    return payload;
                }
            }

            return null;
        }

        /// <summary>Remote file names start with this prefix; only such files are pruned.</summary>
        public const string FilePrefix = "CLIPLIFT_";

        private static string NewBaseName() =>
            FilePrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

        private static string TempPath(string fileName)
        {
            string dir = Path.Combine(Path.GetTempPath(), "ClipLift");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
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
            foreach (string file in _tempFiles ?? Enumerable.Empty<string>())
            {
                try { File.Delete(file); }
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
