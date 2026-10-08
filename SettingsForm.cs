using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipLift
{
    /// <summary>Profile list on the left, the selected profile on the right, global options below.</summary>
    /// <remarks>
    /// WinForms auto scaling is disabled: every explicit size is derived from the font height
    /// (see <see cref="Px"/>), and layout panels size themselves to their content.
    /// </remarks>
    internal sealed class SettingsForm : Form
    {
        private const string SampleFileName = "shot_20260101_120000_000.png";

        private readonly Settings _settings;
        private bool _loading;

        private readonly ProfileList _list = new ProfileList();
        private readonly Button _add = new Button { Text = "&Add" };
        private readonly Button _duplicate = new Button { Text = "&Duplicate" };
        private readonly Button _remove = new Button { Text = "&Remove" };

        private readonly GroupBox _editor = new GroupBox { Text = "Profile" };
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _host = new TextBox();
        private readonly NumericUpDown _port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = Profile.DefaultPort };
        private readonly TextBox _remoteDir = new TextBox();
        private readonly TextBox _pathPrefix = new TextBox();
        private readonly Label _hint = new Label();
        private readonly Label _preview = new Label();
        private readonly Button _test = new Button { Text = "&Test connection" };
        private readonly Label _testStatus = new Label();

        private readonly CheckBox _keepImage = new CheckBox
        {
            Text = "Keep the original image in the clipboard (the path is added as text)",
        };
        private readonly CheckBox _trailingSpace = new CheckBox { Text = "Add a trailing space after the pasted path" };
        private readonly CheckBox _autostart = new CheckBox { Text = "Start ClipLift with Windows" };

        private readonly Button _save = new Button { Text = "Save" };
        private readonly Button _cancel = new Button { Text = "Cancel" };
        private readonly Button _exit = new Button { Text = "E&xit ClipLift" };

        public event Action<Settings> Saved;
        public event Action ExitRequested;

        public SettingsForm(Settings settings)
        {
            _settings = settings;

            // Set the font before any child control is created or added, so nothing gets resized later.
            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;

            BuildLayout();
            WireEvents();

            _keepImage.Checked = settings.KeepImage;
            _trailingSpace.Checked = settings.TrailingSpace;
            try { _autostart.Checked = Autostart.Enabled; } catch (Exception) { _autostart.Enabled = false; }

            foreach (Profile p in settings.Profiles)
                _list.Items.Add(p);
            if (_list.Items.Count > 0)
                _list.SelectedIndex = 0;
            UpdateEditor();
        }

        private Profile Current => _list.SelectedItem as Profile;

        /// <summary>Converts a size designed for a 15 px font (Segoe UI 9pt at 96 DPI) to the current font.</summary>
        private int Px(int logical) => Ui.Px(Font, logical);

        // ---- layout ----

        private void BuildLayout()
        {
            SuspendLayout();

            Text = "ClipLift Settings";
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            AcceptButton = _save;
            CancelButton = _cancel;

            // Left: profile list (fills the height of the editor) with buttons below
            _list.IntegralHeight = false;
            _list.Dock = DockStyle.Fill;
            var listButtons = Flow(FlowDirection.LeftToRight, _add, _duplicate, _remove);
            var left = Table(1);
            left.Dock = DockStyle.Fill;
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.Controls.Add(new Label { Text = "Upload profiles:", AutoSize = true }, 0, 0);
            left.Controls.Add(_list, 0, 1);
            left.Controls.Add(listButtons, 0, 2);

            // Right: profile editor
            var fields = Table(2);
            AddField(fields, "&Name:", _name);
            AddField(fields, "&Host:", _host);
            AddField(fields, "&Port:", _port);
            AddField(fields, "Remote &directory:", _remoteDir);
            AddField(fields, "Paste path &prefix:", _pathPrefix);

            _hint.AutoSize = true;
            _hint.ForeColor = SystemColors.GrayText;
            _hint.Text = "Host: an alias from ~/.ssh/config, or user@host. Key-based login is required.\n" +
                         "Port: keep 22 to use ~/.ssh/config as is; any other port overrides it.\n" +
                         "Paste path prefix: optional, replaces the remote directory in the pasted path " +
                         "(e.g. the directory as mounted inside a container).";
            _preview.AutoSize = true;
            _testStatus.AutoSize = true;
            _testStatus.Anchor = AnchorStyles.Left;
            _test.Anchor = AnchorStyles.Left;
            var testRow = Flow(FlowDirection.LeftToRight, _test, _testStatus);

            var editorBody = Table(1);
            editorBody.Dock = DockStyle.Fill;
            editorBody.Controls.Add(fields);
            editorBody.Controls.Add(_hint);
            editorBody.Controls.Add(_preview);
            editorBody.Controls.Add(testRow);
            _editor.AutoSize = true;
            _editor.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _editor.Controls.Add(editorBody);

            var top = Table(2);
            top.Controls.Add(left, 0, 0);
            top.Controls.Add(_editor, 1, 0);

            // Global options
            foreach (CheckBox c in new[] { _keepImage, _trailingSpace, _autostart })
                c.AutoSize = true;
            var optionsBody = Flow(FlowDirection.TopDown, _keepImage, _trailingSpace, _autostart);
            optionsBody.Dock = DockStyle.Fill;
            var options = new GroupBox
            {
                Text = "Options",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };
            options.Controls.Add(optionsBody);

            // Bottom: Exit on the left, Save / Cancel on the right
            var rightButtons = Flow(FlowDirection.RightToLeft, _cancel, _save);
            rightButtons.Anchor = AnchorStyles.Right;
            _exit.Anchor = AnchorStyles.Left;
            var bottom = Table(2);
            bottom.Dock = DockStyle.Fill;
            bottom.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100F);
            bottom.Controls.Add(_exit, 0, 0);
            bottom.Controls.Add(rightButtons, 1, 0);

            var root = Table(1);
            root.Controls.Add(top);
            root.Controls.Add(options);
            root.Controls.Add(bottom);
            Controls.Add(root);

            foreach (Button b in new[] { _add, _duplicate, _remove, _test, _save, _cancel, _exit })
            {
                b.AutoSize = true;
                b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            }

            // Everything that depends on pixel sizes; re-applied when the DPI changes.
            _applySizes = () =>
            {
                Padding = new Padding(Px(10));
                left.Margin = new Padding(0, 0, Px(10), 0);
                _list.MinimumSize = new Size(Px(170), Px(120));
                _list.Margin = new Padding(0, Px(4), 0, Px(6));
                foreach (Button b in new[] { _add, _duplicate, _remove })
                {
                    b.Margin = new Padding(0, 0, Px(4), 0);
                    b.Padding = new Padding(Px(4), 0, Px(4), 0);
                }

                editorBody.Padding = new Padding(Px(6));
                foreach (Control input in new Control[] { _name, _host, _remoteDir, _pathPrefix })
                    input.Width = Px(300);
                _port.Width = Px(80);
                _hint.MaximumSize = new Size(Px(420), 0);
                _hint.Margin = new Padding(0, Px(6), 0, 0);
                _preview.MaximumSize = new Size(Px(420), 0);
                _preview.Margin = new Padding(0, Px(8), 0, 0);
                testRow.Margin = new Padding(0, Px(8), 0, 0);
                _test.Margin = new Padding(0);
                _test.Padding = new Padding(Px(4), 0, Px(4), 0);
                _testStatus.MaximumSize = new Size(Px(300), 0);
                _testStatus.Margin = new Padding(Px(8), 0, 0, 0);

                options.Margin = new Padding(0, Px(10), 0, 0);
                optionsBody.Padding = new Padding(Px(6));
                foreach (CheckBox c in new[] { _keepImage, _trailingSpace, _autostart })
                    c.Margin = new Padding(0, Px(2), 0, Px(2));

                bottom.Margin = new Padding(0, Px(12), 0, 0);
                foreach (Button b in new[] { _save, _cancel, _exit })
                {
                    b.MinimumSize = new Size(Px(80), 0);
                    b.Padding = new Padding(Px(4), 0, Px(4), 0);
                    b.Margin = new Padding(0);
                }
                _save.Margin = new Padding(0, 0, Px(6), 0);

                foreach (Label label in fields.Controls.OfType<Label>())
                    label.Margin = new Padding(0, Px(3), Px(8), Px(3));
                foreach (Control input in fields.Controls.Cast<Control>().Where(c => !(c is Label)))
                    input.Margin = new Padding(0, Px(3), 0, Px(3));
            };
            _applySizes();

            ResumeLayout(true);
        }

        private Action _applySizes;

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e); // rescales the font
            if (!e.Cancel)
                _applySizes?.Invoke();
        }

        private static TableLayoutPanel Table(int columns)
        {
            var t = new TableLayoutPanel
            {
                ColumnCount = columns,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            for (int i = 0; i < columns; i++)
                t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return t;
        }

        private static FlowLayoutPanel Flow(FlowDirection direction, params Control[] controls)
        {
            var f = new FlowLayoutPanel
            {
                FlowDirection = direction,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            f.Controls.AddRange(controls);
            return f;
        }

        private static void AddField(TableLayoutPanel table, string label, Control input)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left });
            input.Anchor = AnchorStyles.Left;
            table.Controls.Add(input);
        }

        // ---- behaviour ----

        private void WireEvents()
        {
            _list.SelectedIndexChanged += (s, e) =>
            {
                if (!_list.Refreshing)
                    UpdateEditor();
            };
            _add.Click += (s, e) => AddProfile(new Profile { Name = UniqueName("New profile") });
            _duplicate.Click += (s, e) =>
            {
                if (Current == null)
                    return;
                Profile copy = Current.Clone();
                copy.Name = UniqueName(Current.Name + " copy");
                AddProfile(copy);
            };
            _remove.Click += (s, e) => RemoveProfile();

            // The list shows the name; refresh it once editing the name is done, not on every keystroke.
            _name.TextChanged += (s, e) => Edit(p => p.Name = _name.Text);
            _name.Leave += (s, e) => _list.RefreshItemAt(_list.SelectedIndex);
            _host.TextChanged += (s, e) => Edit(p => p.Host = _host.Text);
            _port.ValueChanged += (s, e) => Edit(p => p.Port = (int)_port.Value);
            _remoteDir.TextChanged += (s, e) => Edit(p => p.RemoteDir = _remoteDir.Text);
            _pathPrefix.TextChanged += (s, e) => Edit(p => p.PathPrefix = _pathPrefix.Text);

            _test.Click += async (s, e) => await TestConnectionAsync();
            _save.Click += (s, e) => SaveAndClose();
            _cancel.Click += (s, e) => Close();
            _exit.Click += (s, e) => ExitRequested?.Invoke();
        }

        private void Edit(Action<Profile> apply)
        {
            if (_loading || Current == null)
                return;
            apply(Current);
            UpdatePreview();
        }

        private void UpdateEditor()
        {
            Profile p = Current;
            _loading = true;
            try
            {
                _name.Text = p?.Name ?? "";
                _host.Text = p?.Host ?? "";
                _port.Value = p == null || p.Port <= 0 || p.Port > 65535 ? Profile.DefaultPort : p.Port;
                _remoteDir.Text = p?.RemoteDir ?? "";
                _pathPrefix.Text = p?.PathPrefix ?? "";
            }
            finally
            {
                _loading = false;
            }

            _editor.Enabled = p != null;
            _duplicate.Enabled = _remove.Enabled = p != null;
            _testStatus.Text = "";
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            Profile p = Current;
            if (p == null)
                _preview.Text = "Add a profile to get started.";
            else if (Profile.IsBlank(p.RemoteDir) && Profile.IsBlank(p.PathPrefix))
                _preview.Text = "Pasted as: (set a remote directory)";
            else
                _preview.Text = "Pasted as: " + p.PastePath(SampleFileName);
        }

        private void AddProfile(Profile profile)
        {
            _settings.Profiles.Add(profile);
            _list.Items.Add(profile);
            _list.SelectedItem = profile;
            _name.Focus();
            _name.SelectAll();
        }

        private void RemoveProfile()
        {
            int index = _list.SelectedIndex;
            if (index < 0)
                return;
            _settings.Profiles.Remove(Current);
            _list.Items.RemoveAt(index);
            if (_list.Items.Count > 0)
                _list.SelectedIndex = Math.Min(index, _list.Items.Count - 1);
            else
                UpdateEditor();
        }

        private string UniqueName(string baseName)
        {
            string name = baseName;
            for (int i = 2; _settings.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); i++)
                name = baseName + " " + i;
            return name;
        }

        private async Task TestConnectionAsync()
        {
            Profile p = Current;
            if (p == null)
                return;
            if (Profile.IsBlank(p.Host) || Profile.IsBlank(p.RemoteDir))
            {
                _testStatus.Text = "Host and remote directory are required.";
                return;
            }

            Profile snapshot = p.Clone();
            int timeout = _settings.TimeoutSeconds;
            _test.Enabled = false;
            _testStatus.Text = "Connecting...";
            try
            {
                await Task.Run(() => Uploader.TestConnection(snapshot, timeout));
                if (!IsDisposed)
                    _testStatus.Text = "OK - the remote directory is ready.";
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                    return;
                _testStatus.Text = "Failed.";
                MessageBox.Show(this, ex.Message, "Test connection - " + snapshot.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                    _test.Enabled = true;
            }
        }

        private void SaveAndClose()
        {
            for (int i = 0; i < _settings.Profiles.Count; i++)
            {
                Profile p = _settings.Profiles[i];
                string error = null;
                Control field = null;
                if (Profile.IsBlank(p.Name))
                    (error, field) = ("Name is required.", _name);
                else if (_settings.Profiles.Take(i).Any(o => string.Equals(o.Name.Trim(), p.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    (error, field) = ("Another profile already uses the name \"" + p.Name.Trim() + "\".", _name);
                else if (Profile.IsBlank(p.Host))
                    (error, field) = ("Host is required.", _host);
                else if (Profile.IsBlank(p.RemoteDir))
                    (error, field) = ("Remote directory is required.", _remoteDir);

                if (error != null)
                {
                    _list.SelectedIndex = i;
                    MessageBox.Show(this, error, "ClipLift Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    field.Focus();
                    return;
                }
            }

            foreach (Profile p in _settings.Profiles)
            {
                p.Name = p.Name.Trim();
                p.Host = p.Host.Trim();
                p.RemoteDir = p.RemoteDir.Trim();
                p.PathPrefix = (p.PathPrefix ?? "").Trim();
            }
            _settings.KeepImage = _keepImage.Checked;
            _settings.TrailingSpace = _trailingSpace.Checked;

            if (_autostart.Enabled)
            {
                try
                {
                    Autostart.Enabled = _autostart.Checked;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Cannot change the startup setting:\n" + ex.Message, "ClipLift Settings",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            Saved?.Invoke(_settings);
            Close();
        }

        /// <summary>ListBox that can refresh one item's text; selection events raised meanwhile are flagged.</summary>
        private sealed class ProfileList : ListBox
        {
            public bool Refreshing { get; private set; }

            public void RefreshItemAt(int index)
            {
                if (index < 0 || index >= Items.Count)
                    return;
                Refreshing = true;
                try
                {
                    RefreshItem(index);
                }
                finally
                {
                    Refreshing = false;
                }
            }
        }
    }
}
