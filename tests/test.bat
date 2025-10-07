chcp 65001
set SECONDS=5
pushd %~dp0

timer %SECONDS%
timer %SECONDS% --sound=Hand
timer %SECONDS% --sound=Beep --sound-timing=start
timer %SECONDS% --sound=Asterisk --sound-timing=end
timer %SECONDS% --sound=Exclamation --sound-timing=middle
timer %SECONDS% --speak=test
timer %SECONDS% --speak=start --speak-timing=start
timer %SECONDS% --speak=end --speak-timing=end
timer %SECONDS% --speak=middle --speak-timing=middle
timer %SECONDS% --speak="はるか" --speak-voice="Microsoft Haruka Desktop"
timer %SECONDS% --speak=David --speak-voice="Microsoft David Desktop"
timer %SECONDS% --pos=tl
timer %SECONDS% --pos=tr
timer %SECONDS% --pos=bl
timer %SECONDS% --pos=br
timer %SECONDS% --clock --clickthrough
timer %SECONDS% --font=consolas --color=red --opacity=0.2
timer %SECONDS% --x=100 --y=100 --size=100

timer %SECONDS% -sd=Hand
timer %SECONDS% -sd=Beep -sdt=start
timer %SECONDS% -sd=Asterisk -sdt=end
timer %SECONDS% -sd=Exclamation -sdt=middle
timer %SECONDS% -sp=test
timer %SECONDS% -sp=start -spt=start
timer %SECONDS% -sp=end -spt=end
timer %SECONDS% -sp=middle -spt=middle
timer %SECONDS% -sp="はるか" -spv="Microsoft Haruka Desktop"
timer %SECONDS% -sp=David -spv="Microsoft David Desktop"
timer %SECONDS% -p=tl
timer %SECONDS% -p=tr
timer %SECONDS% -p=bl
timer %SECONDS% -p=br
timer %SECONDS% -cl -ct
timer %SECONDS% -f=consolas -c=red -o=0.2
timer %SECONDS% -x=100 -y=100 -s=100

popd
pause
