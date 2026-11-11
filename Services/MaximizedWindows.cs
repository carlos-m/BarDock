using System.Runtime.InteropServices;
using System.Text;
using BarDock.Interop;

namespace BarDock.Services;

/// <summary>
/// Windows no recoloca las ventanas que ya estaban maximizadas cuando cambia el area de
/// trabajo, asi que al cambiar el alto de la barra queda una franja de escritorio.
/// Restaurarlas y volver a maximizarlas las ajusta al hueco real.
/// </summary>
public static class MaximizedWindows
{
    public static void Refit(IntPtr ownHandle)
    {
        if (ownHandle == IntPtr.Zero)
        {
            return;
        }

        var primary = Native.MonitorFromPoint(
            new Native.POINT { X = 0, Y = 0 },
            Native.MONITOR_DEFAULTTOPRIMARY);

        if (primary == IntPtr.Zero)
        {
            return;
        }

        var foreground = Native.GetForegroundWindow();

        Native.EnumWindows((handle, _) =>
        {
            try
            {
                Refit(handle, ownHandle, primary);
            }
            catch
            {
                // ventana que desaparece a mitad de la enumeracion
            }

            return true;
        }, IntPtr.Zero);

        // Restaurar y maximizar activa cada ventana: devolver el foco a quien lo tenia.
        if (foreground != IntPtr.Zero && Native.IsWindow(foreground))
        {
            Native.SetForegroundWindow(foreground);
        }
    }

    private static void Refit(IntPtr handle, IntPtr ownHandle, IntPtr primary)
    {
        if (handle == ownHandle ||
            !Native.IsWindowVisible(handle) ||
            Native.IsIconic(handle) ||
            !Native.IsZoomed(handle))
        {
            return;
        }

        if (Native.MonitorFromWindow(handle, Native.MONITOR_DEFAULTTONEAREST) != primary)
        {
            return;
        }

        if ((Native.GetWindowLong(handle, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0)
        {
            return;
        }

        // Ventanas de otro escritorio virtual o apps UWP suspendidas.
        if (Native.DwmGetWindowAttribute(handle, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return;
        }

        var className = new StringBuilder(64);
        Native.GetClassName(handle, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return;
        }

        Native.ShowWindow(handle, Native.SW_RESTORE);
        Native.ShowWindow(handle, Native.SW_MAXIMIZE);
    }
}
