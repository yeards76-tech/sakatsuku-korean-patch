@echo off
cd /d "%~dp0"
echo Building Sakatsuku_Korean_Patch.exe...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /platform:anycpu /r:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll,System.Runtime.Serialization.dll "/resource:korean.patch,korean.patch" "/out:Sakatsuku_Korean_Patch.exe" src\Program.cs
if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Sakatsuku_Korean_Patch.exe built successfully!
) else (
    echo [FAILED] Build failed.
)
pause
