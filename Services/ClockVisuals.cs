using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BarDock.Services;

/// <summary>
/// Bloque visual de un reloj: el alias en vertical a la izquierda (E/S) y la hora a la derecha.
/// Compartido por la barra y por el dialogo de ajustes (vista previa).
/// </summary>
public static class ClockVisuals
{
    public const double DefaultSize = 14.0;
    public const double MinSize = 7.0;
    public const double MaxSize = 26.0;

    public static readonly FontFamily DefaultFont = new("Segoe UI Variable Text, Segoe UI");

    public sealed record Visual(StackPanel Panel, TextBlock Alias, TextBlock Time);

    public static Visual Create(
        string? alias,
        string? fontFamily,
        bool bold,
        string? color,
        double? size,
        double letterSpacing,
        Brush defaultAliasBrush,
        Brush defaultTimeBrush,
        string timeText = "--:--",
        string? toolTip = null)
    {
        var fontSize = Math.Clamp(size ?? DefaultSize, MinSize, MaxSize);
        var custom = ParseColor(color);
        var family = ResolveFont(fontFamily);
        var aliasText = Clocks.Vertical(alias ?? "");

        // El alias ocupa como mucho el alto de la barra: con 2 letras caben mas grandes
        // que con 3 (LUN, MAR...).
        var letters = Math.Max(1, aliasText.Count(c => c != '\n'));
        var perLetter = 32.0 / letters;
        var aliasSize = Math.Min(Math.Clamp(fontSize * 0.8, 8.0, 14.0), perLetter * 0.95);
        var aliasLine = Math.Min(aliasSize * Math.Clamp(letterSpacing, 0.7, 1.6), perLetter);

        var aliasBlock = new TextBlock
        {
            Text = aliasText,
            FontFamily = family,
            FontSize = aliasSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = custom ?? defaultAliasBrush,
            TextAlignment = TextAlignment.Center,
            LineHeight = aliasLine,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0.0, 0.0, 4.0, 0.0),
        };

        var time = new TextBlock
        {
            Text = timeText,
            FontFamily = family,
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = custom ?? defaultTimeBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        panel.Children.Add(aliasBlock);
        panel.Children.Add(time);

        if (toolTip is not null)
        {
            panel.ToolTip = toolTip;
        }

        return new Visual(panel, aliasBlock, time);
    }

    public static FontFamily ResolveFont(string? family) =>
        string.IsNullOrWhiteSpace(family) ? DefaultFont : new FontFamily(family);

    public static Brush? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            if (ColorConverter.ConvertFromString(value.Trim()) is Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // color invalido: se usa el del tema
        }

        return null;
    }
}
