using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BarDock.Services;

/// <summary>Lee y guarda %APPDATA%\BarDock\settings.json</summary>
public static class SettingsStore
{
    public const int CurrentVersion = 5;

    private const string TaskbandKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband";

    /// <summary>
    /// Identidad de una app empaquetada dentro del blob binario de Taskband:
    /// "Familia_13chars" y, si aparece, "!AppId".
    /// </summary>
    private static readonly Regex AumidPattern = new(
        @"(?<![A-Za-z0-9.\-])([A-Za-z0-9.\-]+_[a-z0-9]{13})(?:!([A-Za-z0-9.\-]+))?",
        RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BarDock");

    public static string FilePath => Path.Combine(Folder, "settings.json");

    /// <summary>Copia los ajustes de la version anterior (MacDock) la primera vez.</summary>
    private static void MigrateLegacySettings()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return;
            }

            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MacDock",
                "settings.json");

            if (File.Exists(legacy))
            {
                Directory.CreateDirectory(Folder);
                File.Copy(legacy, FilePath);
            }
        }
        catch
        {
            // sin permisos: se generan ajustes nuevos
        }
    }

    public static BarSettings Load()
    {
        MigrateLegacySettings();

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<BarSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Items ??= new List<BarItemSettings>();
                    loaded.Clocks ??= new List<ClockSettings>();
                    loaded.BarHeight = Math.Clamp(loaded.BarHeight, 30, 96);
                    loaded.IconSize = Math.Clamp(loaded.IconSize, 14, 64);
                    loaded.MaxScale = Math.Clamp(loaded.MaxScale, 1.0, 2.5);
                    loaded.Spacing = Math.Clamp(loaded.Spacing, 0, 24);
                    loaded.IconSize = Math.Min(loaded.IconSize, loaded.BarHeight - 6);
                    loaded.BarPosition = loaded.BarAtBottom ? "bottom" : "top";
                    loaded.IconAlignment = string.Equals(loaded.IconAlignment, "left", StringComparison.OrdinalIgnoreCase)
                        ? "left"
                        : "center";
                    loaded.Language = string.Equals(loaded.Language, "en", StringComparison.OrdinalIgnoreCase)
                        ? "en"
                        : "es";

                    if (loaded.Clocks.Count == 0)
                    {
                        loaded.Clocks.AddRange(DefaultClocks());
                    }

                    if (loaded.Version < CurrentVersion)
                    {
                        Migrate(loaded);
                        Save(loaded);
                    }

                    return loaded;
                }
            }
        }
        catch
        {
            // Un JSON roto no debe impedir arrancar: se regenera con los valores por defecto.
        }

        var settings = new BarSettings();
        settings.Items.AddRange(SeedFromTaskbarPins());
        settings.Clocks.AddRange(DefaultClocks());
        Save(settings);
        return settings;
    }

    public static void Save(BarSettings settings)
    {
        try
        {
            settings.Version = CurrentVersion;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // Persistir es best-effort.
        }
    }

    public static IEnumerable<ClockSettings> DefaultClocks()
    {
        yield return new ClockSettings { Label = "Madrid", Zone = "Romance Standard Time", Alias = "ES" };
        yield return new ClockSettings { Label = "Santiago", Zone = "Pacific SA Standard Time", Alias = "CL" };
        yield return new ClockSettings { Label = "Lisboa", Zone = "GMT Standard Time", Alias = "PT" };
        yield return new ClockSettings { Label = "Tel Aviv", Zone = "Israel Standard Time", Alias = "IL" };
    }

    /// <summary>
    /// Version 2: recupera las ancladas reales de la barra de Windows (Taskband) en lugar
    /// de la carpeta de accesos directos, que puede contener enlaces obsoletos.
    /// Version 3: alias vertical de cada reloj y de la fecha.
    /// </summary>
    private static void Migrate(BarSettings settings)
    {
        if (settings.Version < 2)
        {
            var seeds = SeedFromTaskbarPins().ToList();
            if (seeds.Count > 0)
            {
                settings.Items = MergePins(settings.Items, seeds);
            }
        }

        if (settings.Version < 3)
        {
            foreach (var clock in settings.Clocks)
            {
                if (string.IsNullOrWhiteSpace(clock.Alias))
                {
                    clock.Alias = Clocks.AliasFor(clock.Zone, clock.Label);
                }
            }
        }

        if (settings.Version < 4)
        {
            // La fecha pasa a mostrar el dia de la semana si no hay alias propio.
            if (string.IsNullOrWhiteSpace(settings.DateAlias) ||
                string.Equals(settings.DateAlias.Trim(), "DÍA", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settings.DateAlias.Trim(), "DIA", StringComparison.OrdinalIgnoreCase))
            {
                settings.DateAlias = "";
            }
        }

        if (settings.Version < 5)
        {
            // La tipografia, el tamano y la negrita pasan a ser comunes a toda la barra.
            settings.ClockLetterSpacing = 0.95;
        }

        settings.Version = CurrentVersion;
    }

    /// <summary>
    /// Coloca las ancladas en su orden real y conserva al final los elementos que el
    /// usuario anadio a mano. Solo descarta .lnk sembrados por versiones anteriores
    /// que ya no estan en la barra de Windows.
    /// </summary>
    private static List<BarItemSettings> MergePins(
        List<BarItemSettings> existing,
        IReadOnlyList<BarItemSettings> seeds)
    {
        var pinsFolder = TaskbarPinsFolder();
        var text = ReadTaskbandText(includeResolve: true);

        var kept = existing
            .Where(item => !IsStaleShortcut(item.Path, pinsFolder, text))
            .ToList();

        var byKey = new Dictionary<string, BarItemSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in kept)
        {
            byKey[ItemKey(item.Path)] = item;
        }

        var merged = new List<BarItemSettings>();

        foreach (var seed in seeds)
        {
            if (byKey.Remove(ItemKey(seed.Path), out var match))
            {
                merged.Add(match); // conserva el nombre que eligio el usuario
            }
            else
            {
                merged.Add(seed);
            }
        }

        merged.AddRange(kept.Where(item => !merged.Contains(item)));
        return merged;
    }

    private static string ItemKey(string path)
    {
        var aumid = PackagedApps.AumidFromShellPath(path);
        return aumid is null ? "p:" + path : "a:" + aumid;
    }

    private static bool IsStaleShortcut(string path, string pinsFolder, string taskbandText)
    {
        if (taskbandText.Length == 0 ||
            !path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
            !path.StartsWith(pinsFolder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !taskbandText.Contains(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
    }

    private static string TaskbarPinsFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

    /// <summary>Usa las ancladas reales de la barra de tareas como contenido inicial.</summary>
    private static IEnumerable<BarItemSettings> SeedFromTaskbarPins()
    {
        var found = new List<BarItemSettings>();

        try
        {
            var text = ReadTaskbandText(includeResolve: false);
            if (text.Length > 0)
            {
                var entries = new List<(int Index, BarItemSettings Item)>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (Match match in AumidPattern.Matches(text))
                {
                    var family = match.Groups[1].Value;
                    var appId = match.Groups[2].Success ? match.Groups[2].Value : null;
                    var aumid = PackagedApps.Resolve(family, appId);

                    if (aumid is null || !seen.Add(aumid))
                    {
                        continue;
                    }

                    entries.Add((match.Index, new BarItemSettings
                    {
                        Path = PackagedApps.ShellPath(aumid),
                        Name = PackagedApps.NameOf(aumid),
                    }));
                }

                foreach (var file in EnumeratePinnedShortcuts(pinsFolder: TaskbarPinsFolder()))
                {
                    var fileName = Path.GetFileName(file);
                    var index = text.IndexOf(fileName, StringComparison.OrdinalIgnoreCase);

                    if (index < 0 || !seen.Add(file))
                    {
                        continue;
                    }

                    entries.Add((index, new BarItemSettings
                    {
                        Path = file,
                        Name = Path.GetFileNameWithoutExtension(file),
                    }));
                }

                found.AddRange(entries.OrderBy(entry => entry.Index).Select(entry => entry.Item));
            }
        }
        catch
        {
            // sin permisos: se usa la carpeta de accesos directos
        }

        if (found.Count == 0)
        {
            found.AddRange(EnumeratePinnedShortcuts(pinsFolder: TaskbarPinsFolder())
                .Select(file => new BarItemSettings
                {
                    Path = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                }));
        }

        if (found.Count > 0)
        {
            return found;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return new[]
        {
            new BarItemSettings { Path = Path.Combine(windows, "explorer.exe"), Name = "Explorador" },
            new BarItemSettings { Path = Path.Combine(windows, "notepad.exe"), Name = "Notepad" },
        };
    }

    private static IEnumerable<string> EnumeratePinnedShortcuts(string pinsFolder)
    {
        try
        {
            if (Directory.Exists(pinsFolder))
            {
                return Directory.EnumerateFiles(pinsFolder, "*.lnk")
                    .OrderBy(file => file, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
        }
        catch
        {
            // sin permisos de lectura
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Taskband guarda las ancladas como un blob UTF-16. Se prefiere "Favorites"
    /// (orden real y accesos directos); "FavoritesResolve" solo como respaldo.
    /// </summary>
    private static string ReadTaskbandText(bool includeResolve)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TaskbandKey);
            if (key is null)
            {
                return "";
            }

            var favorites = Decode(key.GetValue("Favorites") as byte[]);
            if (favorites.Length > 0 && !includeResolve)
            {
                return favorites;
            }

            var resolve = Decode(key.GetValue("FavoritesResolve") as byte[]);
            return (favorites + "\n" + resolve).Trim();
        }
        catch
        {
            return "";
        }
    }

    private static string Decode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 2)
        {
            return "";
        }

        try
        {
            return Encoding.Unicode.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }
}
