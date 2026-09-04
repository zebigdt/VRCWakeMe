using System.Net;
using System.Net.Sockets;
using VRCWakeMe.Core;

namespace VRCWakeMe.App.Osc;

internal sealed class UdpOscReceiver : IDisposable
{
    private readonly UdpClient _udp;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public UdpOscReceiver(IPAddress address)
    {
        _udp = new UdpClient(new IPEndPoint(address, 0));
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
    }

    public int Port { get; }

    public event Action<OscMessage>? MessageReceived;

    public void Start()
    {
        if (_loop != null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _udp.Dispose();
        try
        {
            _loop?.GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        _cts?.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udp.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }

            IReadOnlyList<OscMessage> messages;
            try
            {
                messages = OscPacketParser.Parse(result.Buffer);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var message in messages)
            {
                MessageReceived?.Invoke(message);
            }
        }
    }
}
