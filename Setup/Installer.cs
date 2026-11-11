using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BarDock.Setup;

/// <summary>
/// Instalacion por usuario (sin administrador): copia BarDock.exe a
/// %LOCALAPPDATA%\Programs\BarDock, crea accesos directos y registra la desinstalacion.
/// </summary>
internal static class Installer
{
    public const string Version = "1.0.0";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BarDock";
    private const string AppName = "BarDock";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        AppName);

    public static string AppExe => Path.Combine(InstallDir, "BarDock.exe");

    public static string StartMenuShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "Windows",
        "Start Menu",
        "Programs",
        "BarDock.lnk");

    public static string DesktopShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "BarDock.lnk");

    public static void Install(bool desktopShortcut, bool startup, bool launch)
    {
        StopRunningInstance();

        Directory.CreateDirectory(InstallDir);
        ExtractPayload();

        CreateShortcut(StartMenuShortcut);
        if (desktopShortcut)
        {
            CreateShortcut(DesktopShortcut);
        }

        SetStartup(startup);
        WriteUninstallEntry();

        if (launch)
        {
            Process.Start(new ProcessStartInfo(AppExe) { UseShellExecute = true, WorkingDirectory = InstallDir });
        }
    }

    private static void ExtractPayload()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("BarDock.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("El instalador no contiene BarDock.exe.");

        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("No se pudo leer el contenido del instalador.");

        var temporary = AppExe + ".tmp";

        using (var file = File.Create(temporary))
        {
            stream.CopyTo(file);
        }

        File.Move(temporary, AppExe, overwrite: true);
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

        if (Native.FindWindow(null, "BarDock") != IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "BarDock esta en ejecucion. Cierralo desde su menu (Salir) y vuelve a intentarlo.");
        }
    }

    private static void CreateShortcut(string path)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell no esta disponible.");

        dynamic shell = Activator.CreateInstance(type)!;

        try
        {
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = AppExe;
            link.WorkingDirectory = InstallDir;
            link.IconLocation = AppExe + ",0";
            link.Description = AppName;
            link.Save();
            Marshal.FinalReleaseComObject(link);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(AppName, $"\"{AppExe}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    private static void WriteUninstallEntry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey, writable: true);
        if (key is null)
        {
            return;
        }

        var sizeKb = 0;
        try
        {
            sizeKb = (int)(new FileInfo(AppExe).Length / 1024);
        }
        catch
        {
            // tamano desconocido
        }

        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", Version);
        key.SetValue("Publisher", AppName);
        key.SetValue("DisplayIcon", AppExe);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("UninstallString", $"\"{AppExe}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

        if (sizeKb > 0)
        {
            key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
        }
    }
}
