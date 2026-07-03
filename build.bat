@echo off
echo Compiling UNICUT...
set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
%CSC% /nologo /target:winexe /win32icon:logo.ico /resource:logo.ico /out:UNICUT.exe /lib:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF" /reference:PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll,System.Xaml.dll,System.Drawing.dll,System.Windows.Forms.dll unicut.cs
if %errorlevel% neq 0 (
    echo Build failed.
    exit /b %errorlevel%
)
echo Build succeeded! You can now run UNICUT.exe.
