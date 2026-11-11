using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BarDock.Services;

namespace BarDock;

public partial class ClockPickerWindow : Window
{
    /// <summary>Reloj dentro de la lista, con el texto que se muestra.</summary>
    private sealed class ClockRow : INotifyPropertyChanged
    {
        public ClockRow(ClockSettings clock) => Clock = clock;

        public ClockSettings Clock { get; }

        public string Display => $"{Clocks.AliasFor(Clock)}   {Clock.Label}";

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Refresh() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
    }

    private readonly ObservableCollection<ClockRow> _rows = new();
    private readonly IReadOnlyList<TimeZoneInfo> _zones = TimeZoneInfo.GetSystemTimeZones();

    private ClockRow? _current;
    private bool _loading;

    private string? _fontFamily;
    private bool _bold;
    private double? _size;
    private double _spacing;

    public ClockPickerWindow(BarSettings settings)
    {
        InitializeComponent();

        Loc.Apply(this);
        ApplyLanguage();

        _loading = true;

        _fontFamily = settings.ClockFontFamily;
        _bold = settings.ClockBold;
        _size = settings.ClockFontSize;
        _spacing = settings.ClockLetterSpacing;

        foreach (var clock in settings.Clocks)
        {
            _rows.Add(new ClockRow(Clone(clock)));
        }

        ClockList.ItemsSource = _rows;

        ZoneBox.ItemsSource = _zones;
        ZoneBox.DisplayMemberPath = "DisplayName";

        var fonts = Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           !name.StartsWith("@", StringComparison.Ordinal))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        fonts.Insert(0, "");
        FontBox.ItemsSource = fonts;
        FontBox.Text = _fontFamily ?? "";
        BoldBox.IsChecked = _bold;
        SizeBox.Text = FormatNumber(_size, "0.#");
        SpacingBox.Text = FormatNumber(_spacing, "0.00");

        DateAliasBox.Text = settings.DateAlias ?? "";
        DateColorBox.Text = settings.DateColor ?? "";

        FontBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnGlobalStyleChanged));

        _loading = false;

        EditorPanel.IsEnabled = false;

        if (_rows.Count > 0)
        {
            ClockList.SelectedIndex = 0;
        }

        UpdateSwatch(ColorSwatch, ColorBox.Text);
        UpdateSwatch(DateSwatch, DateColorBox.Text);
        UpdatePreview();
        UpdateDatePreview();
    }

    public List<ClockSettings> Result { get; private set; } = new();

    public string DateAlias { get; private set; } = "";

    public string? DateColor { get; private set; }

    public string? ClockFontFamily { get; private set; }

    public double? ClockFontSize { get; private set; }

    public bool ClockBold { get; private set; }

    public double ClockLetterSpacing { get; private set; } = 0.95;

    private static ClockSettings Clone(ClockSettings clock) => new()
    {
        Label = clock.Label,
        Zone = clock.Zone,
        Alias = clock.Alias,
        Color = clock.Color,
    };

    /// <summary>Tooltips que no caben en el XAML.</summary>
    private void ApplyLanguage()
    {
        FontBox.ToolTip = Loc.S("Vacio = la tipografia de la barra", "Empty = bar font");
        SizeBox.ToolTip = Loc.S("Tamano de la hora en DIPs (7-26)", "Time size in DIPs (7-26)");
        SpacingBox.ToolTip = Loc.S("Espacio entre las letras verticales", "Space between vertical letters");
        AliasBox.ToolTip = Loc.S("Dos o tres letras; se muestran en vertical", "Two or three letters, shown vertically");
        ColorBox.ToolTip = Loc.S("Vacio = color del tema", "Empty = theme color");
        DateAliasBox.ToolTip = Loc.S("Vacio = dia de la semana", "Empty = weekday");
        DateColorBox.ToolTip = Loc.S("Vacio = color del tema", "Empty = theme color");
    }

    private void OnGlobalStyleChanged(object sender, RoutedEventArgs e) => ApplyGlobalStyle();

    private void OnGlobalStyleChanged(object sender, TextChangedEventArgs e) => ApplyGlobalStyle();

    private void ApplyGlobalStyle()
    {
        if (_loading)
        {
            return;
        }

        _fontFamily = string.IsNullOrWhiteSpace(FontBox.Text) ? null : FontBox.Text.Trim();
        _bold = BoldBox.IsChecked == true;
        _size = ParseNumber(SizeBox.Text, ClockVisuals.MinSize, ClockVisuals.MaxSize);
        _spacing = ParseNumber(SpacingBox.Text, 0.8, 1.4) ?? 0.95;

        UpdatePreview();
        UpdateDatePreview();
    }

    private void OnClockSelected(object sender, SelectionChangedEventArgs e)
    {
        _current = ClockList.SelectedItem as ClockRow;

        _loading = true;
        EditorPanel.IsEnabled = _current is not null;

        if (_current is not null)
        {
            var clock = _current.Clock;
            ZoneBox.SelectedItem = _zones.FirstOrDefault(zone => zone.Id == clock.Zone);
            AliasBox.Text = clock.Alias ?? Clocks.AliasFor(clock);
            ColorBox.Text = clock.Color ?? "";
            UpdateSwatch(ColorSwatch, ColorBox.Text);
        }

        _loading = false;
        UpdatePreview();
    }

    private void OnZoneChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _current is null || ZoneBox.SelectedItem is not TimeZoneInfo zone)
        {
            return;
        }

        var clock = _current.Clock;
        clock.Zone = zone.Id;
        clock.Label = Clocks.GuessLabel(zone);

        if (string.IsNullOrWhiteSpace(clock.Alias))
        {
            clock.Alias = Clocks.AliasFor(zone.Id, clock.Label);
        }

        _current.Refresh();
        UpdatePreview();
    }

    private void OnAliasChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _current is null)
        {
            return;
        }

        var text = AliasBox.Text.Trim();
        _current.Clock.Alias = text.Length == 0 ? null : Clocks.NormalizeAlias(text);
        _current.Refresh();
        UpdatePreview();
    }

    private void OnColorChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _current is null)
        {
            return;
        }

        var text = ColorBox.Text.Trim();
        _current.Clock.Color = ClockVisuals.ParseColor(text) is null ? null : text;
        UpdateSwatch(ColorSwatch, text);
        UpdatePreview();
    }

    private void OnPickClockColor(object sender, RoutedEventArgs e) => PickColor(ColorBox, ColorSwatch);

    private void OnClearClockColor(object sender, RoutedEventArgs e)
    {
        ColorBox.Text = "";
    }

    private void OnAddClock(object sender, RoutedEventArgs e)
    {
        var used = _rows.Select(row => row.Clock.Zone).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var zone = _zones.FirstOrDefault(z => !used.Contains(z.Id)) ?? _zones.FirstOrDefault();

        if (zone is null)
        {
            return;
        }

        var label = Clocks.GuessLabel(zone);
        var clock = new ClockSettings
        {
            Zone = zone.Id,
            Label = label,
            Alias = Clocks.AliasFor(zone.Id, label),
        };

        var row = new ClockRow(clock);
        _rows.Add(row);
        ClockList.SelectedItem = row;
    }

    private void OnRemoveClock(object sender, RoutedEventArgs e)
    {
        if (ClockList.SelectedItem is not ClockRow row)
        {
            return;
        }

        var index = _rows.IndexOf(row);
        _rows.Remove(row);

        if (_rows.Count > 0)
        {
            ClockList.SelectedIndex = Math.Clamp(index, 0, _rows.Count - 1);
        }
    }

    private void OnDateChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        UpdateSwatch(DateSwatch, DateColorBox.Text);
        UpdateDatePreview();
    }

    private void OnPickDateColor(object sender, RoutedEventArgs e) => PickColor(DateColorBox, DateSwatch);

    private void OnClearDateColor(object sender, RoutedEventArgs e)
    {
        DateColorBox.Text = "";
    }

    private void PickColor(TextBox target, Border swatch)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
        };

        if (ClockVisuals.ParseColor(target.Text) is SolidColorBrush brush)
        {
            dialog.Color = System.Drawing.Color.FromArgb(brush.Color.R, brush.Color.G, brush.Color.B);
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            target.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            UpdateSwatch(swatch, target.Text);
        }
    }

    private void UpdatePreview()
    {
        PreviewHost.Children.Clear();

        if (_current is null)
        {
            return;
        }

        var clock = _current.Clock;
        var visual = ClockVisuals.Create(
            Clocks.AliasFor(clock),
            _fontFamily,
            _bold,
            clock.Color,
            _size,
            _spacing,
            PreviewAliasBrush,
            PreviewTimeBrush,
            "07:09");

        PreviewHost.Children.Add(visual.Panel);
    }

    private void UpdateDatePreview()
    {
        DatePreviewHost.Children.Clear();

        var alias = string.IsNullOrWhiteSpace(DateAliasBox.Text)
            ? Clocks.WeekdayAlias(DateTime.Now)
            : DateAliasBox.Text;

        var visual = ClockVisuals.Create(
            alias,
            _fontFamily,
            _bold,
            DateColorBox.Text,
            _size,
            _spacing,
            PreviewAliasBrush,
            PreviewTimeBrush,
            DateTime.Now.ToString("dd/MM/yy", CultureInfo.InvariantCulture));

        DatePreviewHost.Children.Add(visual.Panel);
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        Result = _rows.Select(row => row.Clock).ToList();

        var alias = DateAliasBox.Text.Trim();
        DateAlias = alias.Length == 0 ? "" : Clocks.NormalizeAlias(alias);
        DateColor = ClockVisuals.ParseColor(DateColorBox.Text) is null
            ? null
            : DateColorBox.Text.Trim();

        ClockFontFamily = string.IsNullOrWhiteSpace(FontBox.Text) ? null : FontBox.Text.Trim();
        ClockFontSize = ParseNumber(SizeBox.Text, ClockVisuals.MinSize, ClockVisuals.MaxSize);
        ClockBold = BoldBox.IsChecked == true;
        ClockLetterSpacing = ParseNumber(SpacingBox.Text, 0.8, 1.4) ?? 0.95;

        DialogResult = true;
        Close();
    }

    private static readonly Brush PreviewAliasBrush = Frozen(Color.FromRgb(0x88, 0x88, 0x88));
    private static readonly Brush PreviewTimeBrush = Frozen(Color.FromRgb(0x11, 0x11, 0x11));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void UpdateSwatch(Border swatch, string? text) =>
        swatch.Background = ClockVisuals.ParseColor(text) ?? Brushes.Transparent;

    private static double? ParseNumber(string? text, double min, double max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim().Replace(',', '.');

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number, min, max)
            : null;
    }

    private static string FormatNumber(double? value, string format) =>
        value is null ? "" : value.Value.ToString(format, CultureInfo.InvariantCulture);
}
