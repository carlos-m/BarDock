using System.Windows;
using BarDock.Interop;

namespace BarDock.Services;

/// <summary>
/// Oculta la barra de tareas de Windows (Shell_TrayWnd) y reserva el alto de BarDock
/// como area de trabajo, para que las ventanas maximizadas empiecen justo debajo.
/// </summary>
public static class WindowsTaskbar
{
    private static IntPtr _handle = IntPtr.Zero;
    private static bool _hidden;

    private static Native.RECT _savedWorkArea;
    private static bool _saved;
    private static int _reserved = -1;
    private static bool _reservedBottom;

    public static bool IsHidden => _hidden;

    /// <summary>Rectangulo de la barra de tareas de Windows (si esta visible).</summary>
    internal static Native.RECT? TaskbarRect()
    {
        var handle = Native.FindWindow("Shell_TrayWnd", null);
        if (handle == IntPtr.Zero || !Native.IsWindowVisible(handle))
        {
            return null;
        }

        return Native.GetWindowRect(handle, out var rect) ? rect : null;
    }

    public static void Hide()
    {
        _handle = Native.FindWindow("Shell_TrayWnd", null);
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        if (!_saved)
        {
            _savedWorkArea = NaturalWorkArea(_handle);
            _saved = true;
        }

        // No se oculta con ShowWindow: se deja "visible" pero con una region vacia.
        // Asi el shell sigue teniendo su arbol de UI (bandeja nativa) sin dibujarla.
        ApplyEmptyRegion(_handle);
        _hidden = true;
    }

    private static void ApplyEmptyRegion(IntPtr handle)
    {
        var region = Native.CreateRectRgn(0, 0, 0, 0);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (Native.SetWindowRgn(handle, region, true) == 0)
        {
            Native.DeleteObject(region);
        }
    }

    private static bool IsRegionEmpty(IntPtr handle)
    {
        var region = Native.CreateRectRgn(0, 0, 0, 0);
        if (region == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            // NULLREGION = 1
            return Native.GetWindowRgn(handle, region) == 1;
        }
        finally
        {
            Native.DeleteObject(region);
        }
    }

    /// <summary>
    /// Reserva el alto de la barra (en DIPs) en el borde superior o inferior del monitor.
    /// Solo actua mientras la barra de Windows esta oculta.
    /// </summary>
    public static void Reserve(double sizeDip, bool bottom)
    {
        _reserved = ToPixels(sizeDip);
        _reservedBottom = bottom;
        ApplyReservation();
    }

    /// <summary>Vuelve a ocultarla y a reservar el alto si el Explorador lo ha restaurado.</summary>
    public static bool EnsureHidden()
    {
        if (!_hidden)
        {
            return false;
        }

        if (_handle == IntPtr.Zero)
        {
            _handle = Native.FindWindow("Shell_TrayWnd", null);
            if (_handle == IntPtr.Zero)
            {
                return false;
            }
        }

        var restored = false;

        if (!Native.IsWindowVisible(_handle))
        {
            Native.ShowWindow(_handle, Native.SW_SHOW);
            restored = true;
        }

        if (!IsRegionEmpty(_handle))
        {
            ApplyEmptyRegion(_handle);
            restored = true;
        }

        ApplyReservation();
        return restored;
    }

    public static void Restore()
    {
        if (!_hidden)
        {
            return;
        }

        if (_handle == IntPtr.Zero)
        {
            _handle = Native.FindWindow("Shell_TrayWnd", null);
        }

        if (_handle != IntPtr.Zero)
        {
            Native.SetWindowRgn(_handle, IntPtr.Zero, true);
            Native.ShowWindow(_handle, Native.SW_SHOW);
        }

        _hidden = false;

        if (_saved)
        {
            SetWorkArea(_savedWorkArea);
            _saved = false;
        }

        _reserved = -1;
    }

    private static void ApplyReservation()
    {
        if (!_hidden || _reserved < 0)
        {
            return;
        }

        var width = Native.GetSystemMetrics(Native.SM_CXSCREEN);
        var height = Native.GetSystemMetrics(Native.SM_CYSCREEN);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var size = Math.Clamp(_reserved, 0, Math.Max(0, height - 40));
        var target = new Native.RECT
        {
            Left = 0,
            Top = _reservedBottom ? 0 : size,
            Right = width,
            Bottom = _reservedBottom ? height - size : height,
        };

        if (TryGetWorkArea(out var current) && Same(current, target))
        {
            return;
        }

        SetWorkArea(target);
    }

    /// <summary>
    /// Area de trabajo que Windows usaria con la barra visible, calculada a partir de
    /// la posicion real de Shell_TrayWnd (puede estar arriba, abajo o a un lado).
    /// </summary>
    private static Native.RECT NaturalWorkArea(IntPtr taskbar)
    {
        var width = Native.GetSystemMetrics(Native.SM_CXSCREEN);
        var height = Native.GetSystemMetrics(Native.SM_CYSCREEN);
        var area = new Native.RECT { Left = 0, Top = 0, Right = width, Bottom = height };

        if (Native.GetWindowRect(taskbar, out var rect))
        {
            if (rect.Top <= 0 && rect.Bottom > 0 && rect.Bottom < height / 2)
            {
                area.Top = rect.Bottom;
                return area;
            }

            if (rect.Bottom >= height && rect.Top > height / 2)
            {
                area.Bottom = rect.Top;
                return area;
            }

            if (rect.Left <= 0 && rect.Right > 0 && rect.Right < width / 2)
            {
                area.Left = rect.Right;
                return area;
            }

            if (rect.Right >= width && rect.Left > width / 2)
            {
                area.Right = rect.Left;
                return area;
            }
        }

        return TryGetWorkArea(out var current) ? current : area;
    }

    /// <summary>Se llama sin SPIF_SENDCHANGE: si se avisa al shell, este revierte el cambio.</summary>
    private static void SetWorkArea(Native.RECT area)
    {
        try
        {
            Native.SystemParametersInfo(Native.SPI_SETWORKAREA, 0, ref area, 0);
        }
        catch
        {
            // sin permiso para cambiar el area de trabajo
        }
    }

    private static bool TryGetWorkArea(out Native.RECT area)
    {
        area = default;

        try
        {
            return Native.SystemParametersInfo(Native.SPI_GETWORKAREA, 0, ref area, 0);
        }
        catch
        {
            return false;
        }
    }

    private static bool Same(Native.RECT left, Native.RECT right) =>
        left.Left == right.Left &&
        left.Top == right.Top &&
        left.Right == right.Right &&
        left.Bottom == right.Bottom;

    /// <summary>Pixeles fisicos por DIP (la barra mide en DIPs y la API en pixeles).</summary>
    private static int ToPixels(double dip)
    {
        var pixels = Native.GetSystemMetrics(Native.SM_CXSCREEN);
        var dips = SystemParameters.PrimaryScreenWidth;
        var scale = pixels > 0 && dips > 0 ? pixels / dips : 1.0;

        return Math.Max(0, (int)Math.Round(dip * scale));
    }
}
