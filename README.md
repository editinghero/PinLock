# PinLock 

A premium Windows application pinninf tool that allows you to "pin" specific applications and automatically locks your computer when you switch away from them. Perfect for guest usage and securing sensitive data.


![PinLock](https://img.shields.io/badge/PinLock-orange?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)
![Platform](https://img.shields.io/badge/Platform-Windows-blue?style=for-the-badge)

## Interface
<img width="1224" height="821" alt="image" src="https://github.com/user-attachments/assets/4220cf86-2267-43e5-8e17-d53b987691e3" />


## Installation & Setup

### Installation
1. Download PinLock.exe from the latest [Release](https://github.com/editinghero/PinLock/releases)
2. Handle Windows SmartScreen warning if prompted
3. Launch the application
4. Optionally add to Windows Defender exclusions


### Creating Shortcuts
- **Desktop Shortcut**: Right-click PinLock.exe → "Create shortcut"
- **Start Menu**: Copy to Start Menu Programs folder
- **Auto-start**: Add shortcut to Windows Startup folder

### Basic Usage
1. **Launch PinLock** - The application will scan for running programs
2. **Select an Application** - Choose from the list of detected running applications
3. **Click "Pin Selected App"** - The selected app will maximize and stay on top
4. **Stay Focused** - Work normally in your pinned application
5. **Automatic Protection** - Switch to any other app and Windows locks immediately

## Features

### Core Security Features
- **Application Pinning**: Select any running application and pin it to stay maximized and on top
- **Automatic Locking**: Windows locks instantly when you switch to any other application
- **Resize Protection**: Attempting to resize the PinLock window while monitoring triggers an immediate lock
- **Real-time Monitoring**: Uses Windows API hooks for instant focus change detection


## System Requirements

- **Operating System**: Windows 10 or Windows 11
- **Framework**: .NET Framework 4.8 (usually pre-installed)
- **Architecture**: x64 (64-bit Windows)



## Troubleshooting

### App Not Detected?
- Click "Refresh Apps" to update the list
- Use "Add Custom App" to manually launch applications
- Ensure the application has a visible window (not just running in background)

### Windows SmartScreen Warning?
- Click "More info" then "Run anyway"
- Add the application folder to Windows Defender exclusions
- Normal behavior for unsigned applications

##  Development Scripts

| Script | Description |
|--------|-------------|
| `build_release.bat` | Build the application using MSBuild |
| `PinLock.csproj` | Visual Studio project file |
| `MainWindow.xaml` | Main UI layout and styling |
| `MainWindow.xaml.cs` | Core application logic |

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details.

---

**PinLock**
