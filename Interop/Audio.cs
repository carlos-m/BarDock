using System.Runtime.InteropServices;

namespace BarDock.Interop;

/// <summary>Volumen maestro del dispositivo de salida por defecto (Core Audio).</summary>
internal static class Audio
{
    private static IAudioEndpointVolume? _volume;

    public static (bool Available, bool Muted, int Percent) Read()
    {
        try
        {
            var volume = _volume ??= Create();
            if (volume is null)
            {
                return (false, false, 0);
            }

            if (volume.GetMute(out var muted) != 0 || volume.GetMasterVolumeLevelScalar(out var level) != 0)
            {
                _volume = null;
                return (false, false, 0);
            }

            return (true, muted, (int)Math.Round(level * 100.0));
        }
        catch
        {
            _volume = null;
            return (false, false, 0);
        }
    }

    public static bool TrySetVolume(int percent)
    {
        try
        {
            var volume = _volume ??= Create();
            if (volume is null)
            {
                return false;
            }

            var context = Guid.Empty;
            return volume.SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, ref context) == 0;
        }
        catch
        {
            _volume = null;
            return false;
        }
    }

    public static bool TryToggleMute()
    {
        try
        {
            var volume = _volume ??= Create();
            if (volume is null)
            {
                return false;
            }

            if (volume.GetMute(out var muted) != 0)
            {
                return false;
            }

            var context = Guid.Empty;
            return volume.SetMute(!muted, ref context) == 0;
        }
        catch
        {
            _volume = null;
            return false;
        }
    }

    private static IAudioEndpointVolume? Create()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 0 /* eConsole */, out var device) != 0)
            {
                return null;
            }

            var iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var obj) != 0)
            {
                return null;
            }

            return obj as IAudioEndpointVolume;
        }
        catch
        {
            return null;
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid iid,
            uint clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int GetChannelCount(out uint count);

        [PreserveSig]
        int SetMasterVolumeLevel(float level, ref Guid eventContext);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolumeLevel(out float level);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float level, ref Guid eventContext);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float level);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
