using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BarDock.Interop;
using BarDock.Services;

namespace BarDock;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            base.OnStartup(e);
            Uninstaller.Run();

            // Sin ventanas abiertas: salir ya para soltar el .exe y poder borrar la carpeta.
            Environment.Exit(0);
            return;
        }

        if (e.Args.Contains("--tray-panel", StringComparer.OrdinalIgnoreCase))
        {
            base.OnStartup(e);
            TrayOverflow.Show();
            Environment.Exit(0);
            return;
        }

        _singleInstance = new Mutex(true, @"Local\BarDock.SingleInstance", out var created);
        if (!created)
        {
            BringExistingInstanceToFront();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Pase lo que pase, la barra de Windows debe volver.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => WindowsTaskbar.Restore();
        SessionEnding += (_, _) => WindowsTaskbar.Restore();

        base.OnStartup(e);

        if (e.Args.Contains("--dump-icons", StringComparer.OrdinalIgnoreCase))
        {
            DumpIcons();
            Shutdown();
            return;
        }

        if (e.Args.Contains("--dump-windows", StringComparer.OrdinalIgnoreCase))
        {
            DumpWindows();
            Shutdown();
            return;
        }

        var settings = SettingsStore.Load();
        Loc.Set(settings.Language);
        var window = new MainWindow(settings) { Title = "BarDock" };
        MainWindow = window;
        window.Show();
    }

    /// <summary>Si ya hay una instancia, traer su barra al frente (util si quedo tapada).</summary>
    private static void BringExistingInstanceToFront()
    {
        try
        {
            var handle = Native.FindWindow(null, "BarDock");
            if (handle == IntPtr.Zero)
            {
                return;
            }

            Native.ShowWindow(handle, Native.SW_SHOW);
            Native.BringWindowToTop(handle);
            Native.SetForegroundWindow(handle);
        }
        catch
        {
            // la instancia existente ya no responde
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.ToString(),
            "BarDock - error inesperado",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>Diagnostico: vuelca los iconos extraidos a %TEMP%\macdock-icons</summary>
    private static void DumpIcons()
    {
        var settings = SettingsStore.Load();
        var folder = Path.Combine(Path.GetTempPath(), "barDock-icons");
        Directory.CreateDirectory(folder);

        var index = 0;
        foreach (var item in settings.Items)
        {
            var source = IconExtractor.GetIcon(item.Path, ShellHelper.GetAumid(item.Path)) as BitmapSource;
            var name = $"{(index++)}-{item.Name ?? "icon"}";
            if (source is null)
            {
                File.WriteAllText(Path.Combine(folder, name + ".missing.txt"), item.Path);
                continue;
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(Path.Combine(folder, name + ".png"));
            encoder.Save(stream);
        }
    }

    /// <summary>Diagnostico: vuelca las ventanas abiertas agrupadas por app.</summary>
    private static void DumpWindows()
    {
        var lines = new List<string>();

        foreach (var app in RunningApps.Get())
        {
            lines.Add($"{app.ProcessName} | aumid={app.Aumid ?? "-"} | exe={app.ExecutablePath ?? "-"}");

            foreach (var window in app.Windows)
            {
                lines.Add($"    0x{window.Handle.ToInt64():X}  {window.Title}");
            }
        }

        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "barDock-windows.txt"), lines);
    }
}
