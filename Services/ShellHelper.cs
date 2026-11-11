using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using BarDock.Interop;

namespace BarDock.Services;

public static class ShellHelper
{
    private static readonly ConcurrentDictionary<string, string> TargetCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Si la ruta es un .lnk devuelve el destino; en cualquier otro caso devuelve la propia ruta.</summary>
    public static string ResolveTarget(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return TargetCache.GetOrAdd(path, ResolveShortcut);
    }

    private static string ResolveShortcut(string path)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is not null)
            {
                dynamic shell = Activator.CreateInstance(type)!;
                dynamic shortcut = shell.CreateShortcut(path);
                string target = (string)shortcut.TargetPath;
                if (!string.IsNullOrWhiteSpace(target))
                {
                    return target;
                }
            }
        }
        catch
        {
            // acceso directo roto o COM no disponible
        }

        return path;
    }

    /// <summary>AppUserModelID si el elemento es una app empaquetada (shell:AppsFolder\...).</summary>
    public static string? GetAumid(string path) => PackagedApps.AumidFromShellPath(path);

    /// <summary>Nombre del ejecutable sin extension (coincide con Process.ProcessName).</summary>
    public static string GetProcessName(string path)
    {
        // Las apps empaquetadas se identifican por AUMID, no por el nombre del .exe.
        if (GetAumid(path) is not null)
        {
            return "";
        }

        var target = ResolveTarget(path);
        return Path.GetFileNameWithoutExtension(target);
    }

    public static string GetDisplayName(BarItemSettings item)
    {
        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            return item.Name!;
        }

        if (GetAumid(item.Path) is { } aumid)
        {
            return PackagedApps.NameOf(aumid) ?? aumid;
        }

        var target = ResolveTarget(item.Path);
        var name = Path.GetFileNameWithoutExtension(target);
        return string.IsNullOrWhiteSpace(name) ? item.Path : name;
    }

    /// <summary>Dos AUMID representan la misma app si coinciden o comparten familia de paquete.</summary>
    public static bool SamePackagedApp(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Family(left!), Family(right!), StringComparison.OrdinalIgnoreCase);
    }

    private static string Family(string aumid)
    {
        var bang = aumid.IndexOf('!');
        return bang > 0 ? aumid[..bang] : aumid;
    }

    public static void Launch(string path)
    {
        if (GetAumid(path) is { } aumid)
        {
            if (ApplicationActivation.TryActivate(aumid))
            {
                return;
            }

            // Respaldo: dejar que el shell active la identidad de la app.
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"shell:AppsFolder\\{aumid}\"",
                UseShellExecute = true,
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    /// <summary>Trae al frente la ventana indicada sin cambiar su estado. Devuelve false si ya no existe.</summary>
    public static bool TryActivate(IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == new IntPtr(-1) || !Native.IsWindow(handle))
        {
            return false;
        }

        try
        {
            // Solo restaurar si estaba minimizada: SW_RESTORE en una ventana maximizada
            // la devolveria a tamano normal.
            if (Native.IsIconic(handle))
            {
                Native.ShowWindow(handle, Native.SW_RESTORE);
            }

            var activated = Native.SetForegroundWindow(handle);

            // Algunas apps (Excel) suben sus otras ventanas al activarse. Dejar la elegida
            // arriba del todo para que sea la que se ve.
            Native.SetWindowPos(
                handle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);

            return activated;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Trae al frente la primera ventana del proceso indicado. Devuelve false si no hay ninguna.</summary>
    public static bool TryActivate(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            return false;
        }

        foreach (var process in processes)
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero &&
                    process.MainWindowHandle != new IntPtr(-1) &&
                    TryActivate(process.MainWindowHandle))
                {
                    return true;
                }
            }
            catch
            {
                // proceso protegido / ya terminado
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    /// <summary>Abre el menu Inicio de Windows con la tecla Windows.</summary>
    public static void OpenStartMenu()
    {
        try
        {
            SendKey(Native.VK_LWIN, down: true);
            Thread.Sleep(50);
            SendKey(Native.VK_LWIN, down: false);
        }
        catch
        {
            // sin permiso para inyectar teclas
        }
    }

    /// <summary>Abre la vista de tareas (Win+Tab).</summary>
    public static void OpenTaskView()
    {
        try
        {
            SendKey(Native.VK_LWIN, down: true);
            Thread.Sleep(40);
            SendKey(Native.VK_TAB, down: true);
            Thread.Sleep(40);
            SendKey(Native.VK_TAB, down: false);
            Thread.Sleep(40);
            SendKey(Native.VK_LWIN, down: false);
        }
        catch
        {
            // sin permiso para inyectar teclas
        }
    }

    private static void SendKey(ushort virtualKey, bool down)
    {
        var input = new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            ki = new Native.KEYBDINPUT
            {
                wVk = virtualKey,
                wScan = virtualKey,
                dwFlags = down ? 0u : Native.KEYEVENTF_KEYUP,
            },
        };

        Native.SendInput(1, new[] { input }, Marshal.SizeOf<Native.INPUT>());
    }

    public static void RevealInExplorer(string path)
    {
        if (GetAumid(path) is { } aumid)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"shell:AppsFolder\\{aumid}\"",
                UseShellExecute = true,
            });
            return;
        }

        var target = ResolveTarget(path);
        if (!File.Exists(target) && !Directory.Exists(target))
        {
            target = path;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{target}\"",
            UseShellExecute = true,
        });
    }
}
