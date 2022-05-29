namespace NsbDeviceSimulator.Logic;

public class StatusManager
{
    private readonly CancellationToken _cancellationToken;
    private readonly Connection _connection;

    private Status Status { get; set; }

    public StatusManager(Connection connection, CancellationToken cancellationToken)
    {
        Status = Status.Idle;
        _cancellationToken = cancellationToken;
        _connection = connection;
        Task.Run(Manage, cancellationToken);
    }

    private void Manage()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            switch (Status)
            {
                case Status.Idle:
                    if (_connection.Start(reason => {  Status = Status.Error; Console.WriteLine(reason); }))
                        Status = Status.Connected;
                    break;
                case Status.Connected:
                    var loginLock = new Semaphore(0, 1);
                    // ReSharper disable once AccessToModifiedClosure
                    _connection.Login(_ =>
                    {
                        loginLock.Release();
                        Status = Status.Logged;
                    });
                    loginLock.WaitOne(10 * 1000);
                    break;
                case Status.Logged:
                    break;
                case Status.Error:
                    _connection.Stop();
                    Status = Status.Idle;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}

public enum Status
{
    Idle,
    Connected,
    Logged,
    Error
}