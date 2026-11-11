using System.IO;
using System.Runtime.InteropServices;

namespace BarDock.Services;

/// <summary>
/// Apps empaquetadas (MSIX/Store) instaladas: resuelve su AUMID a partir del ejecutable
/// y permite obtener su icono y lanzarlas por identidad (aunque el .exe este protegido).
/// </summary>
public static class PackagedApps
{
    public const string AppsFolderPrefix = "shell:AppsFolder\\";

    private static bool _loaded;
    private static readonly Dictionary<string, string> FamilyToAumid = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> AumidToName = new(StringComparer.OrdinalIgnoreCase);

    public static string ShellPath(string aumid) => AppsFolderPrefix + aumid;

    public static string? AumidFromShellPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase)
            ? path[AppsFolderPrefix.Length..]
            : null;
    }

    /// <summary>Familia del paquete a partir de "...\WindowsApps\Nombre_Version_Arch__Publisher\...".</summary>
    public static string? FamilyFromExePath(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        const string marker = @"\WindowsApps\";
        var index = exePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var rest = exePath[(index + marker.Length)..];
        var slash = rest.IndexOf('\\');
        if (slash <= 0)
        {
            return null;
        }

        var parts = rest[..slash].Split('_');
        if (parts.Length < 2)
        {
            return null;
        }

        return $"{parts[0]}_{parts[^1]}";
    }

    public static string? AumidFromExePath(string? exePath)
    {
        var family = FamilyFromExePath(exePath);
        return family is null ? null : Resolve(family, null);
    }

    /// <summary>Busca el AUMID completo de una familia (y opcionalmente de una app concreta).</summary>
    public static string? Resolve(string family, string? appId)
    {
        EnsureLoaded();

        if (!string.IsNullOrWhiteSpace(appId))
        {
            var exact = $"{family}!{appId}";
            if (AumidToName.ContainsKey(exact))
            {
                return exact;
            }
        }

        return FamilyToAumid.TryGetValue(family, out var aumid) ? aumid : null;
    }

    public static string? NameOf(string aumid)
    {
        EnsureLoaded();
        return AumidToName.TryGetValue(aumid, out var name) ? name : null;
    }

    public static IReadOnlyList<(string Aumid, string Name)> All()
    {
        EnsureLoaded();
        return AumidToName
            .Select(pair => (pair.Key, pair.Value))
            .OrderBy(pair => pair.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            if (folder is null)
            {
                return;
            }

            foreach (dynamic item in folder.Items())
            {
                try
                {
                    string name = item.Name;
                    string aumid = item.ExtendedProperty("System.AppUserModel.ID");
                    if (string.IsNullOrWhiteSpace(aumid))
                    {
                        continue;
                    }

                    AumidToName[aumid] = name;

                    var bang = aumid.IndexOf('!');
                    var family = bang > 0 ? aumid[..bang] : aumid;
                    if (!FamilyToAumid.ContainsKey(family))
                    {
                        FamilyToAumid[family] = aumid;
                    }
                }
                catch
                {
                    // elemento sin identidad de paquete
                }
            }
        }
        catch
        {
            // Shell.Application no disponible: se seguira usando la ruta del .exe
        }
    }
}
