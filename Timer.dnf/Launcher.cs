using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Speech.Synthesis;
using System.Text;
using System.Windows.Forms;

namespace TimerApp
{
    internal sealed class ColorButton : Button
    {
        private Color _value = Color.White;

        public event EventHandler ColorChanged;

        public ColorButton(Color value)
        {
            UseVisualStyleBackColor = false;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderColor = Color.Gray;
            Width = 90;
            Height = 26;
            Value = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color Value
        {
            get { return _value; }
            set
            {
                _value = Color.FromArgb(value.R, value.G, value.B);
                BackColor = _value;
                ForeColor = _value.GetBrightness() > 0.55f ? Color.Black : Color.White;
                Text = ToHex(_value);
            }
        }

        public static string ToHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            using (var dialog = new ColorDialog { Color = _value, FullOpen = true })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                Value = dialog.Color;
            }

            var handler = ColorChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    internal sealed class WarnEntry
    {
        public string Time;
        public Color Color;

        public override string ToString()
        {
            return Time + "  \u2192  " + ColorButton.ToHex(Color);
        }
    }

    internal static class LauncherStore
    {
        private static string FilePath
        {
            get
            {
                string exe = Application.ExecutablePath;
                if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    exe = Assembly.GetEntryAssembly()?.Location ?? exe;
                }
                return Path.ChangeExtension(exe, ".ini");
            }
        }

        public static Dictionary<string, Dictionary<string, string>> Load()
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return result;

                Dictionary<string, string> current = null;
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.TrimEnd();
                    if (line.Length == 0) continue;

                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        current = new Dictionary<string, string>(StringComparer.Ordinal);
                        result[line.Substring(1, line.Length - 2)] = current;
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (current == null || eq <= 0) continue;
                    current[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return result;
        }

        public static void Save(Dictionary<string, Dictionary<string, string>> data)
        {
            try
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (directory != null) Directory.CreateDirectory(directory);

                var sb = new StringBuilder();
                foreach (var section in data)
                {
                    sb.AppendLine("[" + section.Key + "]");
                    foreach (var pair in section.Value)
                    {
                        sb.AppendLine(pair.Key + "=" + pair.Value);
                    }
                    sb.AppendLine();
                }
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    internal static class Prompt
    {
        public static string Show(IWin32Window owner, string title, string label, string text)
        {
            using (var form = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            })
            {
                var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(10) };
                var prompt = new Label { Text = label, AutoSize = true };
                var box = new TextBox { Text = text, Width = 280 };
                var buttons = new FlowLayoutPanel
                {
                    AutoSize = true,
                    FlowDirection = FlowDirection.RightToLeft,
                    Dock = DockStyle.Fill
                };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };

                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                layout.Controls.Add(prompt);
                layout.Controls.Add(box);
                layout.Controls.Add(buttons);
                form.Controls.Add(layout);
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                return form.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
            }
        }
    }

    internal sealed class LauncherForm : Form
    {
        private const string DefaultFontName = "Segoe UI";
        private const string LastSection = "last";

        private static readonly string[] PositionCodes = { null, "tl", "tr", "bl", "br" };

        private readonly Dictionary<string, Control> _fields = new Dictionary<string, Control>();
        private Dictionary<string, string> _defaults;
        private Dictionary<string, Dictionary<string, string>> _store;
        private bool _loading = true;

        private ComboBox _mode, _font, _position, _sound, _soundTiming, _voice, _speakTiming, _preset;
        private TextBox _time, _blink, _soundFile, _speakText, _exec, _warnTime, _preview;
        private DateTimePicker _until;
        private NumericUpDown _size, _x, _y, _plateOpacity;
        private TrackBar _opacity;
        private Label _opacityLabel, _error;
        private CheckBox _clock, _invert, _plate, _custom, _all, _clickThrough, _closeAfter;
        private ColorButton _color, _plateColor, _warnColor;
        private ListBox _warnList;
        private Button _start;

        public LauncherForm()
        {
            Text = "Timer Launcher";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, 780);
            MinimumSize = new Size(560, 520);

            BuildUi();

            _defaults = GetState();
            _store = LauncherStore.Load();

            Dictionary<string, string> last;
            if (_store.TryGetValue(LastSection, out last)) SetState(last);

            RefreshPresets(null);
            HookChanges();

            _loading = false;
            UpdatePreview();
        }

        private void BuildUi()
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                Padding = new Padding(8)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddGroup(content, "Time", BuildTimeGroup);
            AddGroup(content, "Appearance", BuildAppearanceGroup);
            AddGroup(content, "Position", BuildPositionGroup);
            AddGroup(content, "Warning colors and blinking", BuildAlertGroup);
            AddGroup(content, "Sound and speech", BuildSoundGroup);
            AddGroup(content, "When the timer ends", BuildEndGroup);

            Controls.Add(content);
            Controls.Add(BuildBottom());
        }

        private static void AddGroup(TableLayoutPanel content, string title, Action<TableLayoutPanel> fill)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(4)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fill(table);

            var group = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6),
                Margin = new Padding(0, 0, 0, 8)
            };
            group.Controls.Add(table);

            int row = content.RowCount;
            content.RowCount = row + 1;
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.Controls.Add(group, 0, row);
        }

        private static void Row(TableLayoutPanel table, string label, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            if (label != null)
            {
                table.Controls.Add(new Label
                {
                    Text = label,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(3, 6, 10, 6)
                }, 0, row);
            }
            table.Controls.Add(control, 1, row);
        }

        private static Label Caption(string text)
        {
            return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 7, 6, 0) };
        }

        private static FlowLayoutPanel Flow(params Control[] controls)
        {
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Anchor = AnchorStyles.Left
            };
            foreach (var control in controls)
            {
                if (!(control is Label)) control.Margin = new Padding(0, 2, 8, 2);
                flow.Controls.Add(control);
            }
            return flow;
        }

        private T Register<T>(string key, T control) where T : Control
        {
            _fields[key] = control;
            return control;
        }

        private ComboBox Combo(string key, string[] items, int selected, bool editable)
        {
            var box = new ComboBox
            {
                DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 2, 8, 2)
            };
            box.Items.AddRange(items);
            if (selected >= 0) box.SelectedIndex = selected;
            return Register(key, box);
        }

        private TextBox Field(string key, string text, int width)
        {
            var box = new TextBox { Text = text, Margin = new Padding(0, 2, 8, 2) };
            if (width > 0)
            {
                box.Width = width;
            }
            else
            {
                box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }
            return Register(key, box);
        }

        private CheckBox Check(string key, string text, bool isChecked)
        {
            return Register(key, new CheckBox { Text = text, Checked = isChecked, AutoSize = true });
        }

        private NumericUpDown Number(string key, int min, int max, int value)
        {
            return Register(key, new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 80 });
        }

        private void BuildTimeGroup(TableLayoutPanel t)
        {
            _mode = Combo("Mode", new[] { "Countdown", "Until a time of day" }, 0, false);
            _time = Field("Time", "", 160);
            _until = Register("Until", new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm:ss",
                ShowUpDown = true,
                Width = 110,
                Value = DateTime.Today.AddHours(18)
            });
            _clock = Check("Clock", "Show the current time instead of the countdown", false);

            Row(t, "Mode", _mode);
            Row(t, "Time", Flow(_time, _until, Caption("e.g. 5m, 1h30m, 90, 1:30")));
            Row(t, null, _clock);
        }

        private void BuildAppearanceGroup(TableLayoutPanel t)
        {
            _font = Combo("Font", FontFamily.Families.Select(f => f.Name).ToArray(), -1, true);
            _font.Text = DefaultFontName;
            _size = Number("Size", 6, 1000, 48);
            _color = Register("Color", new ColorButton(Color.White));
            _opacity = Register("Opacity", new TrackBar
            {
                Minimum = 10,
                Maximum = 100,
                TickFrequency = 10,
                LargeChange = 10,
                Value = 95,
                Width = 240
            });
            _opacityLabel = Caption("95%");
            _invert = Check("Invert", "Invert the text color against the background (Windows 10 2004+)", false);
            _plate = Check("Plate", "Background plate", false);
            _plateColor = Register("PlateColor", new ColorButton(Color.Black));
            _plateOpacity = Number("PlateOpacity", 0, 100, 60);

            Row(t, "Font", _font);
            Row(t, "Size", Flow(_size, Caption("pt")));
            Row(t, "Text color", _color);
            Row(t, "Opacity", Flow(_opacity, _opacityLabel));
            Row(t, null, _invert);
            Row(t, "Plate", Flow(_plate, _plateColor, Caption("opacity"), _plateOpacity, Caption("%")));
        }

        private void BuildPositionGroup(TableLayoutPanel t)
        {
            _position = Combo("Position", new[] { "Center", "Top left", "Top right", "Bottom left", "Bottom right" }, 0, false);
            _custom = Check("Custom", "Specify X / Y", false);
            _x = Number("X", -20000, 20000, 0);
            _y = Number("Y", -20000, 20000, 0);
            _all = Check("All", "Show on all displays", false);
            _clickThrough = Check("ClickThrough", "Click-through (the window ignores the mouse)", false);

            Row(t, "Position", _position);
            Row(t, null, Flow(_custom, Caption("X"), _x, Caption("Y"), _y));
            Row(t, null, Flow(_all, _clickThrough));
        }

        private void BuildAlertGroup(TableLayoutPanel t)
        {
            _warnList = new ListBox
            {
                Height = 72,
                IntegralHeight = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 2, 8, 2)
            };
            _warnTime = new TextBox { Width = 80 };
            _warnColor = new ColorButton(Color.Red);

            var add = new Button { Text = "Add", AutoSize = true };
            add.Click += (s, e) => AddWarning();
            var remove = new Button { Text = "Remove", AutoSize = true };
            remove.Click += (s, e) => RemoveWarning();

            _blink = Field("Blink", "", 80);

            Row(t, "Warnings", _warnList);
            Row(t, null, Flow(Caption("Remaining time"), _warnTime, _warnColor, add, remove));
            Row(t, "Blink", Flow(_blink, Caption("during the last TIME (e.g. 10s)")));
        }

        private void BuildSoundGroup(TableLayoutPanel t)
        {
            _sound = Combo("Sound", new[] { "(none)", "Beep", "Asterisk", "Exclamation", "Hand", "Question" }, 0, false);
            _soundFile = Field("SoundFile", "", 300);
            var browse = new Button { Text = "Browse...", AutoSize = true };
            browse.Click += (s, e) => BrowseSoundFile();
            _soundTiming = Combo("SoundTiming", new[] { "start", "middle", "end" }, 2, false);

            _speakText = Field("SpeakText", "", 0);
            _voice = Combo("Voice", new[] { "(default)" }, 0, false);
            LoadVoices();
            _speakTiming = Combo("SpeakTiming", new[] { "start", "middle", "end" }, 2, false);

            Row(t, "System sound", _sound);
            Row(t, "Sound file", Flow(_soundFile, browse));
            Row(t, "Sound timing", _soundTiming);
            Row(t, "Speak text", _speakText);
            Row(t, "Voice", _voice);
            Row(t, "Speak timing", _speakTiming);
        }

        private void BuildEndGroup(TableLayoutPanel t)
        {
            _exec = Field("Exec", "", 0);
            Row(t, "Run command", _exec);
        }

        private Control BuildBottom()
        {
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ColumnCount = 1,
                Padding = new Padding(8)
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _preview = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Height = 64,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            _error = new Label { AutoSize = true, ForeColor = Color.Firebrick, Margin = new Padding(3, 4, 3, 4) };

            _preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            _preset.SelectedIndexChanged += (s, e) => LoadSelectedPreset();
            var save = new Button { Text = "Save as...", AutoSize = true };
            save.Click += (s, e) => SavePreset();
            var delete = new Button { Text = "Delete", AutoSize = true };
            delete.Click += (s, e) => DeletePreset();
            var reset = new Button { Text = "Reset", AutoSize = true };
            reset.Click += (s, e) => SetState(_defaults);
            var presets = Flow(Caption("Preset"), _preset, save, delete, reset);

            _closeAfter = Check("CloseAfter", "Close this window after starting", true);
            var help = new Button { Text = "Help", AutoSize = true };
            help.Click += (s, e) => Program.ShowHelp();
            var copy = new Button { Text = "Copy command", AutoSize = true };
            copy.Click += (s, e) => CopyCommand();
            _start = new Button { Text = "Start", AutoSize = true };
            _start.Click += (s, e) => StartTimer();
            AcceptButton = _start;
            var actions = Flow(_closeAfter, help, copy, _start);

            AddBottomRow(bottom, _preview);
            AddBottomRow(bottom, _error);
            AddBottomRow(bottom, presets);
            AddBottomRow(bottom, actions);
            return bottom;
        }

        private static void AddBottomRow(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(control, 0, row);
        }

        private void LoadVoices()
        {
            try
            {
                using (var synth = new SpeechSynthesizer())
                {
                    foreach (var voice in synth.GetInstalledVoices())
                    {
                        _voice.Items.Add(voice.VoiceInfo.Name);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void HookChanges()
        {
            EventHandler changed = (s, e) => UpdatePreview();

            foreach (var control in _fields.Values)
            {
                switch (control)
                {
                    case ColorButton button:
                        button.ColorChanged += changed;
                        break;
                    case TextBox text:
                        text.TextChanged += changed;
                        break;
                    case ComboBox combo:
                        combo.TextChanged += changed;
                        break;
                    case CheckBox check:
                        check.CheckedChanged += changed;
                        break;
                    case NumericUpDown number:
                        number.ValueChanged += changed;
                        break;
                    case TrackBar track:
                        track.ValueChanged += changed;
                        break;
                    case DateTimePicker picker:
                        picker.ValueChanged += changed;
                        break;
                }
            }
        }

        private void AddWarning()
        {
            string time = _warnTime.Text.Trim();
            double seconds;
            if (!TimeSpec.TryParseDuration(time, out seconds))
            {
                MessageBox.Show(this, "Invalid time: " + time, "Timer Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _warnList.Items.Add(new WarnEntry { Time = time, Color = _warnColor.Value });
            _warnTime.Clear();
            UpdatePreview();
        }

        private void RemoveWarning()
        {
            int index = _warnList.SelectedIndex;
            if (index < 0) return;

            _warnList.Items.RemoveAt(index);
            UpdatePreview();
        }

        private void BrowseSoundFile()
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "Audio files|*.wav;*.mp3;*.wma;*.m4a;*.aac|All files|*.*"
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) _soundFile.Text = dialog.FileName;
            }
        }

        private Dictionary<string, string> GetState()
        {
            var state = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in _fields)
            {
                state[pair.Key] = GetValue(pair.Value);
            }
            state["Warnings"] = string.Join(";", _warnList.Items.Cast<WarnEntry>()
                .Select(w => w.Time + "|" + ColorButton.ToHex(w.Color)));
            return state;
        }

        private void SetState(Dictionary<string, string> state)
        {
            bool previous = _loading;
            _loading = true;
            try
            {
                foreach (var pair in _fields)
                {
                    string value;
                    if (state.TryGetValue(pair.Key, out value)) SetValue(pair.Value, value);
                }

                string warnings;
                if (state.TryGetValue("Warnings", out warnings)) LoadWarnings(warnings);
            }
            finally
            {
                _loading = previous;
            }

            if (!previous) UpdatePreview();
        }

        private void LoadWarnings(string text)
        {
            _warnList.Items.Clear();
            foreach (var item in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int bar = item.IndexOf('|');
                Color color;
                if (bar <= 0 || !ColorParser.TryParse(item.Substring(bar + 1), out color)) continue;
                _warnList.Items.Add(new WarnEntry { Time = item.Substring(0, bar), Color = color });
            }
        }

        private static string GetValue(Control control)
        {
            switch (control)
            {
                case ColorButton button:
                    return ColorButton.ToHex(button.Value);
                case TextBox text:
                    return text.Text;
                case ComboBox combo:
                    return combo.Text;
                case CheckBox check:
                    return check.Checked ? "1" : "0";
                case NumericUpDown number:
                    return number.Value.ToString(CultureInfo.InvariantCulture);
                case TrackBar track:
                    return track.Value.ToString(CultureInfo.InvariantCulture);
                case DateTimePicker picker:
                    return picker.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                default:
                    return "";
            }
        }

        private static void SetValue(Control control, string value)
        {
            switch (control)
            {
                case ColorButton button:
                    Color color;
                    if (ColorParser.TryParse(value, out color)) button.Value = color;
                    break;
                case TextBox text:
                    text.Text = value;
                    break;
                case ComboBox combo:
                    combo.Text = value;
                    break;
                case CheckBox check:
                    check.Checked = value == "1";
                    break;
                case NumericUpDown number:
                    decimal amount;
                    if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
                        number.Value = Math.Max(number.Minimum, Math.Min(number.Maximum, amount));
                    break;
                case TrackBar track:
                    int position;
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out position))
                        track.Value = Math.Max(track.Minimum, Math.Min(track.Maximum, position));
                    break;
                case DateTimePicker picker:
                    DateTime time;
                    if (DateTime.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                        picker.Value = DateTime.Today.Add(time.TimeOfDay);
                    break;
            }
        }

        private void RefreshPresets(string select)
        {
            bool previous = _loading;
            _loading = true;
            try
            {
                _preset.Items.Clear();
                foreach (var name in _store.Keys.Where(k => !k.Equals(LastSection, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k))
                {
                    _preset.Items.Add(name);
                }
                _preset.SelectedItem = select;
            }
            finally
            {
                _loading = previous;
            }
        }

        private void LoadSelectedPreset()
        {
            if (_loading) return;

            var name = _preset.SelectedItem as string;
            Dictionary<string, string> state;
            if (name != null && _store.TryGetValue(name, out state)) SetState(state);
        }

        private void SavePreset()
        {
            string name = Prompt.Show(this, "Save preset", "Preset name", _preset.Text);
            if (string.IsNullOrEmpty(name)) return;

            if (name.Equals(LastSection, StringComparison.OrdinalIgnoreCase) || name.IndexOfAny(new[] { '[', ']', '=', '\r', '\n' }) >= 0)
            {
                MessageBox.Show(this, "This name cannot be used.", "Timer Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _store[name] = GetState();
            LauncherStore.Save(_store);
            RefreshPresets(name);
        }

        private void DeletePreset()
        {
            var name = _preset.SelectedItem as string;
            if (name == null) return;

            _store.Remove(name);
            LauncherStore.Save(_store);
            RefreshPresets(null);
        }

        private static string Percent(int value)
        {
            return (value / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private List<string> BuildArguments(out string error)
        {
            string problem = null;
            var args = new List<string>();
            bool until = _mode.SelectedIndex == 1;
            double seconds;

            if (!until)
            {
                string time = _time.Text.Trim();
                if (time.Length > 0)
                {
                    if (TimeSpec.TryParseDuration(time, out seconds)) args.Add(time);
                    else problem = "Invalid time: " + time;
                }
                else if (!_clock.Checked)
                {
                    problem = "Enter a time (for example 5m, 1h30m, 90 or 1:30).";
                }
            }
            else
            {
                args.Add("--until=" + _until.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            }

            if (_clock.Checked) args.Add("--clock");

            string font = _font.Text.Trim();
            if (font.Length > 0 && !font.Equals(DefaultFontName, StringComparison.OrdinalIgnoreCase))
                args.Add("--font=" + font);
            if (_size.Value != 48)
                args.Add("--size=" + _size.Value.ToString(CultureInfo.InvariantCulture));
            if (_color.Value.ToArgb() != Color.White.ToArgb())
                args.Add("--color=" + ColorButton.ToHex(_color.Value));
            if (_opacity.Value != 95)
                args.Add("--opacity=" + Percent(_opacity.Value));
            if (_invert.Checked)
                args.Add("--invert");

            if (_plate.Checked)
            {
                args.Add("--bg=" + ColorButton.ToHex(_plateColor.Value));
                if (_plateOpacity.Value != 60)
                    args.Add("--bg-opacity=" + Percent((int)_plateOpacity.Value));
            }

            string pos = PositionCodes[Math.Max(0, _position.SelectedIndex)];
            if (pos != null) args.Add("--pos=" + pos);
            if (_custom.Checked)
            {
                args.Add("--x=" + ((int)_x.Value).ToString(CultureInfo.InvariantCulture));
                args.Add("--y=" + ((int)_y.Value).ToString(CultureInfo.InvariantCulture));
            }
            if (_all.Checked) args.Add("--all");
            if (_clickThrough.Checked) args.Add("--clickthrough");

            foreach (WarnEntry entry in _warnList.Items)
            {
                args.Add("--warn=" + entry.Time + ":" + ColorButton.ToHex(entry.Color));
            }
            string blink = _blink.Text.Trim();
            if (blink.Length > 0)
            {
                if (TimeSpec.TryParseDuration(blink, out seconds)) args.Add("--blink=" + blink);
                else if (problem == null) problem = "Invalid blink time: " + blink;
            }

            bool hasSound = false;
            if (_sound.SelectedIndex > 0)
            {
                args.Add("--sound=" + _sound.Text.ToLowerInvariant());
                hasSound = true;
            }
            string file = _soundFile.Text.Trim();
            if (file.Length > 0)
            {
                if (File.Exists(file))
                {
                    args.Add("--sound-file=" + file);
                    hasSound = true;
                }
                else if (problem == null)
                {
                    problem = "Sound file not found: " + file;
                }
            }
            if (hasSound && _soundTiming.Text != "end")
                args.Add("--sound-timing=" + _soundTiming.Text);

            if (_speakText.Text.Length > 0)
            {
                args.Add("--speak=" + _speakText.Text);
                if (_voice.SelectedIndex > 0) args.Add("--speak-voice=" + _voice.Text);
                if (_speakTiming.Text != "end") args.Add("--speak-timing=" + _speakTiming.Text);
            }

            string command = _exec.Text.Trim();
            if (command.Length > 0) args.Add("--exec=" + command);

            error = problem;
            return args;
        }

        private static string Quote(string value)
        {
            if (value.Length > 0 && value.IndexOfAny(new[] { ' ', '\t', '"', '&', '|', '<', '>', '^', '%', '(', ')' }) < 0)
                return value;

            var sb = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    sb.Append('\\', backslashes);
                    sb.Append(c);
                }
                backslashes = 0;
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        private static string QuoteArgument(string arg)
        {
            int eq = arg.IndexOf('=');
            if (arg.StartsWith("-") && eq > 0) return arg.Substring(0, eq + 1) + Quote(arg.Substring(eq + 1));
            return Quote(arg);
        }

        private static string CommandLine(IEnumerable<string> args)
        {
            return string.Join(" ", args.Select(QuoteArgument));
        }

        private void UpdatePreview()
        {
            if (_loading) return;

            _opacityLabel.Text = _opacity.Value.ToString(CultureInfo.InvariantCulture) + "%";

            bool until = _mode.SelectedIndex == 1;
            _time.Visible = !until;
            _until.Visible = until;
            _x.Enabled = _custom.Checked;
            _y.Enabled = _custom.Checked;
            _plateColor.Enabled = _plate.Checked;
            _plateOpacity.Enabled = _plate.Checked;

            string error;
            var args = BuildArguments(out error);

            string exeName = Path.GetFileName(Application.ExecutablePath);
            _preview.Text = args.Count == 0 ? exeName : exeName + " " + CommandLine(args);
            _error.Text = error ?? "";
            _start.Enabled = error == null;
        }

        private void CopyCommand()
        {
            try
            {
                Clipboard.SetText(_preview.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Timer Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StartTimer()
        {
            string error;
            var args = BuildArguments(out error);
            if (error != null)
            {
                MessageBox.Show(this, error, "Timer Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Options options;
            string parseError;
            if (!OptionParser.TryParse(args.ToArray(), out options, out parseError))
            {
                MessageBox.Show(this, parseError ?? "Invalid arguments.", "Timer Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _store[LastSection] = GetState();
            LauncherStore.Save(_store);

            string exe = Application.ExecutablePath;
            string prefix = "";
            if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                prefix = Quote(Assembly.GetEntryAssembly()?.Location ?? "") + " ";
            }

            try
            {
                var process = Process.Start(new ProcessStartInfo(exe, prefix + CommandLine(args)) { UseShellExecute = false });
                if (process != null) process.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Timer Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_closeAfter.Checked) Close();
        }
    }
}