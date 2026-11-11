using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using BarDock.Interop;
using BarDock.Services;
using Microsoft.Win32;

namespace BarDock;

public partial class MainWindow : Window
{
    private const double IndicatorRow = 5.0;
    private const double BounceSeconds = 0.6;

    private readonly BarSettings _settings;
    private readonly List<ItemView> _views = new();
    private readonly List<ClockView> _clockViews = new();
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _hoverCloseTimer;

    private Point? _cursor;
    private double _lastFrame;
    private bool _loopHooked;
    private bool _dialogOpen;
    private string _extrasSignature = "";
    private ThemeSnapshot _theme;
    private double _iconTop;
    private string _appliedBarEdge = "";
    private IntPtr _handle;

    private TextBlock? _bellGlyph;
    private TextBlock? _dateText;
    private TextBlock? _dateAliasText;
    private Popup? _hoverPopup;
    private ItemView? _hoverView;

    private StackPanel _batteryGroup = null!;
    private Border _batteryShell = null!;
    private Border _batteryFill = null!;
    private Border _batteryNub = null!;
    private TextBlock _batteryText = null!;
    private TextBlock _networkGlyph = null!;
    private TextBlock _volumeGlyph = null!;
    private Border _networkButton = null!;
    private Border _volumeButton = null!;
    private SystemStatusSnapshot _status;
    private bool _statusApplied;

    private Brush _foreground = Brushes.White;
    private Brush _subtle = Brushes.Gray;
    private Brush _accent = Brushes.DodgerBlue;
    private Brush _separator = Brushes.DimGray;

    public MainWindow(BarSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClocks();

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _refreshTimer.Tick += (_, _) => RefreshLiveState();

        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            ShowWindowPopup();
        };

        _hoverCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        _hoverCloseTimer.Tick += (_, _) =>
        {
            _hoverCloseTimer.Stop();
            CloseWindowPopup();
        };

        Opacity = 0.0;
        _theme = Theme.Read();
        ApplyTheme(_theme, force: true);
        ApplyMetrics();
        BuildItems();
        BuildClocks();
        UpdateStatus();

        Loaded += OnWindowLoaded;
        Closed += (_, _) =>
        {
            _clockTimer.Stop();
            _refreshTimer.Stop();
            _hoverTimer.Stop();
            _hoverCloseTimer.Stop();
            CloseWindowPopup();
            StopLoop();

            if (WindowsTaskbar.IsHidden)
            {
                WindowsTaskbar.Restore();
                MaximizedWindows.Refit(_handle);
            }

            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    // ---------------------------------------------------------------- medidas

    /// <summary>La barra tiene alto fijo: los iconos ampliados sobresalen por debajo.</summary>
    private void ApplyMetrics()
    {
        Normalize();

        _iconTop = Math.Max(0.0, (_settings.BarHeight - _settings.IconSize - IndicatorRow) / 2.0);

        Bar.Height = _settings.BarHeight;

        var leftAligned = IsLeftAligned;
        ItemsHost.HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        Stack.HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        Stack.Margin = new Thickness(leftAligned ? IconsLeftInset() : 0.0, _iconTop, 0.0, 0.0);

        var maxBottom = _iconTop
                        + (_settings.IconSize * _settings.MaxScale)
                        + IndicatorRow
                        + (_settings.IconSize * 0.35);

        var windowHeight = Math.Max(_settings.BarHeight, maxBottom) + 12.0;
        var bottom = _settings.BarAtBottom;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var taskbarOffset = TaskbarOffset(bottom, screenHeight);

        Bar.VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;

        // Sin borde: un borde semitransparente dejaba una linea con el escritorio
        // visible entre la barra y el contenido.
        Bar.BorderThickness = new Thickness(0.0);

        if (bottom)
        {
            // Pegar el borde inferior de la ventana (y de la barra) al de la pantalla,
            // sin huecos por redondeo.
            var top = Math.Round(screenHeight - windowHeight - taskbarOffset - _settings.TopMargin);
            Top = top;
            Height = Math.Max(_settings.BarHeight, screenHeight - top);
        }
        else
        {
            Height = windowHeight;
            Top = taskbarOffset + _settings.TopMargin;
        }

        HoverBand.Height = Math.Max(0.0, Height - _settings.BarHeight - 1.0);
        HoverBand.VerticalAlignment = VerticalAlignment.Top;
        HoverBand.Margin = bottom
            ? new Thickness(0.0)
            : new Thickness(0.0, _settings.BarHeight, 0.0, 0.0);

        Left = 0.0;
        Width = SystemParameters.PrimaryScreenWidth;

        ReserveBarSpace();
    }

    /// <summary>
    /// Desplazamiento para no solaparse con la barra de tareas de Windows cuando NO la
    /// sustituimos (si esta en el mismo borde).
    /// </summary>
    private double TaskbarOffset(bool bottom, double screenHeight)
    {
        if (_settings.ReplaceTaskbar || WindowsTaskbar.TaskbarRect() is not { } taskbar)
        {
            return 0.0;
        }

        if (bottom)
        {
            return taskbar.Top > screenHeight / 2.0 ? screenHeight - taskbar.Top : 0.0;
        }

        return taskbar.Bottom < screenHeight / 2.0 ? taskbar.Bottom : 0.0;
    }

    private void ReserveBarSpace()
    {
        if (!_settings.ReplaceTaskbar)
        {
            _appliedBarEdge = "";
            return;
        }

        // Al arrancar aun no se ha ocultado la barra de Windows: se aplica en OnWindowLoaded.
        if (!WindowsTaskbar.IsHidden)
        {
            return;
        }

        var bottom = _settings.BarAtBottom;
        var size = _settings.TopMargin + _settings.BarHeight;
        var edge = $"{(bottom ? "b" : "t")}:{size:0.##}";

        WindowsTaskbar.Reserve(size, bottom);

        if (edge == _appliedBarEdge)
        {
            return;
        }

        // Las ventanas ya maximizadas siguen ancladas al area de trabajo anterior:
        // hay que restaurarlas y maximizarlas de nuevo para que empiecen bajo la barra.
        _appliedBarEdge = edge;
        MaximizedWindows.Refit(_handle);
    }

    private void Normalize()
    {
        _settings.BarHeight = Math.Clamp(_settings.BarHeight, 30, 96);
        _settings.IconSize = Math.Clamp(_settings.IconSize, 14, 64);
        _settings.IconSize = Math.Min(_settings.IconSize, _settings.BarHeight - 6);
        _settings.MaxScale = Math.Clamp(_settings.MaxScale, 1.0, 2.5);
        _settings.Spacing = Math.Clamp(_settings.Spacing, 0, 24);

        _settings.ClockFontSize = _settings.ClockFontSize is { } size
            ? Math.Clamp(size, ClockVisuals.MinSize, ClockVisuals.MaxSize)
            : null;
        _settings.ClockLetterSpacing = Math.Clamp(_settings.ClockLetterSpacing, 0.8, 1.4);
        _settings.BarOpacity = Math.Clamp(_settings.BarOpacity, 0.3, 1.0);
        _settings.IconAlignment = string.Equals(_settings.IconAlignment, "left", StringComparison.OrdinalIgnoreCase)
            ? "left"
            : "center";
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ApplyMetrics();

        if (_settings.ReplaceTaskbar)
        {
            WindowsTaskbar.Hide();
            ReserveBarSpace();
        }

        _clockTimer.Start();
        _refreshTimer.Start();
        UpdateClocks();
        UpdateStatus();
        BuildTray(force: true);
        Opacity = 1.0;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(ApplyMetrics);

    // ---------------------------------------------------------------- tema

    private void ApplyTheme(ThemeSnapshot theme, bool force = false)
    {
        if (!force && theme == _theme)
        {
            return;
        }

        _theme = theme;

        var alpha = (byte)Math.Round(Math.Clamp(_settings.BarOpacity, 0.3, 1.0) * 255.0);

        var barBackground = theme.IsLight
            ? Color.FromArgb(alpha, 0xF3, 0xF3, 0xF3)
            : Color.FromArgb(alpha, 0x20, 0x20, 0x20);

        _foreground = Frozen(theme.IsLight ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Colors.White);
        _subtle = Frozen(theme.IsLight ? Color.FromArgb(0x99, 0x00, 0x00, 0x00) : Color.FromArgb(0xA6, 0xFF, 0xFF, 0xFF));
        _accent = Frozen(theme.Accent);
        _separator = Frozen(theme.IsLight ? Color.FromArgb(0x33, 0x00, 0x00, 0x00) : Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        Bar.Background = Frozen(barBackground);
        TraySeparator.Background = _separator;
        TrayEdge.Background = _separator;

        foreach (var view in _views)
        {
            view.Dot.Background = _accent;
        }

        BuildStatus();
        BuildClocks();
        UpdateStatus(force: true);
        BuildTray(force: true);
        BuildStartButton();
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    // ---------------------------------------------------------------- boton de inicio

    private void BuildStartButton()
    {
        LeftPanel.Children.Clear();
        LeftPanel.Children.Add(CreateStartButton());

        if (_settings.ShowTaskView)
        {
            LeftPanel.Children.Add(CreateTaskViewButton());
        }
    }

    private Border CreateStartButton()
    {
        var logo = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(
                "M0,0 H6 V6 H0 Z M8,0 H14 V6 H8 Z M0,8 H6 V14 H0 Z M8,8 H14 V14 H8 Z"),
            Fill = _foreground,
            Width = 14.0,
            Height = 14.0,
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var button = CreateLeftButton(logo, Loc.S("Inicio", "Start"));

        button.MouseLeftButtonUp += (_, e) =>
        {
            ShellHelper.OpenStartMenu();
            e.Handled = true;
        };
        button.MouseRightButtonUp += (_, e) =>
        {
            ShowStartMenu(button);
            e.Handled = true;
        };

        return button;
    }

    private Border CreateTaskViewButton()
    {
        var glyph = StatusGlyph("\uE7C4");
        glyph.Foreground = _foreground;

        var button = CreateLeftButton(glyph, Loc.S("Vista de tareas", "Task view"));
        button.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);

        button.MouseLeftButtonUp += (_, e) =>
        {
            ShellHelper.OpenTaskView();
            e.Handled = true;
        };

        return button;
    }

    private Border CreateLeftButton(UIElement content, string toolTip)
    {
        var hover = Frozen(_theme.IsLight
            ? Color.FromArgb(0x14, 0x00, 0x00, 0x00)
            : Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));

        var button = new Border
        {
            Width = 28.0,
            Height = 28.0,
            CornerRadius = new CornerRadius(7.0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = toolTip,
            Child = content,
            VerticalAlignment = VerticalAlignment.Center,
        };

        button.MouseEnter += (_, _) => button.Background = hover;
        button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;

        return button;
    }

    private static void ShowStartMenu(Border target)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Entry(Loc.S("Todas las aplicaciones", "All apps"), () => OpenUri("shell:AppsFolder")));
        menu.Items.Add(Entry(Loc.S("Configuracion", "Settings"), () => OpenUri("ms-settings:")));
        menu.PlacementTarget = target;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    // ---------------------------------------------------------------- iconos

    private void BuildItems()
    {
        CloseWindowPopup();
        _hoverView = null;

        var running = RunningApps.Get();

        var extras = running
            .Where(app => !IsPinned(app))
            .GroupBy(app => app.Aumid ?? app.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(app => app.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Stack.Children.Clear();
        _views.Clear();

        foreach (var item in _settings.Items)
        {
            var aumid = ShellHelper.GetAumid(item.Path);
            var processName = aumid is null ? ShellHelper.GetProcessName(item.Path) : "";

            AddView(new ItemView
            {
                Model = item,
                Name = ShellHelper.GetDisplayName(item),
                Path = item.Path,
                Aumid = aumid,
                ProcessName = processName,
                IsRunning = running.Any(app => IsSameApp(app, aumid, processName)),
                RunningOnly = false,
            });
        }

        foreach (var app in extras)
        {
            AddView(new ItemView
            {
                Model = null,
                Name = FriendlyNames.Label(app.ProcessName),
                Path = app.ExecutablePath ?? app.ProcessName,
                Aumid = app.Aumid,
                ProcessName = app.ProcessName,
                Handle = app.Handle,
                IsRunning = true,
                RunningOnly = true,
            });
        }

        _extrasSignature = ExtrasSignature(running);
        ApplyMetrics();
    }

    /// <summary>Una app esta anclada si comparte AUMID (o proceso, para .exe/.lnk).</summary>
    private bool IsPinned(RunningApp app)
    {
        foreach (var item in _settings.Items)
        {
            var aumid = ShellHelper.GetAumid(item.Path);
            var processName = aumid is null ? ShellHelper.GetProcessName(item.Path) : "";

            if (IsSameApp(app, aumid, processName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameApp(RunningApp app, string? aumid, string processName)
    {
        if (!string.IsNullOrWhiteSpace(aumid))
        {
            return ShellHelper.SamePackagedApp(aumid, app.Aumid);
        }

        return processName.Length > 0 &&
               string.Equals(processName, app.ProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private string ExtrasSignature(IEnumerable<RunningApp> running)
    {
        return string.Join(
            "|",
            running
                .Where(app => !IsPinned(app))
                .Select(app => app.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
    }

    private void AddView(ItemView view)
    {
        var icon = IconExtractor.GetIcon(view.Path, view.Aumid);
        var bottom = _settings.BarAtBottom;

        // Con la barra abajo los iconos crecen hacia arriba y el indicador va encima.
        var holder = new Grid
        {
            Width = _settings.IconSize,
            Height = _settings.IconSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, bottom ? 1.0 : 0.0),
        };

        if (icon is not null)
        {
            var image = new Image
            {
                Source = icon,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            holder.Children.Add(image);
        }
        else
        {
            holder.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(_settings.IconSize * 0.22),
                Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x52)),
                Child = new TextBlock
                {
                    Text = IconExtractor.InitialFor(view.Name),
                    Foreground = Brushes.White,
                    FontSize = _settings.IconSize * 0.45,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
        }

        var scale = new ScaleTransform(1.0, 1.0);
        var translate = new TranslateTransform(0.0, 0.0);
        var transform = new TransformGroup();
        transform.Children.Add(scale);
        transform.Children.Add(translate);
        holder.RenderTransform = transform;

        var dot = new Border
        {
            Width = 12.0,
            Height = 2.0,
            CornerRadius = new CornerRadius(1.0),
            Background = _accent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = bottom ? VerticalAlignment.Top : VerticalAlignment.Bottom,
            Margin = bottom
                ? new Thickness(0.0, 1.0, 0.0, 0.0)
                : new Thickness(0.0, 0.0, 0.0, 1.0),
            Opacity = view.IsRunning ? 1.0 : 0.0,
            RenderTransformOrigin = new Point(0.5, bottom ? 1.0 : 0.0),
        };

        // El indicador crece con el icono y se desplaza con su borde inferior.
        var dotScale = new ScaleTransform(1.0, 1.0);
        var dotTranslate = new TranslateTransform(0.0, 0.0);
        var dotTransform = new TransformGroup();
        dotTransform.Children.Add(dotScale);
        dotTransform.Children.Add(dotTranslate);
        dot.RenderTransform = dotTransform;

        var container = new Grid
        {
            Background = Brushes.Transparent,
            Margin = new Thickness(_settings.Spacing / 2.0, 0.0, _settings.Spacing / 2.0, 0.0),
            ToolTip = view.Name,
            Cursor = Cursors.Hand,
        };
        AutomationProperties.SetName(container, view.Name);

        // La zona clicable se mueve con el icono, para que el clic caiga siempre
        // sobre el icono que se ve (no sobre el de al lado).
        var containerTranslate = new TranslateTransform(0.0, 0.0);
        container.RenderTransform = containerTranslate;

        if (bottom)
        {
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(IndicatorRow) });
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(dot, 0);
            Grid.SetRow(holder, 1);
        }
        else
        {
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(IndicatorRow) });
            Grid.SetRow(dot, 1);
            Grid.SetRow(holder, 0);
        }

        container.Children.Add(dot);
        container.Children.Add(holder);

        view.Container = container;
        view.Holder = holder;
        view.Scale = scale;
        view.Translate = translate;
        view.ContainerTranslate = containerTranslate;
        view.Dot = dot;
        view.DotScale = dotScale;
        view.DotTranslate = dotTranslate;

        container.MouseLeftButtonUp += (_, e) =>
        {
            Launch(view);
            e.Handled = true;
        };
        container.MouseRightButtonUp += (_, e) =>
        {
            ShowItemMenu(view);
            e.Handled = true;
        };
        container.MouseEnter += (_, _) => StartHover(view);
        container.MouseLeave += (_, _) => ScheduleHoverClose();

        _views.Add(view);
        Stack.Children.Add(container);
    }

    // ---------------------------------------------------------------- ventanas por icono

    private void StartHover(ItemView view)
    {
        _hoverCloseTimer.Stop();

        if (_dialogOpen || !view.IsRunning)
        {
            return;
        }

        _hoverView = view;
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void ScheduleHoverClose()
    {
        _hoverTimer.Stop();

        if (_hoverPopup is null)
        {
            return;
        }

        _hoverCloseTimer.Stop();
        _hoverCloseTimer.Start();
    }

    private static RunningApp? FindRunning(ItemView view) =>
        RunningApps.Get().FirstOrDefault(app => IsSameApp(app, view.Aumid, view.ProcessName));

    /// <summary>Muestra las ventanas abiertas de la app para poder elegir cual traer al frente.</summary>
    private void ShowWindowPopup()
    {
        if (_hoverView is not { } view)
        {
            return;
        }

        var app = FindRunning(view);
        if (app is null || app.Windows.Count == 0)
        {
            CloseWindowPopup();
            return;
        }

        CloseWindowPopup();

        var light = _theme.IsLight;
        var panel = new StackPanel { MaxWidth = 430 };

        panel.Children.Add(new TextBlock
        {
            Text = app.Windows.Count == 1
                ? "1 ventana abierta"
                : $"{app.Windows.Count} ventanas abiertas",
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 10.0,
            Foreground = _subtle,
            Margin = new Thickness(10, 6, 10, 4),
        });

        foreach (var window in app.Windows)
        {
            var row = new Border
            {
                CornerRadius = new CornerRadius(5),
                Background = Brushes.Transparent,
                Padding = new Thickness(10, 7, 10, 7),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = window.Title,
                    FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                    FontSize = 12.0,
                    Foreground = _foreground,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 390,
                },
            };

            row.MouseEnter += (_, _) => row.Background = Frozen(light
                ? Color.FromArgb(0x14, 0x00, 0x00, 0x00)
                : Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (_, e) =>
            {
                ShellHelper.TryActivate(window.Handle);
                CloseWindowPopup();
                e.Handled = true;
            };

            panel.Children.Add(row);
        }

        var content = new Border
        {
            Background = Frozen(light
                ? Color.FromArgb(0xFF, 0xF7, 0xF7, 0xF7)
                : Color.FromArgb(0xFF, 0x2B, 0x2B, 0x2B)),
            BorderBrush = Frozen(light
                ? Color.FromArgb(0x33, 0x00, 0x00, 0x00)
                : Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0, 2, 0, 6),
            Child = panel,
            Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 3,
                Opacity = 0.35,
                Color = Colors.Black,
            },
        };

        content.MouseEnter += (_, _) => _hoverCloseTimer.Stop();
        content.MouseLeave += (_, _) => ScheduleHoverClose();

        var popup = new Popup
        {
            AllowsTransparency = true,
            StaysOpen = true,
            PlacementTarget = view.Container,
            Placement = _settings.BarAtBottom ? PlacementMode.Top : PlacementMode.Bottom,
            VerticalOffset = _settings.BarAtBottom ? -2 : 2,
            Child = content,
        };

        popup.Closed += (sender, _) =>
        {
            if (ReferenceEquals(_hoverPopup, sender))
            {
                _hoverPopup = null;
            }
        };

        _hoverPopup = popup;
        popup.IsOpen = true;
    }

    private void CloseWindowPopup()
    {
        _hoverTimer.Stop();
        _hoverCloseTimer.Stop();

        var popup = _hoverPopup;
        _hoverPopup = null;
        _hoverView = null;

        if (popup is not null)
        {
            popup.IsOpen = false;
        }
    }

    // ---------------------------------------------------------------- bandeja y notificaciones

    private void BuildTray(bool force = false)
    {
        if (!force)
        {
            return;
        }

        TrayPanel.Children.Clear();

        // Solo la vineta: abre el panel nativo de la bandeja de Windows.
        TrayPanel.Children.Add(CreateOverflowButton());

        _bellGlyph = StatusGlyph("\uEA8F");
        _bellGlyph.Foreground = _foreground;

        var bell = new Border
        {
            Width = 24.0,
            Height = 24.0,
            Margin = new Thickness(2.0, 0.0, 0.0, 0.0),
            CornerRadius = new CornerRadius(4.0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = Loc.S("Notificaciones", "Notifications"),
            Child = _bellGlyph,
            VerticalAlignment = VerticalAlignment.Center,
        };
        bell.MouseLeftButtonUp += (_, e) =>
        {
            OpenUri("ms-actioncenter:");
            e.Handled = true;
        };

        TrayPanel.Children.Add(bell);
    }

    /// <summary>Chevron que abre el panel nativo de iconos ocultos de Windows.</summary>
    private Border CreateOverflowButton()
    {
        // Un trazo en vez de un glifo: se ve igual con cualquier fuente.
        var glyph = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M0,0 L4.5,4.5 L9,0"),
            Stroke = _foreground,
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 9.0,
            Height = 5.0,
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
        };

        var hover = Frozen(_theme.IsLight
            ? Color.FromArgb(0x14, 0x00, 0x00, 0x00)
            : Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));

        var button = new Border
        {
            Width = 20.0,
            Height = 24.0,
            Margin = new Thickness(1.0, 0.0, 3.0, 0.0),
            CornerRadius = new CornerRadius(4.0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = Loc.S("Iconos ocultos de Windows", "Windows hidden icons"),
            Child = glyph,
            VerticalAlignment = VerticalAlignment.Center,
        };

        AutomationProperties.SetName(button, Loc.S("Iconos ocultos de Windows", "Windows hidden icons"));

        button.MouseEnter += (_, _) => button.Background = hover;
        button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;
        button.MouseLeftButtonUp += (_, e) =>
        {
            TrayOverflow.ShowAsync();
            e.Handled = true;
        };
        button.MouseRightButtonUp += (_, e) =>
        {
            TrayOverflow.ShowAsync();
            e.Handled = true;
        };

        return button;
    }

    // ---------------------------------------------------------------- estado del sistema

    private void BuildStatus()
    {
        StatusPanel.Children.Clear();

        _batteryShell = new Border
        {
            Width = 21.0,
            Height = 12.0,
            CornerRadius = new CornerRadius(2.5),
            BorderThickness = new Thickness(1.2),
            BorderBrush = _foreground,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _batteryFill = new Border
        {
            Height = 6.0,
            CornerRadius = new CornerRadius(1.0),
            Background = _foreground,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2.5, 0.0, 0.0, 0.0),
        };
        _batteryShell.Child = _batteryFill;

        _batteryNub = new Border
        {
            Width = 2.0,
            Height = 5.0,
            CornerRadius = new CornerRadius(1.0),
            Background = _foreground,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var batteryShape = new Grid { Width = 24.0, Height = 14.0, VerticalAlignment = VerticalAlignment.Center };
        batteryShape.Children.Add(_batteryShell);
        batteryShape.Children.Add(_batteryNub);

        _batteryText = new TextBlock
        {
            Text = "--%",
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 10.5,
            Foreground = _subtle,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4.0, 0.0, 0.0, 0.0),
        };

        _batteryGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
            Cursor = Cursors.Hand,
        };
        _batteryGroup.Children.Add(batteryShape);
        _batteryGroup.Children.Add(_batteryText);
        _batteryGroup.MouseLeftButtonUp += (_, _) => OpenUri("ms-settings:batterysaver");
        StatusPanel.Children.Add(_batteryGroup);

        _networkGlyph = StatusGlyph("\uE774");
        _networkButton = StatusButton(_networkGlyph, 18.0);
        _networkButton.MouseLeftButtonUp += (_, _) => OpenUri("ms-availablenetworks:");
        StatusPanel.Children.Add(_networkButton);

        _volumeGlyph = StatusGlyph("\uE767");
        _volumeButton = StatusButton(_volumeGlyph, 18.0);
        _volumeButton.MouseLeftButtonUp += (_, _) => ToggleMute();
        _volumeButton.MouseWheel += Volume_MouseWheel;
        StatusPanel.Children.Add(_volumeButton);
    }

    private static TextBlock StatusGlyph(string code) => new()
    {
        Text = code,
        FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        FontSize = 13.0,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        Padding = new Thickness(0.0, 0.0, 0.0, 1.0),
    };

    private Border StatusButton(TextBlock glyph, double width)
    {
        glyph.Foreground = _foreground;

        return new Border
        {
            Width = width,
            Height = 24.0,
            Margin = new Thickness(0.0, 0.0, 2.0, 0.0),
            CornerRadius = new CornerRadius(4.0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = glyph,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private void UpdateStatus(bool force = false)
    {
        var status = SystemStatus.Read();

        // Repintar la barra solo cuando algo cambia de verdad.
        if (!force && _statusApplied && status == _status)
        {
            return;
        }

        _status = status;
        _statusApplied = true;

        _batteryGroup.Visibility = status.HasBattery ? Visibility.Visible : Visibility.Collapsed;

        if (status.HasBattery)
        {
            var low = status.BatteryPercent <= 20 && !status.Charging;
            var batteryBrush = low ? Frozen(Color.FromRgb(0xE8, 0x11, 0x23)) : _foreground;

            _batteryShell.BorderBrush = batteryBrush;
            _batteryNub.Background = batteryBrush;
            _batteryFill.Background = batteryBrush;
            _batteryFill.Width = Math.Max(0.0, 16.1 * (status.BatteryPercent / 100.0));

            _batteryText.Text = $"{status.BatteryPercent}%";
            _batteryGroup.ToolTip = status.Charging
                ? $"Bateria {status.BatteryPercent}% (cargando)"
                : $"Bateria {status.BatteryPercent}%";
        }

        _networkGlyph.Text = status.NetworkAvailable
            ? (status.Wireless ? "\uE701" : "\uE839")
            : "\uE774";

        _networkButton.ToolTip = status.NetworkAvailable
            ? (string.IsNullOrWhiteSpace(status.NetworkName) ? "Conectado" : status.NetworkName)
            : "Sin conexion";

        _volumeGlyph.Text = status.Muted ? "\uE74F" : "\uE767";
        _volumeButton.ToolTip = status.VolumeAvailable
            ? (status.Muted ? "Silenciado (clic para activar, rueda para volumen)" : $"Volumen {status.VolumePercent}% (clic para silenciar, rueda para ajustar)")
            : "Volumen";
    }

    private void ToggleMute()
    {
        if (Audio.TryToggleMute())
        {
            UpdateStatus();
        }
    }

    private void Volume_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var current = _status.VolumeAvailable ? _status.VolumePercent : 50;
        var target = current + (e.Delta > 0 ? 5 : -5);

        if (Audio.TrySetVolume(target))
        {
            UpdateStatus();
        }

        e.Handled = true;
    }

    private static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
            });
        }
        catch
        {
            // sin handler registrado para el protocolo
        }
    }

    // ---------------------------------------------------------------- relojes

    private void BuildClocks()
    {
        ClockPanel.Children.Clear();
        _clockViews.Clear();

        var first = true;

        foreach (var clock in _settings.Clocks)
        {
            var zone = Clocks.Resolve(clock.Zone);
            if (zone is null)
            {
                continue;
            }

            if (!first)
            {
                ClockPanel.Children.Add(ClockSeparator());
            }

            first = false;

            var label = string.IsNullOrWhiteSpace(clock.Label) ? Clocks.GuessLabel(zone) : clock.Label;
            var visual = ClockVisuals.Create(
                Clocks.AliasFor(clock),
                _settings.ClockFontFamily,
                _settings.ClockBold,
                clock.Color,
                _settings.ClockFontSize,
                _settings.ClockLetterSpacing,
                _subtle,
                _foreground,
                toolTip: $"{label} ({Clocks.OffsetLabel(zone)})");

            visual.Panel.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);

            ClockPanel.Children.Add(visual.Panel);
            _clockViews.Add(new ClockView { Zone = zone, Time = visual.Time });
        }

        // Fecha local con el mismo patron: alias vertical + dd/mm/aa.
        ClockPanel.Children.Add(ClockSeparator());

        var dateVisual = ClockVisuals.Create(
            Clocks.DateAliasFor(_settings.DateAlias, DateTime.Now),
            _settings.ClockFontFamily,
            _settings.ClockBold,
            _settings.DateColor,
            _settings.ClockFontSize,
            _settings.ClockLetterSpacing,
            _subtle,
            _foreground);

        dateVisual.Panel.Margin = new Thickness(0.0, 0.0, 2.0, 0.0);
        dateVisual.Panel.ToolTip = "Fecha local";

        _dateAliasText = dateVisual.Alias;
        _dateText = dateVisual.Time;
        ClockPanel.Children.Add(dateVisual.Panel);

        UpdateClocks();
    }

    private Border ClockSeparator() => new()
    {
        Width = 1.0,
        Height = 16.0,
        Margin = new Thickness(0.0, 0.0, 12.0, 0.0),
        VerticalAlignment = VerticalAlignment.Center,
        Background = _separator,
    };

    private void UpdateClocks()
    {
        foreach (var clock in _clockViews)
        {
            var now = Clocks.NowIn(clock.Zone);
            var text = now.ToString("HH:mm");

            // Asignar solo al cambiar evita repintar la barra cada segundo.
            if (clock.Time.Text != text)
            {
                clock.Time.Text = text;
            }
        }

        var local = DateTime.Now;

        if (_dateText is not null)
        {
            var text = local.ToString("dd/MM/yy", CultureInfo.InvariantCulture);

            if (_dateText.Text != text)
            {
                _dateText.Text = text;
            }
        }

        if (_dateAliasText is not null)
        {
            // Si no hay alias propio, la fecha muestra el dia de la semana.
            var alias = Clocks.Vertical(Clocks.DateAliasFor(_settings.DateAlias, local));

            if (_dateAliasText.Text != alias)
            {
                _dateAliasText.Text = alias;
            }
        }
    }

    // ---------------------------------------------------------------- acciones

    private void Launch(ItemView view)
    {
        CloseWindowPopup();

        try
        {
            var activated = ShellHelper.TryActivate(view.Handle);

            if (!activated && view.Aumid is not null)
            {
                var match = RunningApps.Get()
                    .FirstOrDefault(app => ShellHelper.SamePackagedApp(view.Aumid, app.Aumid));
                if (match is not null)
                {
                    activated = ShellHelper.TryActivate(match.Handle);
                }
            }

            if (!activated && view.ProcessName.Length > 0)
            {
                activated = ShellHelper.TryActivate(view.ProcessName);
            }

            if (!activated && !view.RunningOnly)
            {
                ShellHelper.Launch(view.Path);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"No se pudo abrir \"{view.Name}\".\n\n{ex.Message}",
                "BarDock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        view.BounceTime = 0.0;
        EnsureLoop();
    }

    private void AddFromDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Anadir aplicacion a la barra",
            Filter = "Aplicaciones (*.exe;*.lnk;*.url)|*.exe;*.lnk;*.url|Todos los archivos (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var file in dialog.FileNames)
        {
            AddItemPath(file);
        }
    }

    private void AddItemPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var aumid = PackagedApps.AumidFromShellPath(path);

        if (_settings.Items.Any(item =>
                string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase) ||
                (aumid is not null && ShellHelper.SamePackagedApp(
                    PackagedApps.AumidFromShellPath(item.Path), aumid))))
        {
            return;
        }

        var name = aumid is not null
            ? PackagedApps.NameOf(aumid)
            : System.IO.Path.GetFileNameWithoutExtension(ShellHelper.ResolveTarget(path));

        _settings.Items.Add(new BarItemSettings
        {
            Path = path,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
        });

        SettingsStore.Save(_settings);
        BuildItems();
    }

    private void RemoveItem(ItemView view)
    {
        if (view.Model is null)
        {
            return;
        }

        _settings.Items.Remove(view.Model);
        SettingsStore.Save(_settings);
        BuildItems();
    }

    private void Reload()
    {
        var wasReplacing = _settings.ReplaceTaskbar;
        var loaded = SettingsStore.Load();

        _settings.Items.Clear();
        _settings.Items.AddRange(loaded.Items);
        _settings.Clocks.Clear();
        _settings.Clocks.AddRange(loaded.Clocks);
        _settings.IconSize = loaded.IconSize;
        _settings.MaxScale = loaded.MaxScale;
        _settings.Spacing = loaded.Spacing;
        _settings.BarHeight = loaded.BarHeight;
        _settings.TopMargin = loaded.TopMargin;
        _settings.ReplaceTaskbar = loaded.ReplaceTaskbar;
        _settings.DateAlias = loaded.DateAlias;
        _settings.DateColor = loaded.DateColor;
        _settings.ClockFontFamily = loaded.ClockFontFamily;
        _settings.ClockFontSize = loaded.ClockFontSize;
        _settings.ClockBold = loaded.ClockBold;
        _settings.ClockLetterSpacing = loaded.ClockLetterSpacing;
        _settings.BarPosition = loaded.BarPosition;
        _settings.BarOpacity = loaded.BarOpacity;
        _settings.IconAlignment = loaded.IconAlignment;
        _settings.ShowTaskView = loaded.ShowTaskView;
        _settings.Language = loaded.Language;

        Loc.Set(_settings.Language);

        if (_settings.ReplaceTaskbar && !wasReplacing)
        {
            WindowsTaskbar.Hide();
        }
        else if (!_settings.ReplaceTaskbar && wasReplacing)
        {
            WindowsTaskbar.Restore();
            MaximizedWindows.Refit(_handle);
            _appliedBarEdge = "";
        }

        IconExtractor.ClearCache();
        ApplyMetrics();
        BuildItems();
        BuildClocks();
        BuildTray(force: true);
    }

    private void ChooseClocks()
    {
        _dialogOpen = true;
        try
        {
            var dialog = new ClockPickerWindow(_settings)
            {
                Owner = this,
                Topmost = true,
            };

            if (dialog.ShowDialog() == true)
            {
                _settings.Clocks.Clear();
                _settings.Clocks.AddRange(dialog.Result);
                _settings.DateAlias = dialog.DateAlias;
                _settings.DateColor = dialog.DateColor;
                _settings.ClockFontFamily = dialog.ClockFontFamily;
                _settings.ClockFontSize = dialog.ClockFontSize;
                _settings.ClockBold = dialog.ClockBold;
                _settings.ClockLetterSpacing = dialog.ClockLetterSpacing;
                SettingsStore.Save(_settings);
                BuildClocks();
                ApplyMetrics();
            }
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private void RefreshLiveState()
    {
        if (_dialogOpen)
        {
            return;
        }

        ApplyTheme(Theme.Read());

        if (_settings.ReplaceTaskbar)
        {
            WindowsTaskbar.EnsureHidden();
        }

        UpdateStatus();
        BuildTray();

        var running = RunningApps.Get();

        var signature = ExtrasSignature(running);
        if (signature != _extrasSignature)
        {
            BuildItems();
            return;
        }

        foreach (var view in _views)
        {
            var match = running.FirstOrDefault(app => IsSameApp(app, view.Aumid, view.ProcessName));
            view.IsRunning = view.RunningOnly || match is not null;

            if (match is not null)
            {
                view.Handle = match.Handle;
            }

            view.Dot.Opacity = view.IsRunning ? 1.0 : 0.0;
        }
    }

    // ---------------------------------------------------------------- menus

    private void Bar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Entry(Loc.S("Anadir aplicacion...", "Add app..."), AddFromDialog));
        menu.Items.Add(Entry(Loc.S("Relojes y fecha...", "Clocks and date..."), ChooseClocks));
        menu.Items.Add(Entry(Loc.S("Centro de notificaciones", "Notification center"), () => OpenUri("ms-actioncenter:")));

        var taskView = new MenuItem
        {
            Header = Loc.S("Boton de vista de tareas", "Task view button"),
            IsCheckable = true,
            IsChecked = _settings.ShowTaskView,
        };
        taskView.Click += (_, _) =>
        {
            _settings.ShowTaskView = !_settings.ShowTaskView;
            taskView.IsChecked = _settings.ShowTaskView;
            SettingsStore.Save(_settings);
            BuildStartButton();
            ApplyMetrics();
            BuildItems();
        };
        menu.Items.Add(taskView);

        menu.Items.Add(new Separator());

        menu.Items.Add(BuildPositionMenu());
        menu.Items.Add(BuildAlignmentMenu());

        menu.Items.Add(BuildSizeMenu(
            Loc.S("Altura de la barra", "Bar height"), _settings.BarHeight,
            new[] { 36.0, 40.0, 44.0, 48.0, 52.0, 56.0, 64.0, 72.0 },
            value => _settings.BarHeight = value));

        menu.Items.Add(BuildSizeMenu(
            Loc.S("Tamano del icono", "Icon size"), _settings.IconSize,
            new[] { 18.0, 20.0, 22.0, 24.0, 26.0, 28.0, 30.0, 32.0, 36.0 },
            value => _settings.IconSize = value));

        menu.Items.Add(BuildSizeMenu(
            Loc.S("Ampliacion", "Magnification"), _settings.MaxScale,
            new[] { 1.0, 1.2, 1.4, 1.6, 1.8, 2.0, 2.2 },
            value => _settings.MaxScale = value,
            suffix: "x", format: "0.0"));

        menu.Items.Add(BuildSizeMenu(
            Loc.S("Separacion", "Spacing"), _settings.Spacing,
            new[] { 0.0, 2.0, 4.0, 6.0, 8.0, 10.0, 12.0, 16.0 },
            value => _settings.Spacing = value));

        menu.Items.Add(BuildOpacityMenu());

        menu.Items.Add(new Separator());

        var taskbar = new MenuItem
        {
            Header = Loc.S("Sustituir la barra de Windows", "Replace the Windows taskbar"),
            IsCheckable = true,
            IsChecked = _settings.ReplaceTaskbar,
        };
        taskbar.Click += (_, _) =>
        {
            _settings.ReplaceTaskbar = !_settings.ReplaceTaskbar;
            taskbar.IsChecked = _settings.ReplaceTaskbar;
            SettingsStore.Save(_settings);

            if (_settings.ReplaceTaskbar)
            {
                WindowsTaskbar.Hide();
                ApplyMetrics();
                ReserveBarSpace();
            }
            else
            {
                WindowsTaskbar.Restore();
                MaximizedWindows.Refit(_handle);
                _appliedBarEdge = "";
                ApplyMetrics();
            }
        };
        menu.Items.Add(taskbar);

        var startup = new MenuItem
        {
            Header = Loc.S("Iniciar con Windows", "Start with Windows"),
            IsCheckable = true,
            IsChecked = Startup.IsEnabled(),
        };
        startup.Click += (_, _) =>
        {
            Startup.Set(!startup.IsChecked);
            startup.IsChecked = Startup.IsEnabled();
        };
        menu.Items.Add(startup);

        menu.Items.Add(Entry(Loc.S("Recargar", "Reload"), Reload));
        menu.Items.Add(Entry(Loc.S("Editar settings.json", "Edit settings.json"), OpenSettingsFile));
        menu.Items.Add(BuildLanguageMenu());

        menu.Items.Add(new Separator());
        menu.Items.Add(Entry(Loc.S("Salir", "Exit"), Close));

        menu.PlacementTarget = Bar;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private MenuItem BuildSizeMenu(
        string header,
        double current,
        IReadOnlyList<double> values,
        Action<double> setter,
        string suffix = "",
        string format = "0.#")
    {
        var parent = new MenuItem { Header = header };

        foreach (var value in values)
        {
            var item = new MenuItem
            {
                Header = value.ToString(format) + suffix,
                IsCheckable = true,
                IsChecked = Math.Abs(value - current) < 0.01,
            };

            var chosen = value;
            item.Click += (_, _) =>
            {
                setter(chosen);
                Normalize();
                SettingsStore.Save(_settings);
                BuildItems();
            };

            parent.Items.Add(item);
        }

        return parent;
    }

    private MenuItem BuildOpacityMenu()
    {
        var parent = new MenuItem { Header = Loc.S("Transparencia", "Opacity") };

        foreach (var value in new[] { 1.0, 0.95, 0.9, 0.8, 0.7, 0.6, 0.5 })
        {
            var item = new MenuItem
            {
                Header = value.ToString("0%"),
                IsCheckable = true,
                IsChecked = Math.Abs(value - _settings.BarOpacity) < 0.011,
            };

            var chosen = value;
            item.Click += (_, _) =>
            {
                _settings.BarOpacity = chosen;
                Normalize();
                SettingsStore.Save(_settings);
                ApplyTheme(_theme, force: true);
            };

            parent.Items.Add(item);
        }

        return parent;
    }

    private MenuItem BuildAlignmentMenu()
    {
        var parent = new MenuItem { Header = Loc.S("Alineacion de iconos", "Icon alignment") };

        var center = new MenuItem
        {
            Header = Loc.S("Centrados", "Centered"),
            IsCheckable = true,
            IsChecked = !IsLeftAligned,
        };

        var left = new MenuItem
        {
            Header = Loc.S("Izquierda", "Left"),
            IsCheckable = true,
            IsChecked = IsLeftAligned,
        };

        center.Click += (_, _) => SetIconAlignment("center");
        left.Click += (_, _) => SetIconAlignment("left");

        parent.Items.Add(center);
        parent.Items.Add(left);
        return parent;
    }

    private void SetIconAlignment(string alignment)
    {
        if (string.Equals(_settings.IconAlignment, alignment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.IconAlignment = alignment;
        SettingsStore.Save(_settings);

        ApplyMetrics();
        BuildItems();
    }

    private MenuItem BuildPositionMenu()
    {        var parent = new MenuItem { Header = Loc.S("Posicion de la barra", "Bar position") };

        var top = new MenuItem
        {
            Header = Loc.S("Arriba", "Top"),
            IsCheckable = true,
            IsChecked = !_settings.BarAtBottom,
        };

        var bottom = new MenuItem
        {
            Header = Loc.S("Abajo", "Bottom"),
            IsCheckable = true,
            IsChecked = _settings.BarAtBottom,
        };

        top.Click += (_, _) => SetBarPosition(false);
        bottom.Click += (_, _) => SetBarPosition(true);

        parent.Items.Add(top);
        parent.Items.Add(bottom);
        return parent;
    }

    private void SetBarPosition(bool bottom)
    {
        if (_settings.BarAtBottom == bottom)
        {
            return;
        }

        _settings.BarAtBottom = bottom;
        SettingsStore.Save(_settings);

        ApplyMetrics();
        BuildItems();
    }

    private MenuItem BuildLanguageMenu()
    {
        var parent = new MenuItem { Header = Loc.S("Idioma", "Language") };

        var spanish = new MenuItem
        {
            Header = "Español",
            IsCheckable = true,
            IsChecked = !Loc.English,
        };

        var english = new MenuItem
        {
            Header = "English",
            IsCheckable = true,
            IsChecked = Loc.English,
        };

        spanish.Click += (_, _) => SetLanguage("es");
        english.Click += (_, _) => SetLanguage("en");

        parent.Items.Add(spanish);
        parent.Items.Add(english);
        return parent;
    }

    private void SetLanguage(string language)
    {
        Loc.Set(language);
        _settings.Language = Loc.Language;
        SettingsStore.Save(_settings);

        // Refrescar tooltips y textos ya dibujados.
        BuildItems();
        BuildClocks();
        BuildStartButton();
        BuildTray(force: true);
    }

    private void TrayPanel_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Entry(Loc.S("Centro de notificaciones", "Notification center"), () => OpenUri("ms-actioncenter:")));
        menu.Items.Add(Entry(Loc.S("Configuracion de notificaciones", "Notification settings"), () => OpenUri("ms-settings:notifications")));
        menu.Items.Add(new Separator());
        menu.Items.Add(Entry(Loc.S("Configuracion de la barra de tareas", "Taskbar settings"), () => OpenUri("ms-settings:taskbar")));
        menu.PlacementTarget = TrayPanel;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void StatusPanel_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Entry(Loc.S("Red e Internet", "Network & Internet"), () => OpenUri("ms-settings:network")));
        menu.Items.Add(Entry(Loc.S("Sonido", "Sound"), () => OpenUri("ms-settings:sound")));
        menu.Items.Add(Entry(Loc.S("Energia y bateria", "Power & battery"), () => OpenUri("ms-settings:batterysaver")));
        menu.PlacementTarget = StatusPanel;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ClockPanel_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Entry(Loc.S("Relojes y fecha...", "Clocks and date..."), ChooseClocks));
        menu.PlacementTarget = ClockPanel;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ShowItemMenu(ItemView view)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Entry(Loc.S("Abrir", "Open"), () => Launch(view)));
        menu.Items.Add(Entry(Loc.S("Mostrar en el Explorador", "Show in Explorer"), () => ShellHelper.RevealInExplorer(view.Path)));

        if (!view.RunningOnly)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Entry(Loc.S("Quitar de la barra", "Remove from bar"), () => RemoveItem(view)));
        }

        menu.PlacementTarget = view.Container;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static MenuItem Entry(string header, Action? action)
    {
        var item = new MenuItem { Header = header };
        if (action is not null)
        {
            item.Click += (_, _) => action();
        }

        return item;
    }

    private static void OpenSettingsFile()
    {
        SettingsStore.Save(SettingsStore.Load());
        Process.Start(new ProcessStartInfo
        {
            FileName = SettingsStore.FilePath,
            UseShellExecute = true,
        });
    }

    // ---------------------------------------------------------------- drag & drop

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        foreach (var file in files)
        {
            AddItemPath(file);
        }

        e.Handled = true;
    }

    // ---------------------------------------------------------------- animacion

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        _cursor = e.GetPosition(Root);
        HoverBand.IsHitTestVisible = true;
        UpdateTargets(_cursor.Value.X);
        EnsureLoop();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        _cursor = null;
        HoverBand.IsHitTestVisible = false;
        ScheduleHoverClose();

        foreach (var view in _views)
        {
            view.Target = 1.0;
        }

        EnsureLoop();
    }

    private void UpdateTargets(double cursorX)
    {
        if (_views.Count == 0)
        {
            return;
        }

        var first = RestCenter(0);
        var last = RestCenter(_views.Count - 1);
        var margin = _settings.IconSize * 0.7;

        if (cursorX < first - (_settings.IconSize / 2.0) - margin ||
            cursorX > last + (_settings.IconSize / 2.0) + margin)
        {
            foreach (var view in _views)
            {
                view.Target = 1.0;
            }

            return;
        }

        var sigma = _settings.IconSize * 0.95;
        var amplitude = _settings.MaxScale - 1.0;
        var denominator = 2.0 * sigma * sigma;

        for (var i = 0; i < _views.Count; i++)
        {
            var delta = cursorX - RestCenter(i);
            _views[i].Target = 1.0 + (amplitude * Math.Exp(-(delta * delta) / denominator));
        }
    }

    /// <summary>Centro en reposo calculado a partir del layout, sin realimentacion del desplazamiento.</summary>
    private double RestCenter(int index)
    {
        var itemWidth = _settings.IconSize + _settings.Spacing;
        var stackWidth = _views.Count * itemWidth;
        var left = IsLeftAligned
            ? IconsLeftInset()
            : (Root.ActualWidth - stackWidth) / 2.0;

        return left + (_settings.Spacing / 2.0) + (_settings.IconSize / 2.0) + (index * itemWidth);
    }

    /// <summary>
    /// Referencia centrada para calcular el desplazamiento de la ampliacion. Aunque los
    /// iconos esten alineados a la izquierda, la expansion se calcula como si estuvieran
    /// centrados para que el icono bajo el cursor no se mueva.
    /// </summary>
    private double CenteredRestCenter(int index)
    {
        var itemWidth = _settings.IconSize + _settings.Spacing;
        var stackWidth = _views.Count * itemWidth;
        var left = (Root.ActualWidth - stackWidth) / 2.0;

        return left + (_settings.Spacing / 2.0) + (_settings.IconSize / 2.0) + (index * itemWidth);
    }

    private bool IsLeftAligned =>
        string.Equals(_settings.IconAlignment, "left", StringComparison.OrdinalIgnoreCase);

    /// <summary>Espacio a la izquierda de los iconos cuando van junto al boton de Inicio.</summary>
    private double IconsLeftInset()
    {
        var inset = 12.0 + 28.0; // margen del panel + boton de Inicio

        if (_settings.ShowTaskView)
        {
            inset += 2.0 + 28.0;
        }

        return inset + 10.0;
    }

    private void EnsureLoop()
    {
        if (_loopHooked)
        {
            return;
        }

        _loopHooked = true;
        _lastFrame = 0.0;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopLoop()
    {
        if (!_loopHooked)
        {
            return;
        }

        _loopHooked = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        var dt = _lastFrame <= 0.0 ? 1.0 / 60.0 : Math.Min(0.05, now - _lastFrame);
        _lastFrame = now;

        var animating = UpdateScales(dt);

        if (_views.Count > 0)
        {
            var total = 0.0;
            foreach (var view in _views)
            {
                total += _settings.IconSize * view.Current;
            }

            total += _settings.Spacing * (_views.Count - 1);

            // La expansion se calcula siempre centrada: asi el icono bajo el cursor
            // se queda en su sitio aunque la barra este alineada a la izquierda.
            var left = (Root.ActualWidth / 2.0) - (total / 2.0);

            for (var i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                var width = _settings.IconSize * view.Current;
                var targetCenter = left + (width / 2.0);
                left += width + _settings.Spacing;

                // Mover el contenedor entero: el icono y su zona clicable.
                view.ContainerTranslate.X = targetCenter - CenteredRestCenter(i);
            }
        }

        if (!animating)
        {
            StopLoop();
        }
    }

    private bool UpdateScales(double dt)
    {
        var animating = false;
        var smoothing = 1.0 - Math.Exp(-dt * 20.0);
        var direction = _settings.BarAtBottom ? -1.0 : 1.0;

        foreach (var view in _views)
        {
            view.Current += (view.Target - view.Current) * smoothing;

            if (Math.Abs(view.Target - view.Current) < 0.001)
            {
                view.Current = view.Target;
            }
            else
            {
                animating = true;
            }

            double bounce = 0.0;
            if (view.BounceTime >= 0.0)
            {
                view.BounceTime += dt;
                if (view.BounceTime >= BounceSeconds)
                {
                    view.BounceTime = -1.0;
                }
                else
                {
                    var progress = view.BounceTime / BounceSeconds;
                    bounce = Math.Abs(Math.Sin(progress * Math.PI * 3.0))
                             * (_settings.IconSize * 0.3)
                             * (1.0 - (progress * progress));
                    animating = true;
                }
            }

            view.Scale.ScaleX = view.Current;
            view.Scale.ScaleY = view.Current;
            view.Translate.Y = bounce * direction;

            // El indicador crece y se separa con el borde del icono (abajo o arriba).
            view.DotScale.ScaleX = view.Current;
            view.DotScale.ScaleY = view.Current;
            view.DotTranslate.Y = (direction * (view.Current - 1.0) * _settings.IconSize) + (bounce * direction);
            view.Dot.Opacity = view.IsRunning ? 1.0 : 0.0;
        }

        return animating;
    }

    // ---------------------------------------------------------------- interop

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        var exStyle = Native.GetWindowLong(_handle, Native.GWL_EXSTYLE);
        Native.SetWindowLong(_handle, Native.GWL_EXSTYLE, exStyle | Native.WS_EX_TOOLWINDOW);
    }

    // ---------------------------------------------------------------- modelo de vista

    private sealed class ItemView
    {
        public BarItemSettings? Model { get; init; }

        public string Name { get; init; } = "";

        public string Path { get; init; } = "";

        public string? Aumid { get; init; }

        public string ProcessName { get; init; } = "";

        public IntPtr Handle { get; set; }

        public bool RunningOnly { get; init; }

        public bool IsRunning { get; set; }

        public Grid Container { get; set; } = null!;

        public FrameworkElement Holder { get; set; } = null!;

        public ScaleTransform Scale { get; set; } = null!;

        public TranslateTransform Translate { get; set; } = null!;

        public TranslateTransform ContainerTranslate { get; set; } = null!;

        public Border Dot { get; set; } = null!;

        public ScaleTransform DotScale { get; set; } = null!;

        public TranslateTransform DotTranslate { get; set; } = null!;

        public double Current = 1.0;

        public double Target = 1.0;

        public double BounceTime = -1.0;
    }

    private sealed class ClockView
    {
        public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Utc;

        public TextBlock Time { get; init; } = null!;
    }
}


