```
Usage:
  Timer.exe [time] [options]

Time:
  90, 1.5          : seconds
  5m, 1h30m, 90s   : with units (h, m, s)
  1:30, 1:02:03    : m:s or h:m:s

Options:
  -a, --all                : Show timer on all displays
  -b, --blink=TIME         : Blink the text during the last TIME
  -c, --color=NAME|#RRGGBB : Set text color (name, #RGB, #RRGGBB or #AARRGGBB)
  -e, --exec=COMMAND       : Run a command (via cmd /c) when the timer ends
  -f, --font=NAME          : Set font family
  -h, --help,              : Show this help message
  -i, --invert             : Draw text in the inverted color of the background (Windows 10 2004+)
  -o, --opacity=NUM        : Set window opacity (0.1 - 1.0)
  -p, --pos=tl|tr|bl|br    : Screen corner position
  -s, --size=NUM           : Set font size
  -u, --until=HH:mm[:ss]   : Count down to the next occurrence of that time of day
  -w, --warn=TIME:COLOR    : Change text color when the remaining time is TIME or less (repeatable)
  -x, --x=NUM              : Set window X position (overrides --pos)
  -y, --y=NUM              : Set window Y position (overrides --pos)
  -bg, --bg=COLOR          : Draw a rounded background plate behind the text
  -bgo, --bg-opacity=NUM   : Plate opacity (0.0 - 1.0, default 0.6)
  -cl, --clock             : Show current time instead of countdown
  -ct, --clickthrough      : Make window ignore mouse clicks
  -sd, --sound=NAME        : System sound at timer end (Beep, Asterisk, Exclamation, Hand, Question)
  -sdt, --sound-timing=WHEN: When to play sound (start|middle|end, default end)
  -sdf, --sound-file=PATH  : Play an audio file (WAV, MP3, WMA, ...) at the sound timing
  -sp, --speak=TEXT        : Speak the specified text using speech synthesis
  -spv, --speak-voice=NAME : Select voice for speech synthesis (use with --speak)
  -spt, --speak-timing=WHEN: When to speak (start|middle|end, default end)