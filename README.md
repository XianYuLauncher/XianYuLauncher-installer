# XianYuLauncher Installer

[![GitHub Stars](https://img.shields.io/github/stars/N123999/XianYuLauncher.svg?style=flat-square&label=⭐%20Stars)](https://github.com/XianYuLauncher/Installer)
[![GitHub Release](https://img.shields.io/github/v/release/N123999/XianYuLauncher-Help?style=flat-square%20Release&logo=github)](https://github.com/XianYuLauncher/Installer/releases)
[![Docs Online](https://img.shields.io/badge/Docs-Online-0EA5E9?style=flat-square&logo=gitbook&logoColor=white)](https://docs.xianyulauncher.com)
[![Bilibili](https://img.shields.io/badge/bilibili-@Spirit灵动工作室-FF69B4?style=flat-square&logo=bilibili&logoColor=white)](https://space.bilibili.com/3493299136498148)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6.svg)](https://www.microsoft.com/windows)
[![Made with .NET](https://img.shields.io/badge/Made%20with-.NET%2010-purple.svg)](https://dotnet.microsoft.com/)

A modern Windows installer designed to simplify the installation process of **XianYuLauncher** - a Minecraft launcher application.

## Why This Installer?

Manually installing XianYuLauncher can be cumbersome due to:
- Complex MSIX dependency chains (Windows App Runtime)
- Manual certificate installation requirements
- Multiple setup steps for different components

This installer automates most of the process, providing a seamless experience for end-users.

## Technology Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| **Backend** | .NET 10 | Robust application logic and Windows integration |
| **Frontend** | WinUI 3 | Modern Fluent Design UI with native performance |
| **Packaging** | MSIX | Reliable Windows installation packages |
| **Dependencies** | Windows App Runtime | Required framework components |

## Installation (For Users)

1. **Download** the latest installer from [Releases](https://github.com/XianYu-Launcher/XianYuLauncher-Installer/releases)
2. **Run** `XianYuLauncher-installer.exe`
3. **Select** installation directory
4. **Click** "Install" and let the installer handle everything!

## Building From Source

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or newer
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- [Windows 10/11 SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/)

### Setup
```bash
# Clone the repository
git clone https://github.com/XianYuLauncher/XianYuLauncher-installer.git
cd XianYuLauncher-installer

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Run in debug mode
dotnet run
