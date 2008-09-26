@echo off
if NOT "%1" == "" goto :Build

%windir%\Microsoft.NET\Framework\v3.5\MSBuild.exe SvnBridge.msbuild /p:Configuration=Debug /p:CodePlex3rdParty=..\3rdParty
goto :End

:Build
%windir%\Microsoft.NET\Framework\v3.5\MSBuild.exe SvnBridge.msbuild /p:Configuration=Debug /p:CodePlex3rdParty=..\3rdParty /t:%1

:End