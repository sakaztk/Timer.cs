using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Windows.Forms;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;

using TimerType = System.Windows.Forms.Timer;

namespace TimerApp
{
    internal class TimerForm : Form
    {
        private readonly TimerType _timer;
        private DateTime _endTime;
        private readonly double _durationSeconds;
        private readonly bool _clickThrough;
        private readonly bool _showClock;
        private readonly Color _textColor;
        private readonly float _fontSize;
        private readonly string _fontName;
        private readonly string _pos;
        private readonly int? _x;
        private readonly int? _y;
        private readonly SystemSound _endSound;

        private readonly string _speakText;
        private readonly string _speakVoice;
        private readonly string _speakTiming;
        private readonly string _soundTiming;

        private readonly Screen _targetScreen;

        private bool _middleSoundPlayed = false;
        private bool _middleSpeechPlayed = false;
        private bool _endSpeechStarted = false;

        private const int PaddingPx = 0;
        private const int EdgeMargin = 0;

        public TimerForm(double seconds, int? x, int? y, float fontSize, string fontName,
                         double opacity, Color textColor, bool clickThrough, bool showClock,
                         string pos, SystemSound endSound,
                         string speakText, string speakVoice, string speakTiming, string soundTiming,
                         Screen targetScreen = null)
        {
            _durationSeconds = Math.Max(0.0, seconds);
            _clickThrough = clickThrough;
            _showClock = showClock;
            _textColor = textColor;
            _fontSize = fontSize > 0 ? fontSize : 48;
            _fontName = string.IsNullOrWhiteSpace(fontName) ? "Segoe UI" : fontName;
            _pos = pos ?? "";
            _x = x;
            _y = y;
            _endSound = endSound;
            _speakText = speakText;
            _speakVoice = speakVoice;
            _speakTiming = string.IsNullOrEmpty(speakTiming) ? "end" : speakTiming;
            _soundTiming = string.IsNullOrEmpty(soundTiming) ? "end" : soundTiming;
            _targetScreen = targetScreen;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            Opacity = Clamp(opacity, 0.1, 1.0);

            _timer = new TimerType { Interval = _showClock ? 1000 : 100 };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            if (!_clickThrough)
            {
                MouseClick += (s, e) => Close();
            }

            Width = 400;
            Height = 200;

            if (_durationSeconds > 0)
            {
                _endTime = DateTime.Now.AddSeconds(_durationSeconds);
            }
        }

        // Prevent the form from taking focus when shown
        protected override bool ShowWithoutActivation => true;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_soundTiming == "start" && _endSound != null)
                _endSound.Play();

            if (_speakTiming == "start" && !string.IsNullOrEmpty(_speakText))
            {
                System.Threading.Tasks.Task.Run(() => Speak(_speakText, _speakVoice, true));
            }

            UpdateWindowSizeAndPosition();
            Invalidate();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateWindowSizeAndPosition();
            UpdateTimedEvents();
            UpdateEndCheck();
            Invalidate();
        }

        private void UpdateTimedEvents()
        {
            if (_durationSeconds > 0)
            {
                var now = DateTime.Now;
                var remaining = (_endTime - now).TotalSeconds;
                var elapsed = (_durationSeconds - remaining);

                if (!_middleSoundPlayed && _soundTiming == "middle" && elapsed >= _durationSeconds / 2)
                {
                    if (_endSound != null) _endSound.Play();
                    _middleSoundPlayed = true;
                }
                if (!_middleSpeechPlayed && _speakTiming == "middle" && elapsed >= _durationSeconds / 2)
                {
                    if (!string.IsNullOrEmpty(_speakText))
                    {
                        System.Threading.Tasks.Task.Run(() => Speak(_speakText, _speakVoice, true));
                    }
                    _middleSpeechPlayed = true;
                }
            }
        }

        private void UpdateEndCheck()
        {
            if (_durationSeconds > 0 && DateTime.Now >= _endTime)
            {
                if (_endSpeechStarted) return;
                _endSpeechStarted = true;

                if (_soundTiming == "end" && _endSound != null)
                    _endSound.Play();

                if (_speakTiming == "end" && !string.IsNullOrEmpty(_speakText))
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Speak(_speakText, _speakVoice, true);
                        this.Invoke((Action)(() =>
                        {
                            _timer.Stop();
                            Close();
                        }));
                    });
                    return;
                }

                _timer.Stop();
                Close();
            }
        }

        private void Speak(string text, string voice, bool sync)
        {
            using (var synth = new SpeechSynthesizer())
            {
                if (!string.IsNullOrEmpty(voice))
                {
                    try { synth.SelectVoice(voice); }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Voice select error: " + ex.Message, "Speech Error");
                    }
                }
                if (sync)
                    synth.Speak(text);
                else
                    synth.SpeakAsync(text);
            }
        }

        private string GetDisplayText()
        {
            if (_speakTiming == "end" && _endSpeechStarted)
                return "";

            if (_showClock) return DateTime.Now.ToString("HH:mm:ss");

            if (_durationSeconds <= 0) return "0.0s";

            var remaining = Math.Max(0.0, (_endTime - DateTime.Now).TotalSeconds);
            if (remaining >= 3600)
            {
                var ts = TimeSpan.FromSeconds(remaining);
                return ((int)ts.TotalHours) + ":" + ts.Minutes.ToString("D2") + ":" + ts.Seconds.ToString("D2");
            }
            else if (remaining >= 60)
            {
                var ts = TimeSpan.FromSeconds(remaining);
                return ts.Minutes.ToString("D2") + ":" + ts.Seconds.ToString("D2");
            }
            else
            {
                return remaining.ToString("0.0") + "s";
            }
        }

        private void UpdateWindowSizeAndPosition()
        {
            string text = GetDisplayText();

            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font(_fontName, _fontSize, FontStyle.Bold))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                var sizeF = g.MeasureString(text, font);

                int newWidth = (int)Math.Ceiling(sizeF.Width) + PaddingPx * 2;
                int newHeight = (int)Math.Ceiling(sizeF.Height) + PaddingPx * 2;

                if (newWidth != Width || newHeight != Height)
                {
                    Width = newWidth;
                    Height = newHeight;
                }

                var screenArea = (_targetScreen ?? Screen.FromControl(this)).WorkingArea;

                int left = _x.HasValue ? _x.Value : (screenArea.Left + (screenArea.Width - Width) / 2);
                int top = _y.HasValue ? _y.Value : (screenArea.Top + (screenArea.Height - Height) / 2);

                string posLower = (_pos ?? "").ToLowerInvariant();
                switch (posLower)
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
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(Color.Black);

            string text = GetDisplayText();

            using (var font = new Font(_fontName, _fontSize, FontStyle.Bold))
            using (var brush = new SolidBrush(_textColor))
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                var sizeF = e.Graphics.MeasureString(text, font);

                float x = PaddingPx + (Width - PaddingPx * 2 - sizeF.Width) / 2f;
                float y = PaddingPx + (Height - PaddingPx * 2 - sizeF.Height) / 2f;

                e.Graphics.DrawString(text, font, brush, x, y);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                if (_clickThrough)
                {
                    cp.ExStyle |= 0x20;
                }

                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_APPWINDOW = 0x00040000;
                const int WS_EX_NOACTIVATE = 0x08000000;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle &= ~WS_EX_APPWINDOW;
                cp.ExStyle |= WS_EX_NOACTIVATE;

                return cp;
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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
                // Show here so each form's OnShown runs
                f.Show();
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0 || args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase)
                                              || a.Equals("-h") || a.Equals("-?")))
            {
                ShowHelp();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            double seconds = 0;
            int? x = null;
            int? y = null;
            float fontSize = 0;
            string fontName = null;
            double opacity = 0.95;
            Color textColor = Color.White;
            bool clickThrough = false;
            bool showClock = false;
            string pos = null;
            SystemSound endSound = null;

            string speakText = null;
            string speakVoice = null;
            string speakTiming = null;
            string soundTiming = null;

            bool showAll = false;

            int argIndex = 0;
            if (args.Length > 0 && !args[0].StartsWith("-"))
            {
                if (!double.TryParse(args[0], out seconds)) seconds = 0;
                argIndex = 1;
            }

            for (int i = argIndex; i < args.Length; i++)
            {
                var arg = args[i];
                try
                {
                    if (arg == "--all" || arg == "-a") showAll = true;
                    else if ((arg.StartsWith("--x=") || arg.StartsWith("-x=")) && int.TryParse(arg.Substring(arg.IndexOf('=') + 1), out var xv)) x = xv;
                    else if ((arg.StartsWith("--y=") || arg.StartsWith("-y=")) && int.TryParse(arg.Substring(arg.IndexOf('=') + 1), out var yv)) y = yv;
                    else if ((arg.StartsWith("--size=") || arg.StartsWith("-s=")) && float.TryParse(arg.Substring(arg.IndexOf('=') + 1), out var fv)) fontSize = fv;
                    else if ((arg.StartsWith("--font=") || arg.StartsWith("-f="))) fontName = arg.Substring(arg.IndexOf('=') + 1);
                    else if ((arg.StartsWith("--opacity=") || arg.StartsWith("-o=")) && double.TryParse(arg.Substring(arg.IndexOf('=') + 1), out var ov)) opacity = ov;
                    else if ((arg.StartsWith("--color=") || arg.StartsWith("-c="))) textColor = Color.FromName(arg.Substring(arg.IndexOf('=') + 1));
                    else if (arg == "--clickthrough" || arg == "-ct") clickThrough = true;
                    else if (arg == "--clock" || arg == "-cl") showClock = true;
                    else if ((arg.StartsWith("--pos=") || arg.StartsWith("-p="))) pos = arg.Substring(arg.IndexOf('=') + 1);
                    else if ((arg.StartsWith("--sound=") || arg.StartsWith("-sd=")))
                    {
                        var sndValue = arg.Substring(arg.IndexOf('=') + 1).ToLower();
                        switch (sndValue)
                        {
                            case "beep": endSound = SystemSounds.Beep; break;
                            case "asterisk": endSound = SystemSounds.Asterisk; break;
                            case "exclamation": endSound = SystemSounds.Exclamation; break;
                            case "hand": endSound = SystemSounds.Hand; break;
                            case "question": endSound = SystemSounds.Question; break;
                            default: ShowHelp(); return;
                        }
                    }
                    else if (arg.StartsWith("--speak=") || arg.StartsWith("-sp="))
                        speakText = arg.Substring(arg.IndexOf('=') + 1);
                    else if (arg.StartsWith("--speak-voice=") || arg.StartsWith("-spv="))
                        speakVoice = arg.Substring(arg.IndexOf('=') + 1);
                    else if (arg.StartsWith("--speak-timing=") || arg.StartsWith("-spt="))
                        speakTiming = arg.Substring(arg.IndexOf('=') + 1).ToLower();
                    else if (arg.StartsWith("--sound-timing=") || arg.StartsWith("-sdt="))
                        soundTiming = arg.Substring(arg.IndexOf('=') + 1).ToLower();
                    else { ShowHelp(); return; }
                }
                catch
                {
                    ShowHelp();
                    return;
                }
            }

            string finalFont = string.IsNullOrWhiteSpace(fontName) ? "Segoe UI" : fontName;
            string finalPos = pos ?? "";

            if (showAll)
            {
                var forms = new List<Form>();
                foreach (var screen in Screen.AllScreens)
                {
                    var frm = new TimerForm(
                        seconds, x, y, fontSize, finalFont, opacity, textColor, clickThrough, showClock, finalPos, endSound,
                        speakText, speakVoice, speakTiming, soundTiming,
                        targetScreen: screen);
                    forms.Add(frm);
                }

                Application.Run(new MultiFormContext(forms));
            }
            else
            {
                using (var frm = new TimerForm(
                    seconds, x, y, fontSize, finalFont, opacity, textColor, clickThrough, showClock, finalPos, endSound,
                    speakText, speakVoice, speakTiming, soundTiming))
                {
                    Application.Run(frm);
                }
            }
        }

        private static void ShowHelp()
        {
            string voices = "";
            try
            {
                using (var synth = new SpeechSynthesizer())
                {
                    var voiceList = synth.GetInstalledVoices()
                        .Select(v => v.VoiceInfo.Name)
                        .ToList();
                    voices = voiceList.Count > 0
                        ? string.Join(Environment.NewLine + "    ", voiceList)
                        : "(No voices found)";
                }
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
  Timer.exe [seconds] [options]

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
  -cl, --clock             : Show current time instead of countdown
  -ct, --clickthrough      : Make window ignore mouse clicks
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
