namespace BarDock;

/// <summary>Un elemento de la barra (una aplicacion anclada).</summary>
public sealed class BarItemSettings
{
    /// <summary>Ruta al .exe, .lnk o .url.</summary>
    public string Path { get; set; } = "";

    /// <summary>Nombre visible. Si es null se deduce del destino del acceso directo.</summary>
    public string? Name { get; set; }
}

/// <summary>Un reloj de la barra.</summary>
public sealed class ClockSettings
{
    /// <summary>Etiqueta larga (ciudad o zona); se usa en el tooltip y como alias por defecto.</summary>
    public string Label { get; set; } = "";

    /// <summary>Id de TimeZoneInfo, por ejemplo "Romance Standard Time".</summary>
    public string Zone { get; set; } = "";

    /// <summary>Alias vertical de 2-3 letras (ES, CL...). Si es null se deduce de la zona.</summary>
    public string? Alias { get; set; }

    /// <summary>Color propio de esta zona ("#RRGGBB"). Null = color del tema.</summary>
    public string? Color { get; set; }
}

/// <summary>Ajustes persistidos en %APPDATA%\BarDock\settings.json</summary>
public sealed class BarSettings
{
    /// <summary>Version del formato de ajustes, para migrar sin perder cambios del usuario.</summary>
    public int Version { get; set; }

    /// <summary>Alto de la barra, en DIPs. No cambia al ampliar los iconos.</summary>
    public double BarHeight { get; set; } = 48;

    /// <summary>Tamano base del icono en DIPs.</summary>
    public double IconSize { get; set; } = 26;

    /// <summary>Escala maxima al pasar el raton (1.8 = 80% mas grande).</summary>
    public double MaxScale { get; set; } = 1.8;

    /// <summary>Separacion horizontal entre iconos, en DIPs.</summary>
    public double Spacing { get; set; } = 8;

    /// <summary>Separacion con el borde superior de la pantalla.</summary>
    public double TopMargin { get; set; } = 0;

    /// <summary>Oculta la barra de tareas de Windows mientras BarDock esta abierto.</summary>
    public bool ReplaceTaskbar { get; set; } = true;

    /// <summary>"top" o "bottom": borde de la pantalla donde vive la barra.</summary>
    public string BarPosition { get; set; } = "top";

    /// <summary>Opacidad del fondo de la barra (0.3-1.0).</summary>
    public double BarOpacity { get; set; } = 0.9;

    /// <summary>"center" o "left": alineacion de los iconos.</summary>
    public string IconAlignment { get; set; } = "center";

    /// <summary>Muestra el boton de vista de tareas junto al de Inicio.</summary>
    public bool ShowTaskView { get; set; } = true;

    /// <summary>
    /// "hardware" dibuja con la GPU (mas rapido con animaciones, unos 90 MB mas de memoria) o
    /// "software" (mitad de memoria, algo mas de CPU al repintar).
    /// </summary>
    public string RenderMode { get; set; } = "hardware";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool BarAtBottom
    {
        get => string.Equals(BarPosition, "bottom", StringComparison.OrdinalIgnoreCase);
        set => BarPosition = value ? "bottom" : "top";
    }

    /// <summary>Idioma de menus y configuracion: "es" o "en".</summary>
    public string Language { get; set; } = "es";

    /// <summary>Alias vertical de la fecha. Vacio = dia de la semana (LUN, MAR...).</summary>
    public string DateAlias { get; set; } = "";

    /// <summary>Color de la fecha ("#RRGGBB"). Null = color del tema.</summary>
    public string? DateColor { get; set; }

    /// <summary>Tipografia de relojes y fecha. Null = la de la barra.</summary>
    public string? ClockFontFamily { get; set; }

    /// <summary>Tamano de la hora en DIPs (7-26). Null = 14.</summary>
    public double? ClockFontSize { get; set; }

    /// <summary>Negrita para los relojes y la fecha.</summary>
    public bool ClockBold { get; set; }

    /// <summary>Interlineado del alias vertical (0.8 = junto, 1.4 = separado).</summary>
    public double ClockLetterSpacing { get; set; } = 0.95;

    public List<BarItemSettings> Items { get; set; } = new();

    public List<ClockSettings> Clocks { get; set; } = new();
}
