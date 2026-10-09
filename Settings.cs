using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace ClipLift
{
    public class Profile
    {
        public string Name { get; set; } = "";

        /// <summary>Host alias from ~/.ssh/config, or user@host.</summary>
        public string Host { get; set; } = "";

        public const int DefaultPort = 22;

        /// <summary>SSH port; the default is left to ~/.ssh/config, any other value overrides it.</summary>
        public int Port { get; set; } = DefaultPort;

        /// <summary>Upload directory on the SSH server.</summary>
        public string RemoteDir { get; set; } = "";

        /// <summary>Optional directory used in the pasted path instead of RemoteDir (e.g. a container mount).</summary>
        public string PathPrefix { get; set; } = "";

        public const int DefaultKeepLast = 10;

        /// <summary>Number of ClipLift uploads to keep in RemoteDir; older ones are deleted. 0 keeps all.</summary>
        public int KeepLast { get; set; } = DefaultKeepLast;

        [XmlIgnore]
        public bool IsComplete => !IsBlank(Name) && !IsBlank(Host) && !IsBlank(RemoteDir);

        public string PastePath(string fileName)
        {
            string dir = IsBlank(PathPrefix) ? RemoteDir : PathPrefix;
            return (dir ?? "").Trim().TrimEnd('/') + "/" + fileName;
        }

        public Profile Clone() => (Profile)MemberwiseClone();

        public override string ToString() => IsBlank(Name) ? "(unnamed)" : Name;

        internal static bool IsBlank(string s) => string.IsNullOrWhiteSpace(s);
    }

    public class Settings
    {
        public List<Profile> Profiles { get; set; } = new List<Profile>();

        public string LastProfile { get; set; } = "";

        /// <summary>Keep the original image in the clipboard and add the path as text next to it.</summary>
        public bool KeepImage { get; set; } = true;

        public bool TrailingSpace { get; set; } = true;

        /// <summary>Upload every image (bitmap) put into the clipboard to the default profile.</summary>
        public bool AutoUpload { get; set; }

        public int TimeoutSeconds { get; set; } = 10;

        [XmlIgnore]
        public List<Profile> UsableProfiles => Profiles.Where(p => p.IsComplete).ToList();

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipLift");

        public static string FilePath => Path.Combine(Dir, "settings.xml");

        public static bool Exists => File.Exists(FilePath);

        public static Settings Load()
        {
            try
            {
                Settings settings;
                using (var stream = File.OpenRead(FilePath))
                    settings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);

                // Older settings used 0 for "port from ~/.ssh/config".
                foreach (Profile p in settings.Profiles.Where(p => p.Port <= 0 || p.Port > 65535))
                    p.Port = Profile.DefaultPort;
                foreach (Profile p in settings.Profiles.Where(p => p.KeepLast < 0))
                    p.KeepLast = 0;
                return settings;
            }
            catch
            {
                return new Settings();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Dir);
            string tmp = FilePath + ".tmp";
            using (var stream = File.Create(tmp))
                new XmlSerializer(typeof(Settings)).Serialize(stream, this);
            if (File.Exists(FilePath))
                File.Replace(tmp, FilePath, null);
            else
                File.Move(tmp, FilePath);
        }

        public Settings Clone()
        {
            var copy = (Settings)MemberwiseClone();
            copy.Profiles = Profiles.Select(p => p.Clone()).ToList();
            return copy;
        }
    }

    internal static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ClipLift";

        public static bool Enabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key?.GetValue(ValueName) != null;
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value)
                        key.SetValue(ValueName, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
