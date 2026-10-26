chcp 65001
set SECONDS=3
pushd %~dp0

timer %SECONDS%
timer %SECONDS% --all
timer %SECONDS% --sound=Hand
timer %SECONDS% --sound=Beep --sound-timing=start
timer %SECONDS% --sound=Asterisk --sound-timing=end
timer %SECONDS% --sound=Exclamation --sound-timing=middle
timer %SECONDS% --sound-file=%~dp0\test.wav --sound-timing=start
timer %SECONDS% --speak=test
timer %SECONDS% --speak=start --speak-timing=start
timer %SECONDS% --speak=end --speak-timing=end
timer %SECONDS% --speak=middle --speak-timing=middle
timer %SECONDS% --speak="はるか" --speak-voice="Microsoft Haruka Desktop"
timer %SECONDS% --speak=David --speak-voice="Microsoft David Desktop"
timer %SECONDS% --pos=tl --color=#ff0000 --bg=#00ff00 --bg-opacity=0.3
timer %SECONDS% --pos=tr --color=#0f0 --bg=#00f
timer %SECONDS% --pos=bl --color=#700000ff --bg=#30ff0000
timer %SECONDS% --pos=br
timer %SECONDS% --clock --clickthrough
timer %SECONDS% --font=consolas --color=red --opacity=0.2
timer %SECONDS% --invert
timer %SECONDS% --x=100 --y=100 --size=100
timer %SECONDS% --exec=notepad.exe
timer 10 --warn=8s:orange --warn=4s:red
timer --until=23:00 --speak="Please click the displayed text to close it." --speak-timing=start --speak-voice="Microsoft David Desktop"


timer %SECONDS% -a
timer %SECONDS% -sd=Hand
timer %SECONDS% -sd=Beep -sdt=start
timer %SECONDS% -sd=Asterisk -sdt=end
timer %SECONDS% -sd=Exclamation -sdt=middle
timer %SECONDS% -sf=%~dp0\test.wav -sdt=start
timer %SECONDS% -sp=test
timer %SECONDS% -sp=start -spt=start
timer %SECONDS% -sp=end -spt=end
timer %SECONDS% -sp=middle -spt=middle
timer %SECONDS% -sp="はるか" -spv="Microsoft Haruka Desktop"
timer %SECONDS% -sp=David -spv="Microsoft David Desktop"
timer %SECONDS% -p=tl -c=#ff0000 -bg=#00ff00 -bgo=0.3
timer %SECONDS% -p=tr -c=#0f0 -bg=#00f
timer %SECONDS% -p=bl -c=#700000ff -bg=#30ff0000
timer %SECONDS% -p=br
timer %SECONDS% -cl -ct
timer %SECONDS% -f=consolas -c=red -o=0.2
timer %SECONDS% -i
timer %SECONDS% -x=100 -y=100 -s=100
timer %SECONDS% -e=notepad.exe
timer 10 -w=8s:orange -w=4s:red
timer -u=23:00 --sp="Please click the displayed text to close it." --spt=start -spv="Microsoft David Desktop"

popd
pause
