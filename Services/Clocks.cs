using System.Globalization;

namespace BarDock.Services;

public static class Clocks
{
    public static TimeZoneInfo? Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return null;
        }
    }

    public static DateTimeOffset NowIn(TimeZoneInfo zone)
    {
        var utc = DateTimeOffset.UtcNow;
        return TimeZoneInfo.ConvertTime(utc, zone);
    }

    /// <summary>Etiqueta tipo "UTC+2" a partir del offset actual.</summary>
    public static string OffsetLabel(TimeZoneInfo zone)
    {
        var offset = zone.GetUtcOffset(DateTime.UtcNow);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();

        return absolute.Minutes == 0
            ? $"UTC{sign}{absolute.Hours}"
            : $"UTC{sign}{absolute.Hours}:{absolute.Minutes:00}";
    }

    /// <summary>Etiqueta aproximada a partir del nombre de la zona ("... Madrid, Paris" -> "Madrid").</summary>
    public static string GuessLabel(TimeZoneInfo zone)
    {
        if (zone.Id == TimeZoneInfo.Local.Id)
        {
            return "Local";
        }

        var name = zone.DisplayName;
        var close = name.IndexOf(')');
        if (close >= 0 && close + 1 < name.Length)
        {
            name = name[(close + 1)..].Trim();
        }

        var city = name.Split(',')[0].Trim();
        if (city.Length == 0)
        {
            city = OffsetLabel(zone);
        }

        return city.Length > 14 ? city[..14] : city;
    }

    public static string Format(ClockSettings clock, DateTimeOffset time)
    {
        _ = clock;
        return time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Alias vertical del reloj: el del usuario o uno deducido de la zona/etiqueta.</summary>
    public static string AliasFor(ClockSettings clock) =>
        !string.IsNullOrWhiteSpace(clock.Alias)
            ? NormalizeAlias(clock.Alias!)
            : AliasFor(clock.Zone, clock.Label);

    public static string AliasFor(string zoneId, string label) => zoneId switch
    {
        "Romance Standard Time" => "ES",
        "Pacific SA Standard Time" => "CL",
        "GMT Standard Time" => "PT",
        "Israel Standard Time" => "IL",
        "Argentina Standard Time" => "AR",
        "SA Pacific Standard Time" => "CO",
        "SA Western Standard Time" => "BO",
        "Pacific Standard Time" => "US",
        "Eastern Standard Time" => "US",
        _ => GuessAlias(label),
    };

    /// <summary>Dos o tres letras a partir de la etiqueta ("Tel Aviv" -> "TA", "Lisboa" -> "LI").</summary>
    public static string GuessAlias(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "·";
        }

        var words = label.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);

        var alias = words.Length >= 2
            ? new string(words.Take(2).Select(word => char.ToUpperInvariant(word[0])).ToArray())
            : new string(label.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();

        return alias.Length == 0 ? "·" : alias;
    }

    /// <summary>Quita espacios y signos, y limita a 3 letras.</summary>
    public static string NormalizeAlias(string alias)
    {
        var text = new string(alias.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return text.Length > 3 ? text[..3] : text;
    }

    /// <summary>Abreviatura vertical del dia de la semana en el idioma de la barra.</summary>
    public static string WeekdayAlias(DateTime local)
    {
        var culture = CultureInfo.GetCultureInfo(Loc.English ? "en-US" : "es-ES");
        var name = culture.DateTimeFormat.GetAbbreviatedDayName(local.DayOfWeek);

        return NormalizeAlias(name);
    }

    /// <summary>Alias de la fecha: el del usuario o el dia de la semana.</summary>
    public static string DateAliasFor(string? custom, DateTime local) =>
        string.IsNullOrWhiteSpace(custom) ? WeekdayAlias(local) : NormalizeAlias(custom);

    /// <summary>El alias en vertical: "ES" -> "E\nS".</summary>
    public static string Vertical(string alias)
    {
        var text = NormalizeAlias(alias);
        var builder = new System.Text.StringBuilder(text.Length * 2);

        foreach (var letter in text)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(letter);
        }

        return builder.ToString();
    }
}
