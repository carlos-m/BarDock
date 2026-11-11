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
    /// <summary>Enumerar los adaptadores cuesta ~25 ms: no se hace en cada consulta.</summary>
    private static readonly TimeSpan NetworkStaleAfter = TimeSpan.FromMinutes(10);

    private static readonly object Gate = new();

    private static NetworkInfo _network = new(false, false, "");
    private static DateTime _networkReadUtc = DateTime.MinValue;
    private static bool _hasNetwork;
    private static bool _refreshingNetwork;

    static SystemStatus()
    {
        try
        {
            NetworkChange.NetworkAvailabilityChanged += (_, _) => InvalidateNetwork();
            NetworkChange.NetworkAddressChanged += (_, _) => InvalidateNetwork();
        }
        catch
        {
            // sin eventos se refresca por antiguedad
        }
    }

    public static SystemStatusSnapshot Read()
    {
        var (hasBattery, percent, charging) = ReadBattery();
        var network = NetworkCached();

        var (volumeAvailable, muted, volume) = Audio.Read();

        return new SystemStatusSnapshot(
            hasBattery,
            percent,
            charging,
            network.Available,
            network.Wireless,
            network.Name,
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

    /// <summary>
    /// Ultimo estado de red conocido. La primera lectura es sincrona; despues solo se vuelve a
    /// enumerar en segundo plano cuando cambia la red o pasa el tiempo de validez.
    /// </summary>
    private static NetworkInfo NetworkCached()
    {
        lock (Gate)
        {
            if (_hasNetwork)
            {
                if (DateTime.UtcNow - _networkReadUtc > NetworkStaleAfter && !_refreshingNetwork)
                {
                    _refreshingNetwork = true;
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        var fresh = ReadNetwork();
                        lock (Gate)
                        {
                            _network = fresh;
                            _networkReadUtc = DateTime.UtcNow;
                            _hasNetwork = true;
                            _refreshingNetwork = false;
                        }
                    });
                }

                return _network;
            }
        }

        // Primer uso (arranque, antes de dibujar la barra): una sola enumeracion.
        var first = ReadNetwork();
        lock (Gate)
        {
            _network = first;
            _networkReadUtc = DateTime.UtcNow;
            _hasNetwork = true;
        }

        return first;
    }

    private static void InvalidateNetwork()
    {
        lock (Gate)
        {
            // Marca obsoleto: el proximo Tick lo refresca en segundo plano.
            _networkReadUtc = DateTime.MinValue;
        }
    }

    private static NetworkInfo ReadNetwork()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return new NetworkInfo(false, false, "");
            }

            var active = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                              and not NetworkInterfaceType.Tunnel)
                .ToList();

            if (active.Count == 0)
            {
                return new NetworkInfo(false, false, "");
            }

            var wireless = active.FirstOrDefault(nic =>
                nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            if (wireless is not null)
            {
                return new NetworkInfo(true, true, wireless.Name);
            }

            var wired = active.FirstOrDefault(nic =>
                nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet);

            return new NetworkInfo(true, false, (wired ?? active[0]).Name);
        }
        catch
        {
            return new NetworkInfo(false, false, "");
        }
    }

    private readonly record struct NetworkInfo(bool Available, bool Wireless, string Name);
}
