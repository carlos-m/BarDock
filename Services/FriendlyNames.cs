namespace BarDock.Services;

/// <summary>Nombres legibles para los procesos que aparecen en la barra.</summary>
public static class FriendlyNames
{
    public static string Label(string processName) => processName switch
    {
        "WhatsApp.Root" => "WhatsApp",
        "ms-teams" => "Microsoft Teams",
        "olk" => "Outlook",
        "outlook" => "Outlook (clasico)",
        "PhoneExperienceHost" => "Movil",
        "MSPCManagerService" => "PC Manager",
        "DellOptimizer.Systray" => "Dell Optimizer",
        "DellOptimizer" => "Dell Optimizer",
        "SecurityHealthSystray" => "Seguridad de Windows",
        "MoNotificationUx" => "Windows Update",
        "Antigravity" => "Antigravity",
        "OneDrive.Sync.Service" => "OneDrive",
        "chrome" => "Google Chrome",
        "msedge" => "Microsoft Edge",
        _ => processName,
    };
}
