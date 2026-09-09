using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using MeaMod.DNS.Multicast;

namespace VRCWakeMe;

/// <summary>
/// OSCQuery + UDP modeled on vr_asmr_petting: advertise a LAN IP over mDNS,
/// answer VRChat's / and /?HOST_INFO probes, and listen for /avatar OSC.
/// </summary>
internal sealed class OscLink : IDisposable
{
    public const string ServiceName = "VRCWakeMe";

    private const string OscJsonService = "_oscjson._tcp";
    private const int VrcOscInPort = 9000;
    private const int VrcOscOutPort = 9001;
    private const int LinkTimeoutMs = 5000;
    private const string RootJson = """{"CONTENTS":{"avatar":{"FULL_PATH":"/avatar"},"tracking":{"FULL_PATH":"/tracking"}}}""";

    private readonly UdpClient _udp;
    private readonly UdpClient _toVrc = new();
    private readonly UdpClient? _fallback;
    private readonly TcpListener _http;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _tasks = [];
    private readonly object _gate = new();
    private readonly MulticastService _mdns;
    private readonly ServiceDiscovery _discovery;
    private readonly ServiceProfile _profile;
    private readonly int _oscPort;

    private bool _wasLinked;
    private bool _primedDebug;
    private long _lastRootMs = -10_000;
    private long _lastHostInfoMs = -10_000;
    private long _lastAnnounceMs;
    private long _lastActivityMs;

    private OscLink(
        UdpClient udp,
        UdpClient? fallback,
        TcpListener http,
        int oscPort,
        MulticastService mdns,
        ServiceDiscovery discovery,
        ServiceProfile profile)
    {
        _udp = udp;
        _fallback = fallback;
        _http = http;
        _oscPort = oscPort;
        _mdns = mdns;
        _discovery = discovery;
        _profile = profile;
    }

    public bool IsLinked
    {
        get
        {
            lock (_gate)
            {
                return _lastActivityMs != 0 && Environment.TickCount64 - _lastActivityMs < LinkTimeoutMs;
            }
        }
    }

    public event Action<OscMessage>? MessageReceived;
    public event Action? LinkChanged;

    public static OscLink Start()
    {
        var lanIp = GuessLanIp();
        var udp = Listen(0);
        var oscPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        var fallback = TryListen(VrcOscOutPort);
        var http = new TcpListener(IPAddress.Any, 0);
        http.Start();
        var httpPort = ((IPEndPoint)http.LocalEndpoint).Port;

        var profile = new ServiceProfile(ServiceName, OscJsonService, (ushort)httpPort, [lanIp]);
        var mdns = new MulticastService { UseIpv6 = false, IgnoreDuplicateMessages = true };
        mdns.Start();
        var discovery = new ServiceDiscovery(mdns);
        discovery.Advertise(profile);
        TryAnnounce(discovery, profile);

        var link = new OscLink(udp, fallback, http, oscPort, mdns, discovery, profile);
        link._lastAnnounceMs = Environment.TickCount64;
        link._tasks.Add(Task.Run(() => link.ReceiveLoopAsync(udp, link._cts.Token)));
        if (fallback != null) link._tasks.Add(Task.Run(() => link.ReceiveLoopAsync(fallback, link._cts.Token)));
        link._tasks.Add(Task.Run(() => link.HttpLoopAsync(link._cts.Token)));
        return link;
    }

    public void Tick()
    {
        var linked = IsLinked;
        if (linked != _wasLinked)
        {
            _wasLinked = linked;
            if (!linked) _primedDebug = false;
            LinkChanged?.Invoke();
        }

        if (linked || Environment.TickCount64 - _lastAnnounceMs < 5000) return;
        _lastAnnounceMs = Environment.TickCount64;
        TryAnnounce(_discovery, _profile);
    }

    /// <summary>
    /// One address per state so VRChat's OSC debug console lists a single tile each,
    /// created in a fixed order. Only the address that is currently true keeps ticking.
    /// </summary>
    public void SendDebug(bool armed, bool grabbed, bool pulled)
    {
        try
        {
            if (!_primedDebug)
            {
                _primedDebug = true;
                Send(OscAddresses.DebugArmed, true);
                Send(OscAddresses.DebugDisarmed, true);
                Send(OscAddresses.DebugGrabbed, true);
                Send(OscAddresses.DebugPulled, true);
            }

            Send(armed ? OscAddresses.DebugArmed : OscAddresses.DebugDisarmed, true);
            if (grabbed) Send(OscAddresses.DebugGrabbed, true);
            if (pulled) Send(OscAddresses.DebugPulled, true);
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _http.Stop(); } catch (Exception) { }
        _udp.Dispose();
        _fallback?.Dispose();
        _toVrc.Dispose();
        try { _discovery.Unadvertise(_profile); } catch (Exception) { }
        _discovery.Dispose();
        _mdns.Dispose();
        try { Task.WaitAll([.. _tasks], 500); } catch (Exception) { }
        _cts.Dispose();
    }

    private async Task ReceiveLoopAsync(UdpClient udp, CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await udp.ReceiveAsync(cancel); }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { return; }
            catch (SocketException) { continue; }

            IReadOnlyList<OscMessage> messages;
            try { messages = OscPacketParser.Parse(result.Buffer); }
            catch (Exception) { continue; }

            if (messages.Count == 0) continue;
            NoteActivity();
            foreach (var message in messages) MessageReceived?.Invoke(message);
        }
    }

    private async Task HttpLoopAsync(CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _http.AcceptTcpClientAsync(cancel); }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException) { return; }

            _ = Task.Run(() => HandleHttp(client), cancel);
        }
    }

    private void HandleHttp(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            stream.ReadTimeout = 3000;
            stream.WriteTimeout = 3000;
            var buffer = new byte[4096];
            int read;
            try { read = stream.Read(buffer, 0, buffer.Length); }
            catch (Exception) { return; }
            if (read <= 0) return;

            var request = Encoding.ASCII.GetString(buffer, 0, read);
            var firstLine = request.Split('\n')[0];
            var tokens = firstLine.Split(' ');
            var pathQs = tokens.Length > 1 ? tokens[1].Trim() : "/";
            var hostInfo = pathQs.Contains("HOST_INFO", StringComparison.OrdinalIgnoreCase);
            var root = pathQs == "/" || pathQs.StartsWith("/?", StringComparison.Ordinal);
            var now = Environment.TickCount64;

            if (hostInfo || root)
            {
                lock (_gate)
                {
                    ref var last = ref hostInfo ? ref _lastHostInfoMs : ref _lastRootMs;
                    if (now - last < 1000)
                    {
                        WriteHttp(stream, 404, "text/plain", "Too many requests, please wait a moment.");
                        return;
                    }

                    last = now;
                }

                NoteActivity();
                WriteHttp(
                    stream,
                    200,
                    "application/json",
                    hostInfo
                        ? $"{{\"NAME\":\"{ServiceName}\",\"OSC_IP\":\"127.0.0.1\",\"OSC_PORT\":{_oscPort},\"OSC_TRANSPORT\":\"UDP\"}}"
                        : RootJson);
                return;
            }

            WriteHttp(stream, 200, "application/json", $"{{\"FULL_PATH\":\"{pathQs}\"}}");
        }
    }

    private void NoteActivity()
    {
        var was = IsLinked;
        lock (_gate) _lastActivityMs = Environment.TickCount64;
        if (IsLinked != was) LinkChanged?.Invoke();
    }

    private void Send(string address, params object[] args)
    {
        var packet = OscWriter.Write(address, args);
        _toVrc.Send(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, VrcOscInPort));
    }

    private static void WriteHttp(NetworkStream stream, int status, string contentType, string body)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Not Found")}\r\n" +
            $"Content-Type: {contentType}\r\nConnection: close\r\nContent-Length: {payload.Length}\r\n\r\n");
        try
        {
            stream.Write(header);
            stream.Write(payload);
        }
        catch (Exception)
        {
        }
    }

    private static UdpClient Listen(int port)
    {
        var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        udp.Client.ReceiveBufferSize = 1024 * 1024;
        return udp;
    }

    private static UdpClient? TryListen(int port)
    {
        try { return Listen(port); }
        catch (SocketException) { return null; }
    }

    private static void TryAnnounce(ServiceDiscovery discovery, ServiceProfile profile)
    {
        try { discovery.Announce(profile); }
        catch (Exception) { }
    }

    private static IPAddress GuessLanIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(IPAddress.Parse("1.1.1.1"), 80);
            if (socket.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address))
            {
                return ep.Address;
            }
        }
        catch (Exception)
        {
        }

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                {
                    return addr.Address;
                }
            }
        }

        return IPAddress.Loopback;
    }
}
