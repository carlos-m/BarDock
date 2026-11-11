using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BarDock.Interop;

namespace BarDock.Services;

/// <summary>Extrae iconos del shell con la mayor calidad disponible (hasta 256x256).</summary>
public static class IconExtractor
{
    private static readonly Dictionary<string, ImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string path, string? aumid = null)
    {
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(aumid))
        {
            return null;
        }

        var key = string.IsNullOrWhiteSpace(aumid) ? path : $"{path}|{aumid}";

        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        ImageSource? icon = null;

        try
        {
            // Las apps empaquetadas exponen su icono real en shell:AppsFolder\<AUMID>,
            // aunque el .exe de WindowsApps solo tenga el icono generico.
            if (!string.IsNullOrWhiteSpace(aumid))
            {
                var shellPath = PackagedApps.ShellPath(aumid);
                icon = FromShellItemImageFactory(shellPath) ?? FromShellIcon(shellPath);
            }

            icon ??= FromPrivateExtractIcons(path, 256)
                     ?? FromShellItemImageFactory(path)
                     ?? FromShellIcon(path);
        }
        catch
        {
            icon = null;
        }

        if (icon is not null)
        {
            icon.Freeze();
        }

        Cache[key] = icon;
        return icon;
    }

    public static void ClearCache() => Cache.Clear();

    /// <summary>Extrae el icono directamente del recurso (funciona con .exe y resuelve .lnk).</summary>
    private static ImageSource? FromPrivateExtractIcons(string path, int size)
    {
        var handles = new IntPtr[1];
        var ids = new uint[1];

        var count = Native.PrivateExtractIcons(
            path,
            0,
            size,
            size,
            handles,
            ids,
            1,
            0);

        if (count == 0 || handles[0] == IntPtr.Zero)
        {
            return null;
        }

        var handle = handles[0];

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }

    /// <summary>Camino moderno: IShellItemImageFactory devuelve el icono/thumbnail en 256px si existe.</summary>
    private static ImageSource? FromShellItemImageFactory(string path)
    {
        Native.IShellItemImageFactory? factory = null;

        try
        {
            Native.SHCreateItemFromParsingName(
                path,
                IntPtr.Zero,
                typeof(Native.IShellItemImageFactory).GUID,
                out factory);

            if (factory is null)
            {
                return null;
            }

            var size = new Native.SIZE(256, 256);
            var flags = Native.SIIGBF.BIGGERSIZEOK | Native.SIIGBF.ICONONLY;

            if (factory.GetImage(size, flags, out var hBitmap) != 0 || hBitmap == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                Native.DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (factory is not null)
            {
                try
                {
                    Marshal.ReleaseComObject(factory);
                }
                catch
                {
                    // ignorado
                }
            }
        }
    }

    /// <summary>Fallback clasico: SHGetFileInfo + HICON de 32px.</summary>
    private static ImageSource? FromShellIcon(string path)
    {
        var info = new Native.SHFILEINFO();
        var result = Native.SHGetFileInfo(
            path,
            0,
            ref info,
            (uint)Marshal.SizeOf<Native.SHFILEINFO>(),
            Native.SHGFI_ICON | Native.SHGFI_LARGEICON);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            Native.DestroyIcon(info.hIcon);
        }
    }

    /// <summary>Inicial para el icono de reserva cuando no se puede extraer nada.</summary>
    public static string InitialFor(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length == 0 ? "?" : trimmed[..1].ToUpperInvariant();
    }
}
