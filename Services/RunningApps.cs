using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BarDock.Interop;

namespace BarDock.Services;

/// <summary>Una ventana abierta de una aplicacion (para elegir cual traer al frente).</summary>
public sealed record AppWindow(string Title, IntPtr Handle);

/// <summary>Una aplicacion con al menos una ventana visible (lo que la barra de tareas mostraria).</summary>
public sealed record RunningApp(
    string ProcessName,
    string Title,
    string? ExecutablePath,
    string? Aumid,
    IntPtr Handle,
    IReadOnlyList<AppWindow> Windows);

/// <summary>Enumera las ventanas de nivel superior visibles, agrupadas por aplicacion.</summary>
public static class RunningApps
{
    private static readonly HashSet<string> IgnoredProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "TextInputHost",
        "LockApp",
        "ApplicationFrameHost",
        "SystemSettings",
        "PickerHost",
        "Widgets",
        "WidgetService",
    };

    private sealed record RawWindow(
        string ProcessName,
        string ExecutablePath,
        string Title,
        IntPtr Handle);

    public static List<RunningApp> Get()
    {
        var windows = new List<RawWindow>();
        var paths = new Dictionary<uint, (string? Path, string? Name)>();
        var ownPid = (uint)Environment.ProcessId;

        Native.EnumWindows((handle, _) =>
        {
            try
            {
                Collect(handle, ownPid, paths, windows);
            }
            catch
            {
                // ventana que desaparece a mitad de la enumeracion
            }

            return true;
        }, IntPtr.Zero);

        return windows
            .GroupBy(
                window => PackagedApps.AumidFromExePath(window.ExecutablePath)
                          ?? "p:" + window.ProcessName,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                var aumid = PackagedApps.AumidFromExePath(first.ExecutablePath);

                var open = group
                    .GroupBy(window => window.Handle)
                    .Select(byHandle => byHandle.First())
                    .Select(window => new AppWindow(window.Title, window.Handle))
                    .ToList();

                return new RunningApp(
                    first.ProcessName,
                    open[0].Title,
                    first.ExecutablePath,
                    aumid,
                    open[0].Handle,
                    open);
            })
            .OrderBy(app => app.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Collect(
        IntPtr handle,
        uint ownPid,
        IDictionary<uint, (string? Path, string? Name)> paths,
        ICollection<RawWindow> result)
    {
        if (!Native.IsWindowVisible(handle))
        {
            return;
        }

        if (Native.GetWindowTextLength(handle) == 0)
        {
            return;
        }

        Native.GetWindowThreadProcessId(handle, out var pid);
        if (pid == 0 || pid == ownPid)
        {
            return;
        }

        // Ventanas "cloaked" (UWP suspendidas, escritorios virtuales, etc.)
        if (Native.DwmGetWindowAttribute(handle, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return;
        }

        var className = new StringBuilder(256);
        Native.GetClassName(handle, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return;
        }

        if (!paths.TryGetValue(pid, out var process))
        {
            var exePath = GetProcessPath(pid);
            process = (exePath, exePath is null
                ? null
                : System.IO.Path.GetFileNameWithoutExtension(exePath));
            paths[pid] = process;
        }

        if (process.Path is null || string.IsNullOrWhiteSpace(process.Name) ||
            IgnoredProcesses.Contains(process.Name))
        {
            return;
        }

        var title = new StringBuilder(512);
        Native.GetWindowText(handle, title, title.Capacity);
        var text = title.ToString().Trim();
        if (text.Length == 0)
        {
            return;
        }

        result.Add(new RawWindow(process.Name!, process.Path, text, handle));
    }

    private static string? GetProcessPath(uint pid)
    {
        const uint ProcessQueryLimitedInformation = 0x1000;

        var handle = Native.OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return Native.QueryFullProcessImageName(handle, 0, buffer, ref size)
                ? buffer.ToString()
                : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            Native.CloseHandle(handle);
        }
    }

    public static void Activate(RunningApp app) => ShellHelper.TryActivate(app.Handle);

    public static bool BringToFront(string processName) => ShellHelper.TryActivate(processName);
}
