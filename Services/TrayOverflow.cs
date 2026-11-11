using System.Threading;
using System.Windows.Automation;
using BarDock.Interop;

namespace BarDock.Services;

/// <summary>
/// Abre el panel nativo de iconos ocultos de Windows (el mismo que el chevron de la
/// bandeja). Asi los iconos y sus menus contextuales son los reales.
/// </summary>
public static class TrayOverflow
{
    private const string OverflowClass = "TopLevelWindowForOverflowXamlIsland";

    public static bool IsVisible()
    {
        var handle = Native.FindWindow(OverflowClass, null);
        return handle != IntPtr.Zero && Native.IsWindowVisible(handle);
    }

    private static int _busy;

    /// <summary>Igual que Show, pero sin bloquear el hilo de la interfaz.</summary>
    public static void ShowAsync()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                Show();
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        });
    }

    public static bool Show()
    {
        try
        {
            if (IsVisible())
            {
                return true;
            }

            var taskbar = Native.FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero || !InvokeChevron(taskbar))
            {
                return false;
            }

            Thread.Sleep(500);
            return IsVisible();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>El chevron es el primer boton de la bandeja (el mas a la izquierda).</summary>
    private static bool InvokeChevron(IntPtr taskbar)
    {
        var root = AutomationElement.FromHandle(taskbar);
        var condition = new PropertyCondition(
            AutomationElement.AutomationIdProperty,
            "SystemTrayIcon");

        var candidates = root.FindAll(TreeScope.Descendants, condition);
        AutomationElement? chevron = null;
        var left = double.MaxValue;

        for (var i = 0; i < candidates.Count; i++)
        {
            var element = candidates[i];
            double x;

            try
            {
                x = element.Current.BoundingRectangle.X;
            }
            catch
            {
                continue;
            }

            if (x >= 0 && x < left)
            {
                left = x;
                chevron = element;
            }
        }

        if (chevron is null)
        {
            return false;
        }

        try
        {
            var pattern = (InvokePattern)chevron.GetCurrentPattern(InvokePattern.Pattern);
            pattern.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
