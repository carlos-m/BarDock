namespace BarDock.Setup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(arg => arg.Equals("/S", StringComparison.OrdinalIgnoreCase)))
        {
            Installer.Install(desktopShortcut: true, startup: false, launch: false);
            return;
        }

        Application.Run(new SetupForm());
    }
}
