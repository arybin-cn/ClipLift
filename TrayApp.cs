using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipLift
{
    /// <summary>
    /// Left-click uploads to the last used profile; right-click opens a menu listing all profiles
    /// (click one to upload there) followed by "Settings..." and "Exit".
    /// </summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private const int SuccessPopupMs = 3000;
        private const int ErrorPopupMs = 5000;

        private readonly NotifyIcon _tray;
        private readonly ContextMenu _menu = new ContextMenu();
        private readonly Icon _idleIcon;
        private readonly Icon _busyIcon;
        private Settings _settings;
        private SettingsForm _settingsForm;
        private TrayPopup _popup;
        private Profile _uploading;

        /// <summary>Last upload per profile name: clipboard fingerprint and the pasted text it produced.</summary>
        private readonly Dictionary<string, KeyValuePair<string, string>> _recent =
            new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);

        public TrayApp()
        {
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            _settings = Settings.Load();
            _idleIcon = IconFactory.CreateIdle();
            _busyIcon = IconFactory.CreateBusy();

            // Native menu, shown by the system on right-click; rebuilt each time from the current settings.
            _menu.Popup += (s, e) => RebuildMenu();

            _tray = new NotifyIcon { Icon = _idleIcon, ContextMenu = _menu, Visible = true };
            _tray.MouseClick += OnTrayClick;
            UpdateTray();

            if (!Settings.Exists)
                SynchronizationContext.Current.Post(_ => ShowSettings(), null);
        }

        private bool Busy => _uploading != null;

        /// <summary>The last used profile, or the first usable one.</summary>
        private Profile DefaultProfile
        {
            get
            {
                List<Profile> usable = _settings.UsableProfiles;
                return usable.FirstOrDefault(p => string.Equals(p.Name, _settings.LastProfile, StringComparison.Ordinal))
                    ?? usable.FirstOrDefault();
            }
        }

        private void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || Busy)
                return;

            Profile profile = DefaultProfile;
            if (profile == null)
                ShowSettings();
            else
                _ = UploadAsync(profile);
        }

        private void RebuildMenu()
        {
            foreach (MenuItem old in _menu.MenuItems.Cast<MenuItem>().ToList())
                old.Dispose();
            _menu.MenuItems.Clear();

            List<Profile> usable = _settings.UsableProfiles;
            Profile defaultProfile = DefaultProfile;
            if (usable.Count == 0)
                _menu.MenuItems.Add(new MenuItem("(no profiles)") { Enabled = false });

            foreach (Profile profile in usable)
            {
                Profile target = profile;
                string text = (profile.Name + "  (" + profile.Host + ")").Replace("&", "&&");
                _menu.MenuItems.Add(new MenuItem(text, (s, a) => _ = UploadAsync(target))
                {
                    DefaultItem = ReferenceEquals(profile, defaultProfile),
                    Enabled = !Busy,
                });
            }

            _menu.MenuItems.Add("-");
            _menu.MenuItems.Add(new MenuItem("&Settings...", (s, a) => ShowSettings()));
            _menu.MenuItems.Add(new MenuItem("E&xit", (s, a) => ExitApp()));
        }

        private async Task UploadAsync(Profile profile)
        {
            if (Busy)
                return;

            // The cursor is on the tray icon or menu now; it may be on another screen when the upload ends.
            Screen screen = Screen.FromPoint(Cursor.Position);

            ClipboardPayload payload;
            try
            {
                payload = ClipboardPayload.Capture();
            }
            catch (Exception ex)
            {
                ShowPopup("Cannot read clipboard", ex.Message, true, screen);
                return;
            }
            if (payload == null)
                return; // nothing to upload: stay silent

            using (payload)
            {
                if (_recent.TryGetValue(profile.Name, out KeyValuePair<string, string> recent) &&
                    recent.Key == payload.Fingerprint)
                {
                    // Same image as the last upload to this profile: reuse its remote path.
                    try
                    {
                        payload.PutBack(_settings.TrailingSpace ? recent.Value + " " : recent.Value, _settings.KeepImage);
                        ShowPopup("Already uploaded to " + profile.Name, recent.Value, false, screen);
                    }
                    catch (Exception ex)
                    {
                        ShowPopup("Cannot write clipboard", ex.Message, true, screen);
                    }
                    return;
                }

                SetUploading(profile);
                int timeout = _settings.TimeoutSeconds;
                try
                {
                    List<string> names = await Task.Run(() => Uploader.Upload(profile, payload.Files, timeout));

                    string text = string.Join(" ", names.Select(n => QuoteIfNeeded(profile.PastePath(n))));
                    payload.PutBack(_settings.TrailingSpace ? text + " " : text, _settings.KeepImage);
                    _recent[profile.Name] = new KeyValuePair<string, string>(payload.Fingerprint, text);

                    _settings.LastProfile = profile.Name;
                    TrySave(screen);
                    ShowPopup("Uploaded to " + profile.Name, text, false, screen);
                }
                catch (Exception ex)
                {
                    ShowPopup("Upload failed (" + profile.Name + ")", ex.Message, true, screen);
                    return;
                }
                finally
                {
                    SetUploading(null);
                }

                // Runs after the success popup so it does not delay it; the newest upload is never pruned.
                if (profile.KeepLast > 0)
                {
                    Profile snapshot = profile.Clone();
                    try
                    {
                        await Task.Run(() => Uploader.Prune(snapshot, timeout));
                    }
                    catch (Exception ex)
                    {
                        ShowPopup("Cleanup failed (" + snapshot.Name + ")", ex.Message, true, screen);
                    }
                }
            }
        }

        private void ShowSettings()
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                if (_settingsForm.WindowState == FormWindowState.Minimized)
                    _settingsForm.WindowState = FormWindowState.Normal;
                _settingsForm.Activate();
                return;
            }

            _settingsForm = new SettingsForm(_settings.Clone());
            _settingsForm.Saved += saved =>
            {
                saved.LastProfile = _settings.LastProfile;
                _settings = saved;
                _recent.Clear(); // directories or prefixes may have changed
                TrySave();
                UpdateTray();
            };
            _settingsForm.ExitRequested += ExitApp;
            _settingsForm.FormClosed += (s, a) => _settingsForm = null;
            _settingsForm.Show();
            _settingsForm.Activate();
        }

        private void SetUploading(Profile profile)
        {
            _uploading = profile;
            UpdateTray();
        }

        private void UpdateTray()
        {
            _tray.Icon = Busy ? _busyIcon : _idleIcon;

            string text;
            if (Busy)
                text = "ClipLift - uploading to " + _uploading.Name + "...";
            else if (DefaultProfile is Profile p)
                text = "ClipLift - click to upload to " + p.Name;
            else
                text = "ClipLift - right-click for settings";
            _tray.Text = Truncate(text, 63);
        }

        private void ShowPopup(string title, string text, bool isError, Screen screen = null)
        {
            _popup?.Close();
            var popup = new TrayPopup(Truncate(title, 100), Truncate(text ?? "", 400), isError,
                isError ? ErrorPopupMs : SuccessPopupMs);
            popup.FormClosed += (s, e) =>
            {
                if (ReferenceEquals(_popup, popup))
                    _popup = null;
            };
            _popup = popup;
            popup.ShowNearTray(screen ?? Screen.FromPoint(Cursor.Position));
        }

        private void TrySave(Screen screen = null)
        {
            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                ShowPopup("Cannot save settings", ex.Message, true, screen);
            }
        }

        private void ExitApp()
        {
            _settingsForm?.Close();
            _popup?.Close();
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            _tray.Visible = false;
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tray.Dispose();
                _menu.Dispose();
                _idleIcon.Dispose();
                _busyIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private static string QuoteIfNeeded(string path) =>
            path.IndexOfAny(new[] { ' ', '\t' }) >= 0 ? "\"" + path + "\"" : path;

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max - 3) + "...";
    }
}
