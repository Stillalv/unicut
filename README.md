# UNICUT

UNICUT is a lightweight, features-rich screen capture and screenshot annotation utility for Windows built using C# and WPF. It runs quietly in the system tray and provides a variety of convenient ways to capture, edit, and share your screenshots.

## Features

- **Global Hotkey Support**: Press `Ctrl + Shift + S` from anywhere to trigger screen capture instantly.
- **Floating Widget**: A tiny, draggable, and sleek capture button that stays on top of all windows.
- **Interactive Overlay**: Choose the exact region of the screen you want to capture with a live, responsive cropping overlay.
- **Built-in Editor**: Immediately crop, draw, highlight, copy to your clipboard, or save screenshots to your disk.
- **System Tray Integration**: Runs quietly in the background without cluttering your taskbar.

## Quick Start

### Build from Source
To compile UNICUT from source, run the provided build script:
```cmd
build.bat
```
This script will compile the code using the standard .NET Framework compiler (`csc.exe`) and output the executable `UNICUT.exe` in the root folder.

### Running UNICUT
Double-click `UNICUT.exe` to run the application. 
- You will see a tray icon in your taskbar.
- Press `Ctrl + Shift + S` or click the floating widget to select a region of your screen.
- A popup editor will appear where you can annotate, copy, or save your screenshot.

## Releases
You can download the pre-compiled binary directly from the [GitHub Releases Page](https://github.com/Stillalv/unicut/releases).
