using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using BarDock.Interop;

namespace BarDock.Services;

/// <summary>
/// Desinstalacion por usuario: cierra la barra, borra accesos directos y entradas del
/// registro y programa el borrado de la carpeta de instalacion.
/// </summary>
public static class Uninstaller
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BarDock";

    public static void Run()
    {
        var answer = MessageBox.Show(
            "¿Quieres desinstalar BarDock?\n\nSe cerrara la barra y se borraran sus accesos directos.",
            "Desinstalar BarDock",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        StopRunningInstance();

        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        DeleteShortcut(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "BarDock.lnk"));

        DeleteShortcut(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "BarDock.lnk"));

        DeleteRunEntry();
        DeleteUninstallEntry();

        MessageBox.Show(
            "BarDock se ha desinstalado.",
            "BarDock",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        // Despues de cerrar el aviso: el proceso puede terminar y soltar el .exe.
        ScheduleFolderDelete(installDir);
    }

    private static void StopRunningInstance()
    {
        var handle = Native.FindWindow(null, "BarDock");
        if (handle == IntPtr.Zero)
        {
            return;
        }

        Native.PostMessage(handle, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);

        for (var i = 0; i < 40 && Native.FindWindow(null, "BarDock") != IntPtr.Zero; i++)
        {
            Thread.Sleep(100);
        }
    }

    private static void DeleteShortcut(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // acceso directo en uso
        }
    }

    private static void DeleteRunEntry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue("BarDock", throwOnMissingValue: false);
        }
        catch
        {
            // sin permisos
        }
    }

    private static void DeleteUninstallEntry()
    {
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // sin permisos
        }
    }

    private static void ScheduleFolderDelete(string folder)
    {
        try
        {
            var target = folder.Replace("'", "''");
            var script =
                "Start-Sleep -Milliseconds 1500; " +
                "for ($i = 0; $i -lt 30; $i++) { Start-Sleep -Milliseconds 500; " +
                "Remove-Item -LiteralPath '" + target + "' -Recurse -Force -ErrorAction SilentlyContinue; " +
                "if (-not (Test-Path -LiteralPath '" + target + "')) { break } }";

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -WindowStyle Hidden -Command \"" + script + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            });
        }
        catch
        {
            // se podra borrar a mano
        }
    }
}
