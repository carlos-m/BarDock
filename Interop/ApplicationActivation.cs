using System.Runtime.InteropServices;

namespace BarDock.Interop;

/// <summary>Activa apps empaquetadas por su AppUserModelID.</summary>
internal static class ApplicationActivation
{
    public static bool TryActivate(string aumid)
    {
        try
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            return manager.ActivateApplication(aumid, null, 0, out _) == 0;
        }
        catch
        {
            return false;
        }
    }

    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager
    {
    }

    [ComImport]
    [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            uint options,
            out uint processId);
    }
}
