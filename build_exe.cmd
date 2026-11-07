@echo off
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /platform:x64 /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Runtime.Serialization.dll /resource:korean.patch,korean.patch /out:Sakatsuku_Korean_Patch.exe src\Program.cs src\PatchEngine.cs
exit /b %errorlevel%
