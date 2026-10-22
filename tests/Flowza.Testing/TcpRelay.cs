using System.Net;
using System.Net.Sockets;

namespace Flowza.Testing;

/// <summary>
/// A switchable TCP relay in front of the test SQL Server (T034): while <see cref="IsOpen"/> is false every connection
/// is refused at once, as when the database server is down; once open, connections pass through. Tests point a
/// service at <see cref="Port"/> to start it "while the database is unreachable" and then bring the database back.
/// </summary>
public sealed class TcpRelay : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<TcpClient> _connections = [];
    private readonly Task _accepting;
    private readonly string _targetHost;
    private readonly int _targetPort;

    public TcpRelay(string targetHost, int targetPort)
    {
        _targetHost = targetHost;
        _targetPort = targetPort;
        _listener.Start();
        _accepting = AcceptAsync(_stop.Token);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public bool IsOpen { get; set; }

    /// <summary>A relay in front of the test SQL Server (<c>Sql:Port</c>, default 1433, on this machine).</summary>
    public static TcpRelay ForTestSqlServer() => new("127.0.0.1", TestSqlServer.TcpPort);

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        lock (_connections)
        {
            foreach (var connection in _connections)
            {
                connection.Dispose();
            }
        }

        try
        {
            await _accepting;
        }
        catch (OperationCanceledException)
        {
            // stopped
        }

        _stop.Dispose();
    }

    private async Task AcceptAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(stop);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            if (!IsOpen)
            {
                // A reset, not a wait: the client fails at once, like a server that is not there.
                client.LingerState = new LingerOption(true, 0);
                client.Dispose();
                continue;
            }

            _ = RelayAsync(client, stop);
        }
    }

    private async Task RelayAsync(TcpClient client, CancellationToken stop)
    {
        var server = new TcpClient();
        lock (_connections)
        {
            _connections.Add(client);
            _connections.Add(server);
        }

        try
        {
            await server.ConnectAsync(_targetHost, _targetPort, stop);
            var clientStream = client.GetStream();
            var serverStream = server.GetStream();
            await Task.WhenAny(clientStream.CopyToAsync(serverStream, stop), serverStream.CopyToAsync(clientStream, stop));
        }
        catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // one side closed
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }
}
