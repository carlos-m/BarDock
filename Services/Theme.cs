using System.Windows.Media;
using Microsoft.Win32;

namespace BarDock.Services;

public readonly record struct ThemeSnapshot(bool IsLight, Color Accent);

/// <summary>Lee el tema claro/oscuro y el color de acento del sistema.</summary>
public static class Theme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string DwmKey = @"Software\Microsoft\Windows\DWM";

    public static ThemeSnapshot Read() => new(IsLight(), AccentColor());

    private static bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var value = key?.GetValue("SystemUsesLightTheme");
            if (value is int system)
            {
                return system != 0;
            }

            value = key?.GetValue("AppsUseLightTheme");
            return value is int apps && apps != 0;
        }
        catch
        {
            return false;
        }
    }

    private static Color AccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
            if (key?.GetValue("AccentColor") is int packed)
            {
                // El valor se guarda como 0xAABBGGRR
                var r = (byte)(packed & 0xFF);
                var g = (byte)((packed >> 8) & 0xFF);
                var b = (byte)((packed >> 16) & 0xFF);
                return Color.FromRgb(r, g, b);
            }
        }
        catch
        {
            // sin acceso al registro
        }

        return Color.FromRgb(0x4C, 0xC2, 0xFF);
    }
}
