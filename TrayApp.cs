using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipLift
{
    /// <summary>
    /// Left-click uploads to the last used profile; right-click opens a menu listing all profiles
    /// (click one to upload there) followed by the Auto Mode toggle, "Settings..." and "Exit". With Auto Mode on, every image put
    /// into the clipboard is also uploaded to the default profile.
    /// </summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private const int SuccessPopupMs = 3000;
        private const int ErrorPopupMs = 5000;

        // Auto upload pops up after every copied image, so keep those popups out of the way sooner.
        private const int AutoSuccessPopupMs = 1500;
        private const int AutoErrorPopupMs = 3000;

        /// <summary>Screenshot tools often update the clipboard several times in a row; wait for it to settle.</summary>
        private const int AutoUploadDelayMs = 300;

        private readonly NotifyIcon _tray;
        private readonly ContextMenu _menu = new ContextMenu();
        private readonly Icon _idleIcon;
        private readonly Icon _busyIcon;
        private readonly Icon _idleAutoIcon;
        private readonly ClipboardWatcher _watcher;
        private readonly System.Windows.Forms.Timer _autoTimer = new System.Windows.Forms.Timer { Interval = AutoUploadDelayMs };
        private Settings _settings;
        private SettingsForm _settingsForm;
        private TrayPopup _popup;
        private Profile _uploading;

        /// <summary>Clipboard sequence number right after ClipLift last wrote to it.</summary>
        private uint _ownClipboard;

        /// <summary>The clipboard changed while an upload was running; check it again when that one ends.</summary>
        private bool _autoPending;

        private string _watcherError;

        /// <summary>Last upload per profile name: clipboard fingerprint and the pasted text it produced.</summary>
        private readonly Dictionary<string, KeyValuePair<string, string>> _recent =
            new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);

        public TrayApp()
        {
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            _settings = Settings.Load();
            _idleIcon = IconFactory.CreateIdle(false);
            _busyIcon = IconFactory.CreateBusy();
            _idleAutoIcon = IconFactory.CreateIdle(true);

            // Native menu, shown by the system on right-click; rebuilt each time from the current settings.
            _menu.Popup += (s, e) => RebuildMenu();

            _tray = new NotifyIcon { Icon = _idleIcon, ContextMenu = _menu, Visible = true };
            _tray.MouseClick += OnTrayClick;

            try
            {
                _watcher = new ClipboardWatcher();
                _watcher.Changed += (s, e) => OnClipboardChanged();
            }
            catch (Exception ex)
            {
                // Manual uploads still work; auto upload just never fires.
                _watcherError = ex.Message;
            }
            _autoTimer.Tick += (s, e) => OnClipboardSettled();
            UpdateTray();
            SynchronizationContext.Current.Post(_ => WarnIfCannotWatch(), null);

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

        private void OnClipboardChanged()
        {
            if (!_settings.AutoUpload)
                return;
            _autoTimer.Stop();
            _autoTimer.Start();
        }

        private void OnClipboardSettled()
        {
            _autoTimer.Stop();
            if (!_settings.AutoUpload || ClipboardWatcher.SequenceNumber == _ownClipboard)
                return;
            if (Busy)
            {
                _autoPending = true;
                return;
            }

            // Only bitmaps (screenshots, copied images); image files copied in Explorer need a click.
            bool hasImage;
            try { hasImage = Clipboard.ContainsImage(); }
            catch (ExternalException) { return; } // clipboard held by another app; the next change retries
            if (!hasImage)
                return;

            Profile profile = DefaultProfile;
            if (profile != null)
                _ = UploadAsync(profile);
        }

        private void WarnIfCannotWatch()
        {
            if (_settings.AutoUpload && _watcherError != null)
                ShowPopup("Cannot watch the clipboard", "Auto Mode is unavailable: " + _watcherError, true);
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
            _menu.MenuItems.Add(new MenuItem("&Auto Mode", (s, a) => ToggleAutoUpload())
            {
                Checked = _settings.AutoUpload,
            });
            _menu.MenuItems.Add(new MenuItem("&Settings...", (s, a) => ShowSettings()));
            _menu.MenuItems.Add(new MenuItem("E&xit", (s, a) => ExitApp()));
        }

        private void ToggleAutoUpload()
        {
            _settings.AutoUpload = !_settings.AutoUpload;
            _autoTimer.Stop();
            _autoPending = false;
            TrySave();
            UpdateTray();
            WarnIfCannotWatch();
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

            // Read after capturing: apps that render clipboard data on demand may write it while it is read.
            uint captured = ClipboardWatcher.SequenceNumber;

            using (payload)
            {
                if (_recent.TryGetValue(profile.Name, out KeyValuePair<string, string> recent) &&
                    recent.Key == payload.Fingerprint)
                {
                    // Same image as the last upload to this profile: reuse its remote path.
                    try
                    {
                        PutBack(payload, recent.Value, captured);
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
                    bool replaced = PutBack(payload, text, captured);
                    _recent[profile.Name] = new KeyValuePair<string, string>(payload.Fingerprint, text);

                    _settings.LastProfile = profile.Name;
                    TrySave(screen);
                    ShowPopup("Uploaded to " + profile.Name + (replaced ? "" : " (clipboard changed, path not copied)"),
                        text, false, screen);
                }
                catch (Exception ex)
                {
                    ShowPopup("Upload failed (" + profile.Name + ")", ex.Message, true, screen);
                    return;
                }
                finally
                {
                    SetUploading(null);
                    if (_autoPending)
                    {
                        _autoPending = false;
                        _autoTimer.Start();
                    }
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

        /// <summary>
        /// Puts the pasted text into the clipboard, unless the clipboard changed since it was captured (e.g. a new
        /// screenshot taken during the upload), which must not be overwritten.
        /// </summary>
        private bool PutBack(ClipboardPayload payload, string text, uint captured)
        {
            if (ClipboardWatcher.SequenceNumber != captured)
                return false;
            payload.PutBack(_settings.TrailingSpace ? text + " " : text, _settings.KeepImage);
            _ownClipboard = ClipboardWatcher.SequenceNumber;
            return true;
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
                WarnIfCannotWatch();
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
            bool auto = _settings.AutoUpload;
            _tray.Icon = Busy ? _busyIcon : auto ? _idleAutoIcon : _idleIcon;

            string text;
            if (Busy)
                text = "ClipLift - uploading to " + _uploading.Name + "...";
            else if (DefaultProfile is Profile p)
                text = (auto ? "ClipLift - Auto Mode, uploads to " : "ClipLift - click to upload to ") + p.Name;
            else
                text = "ClipLift - right-click for settings";
            _tray.Text = Truncate(text, 63);
        }

        private void ShowPopup(string title, string text, bool isError, Screen screen = null)
        {
            _popup?.Close();
            var popup = new TrayPopup(Truncate(title, 100), Truncate(text ?? "", 400), isError,
                _settings.AutoUpload
                    ? (isError ? AutoErrorPopupMs : AutoSuccessPopupMs)
                    : (isError ? ErrorPopupMs : SuccessPopupMs));
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
                _watcher?.Dispose();
                _autoTimer.Dispose();
                _idleIcon.Dispose();
                _busyIcon.Dispose();
                _idleAutoIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private static string QuoteIfNeeded(string path) =>
            path.IndexOfAny(new[] { ' ', '\t' }) >= 0 ? "\"" + path + "\"" : path;

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max - 3) + "...";
    }
}
