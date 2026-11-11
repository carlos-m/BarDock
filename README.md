<p align="right">
  <b>English</b> · <a href="README.es.md">Español</a>
</p>

<p align="center">
  <img src="docs/logo.png" alt="BarDock" width="520">
</p>

<p align="center">
  <strong>Your desktop, your way.</strong>
</p>

<p align="center">
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-1e1e1e.svg"></a>
  <img alt="Windows 11" src="https://img.shields.io/badge/Windows-11-1e1e1e.svg">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8-1e1e1e.svg">
</p>

<p align="center">
  <img src="docs/captura.png" alt="BarDock">
</p>

**BarDock** is a minimal top bar for Windows 11: your icons with dock-style magnification, clocks
for several time zones, the date, system status, the native tray and a preview of each app's
windows. It replaces the Windows taskbar and adapts to the way you work: top or bottom, icons
centered or aligned left, in English or Spanish.

## Documentation

- **[Installation guide](docs/INSTALL.md)**: requirements, installing, updating, uninstalling and
  common problems.
- **[User manual](docs/MANUAL.md)**: everything the bar does, menus, clocks, settings and FAQ.

## Features

- **Magnified icons**, dock style: they grow toward the inside of the screen while the icon under
  the cursor stays put, and the green running indicator grows with it.
- **Windows per icon**: hovering lists the app's open windows; clicking brings **only that one** to
  the front.
- **Real Start menu and task view**, without the Windows taskbar.
- **Native tray**: a chevron opens the real Windows overflow panel, with its original context
  menus.
- **Clocks** for several zones with a configurable vertical alias (`E`/`S`), font, bold, per-zone
  color, shared size and line spacing, plus the **date** with the weekday.
- **System status**: battery, network and volume (click and wheel for volume).
- **Customization**: top/bottom position, alignment, height, icon size/magnification/spacing,
  transparency and **English/Spanish**.
- **No dependencies**: self-contained installer; no .NET and no administrator rights.

## Install

Download `BarDockSetup.exe` from **Releases** and run it. The detailed steps are in the
**[installation guide](docs/INSTALL.md)**.

- **Per-user** install in `%LOCALAPPDATA%\Programs\BarDock` (no administrator).
- Creates Start menu shortcuts and, optionally, a desktop shortcut.
- Uninstall from **Settings → Apps → BarDock**.
- Silent install: `BarDockSetup.exe /S`.

> Windows may warn that the installer is unsigned: **More info → Run anyway**. It is our own
> executable, without a code-signing certificate.

## License

**MIT** — free and open software: you may use, copy, modify, merge, publish, distribute,
sublicense and sell copies, keeping the copyright notice and the license. See [LICENSE](LICENSE).

## Run from source

```bat
run.cmd
```

or build and run:

```bat
dotnet run --project BarDock.csproj
```

`publish.cmd` produces `dist\` (lightweight build, needs the .NET Desktop 8 runtime) and
`build-setup.cmd` produces `dist\BarDockSetup.exe` (self-contained, with the app embedded).

## Usage

| Action | Result |
| --- | --- |
| Hover the bar | Icons magnify; the icon under the cursor stays still |
| Hover an open icon | List of **its windows**; clicking brings **only that one** to the front |
| Click the Windows logo | Opens the **Start menu** (right click: all apps and settings) |
| Click task view | Opens **Win+Tab** (virtual desktops); can be hidden |
| Click the tray chevron | Opens the **Windows overflow panel** |
| Click the bell | Windows notification center |
| Click battery / network / volume | The matching panel (volume: click mutes, wheel adjusts) |
| Right click the bar | Add app, clocks and date, sizes, position, alignment, transparency, language, replace the Windows taskbar, start with Windows, reload, edit JSON, quit |
| Drag an `.exe` / `.lnk` / `.url` | Adds it to the bar |

On first run the bar is filled with the apps **actually pinned** to the taskbar (read from
`HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband`) and with the ones that are
already open.

### The Windows taskbar

With "Replace the Windows taskbar" enabled, the original bar is hidden by giving it an **empty
region** (`SetWindowRgn`), so it is not drawn but the shell keeps its tray. When you turn it off,
the taskbar comes back and BarDock moves **next to it** so you can still use its menu.

## Configuration

Settings live in `%APPDATA%\BarDock\settings.json` and are edited from the bar's context menu
(**Clocks and date**, **Height**, **Position**, **Alignment**, **Transparency**, **Language**…) or
by hand and then **Reload**.

```jsonc
{
  "version": 5,
  "barHeight": 36,
  "iconSize": 26,
  "maxScale": 2.2,
  "spacing": 8,
  "topMargin": 0,
  "replaceTaskbar": true,
  "barPosition": "top",       // "top" or "bottom"
  "barOpacity": 0.9,          // 0.3 - 1.0
  "iconAlignment": "left",    // "center" or "left"
  "showTaskView": true,
  "renderMode": "hardware",   // "hardware" or "software" (half the memory, a bit more CPU)
  "language": "en",           // "es" or "en"

  "clockFontFamily": null,
  "clockFontSize": 17,
  "clockBold": false,
  "clockLetterSpacing": 0.95,

  "dateAlias": "",            // empty = weekday (WED, MIÉ...)
  "dateColor": null,

  "items": [
    { "path": "shell:AppsFolder\\MSTeams_8wekyb3d8bbwe!MSTeams", "name": "Microsoft Teams" },
    { "path": "C:\\Path\\App.exe", "name": "Optional name" }
  ],

  "clocks": [
    { "label": "Madrid",   "zone": "Romance Standard Time", "alias": "ES", "color": null },
    { "label": "Santiago", "zone": "Pacific SA Standard Time", "alias": "CL" },
    { "label": "Lisbon",   "zone": "GMT Standard Time", "alias": "PT" },
    { "label": "Tel Aviv", "zone": "Israel Standard Time", "alias": "IL" }
  ]
}
```

- `alias` is the 2-3 letter vertical text; if missing it is derived from the zone.
- `color` (per clock or `dateColor`) accepts `#RRGGBB` or a name (`Gold`, `Tomato`…).
- The date follows the bar's language: `WED` in English, `MIÉ` in Spanish.

## How it works

- **WPF / .NET 8**: `WindowStyle=None` + `AllowsTransparency` window, always on top with `Topmost`
  and hidden from Alt+Tab with `WS_EX_TOOLWINDOW`.
- **Magnification**: Gaussian falloff based on the cursor's horizontal distance, with an
  exponential spring in `CompositionTarget.Rendering` (stops at rest). The expansion is computed
  from the center and each container is shifted as a whole, so the icon and its clickable area
  always match.
- **Taskbar**: `FindWindow("Shell_TrayWnd")` + **empty region**; restored on exit.
- **Work area**: `SPI_SETWORKAREA` with `fWinIni = 0`; when the height or edge changes, maximized
  windows are moved back (`ShowWindow`).
- **Tray**: the chevron drives the native `TopLevelWindowForOverflowXamlIsland` panel through UI
  Automation.
- **Start / task view**: key injection (`SendInput`) into the real shell.
- **Icons**: `shell:AppsFolder\<AUMID>` with `IShellItemImageFactory` for packaged apps;
  `PrivateExtractIcons` → `IShellItemImageFactory` → `SHGetFileInfo` for the rest.

## Limitations

- **Primary monitor only**.
- No real DWM blur: the bar is translucent, not acrylic.
- Packaged apps are launched by AUMID; if Windows changes, the icon may differ.

## Diagnostics

```bat
BarDock.exe --dump-icons
BarDock.exe --dump-windows
```

`--dump-icons` writes the extracted icons to `%TEMP%\barDock-icons`; `--dump-windows` writes the
windows grouped by app to `%TEMP%\barDock-windows.txt`.
