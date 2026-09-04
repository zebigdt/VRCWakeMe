using System.Net;
using VRC.OSCQuery;

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
            "/avatar/parameters/WakeMe/Touched",
            Attributes.AccessValues.WriteOnly,
            new object[] { false },
            "Wake contact from avatar");
    }

    public void Dispose()
    {
        _service?.Dispose();
        _service = null;
    }
}
