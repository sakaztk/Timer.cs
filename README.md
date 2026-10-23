```
Usage:
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