using System;
using System.Drawing;
using System.Media;
using System.Windows.Forms;
using System.Linq;

namespace TimerApp
{
    internal class TimerForm : Form
    {
        private readonly System.Windows.Forms.Timer _timer;
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

        private const int PaddingPx = 0;
        private const int EdgeMargin = 0;

        public TimerForm(double seconds, int? x, int? y, float fontSize, string fontName,
                         double opacity, Color textColor, bool clickThrough, bool showClock,
                         string pos, SystemSound endSound)
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

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            Opacity = Clamp(opacity, 0.1, 1.0);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = _showClock ? 1000 : 100;
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

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateWindowSizeAndPosition();
            Invalidate();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateWindowSizeAndPosition();
            UpdateEndCheck();
            Invalidate();
        }

        private void UpdateEndCheck()
        {
            if (_durationSeconds > 0 && DateTime.Now >= _endTime)
            {
                _timer.Stop();
                if (_endSound != null) _endSound.Play();
                Close();
            }
        }

        private string GetDisplayText()
        {
            if (_showClock) return DateTime.Now.ToString("HH:mm:ss");

            if (_durationSeconds <= 0) return "0.0s";

            var remaining = Math.Max(0.0, (_endTime - DateTime.Now).TotalSeconds);
            if (remaining >= 3600)
            {
                var ts = TimeSpan.FromSeconds(remaining);
                return string.Format("{0}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            }
            else if (remaining >= 60)
            {
                var ts = TimeSpan.FromSeconds(remaining);
                return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
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
            }

            var screenArea = Screen.FromControl(this).WorkingArea;

            int left = _x ?? (screenArea.Left + (screenArea.Width - Width) / 2);
            int top = _y ?? (screenArea.Top + (screenArea.Height - Height) / 2);

            switch ((_pos ?? "").ToLowerInvariant())
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
                    cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                }
                return cp;
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
            string pos = "";
            SystemSound endSound = SystemSounds.Beep;

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
                    if ((arg.StartsWith("--x=") || arg.StartsWith("-x=")) && int.TryParse(arg.Split('=')[1], out var xv)) x = xv;
                    else if ((arg.StartsWith("--y=") || arg.StartsWith("-y=")) && int.TryParse(arg.Split('=')[1], out var yv)) y = yv;
                    else if ((arg.StartsWith("--size=") || arg.StartsWith("-s=")) && float.TryParse(arg.Split('=')[1], out var fv)) fontSize = fv;
                    else if ((arg.StartsWith("--font=") || arg.StartsWith("-f="))) fontName = arg.Split('=')[1];
                    else if ((arg.StartsWith("--opacity=") || arg.StartsWith("-o=")) && double.TryParse(arg.Split('=')[1], out var ov)) opacity = ov;
                    else if ((arg.StartsWith("--color=") || arg.StartsWith("-c="))) textColor = Color.FromName(arg.Split('=')[1]);
                    else if (arg == "--clickthrough" || arg == "-ct") clickThrough = true;
                    else if (arg == "--clock" || arg == "-clk") showClock = true;
                    else if ((arg.StartsWith("--pos=") || arg.StartsWith("-p="))) pos = arg.Split('=')[1];
                    else if ((arg.StartsWith("--sound=") || arg.StartsWith("-snd=")))
                    {
                        switch (arg.Split('=')[1].ToLower())
                        {
                            case "beep": endSound = SystemSounds.Beep; break;
                            case "asterisk": endSound = SystemSounds.Asterisk; break;
                            case "exclamation": endSound = SystemSounds.Exclamation; break;
                            case "hand": endSound = SystemSounds.Hand; break;
                            case "question": endSound = SystemSounds.Question; break;
                            default: ShowHelp(); return;
                        }
                    }
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

            using (var frm = new TimerForm(seconds, x, y, fontSize, finalFont, opacity, textColor, clickThrough, showClock, finalPos, endSound))
            {
                Application.Run(frm);
            }
        }

        private static void ShowHelp()
        {
            Form helpForm = new Form()
            {
                Text = "Help",
                Width = 750,
                Height = 400,
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

            textBox.Text = @"Usage:
  Timer.exe [seconds] [options]

Options:
  -h, --help,           : Show this help message
  -x=NUM                : Set window X position (overrides --pos)
  -y=NUMM               : Set window Y position (overrides --pos)
  -s, --size=NUM        : Set font size
  -f, --font=NAME       : Set font family
  -o, --opacity=NUM     : Set window opacity (0.1 - 1.0)
  -c, --color=NAME      : Set text color
  -ct, --clickthrough   : Make window ignore mouse clicks
  -snd --sound=NAME     : System sound at timer end (Beep, Asterisk, Exclamation, Hand, Question)
  -clk, --clock         : Show current time instead of countdown
  -p, --pos=tl|tr|bl|br : Screen corner position
";

            helpForm.Controls.Add(textBox);
            helpForm.ShowDialog();
        }
    }
}
