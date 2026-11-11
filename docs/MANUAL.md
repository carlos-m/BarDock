# User manual

**English** · [Español](MANUAL.es.md)

BarDock is a minimal top bar for Windows 11. It replaces the taskbar and gathers your apps, the
system tray, clocks and the machine's status into a single discreet strip.

> **Your desktop, your way.**

## The bar at a glance

```
[Start] [Task view]   …app icons…      [chevron] [bell] [battery] [network] [volume] | [ES 12:00] [CL 08:00] … | [WED 11/11/26]
```

- **Left**: Start button and, optionally, task view.
- **Center**: pinned apps and the ones you have open.
- **Right**: native tray (chevron), notifications (bell), system status, clocks and date.

## App icons

- **Hover**: icons magnify in a wave. The icon under the cursor **does not move** and its neighbors
  open on both sides; the green line grows with it.
- **Left click**: opens the app or brings its window to the front (with a small bounce).
- **Right click**: Open, Show in Explorer and Remove from the bar.
- **Hover an open app**: a list of **its windows** appears with each title. Clicking an entry
  brings **only that window** to the front.
- **Drag an `.exe`, `.lnk` or `.url`** onto the bar: it is added.
- **Pin**: apps you want fixed are added with **right click → Add app…**. The rest appear and
  disappear on their own as they open and close.

## Far-left buttons

- **Windows logo**: opens the real **Start menu**. **Right click** offers *All apps* and
  *Settings*.
- **Task view** (optional): opens the Windows task view (`Win + Tab`) to switch virtual desktops.
  Show or hide it with **right click → Task view button**.

## Tray and notifications

- **Chevron**: opens the **native Windows overflow panel**, with the real icons and their original
  context menus (OneDrive, WhatsApp, Teams, Bluetooth…). It is the Windows tray, not a copy.
- **Bell**: opens the Windows **notification center**.
- **Right click the tray**: notification center, notification settings and taskbar settings.

## System status

- **Battery**: percentage; turns red on low charge.
- **Network**: Wi-Fi or wired; the tooltip shows the network name.
- **Volume**: click to mute/unmute, mouse wheel to raise or lower.
- **Right click** the indicators: Network & internet, Sound, Power & battery.

## Clocks and date

Each clock shows a 2-3 letter **vertical alias** and the time:

```
E
S  12:47
```

The date follows the same pattern with the **weekday** in the bar's language:

```
W
E  11/11/26
D
```

### Customize

**Right click the clocks → Clocks and date…**

- **General style** (shared by clocks and date): font, bold, size and line spacing.
- **Per clock**: time zone, alias and color (with a color picker).
- **Date**: alias (empty = weekday) and color.
- **Add / Remove** clocks.
- The preview updates as you change the values.

## Bar menu

**Right click an empty area of the bar**:

| Option | What it does |
| --- | --- |
| Add app… | Pins an `.exe`, `.lnk` or `.url` |
| Clocks and date… | Customizes clocks, date and styles |
| Notification center | Opens the notification panel |
| Bar position | Top or bottom (magnification flips direction) |
| Icon alignment | Centered or left, next to Start |
| Bar height | 36 to 72 px |
| Icon size | 18 to 36 px |
| Magnification | Maximum scale on hover |
| Spacing | Gap between icons |
| Transparency | 100 % (opaque) to 50 % |
| Language | English or Spanish (menus, settings and the date's weekday) |
| Task view button | Show or hide |
| Replace the Windows taskbar | Hides the Windows taskbar while BarDock is running |
| Start with Windows | Automatic start when you sign in |
| Reload | Re-reads `settings.json` |
| Edit settings.json | Opens the settings file |
| Quit | Closes BarDock and restores the Windows taskbar |

## Settings (`settings.json`)

Stored in `%APPDATA%\BarDock\settings.json`; you can edit them by hand and then **Reload**.

```jsonc
{
  "version": 5,
  "barHeight": 36,
  "iconSize": 26,
  "maxScale": 2.2,
  "spacing": 8,
  "topMargin": 0,
  "replaceTaskbar": true,
  "barPosition": "top",        // "top" or "bottom"
  "barOpacity": 0.9,           // 0.3 - 1.0
  "iconAlignment": "center",   // "center" or "left"
  "showTaskView": true,
  "renderMode": "hardware",    // "hardware" or "software"
  "language": "en",            // "es" or "en"

  "clockFontFamily": null,
  "clockFontSize": 14,
  "clockBold": false,
  "clockLetterSpacing": 0.95,

  "dateAlias": "",             // empty = weekday
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

- **`color`** accepts `#RRGGBB` or a name (`Gold`, `Tomato`…). Empty = theme color.
- **`alias`** is the vertical text; if missing it is derived from the zone
  (`Romance Standard Time` → `ES`).
- **`barOpacity`**: opacity of the bar's background.
- **`renderMode`**: `hardware` (default) uses the GPU; `software` draws on the CPU and uses **about
  90 MB less** memory, at the cost of a bit more CPU when repainting and less smooth animations.
- The Windows taskbar is hidden with an **empty region** so the native tray keeps working.

## Tips

- If you want the bar at the bottom, change **Position** and the work area is reserved at the bottom
  edge; maximized windows are moved automatically.
- With **left-aligned icons**, the dock starts next to the Start button.
- 50 % transparency lets the desktop show through; icons and text stay opaque.
- You can run BarDock without replacing the Windows taskbar: it sits right next to it and you keep
  using the original tray.

## Diagnostics

```bat
BarDock.exe --dump-icons
BarDock.exe --dump-windows
```

- `--dump-icons`: saves the icons as the shell extracts them, in `%TEMP%\barDock-icons`.
- `--dump-windows`: saves the windows grouped by app to `%TEMP%\barDock-windows.txt`.

## FAQ

**Can I use two monitors?**
BarDock lives on the primary monitor.

**Do I need .NET?**
Not with the installer: it is self-contained.

**Can I use it without hiding the Windows taskbar?**
Yes: turn off *Replace the Windows taskbar*.

**What happens if I close BarDock?**
The Windows taskbar and the work area are restored automatically.

**Are settings lost when updating?**
No; they are kept in `%APPDATA%\BarDock`.
