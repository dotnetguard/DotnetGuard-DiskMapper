# DotnetGuard DiskMap

A disk usage visualizer built with C# / .NET 6 and WPF — see what's eating your disk space as a squarified treemap, drill into folders, jump straight to any file in Explorer. No third-party treemap library, no telemetry.

Built by [dotnetguard.blog](https://dotnetguard.blog).

<img width="1028" height="632" alt="image" src="https://github.com/user-attachments/assets/ee145bbe-7775-40cb-b9d5-7f2e42084420" />
<img width="1029" height="632" alt="image" src="https://github.com/user-attachments/assets/a4396340-453b-4c29-9ccb-58c784e244bd" />


## Features

- Squarified treemap layout, written from scratch (no charting library)
- **Live, progressive scanning** — folders and files appear and grow on screen as the scan runs, instead of waiting for it to finish
- Single-pass directory enumeration (`FileSystemInfo`, not two separate API calls per folder)
- File-type legend — color-coded by extension, sized by total bytes, matches the treemap tile colors exactly
- Click a folder tile (or a breadcrumb segment) to drill in; every breadcrumb level is clickable, not just "Up"
- Right-click any tile → **Show in Explorer** (opens the folder, or selects the file)
- Smooth fade transition when navigating between folders
- Dark, terminal-styled UI

## Requirements

- Windows 10/11
- To build from source: [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)

## Running

```bash
git clone https://github.com/dotnetguard/DotnetGuard-DiskMapper.git
cd DotnetGuard-DiskMapper
dotnet build
dotnet run --project DotnetGuard.DiskMap.App
```

## Publishing a standalone exe

```bash
dotnet publish DotnetGuard.DiskMap.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Produces a single `.exe` under `DotnetGuard.DiskMap.App/bin/Release/net6.0-windows/win-x64/publish/` — no separate .NET install needed to run it.

## Project structure

```
DotnetGuard.DiskMap.Core   DiskNode, LayoutRect — plain models, no WPF dependency
DotnetGuard.DiskMap.Data   DiskScanner (recursive scan + progress), TreemapLayout (squarified algorithm)
DotnetGuard.DiskMap.App    WPF UI (Views), dark theme, app icon
```

## How the treemap layout works

`TreemapLayout` implements the squarified treemap algorithm (Bruls, Huizing, van Wijk, 2000): items are laid out row by row, always picking the row split that keeps rectangle aspect ratios as close to square as possible, rather than the older "slice and dice" approach that produces long thin slivers. It's ~100 lines of pure geometry — no dependency on any charting or visualization package.

## License

No license file yet — all rights reserved by default until one is added.
