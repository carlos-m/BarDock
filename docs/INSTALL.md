# Installation guide

**English** · [Español](INSTALL.es.md)

## Requirements

- **Windows 11** 64-bit (tested on 23H2/24H2/25H2).
- About **250 MB** free.
- You do **not** need to install .NET or have administrator rights: the installer is
  self-contained and installs per user.

## Install

1. Download **[`BarDockSetup.exe`](https://github.com/carlos-m/BarDock/releases/latest/download/BarDockSetup.exe)**
   (latest release).
2. Double-click the file.
3. If Windows shows **"Windows protected your PC"**, click **More info → Run anyway**. This is
   normal: the installer is not signed with a code-signing certificate.
4. Pick the options:
   - **Create a desktop shortcut**.
   - **Start with Windows**.
   - **Open BarDock when done**.
5. Click **Install**. When it finishes, BarDock starts and the Windows taskbar is hidden.

### Silent install

```bat
BarDockSetup.exe /S
```

Installs with the default options (desktop shortcut, no auto-start and no launch at the end).

## Where it installs

| Item | Path |
| --- | --- |
| Program | `%LOCALAPPDATA%\Programs\BarDock\BarDock.exe` |
| Settings | `%APPDATA%\BarDock\settings.json` |
| Shortcut | Start menu (`BarDock`) and, optionally, the desktop |
| Uninstall | Settings → Apps → BarDock |

If you come from the previous version (**MacDock**), your settings are copied automatically from
`%APPDATA%\MacDock\settings.json` the first time; you keep colors, sizes and clocks.

## First run

- The bar appears at the top edge (or the bottom, if you change it) and the Windows taskbar is
  hidden.
- `settings.json` is created and filled with the apps **pinned** to the taskbar.
- To open BarDock again after closing it:
  - **Start → BarDock**, or the desktop shortcut.
  - `Win + R` → `%LOCALAPPDATA%\Programs\BarDock\BarDock.exe`.
  - If it is already running, opening it again **brings it to the front** (no duplicates).

## Update

Run the new installer: it closes BarDock cleanly, replaces the files and keeps your settings. No
need to uninstall first.

## Uninstall

- **Settings → Apps → BarDock → Uninstall**, or
- `"%LOCALAPPDATA%\Programs\BarDock\BarDock.exe" --uninstall`.

The bar closes, the Windows taskbar and the work area are restored, and shortcuts, registry
entries and the install folder are removed. Your settings in `%APPDATA%\BarDock` are kept in case
you install it again.

## Common problems

**Windows warns when running the installer.**
It is our own executable without code signing. Click *More info → Run anyway*.

**I killed BarDock and the Windows taskbar does not come back.**
Open BarDock and close it normally (**right click → Quit**), or restart Explorer from Task
Manager.

**The bar does not appear.**
Check Task Manager for another instance. If the process is running but you cannot see it, open
BarDock again: the existing instance is brought to the front.

**It only shows on one monitor.**
BarDock lives on the **primary monitor** and spans its full width. This is a known limitation.

**I want the Windows taskbar back.**
Right click the bar → **Replace the Windows taskbar** (turn it off). BarDock moves next to the
taskbar so you can still use its menu.
