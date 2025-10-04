```
Usage:
Timer.exe \[seconds] \[options]

Options:
-h, --help,              : Show this help message
-x=NUM                   : Set window X position (overrides --pos)
-y=NUMM                  : Set window Y position (overrides --pos)
-s, --size=NUM           : Set font size
-f, --font=NAME          : Set font family
-o, --opacity=NUM        : Set window opacity (0.1 - 1.0)
-c, --color=NAME         : Set text color
-ct, --clickthrough      : Make window ignore mouse clicks
-snd --sound=NAME        : System sound at timer end (Beep, Asterisk, Exclamation, Hand, Question)
-clk, --clock            : Show current time instead of countdown
-p, --pos=tl|tr|bl|br    : Screen corner position
-sp, --speak=TEXT        : Speak the specified text using speech synthesis
-v, --voice=NAME         : Select voice for speech synthesis (use with --speak)
-st, --speak-timing=WHEN : When to speak (start|middle|end, default end)
-sd, --sound-timing=WHEN : When to play sound (start|middle|end, default end)
