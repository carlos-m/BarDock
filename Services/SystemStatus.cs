using System.Net.NetworkInformation;
using BarDock.Interop;

namespace BarDock.Services;

public readonly record struct SystemStatusSnapshot(
    bool HasBattery,
    int BatteryPercent,
    bool Charging,
    bool NetworkAvailable,
    bool Wireless,
    string NetworkName,
    bool VolumeAvailable,
    bool Muted,
    int VolumePercent);

/// <summary>Estado de bateria, red y volumen del sistema.</summary>
public static class SystemStatus
{
    public static SystemStatusSnapshot Read()
    {
        var (hasBattery, percent, charging) = ReadBattery();
        var (available, wireless, name) = ReadNetwork();
        var (volumeAvailable, muted, volume) = Audio.Read();

        return new SystemStatusSnapshot(
            hasBattery,
            percent,
            charging,
            available,
            wireless,
            name,
            volumeAvailable,
            muted,
            volume);
    }

    private static (bool HasBattery, int Percent, bool Charging) ReadBattery()
    {
        try
        {
            if (!Native.GetSystemPowerStatus(out var status))
            {
                return (false, 0, false);
            }

            // 128 = sin bateria, 255 = desconocido.
            if (status.BatteryFlag is 128 or 255 || status.BatteryLifePercent == 255)
            {
                return (false, 0, false);
            }

            return (true, status.BatteryLifePercent, status.ACLineStatus == 1);
        }
        catch
        {
            return (false, 0, false);
        }
    }

    private static (bool Available, bool Wireless, string Name) ReadNetwork()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return (false, false, "");
            }

            var active = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                              and not NetworkInterfaceType.Tunnel)
                .ToList();

            if (active.Count == 0)
            {
                return (false, false, "");
            }

            var wireless = active.FirstOrDefault(nic =>
                nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            if (wireless is not null)
            {
                return (true, true, wireless.Name);
            }

            var wired = active.FirstOrDefault(nic =>
                nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet);

            return (true, false, (wired ?? active[0]).Name);
        }
        catch
        {
            return (false, false, "");
        }
    }
}
