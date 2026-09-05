using System.Net;
using VRC.OSCQuery;
using VRCWakeMe.Core;

namespace VRCWakeMe.App.Osc;

internal sealed class OscQueryHost : IDisposable
{
    public const string ServiceName = "VRCWakeMe";

    private OSCQueryService? _service;

    public int TcpPort { get; private set; }
    public int UdpPort { get; private set; }

    public void Start(int udpPort)
    {
        TcpPort = Extensions.GetAvailableTcpPort();
        UdpPort = udpPort;

        _service = new OSCQueryServiceBuilder()
            .WithTcpPort(TcpPort)
            .WithUdpPort(udpPort)
            .WithServiceName(ServiceName)
            .WithDefaults()
            .Build();

        _service.AddEndpoint(
            "/avatar",
            "",
            Attributes.AccessValues.NoValue,
            description: "VRChat avatar OSC");
        _service.AddEndpoint(
            "/avatar/change",
            "s",
            Attributes.AccessValues.WriteOnly,
            description: "Avatar id changes");
        _service.AddEndpoint<bool>(
            OscAddresses.Grabbed,
            Attributes.AccessValues.WriteOnly,
            new object[] { false },
            "Wake grab from avatar");
    }

    public bool IsVrChatAdvertised()
    {
        if (_service is null)
        {
            return false;
        }

        try
        {
            _service.RefreshServices();
            return NamedVrchat(_service.GetOSCQueryServices()) || NamedVrchat(_service.GetOSCServices());
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool NamedVrchat(System.Collections.IEnumerable profiles)
    {
        foreach (var profile in profiles)
        {
            var name = profile.GetType().GetProperty("name")?.GetValue(profile) as string
                       ?? profile.GetType().GetProperty("Name")?.GetValue(profile) as string
                       ?? "";
            if (name.Contains("VRChat", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _service?.Dispose();
        _service = null;
    }
}
