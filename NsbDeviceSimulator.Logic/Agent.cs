using NsbDeviceSimulator.Type;

namespace NsbDeviceSimulator.Logic;

public class Agent
{
    private readonly string _host;
    private readonly int _port;
    private readonly DeviceType _type;
    private readonly string _sn;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Connection _connection;

    public Agent(string host, int port, DeviceType type, string sn)
    {
        _host = host;
        _port = port;
        _type = type;
        _sn = sn;

        _cancellationTokenSource = new CancellationTokenSource();
        _connection = new Connection(host, port, _sn, _type, _cancellationTokenSource.Token);
    }

    public void Start()
    {
        _connection.Start();
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _connection.Stop();
    }
}