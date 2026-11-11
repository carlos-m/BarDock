using System.Windows;
using System.Windows.Controls;

namespace BarDock.Services;

/// <summary>
/// Textos de menus y configuracion en español o ingles. Los elementos de XAML se
/// marcan con Tag="clave" y se traducen con <see cref="Apply"/>.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, (string Es, string En)> Texts = new()
    {
        ["dialog.title"] = ("Relojes y fecha", "Clocks and date"),
        ["dialog.intro"] = (
            "El tamaño, la tipografía y el interlineado son comunes a toda la barra. El color es por zona horaria (y para la fecha). La fecha usa el día de la semana en vertical salvo que le pongas otro alias.",
            "Size, font and line spacing are shared by the whole bar. The color is per time zone (and for the date). The date shows the weekday vertically unless you set another alias."),
        ["dialog.clocks"] = ("Relojes en la barra", "Clocks in the bar"),
        ["dialog.add"] = ("Añadir", "Add"),
        ["dialog.remove"] = ("Quitar", "Remove"),
        ["dialog.style"] = ("Estilo general (relojes y fecha)", "General style (clocks and date)"),
        ["dialog.font"] = ("Fuente", "Font"),
        ["dialog.bold"] = ("Negrita", "Bold"),
        ["dialog.boldText"] = ("Texto en negrita", "Bold text"),
        ["dialog.size"] = ("Tamaño", "Size"),
        ["dialog.sizeHint"] = ("7-26", "7-26"),
        ["dialog.spacing"] = ("Interlineado", "Line spacing"),
        ["dialog.spacingHint"] = ("0.8 junto · 1.4 separado", "0.8 tight · 1.4 loose"),
        ["dialog.selected"] = ("Reloj seleccionado", "Selected clock"),
        ["dialog.zone"] = ("Zona", "Zone"),
        ["dialog.alias"] = ("Alias", "Alias"),
        ["dialog.aliasHint"] = ("2-3 letras en vertical", "2-3 letters, vertical"),
        ["dialog.color"] = ("Color", "Color"),
        ["dialog.pick"] = ("Elegir…", "Pick…"),
        ["dialog.clear"] = ("Quitar", "Clear"),
        ["dialog.date"] = ("Fecha", "Date"),
        ["dialog.dateAliasHint"] = (
            "vacío = día de la semana (LUN, MAR...)",
            "empty = weekday (MON, TUE...)"),
        ["dialog.cancel"] = ("Cancelar", "Cancel"),
        ["dialog.ok"] = ("Aceptar", "OK"),
    };

    public static string Language { get; private set; } = "es";

    public static bool English => Language == "en";

    public static void Set(string? language) => Language = language == "en" ? "en" : "es";

    /// <summary>Traduce una clave del diccionario.</summary>
    public static string T(string key) =>
        Texts.TryGetValue(key, out var text) ? (English ? text.En : text.Es) : key;

    /// <summary>Elige entre español e inglés en el mismo sitio del código.</summary>
    public static string S(string es, string en) => English ? en : es;

    /// <summary>Aplica las traducciones a los elementos marcados con Tag.</summary>
    public static void Apply(Window window)
    {
        ApplyElement(window);
        ApplyChildren(window);
    }

    private static void ApplyChildren(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            ApplyElement(element);
            ApplyChildren(element);
        }
    }

    private static void ApplyElement(FrameworkElement element)
    {
        if (element.Tag is not string key || key.Length == 0)
        {
            return;
        }

        switch (element)
        {
            case TextBlock text:
                text.Text = T(key);
                break;
            case Window window:
                window.Title = T(key);
                break;
            case ContentControl content:
                content.Content = T(key);
                break;
        }
    }
}
