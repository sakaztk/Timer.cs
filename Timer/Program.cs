using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace TimerApp
{
    internal sealed class Options
    {
        public double Seconds;
        public int? X;
        public int? Y;
        public float FontSize;
        public string FontName = "Segoe UI";
        public double Opacity = 0.95;
        public Color TextColor = Color.White;
        public bool ClickThrough;
        public bool ShowClock;
        public bool Invert;
        public bool ShowAll;
        public string Pos = "";
        public SystemSound? EndSound;
        public string? SpeakText;
        public string? SpeakVoice;
        public string SpeakTiming = "end";
        public string SoundTiming = "end";
    }

    internal static class OptionParser
    {
        private static readonly Dictionary<string, Action<Options>> Flags = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Func<Options, string, string?>> Values = new(StringComparer.Ordinal);

        private static readonly Dictionary<string, SystemSound> Sounds = new(StringComparer.OrdinalIgnoreCase)
        {
            ["beep"] = SystemSounds.Beep,
            ["asterisk"] = SystemSounds.Asterisk,
            ["exclamation"] = SystemSounds.Exclamation,
            ["hand"] = SystemSounds.Hand,
            ["question"] = SystemSounds.Question,
        };

        static OptionParser()
        {
            AddFlag(o => o.ShowAll = true, "--all", "-a");
            AddFlag(o => o.ClickThrough = true, "--clickthrough", "-ct");
            AddFlag(o => o.ShowClock = true, "--clock", "-cl");
            AddFlag(o => o.Invert = true, "--invert", "-i");

            AddValue((o, v) => ParseInt(v, n => o.X = n), "--x", "-x");
            AddValue((o, v) => ParseInt(v, n => o.Y = n), "--y", "-y");
            AddValue((o, v) => ParseDouble(v, n => o.FontSize = (float)n), "--size", "-s");
            AddValue((o, v) =>
            {
                o.FontName = string.IsNullOrWhiteSpace(v) ? "Segoe UI" : v;
                return null;
            }, "--font", "-f");
            AddValue((o, v) => ParseDouble(v, n => o.Opacity = n), "--opacity", "-o");
            AddValue((o, v) =>
            {
                o.TextColor = Color.FromName(v);
                return null;
            }, "--color", "-c");
            AddValue((o, v) =>
            {
                string pos = v.ToLowerInvariant();
                if (pos is not ("tl" or "tr" or "bl" or "br")) return $"Unknown position: {v}";
                o.Pos = pos;
                return null;
            }, "--pos", "-p");
            AddValue((o, v) =>
            {
                if (!Sounds.TryGetValue(v, out var sound)) return $"Unknown sound: {v}";
                o.EndSound = sound;
                return null;
            }, "--sound", "-sd");
            AddValue((o, v) =>
            {
                o.SpeakText = v;
                return null;
            }, "--speak", "-sp");
            AddValue((o, v) =>
            {
                o.SpeakVoice = v;
                return null;
            }, "--speak-voice", "-spv");
            AddValue((o, v) =>
            {
                if (!TimeSpec.TryParseUntil(v, DateTime.Now, out var seconds)) return $"Invalid time of day: {v}";
                o.Seconds = seconds;
                return null;
            }, "--until", "-u");
            AddValue((o, v) => ParseTiming(v, t => o.SpeakTiming = t), "--speak-timing", "-spt");
            AddValue((o, v) => ParseTiming(v, t => o.SoundTiming = t), "--sound-timing", "-sdt");
        }

        public static bool TryParse(string[] args, out Options options, out string? error)
        {
            options = new Options();
            error = null;

            int index = 0;
            if (args.Length > 0 && !args[0].StartsWith("-"))
            {
                if (!TimeSpec.TryParseDuration(args[0], out var seconds))
                {
                    error = $"Invalid time: {args[0]}";
                    return false;
                }
                options.Seconds = seconds;
                index = 1;
            }

            for (; index < args.Length; index++)
            {
                string arg = args[index];
                int eq = arg.IndexOf('=');

                if (eq < 0)
                {
                    if (!Flags.TryGetValue(arg, out var flag))
                    {
                        error = $"Unknown option: {arg}";
                        return false;
                    }
                    flag(options);
                    continue;
                }

                string name = arg.Substring(0, eq);
                string value = arg.Substring(eq + 1);
                if (!Values.TryGetValue(name, out var handler))
                {
                    error = $"Unknown option: {name}";
                    return false;
                }

                error = handler(options, value);
                if (error != null) return false;
            }

            return true;
        }

        private static void AddFlag(Action<Options> apply, params string[] names)
        {
            foreach (var name in names) Flags[name] = apply;
        }

        private static void AddValue(Func<Options, string, string?> apply, params string[] names)
        {
            foreach (var name in names) Values[name] = apply;
        }

        private static string? ParseInt(string value, Action<int> set)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                return $"Invalid number: {value}";
            set(n);
            return null;
        }

        private static string? ParseDouble(string value, Action<double> set)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n))
                return $"Invalid number: {value}";
            set(n);
            return null;
        }

        private static string? ParseTiming(string value, Action<string> set)
        {
            string timing = value.ToLowerInvariant();
            if (timing is not ("start" or "middle" or "end"))
                return $"Invalid timing (start|middle|end): {value}";
            set(timing);
            return null;
        }
    }

    internal static class TimeSpec
    {
        private static readonly Regex UnitPattern = new(
            @"^(?:(?<h>\d+(?:\.\d+)?)h)?(?:(?<m>\d+(?:\.\d+)?)m)?(?:(?<s>\d+(?:\.\d+)?)s)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] TimeOfDayFormats = { "H:mm", "H:mm:ss" };

        public static bool TryParseDuration(string text, out double seconds)
        {
            seconds = 0;
            text = text.Trim();
            if (text.Length == 0) return false;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain))
            {
                if (!double.IsFinite(plain) || plain < 0) return false;
                seconds = plain;
                return true;
            }

            if (text.Contains(':'))
            {
                var parts = text.Split(':');
                if (parts.Length < 2 || parts.Length > 3) return false;

                double total = 0;
                foreach (var part in parts)
                {
                    if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                        || !double.IsFinite(value) || value < 0)
                        return false;
                    total = total * 60 + value;
                }
                seconds = total;
                return true;
            }

            var m = UnitPattern.Match(text);
            if (!m.Success) return false;
            if (!m.Groups["h"].Success && !m.Groups["m"].Success && !m.Groups["s"].Success) return false;

            seconds = Part(m, "h") * 3600 + Part(m, "m") * 60 + Part(m, "s");
            return true;
        }

        public static bool TryParseUntil(string text, DateTime now, out double seconds)
        {
            seconds = 0;
            if (!DateTime.TryParseExact(text.Trim(), TimeOfDayFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var time))
                return false;

            var target = now.Date + time.TimeOfDay;
            if (target <= now) target = target.AddDays(1);

            seconds = (target - now).TotalSeconds;
            return true;
        }

        private static double Part(Match m, string name)
        {
            var group = m.Groups[name];
            return group.Success ? double.Parse(group.Value, CultureInfo.InvariantCulture) : 0;
        }
    }

    internal class TimerForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private readonly record struct FrameState(string Text, Color Color);

        private const int ULW_ALPHA = 2;
        private const byte AC_SRC_OVER = 0;
        private const byte AC_SRC_ALPHA = 1;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        private const int SRCCOPY = 0x00CC0020;
        private const int CAPTUREBLT = 0x40000000;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int EdgeMargin = 0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest,
            int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        private readonly Options _o;
        private readonly Screen? _targetScreen;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly double _durationSeconds;
        private readonly double _opacity;
        private readonly float _fontSize;
        private readonly Bitmap _measureBitmap = new(1, 1);
        private readonly Graphics _measureGraphics;

        private Font? _font;
        private float _fontScale;
        private string? _measuredText;
        private float _measuredScale;
        private SizeF _measuredSize;
        private FrameState? _lastFrame;
        private bool _invertActive;

        private bool _middleSoundPlayed = false;
        private bool _middleSpeechPlayed = false;
        private bool _endSpeechStarted = false;

        public TimerForm(Options options, Screen? targetScreen = null)
        {
            _o = options;
            _targetScreen = targetScreen;
            _durationSeconds = Math.Max(0.0, options.Seconds);
            _opacity = Math.Clamp(options.Opacity, 0.1, 1.0);
            _fontSize = options.FontSize > 0 ? options.FontSize : 48;

            _measureGraphics = Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            ShowInTaskbar = false;

            _timer = new System.Windows.Forms.Timer { Interval = (_o.ShowClock && !_o.Invert) ? 1000 : 100 };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            if (!_o.ClickThrough)
            {
                MouseClick += (s, e) => Close();
            }

            Width = 400;
            Height = 200;
        }

        // Prevent the form from taking focus when shown
        protected override bool ShowWithoutActivation => true;

        private double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

        private double Remaining => Math.Max(0.0, _durationSeconds - ElapsedSeconds);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_o.SoundTiming == "start") PlaySound();
            if (_o.SpeakTiming == "start") StartSpeech();

            EnableInvert();

            var frame = GetFrameState();
            UpdateWindowSizeAndPosition(frame.Text);
            Render(frame, true);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateTimedEvents();
            UpdateEndCheck();
            if (IsDisposed) return;

            var frame = GetFrameState();
            bool resized = UpdateWindowSizeAndPosition(frame.Text);
            Render(frame, resized);
        }

        private void UpdateTimedEvents()
        {
            if (_durationSeconds <= 0) return;

            double elapsed = ElapsedSeconds;

            if (!_middleSoundPlayed && _o.SoundTiming == "middle" && elapsed >= _durationSeconds / 2)
            {
                PlaySound();
                _middleSoundPlayed = true;
            }
            if (!_middleSpeechPlayed && _o.SpeakTiming == "middle" && elapsed >= _durationSeconds / 2)
            {
                StartSpeech();
                _middleSpeechPlayed = true;
            }
        }

        private void UpdateEndCheck()
        {
            if (_durationSeconds <= 0 || ElapsedSeconds < _durationSeconds) return;
            if (_endSpeechStarted) return;
            _endSpeechStarted = true;

            if (_o.SoundTiming == "end") PlaySound();

            string? text = _o.SpeakText;
            if (_o.SpeakTiming == "end" && !string.IsNullOrEmpty(text))
            {
                string? voice = _o.SpeakVoice;
                System.Threading.Tasks.Task.Run(() =>
                {
                    Speak(text, voice);
                    if (!IsDisposed) BeginInvoke((Action)StopAndClose);
                });
                return;
            }

            StopAndClose();
        }

        private void StopAndClose()
        {
            _timer.Stop();
            Close();
        }

        private void PlaySound()
        {
            _o.EndSound?.Play();
        }

        private void StartSpeech()
        {
            string? text = _o.SpeakText;
            if (string.IsNullOrEmpty(text)) return;

            string? voice = _o.SpeakVoice;
            System.Threading.Tasks.Task.Run(() => Speak(text, voice));
        }

        private void Speak(string text, string? voice)
        {
            using var synth = new SpeechSynthesizer();
            if (!string.IsNullOrEmpty(voice))
            {
                try { synth.SelectVoice(voice); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Voice select error: {ex.Message}", "Speech Error");
                }
            }
            synth.Speak(text);
        }

        private FrameState GetFrameState()
        {
            return new FrameState(GetDisplayText(), _o.TextColor);
        }

        private string GetDisplayText()
        {
            if (_o.SpeakTiming == "end" && _endSpeechStarted)
                return "";

            if (_o.ShowClock) return DateTime.Now.ToString("HH:mm:ss");

            if (_durationSeconds <= 0) return "0.0s";

            double remaining = Remaining;
            if (remaining >= 3600)
            {
                var ts = TimeSpan.FromSeconds(remaining);
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
            else if (remaining >= 60)
            {
                var ts = TimeSpan.FromSeconds(remaining);
                return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
            else
            {
                return remaining.ToString("0.0") + "s";
            }
        }

        private static float GetScale(Rectangle area)
        {
            var pt = new POINT { x = area.Left + area.Width / 2, y = area.Top + area.Height / 2 };
            IntPtr monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
                return dpiX / 96f;
            return 1f;
        }

        private Font GetFont(float scale)
        {
            if (_font == null || _fontScale != scale)
            {
                _font?.Dispose();
                _font = new Font(_o.FontName, _fontSize * scale * 96f / 72f, FontStyle.Bold, GraphicsUnit.Pixel);
                _fontScale = scale;
            }
            return _font;
        }

        private SizeF MeasureText(string text, float scale)
        {
            if (_measuredText != text || _measuredScale != scale)
            {
                _measuredSize = _measureGraphics.MeasureString(text, GetFont(scale));
                _measuredText = text;
                _measuredScale = scale;
            }
            return _measuredSize;
        }

        private bool UpdateWindowSizeAndPosition(string text)
        {
            var screenArea = (_targetScreen ?? Screen.FromControl(this)).WorkingArea;
            float scale = GetScale(screenArea);
            var sizeF = MeasureText(text, scale);

            int newWidth = (int)Math.Ceiling(sizeF.Width);
            int newHeight = (int)Math.Ceiling(sizeF.Height);

            bool resized = newWidth != Width || newHeight != Height;
            if (resized)
            {
                Width = newWidth;
                Height = newHeight;
            }

            int left = _o.X ?? (screenArea.Left + (screenArea.Width - Width) / 2);
            int top = _o.Y ?? (screenArea.Top + (screenArea.Height - Height) / 2);

            switch (_o.Pos)
            {
                case "tl":
                    left = screenArea.Left + EdgeMargin;
                    top = screenArea.Top + EdgeMargin;
                    break;
                case "tr":
                    left = screenArea.Right - Width - EdgeMargin;
                    top = screenArea.Top + EdgeMargin;
                    break;
                case "bl":
                    left = screenArea.Left + EdgeMargin;
                    top = screenArea.Bottom - Height - EdgeMargin;
                    break;
                case "br":
                    left = screenArea.Right - Width - EdgeMargin;
                    top = screenArea.Bottom - Height - EdgeMargin;
                    break;
            }

            left = Math.Max(screenArea.Left + EdgeMargin, Math.Min(left, screenArea.Right - Width - EdgeMargin));
            top = Math.Max(screenArea.Top + EdgeMargin, Math.Min(top, screenArea.Bottom - Height - EdgeMargin));

            Left = left;
            Top = top;

            return resized;
        }

        private void EnableInvert()
        {
            if (!_o.Invert) return;
            _invertActive = SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE);
        }

        private void Render(FrameState frame, bool force)
        {
            var font = _font;
            if (font == null || IsDisposed || !IsHandleCreated || Width <= 0 || Height <= 0) return;
            if (!force && !_invertActive && _lastFrame == frame) return;
            _lastFrame = frame;

            using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                DrawText(g, frame, font, frame.Color);
            }

            if (_invertActive)
            {
                InvertTextColors(bmp, CaptureBackground(bmp.Width, bmp.Height));
            }

            Present(bmp);
        }

        private void DrawText(Graphics g, FrameState frame, Font font, Color color)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var size = g.MeasureString(frame.Text, font);
            float x = (Width - size.Width) / 2f;
            float y = (Height - size.Height) / 2f;

            using var brush = new SolidBrush(color);
            g.DrawString(frame.Text, font, brush, x, y);
        }

        private byte[] CaptureBackground(int w, int h)
        {
            using var bg = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var gb = Graphics.FromImage(bg))
            {
                IntPtr screenDc = GetDC(IntPtr.Zero);
                IntPtr dstDc = gb.GetHdc();
                try
                {
                    BitBlt(dstDc, 0, 0, w, h, screenDc, Left, Top, SRCCOPY | CAPTUREBLT);
                }
                finally
                {
                    gb.ReleaseHdc(dstDc);
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
            return ReadPixels(bg);
        }

        private static void InvertTextColors(Bitmap textBmp, byte[] background)
        {
            var pixels = ReadPixels(textBmp);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] == 0) continue;
                pixels[i] = (byte)(255 - background[i]);
                pixels[i + 1] = (byte)(255 - background[i + 1]);
                pixels[i + 2] = (byte)(255 - background[i + 2]);
            }
            WritePixels(textBmp, pixels);
        }

        private static byte[] ReadPixels(Bitmap bmp)
        {
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var buffer = new byte[data.Stride * data.Height];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
                return buffer;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static void WritePixels(Bitmap bmp, byte[] buffer)
        {
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private void Present(Bitmap bmp)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero;
            IntPtr oldBmp = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                oldBmp = SelectObject(memDc, hBmp);

                var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
                var src = new POINT { x = 0, y = 0 };
                var dst = new POINT { x = Left, y = Top };
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = (byte)Math.Round(_opacity * 255),
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size,
                                    memDc, ref src, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (oldBmp != IntPtr.Zero) SelectObject(memDc, oldBmp);
                if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED;
                if (_o?.ClickThrough == true)
                {
                    cp.ExStyle |= WS_EX_TRANSPARENT;
                }
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle &= ~WS_EX_APPWINDOW;
                cp.ExStyle |= WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _font?.Dispose();
                _measureGraphics.Dispose();
                _measureBitmap.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal class MultiFormContext : ApplicationContext
    {
        private int _openForms;

        public MultiFormContext(IEnumerable<Form> forms)
        {
            var list = forms.Where(f => f != null).ToList();
            _openForms = list.Count;
            if (_openForms == 0)
            {
                ExitThread();
                return;
            }

            foreach (var f in list)
            {
                f.FormClosed += (s, e) =>
                {
                    if (Interlocked.Decrement(ref _openForms) == 0)
                        ExitThread();
                };
                // Show here to ensure OnShown runs for each
                f.Show();
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length == 0 || args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase)
                                              || a.Equals("-h") || a.Equals("-?")))
            {
                ShowHelp();
                return;
            }

            if (!OptionParser.TryParse(args, out var options, out var error))
            {
                MessageBox.Show(error ?? "Invalid arguments.", "Timer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ShowHelp();
                return;
            }

            try
            {
                if (options.ShowAll)
                {
                    var forms = Screen.AllScreens
                        .Select(screen => (Form)new TimerForm(options, screen))
                        .ToList();

                    Application.Run(new MultiFormContext(forms));
                }
                else
                {
                    using var form = new TimerForm(options);
                    Application.Run(form);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Timer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ShowHelp()
        {
            string voices = "";
            try
            {
                using var synth = new SpeechSynthesizer();
                var voiceList = synth.GetInstalledVoices()
                    .Select(v => v.VoiceInfo.Name)
                    .ToList();
                voices = voiceList.Count > 0
                    ? string.Join(Environment.NewLine + "    ", voiceList)
                    : "(No voices found)";
            }
            catch
            {
                voices = "(Failed to get voices)";
            }

            Form helpForm = new Form()
            {
                Text = "Help",
                Width = 750,
                Height = 500,
                StartPosition = FormStartPosition.CenterScreen
            };

            TextBox textBox = new TextBox()
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                ScrollBars = ScrollBars.Vertical
            };

            textBox.Text = $@"Usage:
  Timer.exe [time] [options]

Time:
  90, 1.5          : seconds
  5m, 1h30m, 90s   : with units (h, m, s)
  1:30, 1:02:03    : m:s or h:m:s

Options:
  -h, --help,              : Show this help message
  -a, --all                : Show timer on all displays
  -c, --color=NAME         : Set text color
  -f, --font=NAME          : Set font family
  -o, --opacity=NUM        : Set window opacity (0.1 - 1.0)
  -p, --pos=tl|tr|bl|br    : Screen corner position
  -s, --size=NUM           : Set font size
  -x, --x=NUM              : Set window X position (overrides --pos)
  -y, --y=NUM              : Set window Y position (overrides --pos)
  -u, --until=HH:mm[:ss]   : Count down to the next occurrence of that time of day
  -cl, --clock             : Show current time instead of countdown
  -ct, --clickthrough      : Make window ignore mouse clicks
  -i, --invert             : Draw text in the inverted color of the background (Windows 10 2004+)
  -sd, --sound=NAME        : System sound at timer end (Beep, Asterisk, Exclamation, Hand, Question)
  -sdt, --sound-timing=WHEN: When to play sound (start|middle|end, default end)
  -sp, --speak=TEXT        : Speak the specified text using speech synthesis
  -spv, --speak-voice=NAME : Select voice for speech synthesis (use with --speak)
  -spt, --speak-timing=WHEN: When to speak (start|middle|end, default end)

Available voices:
    {voices}
";

            helpForm.Controls.Add(textBox);
            helpForm.ShowDialog();
        }
    }
}