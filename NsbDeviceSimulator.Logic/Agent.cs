using NsbDeviceSimulator.Type;

namespace NsbDeviceSimulator.Logic;

public class Agent
{
    private readonly string _host;
    private readonly int _port;
    private readonly DeviceType _type;
    private readonly string _sn;
    private readonly CancellationToken _cancellationToken;
    private readonly Connection _connection;
    private readonly StatusManager _statusManager;
    private readonly AudioManager _audioManager;
    private readonly TaskManager _taskManager;

    public Agent(string host, int port, DeviceType type, string sn, CancellationToken cancellationToken)
    {
        _host = host;
        _port = port;
        _type = type;
        _sn = sn;

        _cancellationToken = cancellationToken;
        _audioManager = new AudioManager(sn, cancellationToken);
        _taskManager = new TaskManager(sn, _audioManager);
        _connection = new Connection(host, port, _sn, _type, cancellationToken, _audioManager, _taskManager);
        _statusManager = new StatusManager(_connection, cancellationToken);
    }

    public void Play(int index)
    {
        _audioManager.Play(index);
    }

    public void Pause()
    {
        _audioManager.Pause();
    }

    public void Stop()
    {
        _audioManager.Stop();
    }

    public void Next()
    {
        _audioManager.Next();
    }

    public void Previous()
    {
        _audioManager.Previous();
    }

    public void AddAudio()
    {
        _audioManager.AddAudio("12345678");
    }

    public void DeleteAudio(int index)
    {
        _audioManager.DeleteAudio(index);
    }
}