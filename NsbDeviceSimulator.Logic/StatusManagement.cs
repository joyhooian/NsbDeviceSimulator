namespace NsbDeviceSimulator.Logic;

public class StatusManagement
{
    private Timer? _loginTimer;
    private Timer? _heartbeatTimer;
    private static readonly TimeSpan LoginTimeout = new TimeSpan(0, 0, 0, 20);
    private static readonly TimeSpan HeartbeatTimeout = new TimeSpan(0, 0, 1, 0);
    private static readonly int MaxRetryTimes = 3;
    private readonly TimerCallback _loginCallback;
    private readonly HeartbeatTimeoutCallback _heartbeatCallback;
    private readonly CancellationToken _cancellationToken;

    private bool _isLogin;
    private int _heartbeatCnt;

    public delegate void HeartbeatTimeoutCallback();
    
    public bool IsLogin
    {
        get => _isLogin;
        set
        {
            _isLogin = value;
            if (value)
            {
                _loginTimer?.Dispose();
            }
            else
            {
                _loginTimer = new Timer(_loginCallback, null, LoginTimeout, Timeout.InfiniteTimeSpan);
            }
        }
    }

    public void Heartbeat()
    {
        _heartbeatCnt = 0;
    }

    public StatusManagement(TimerCallback loginCallback, HeartbeatTimeoutCallback heartbeatCallback, CancellationToken cancellationToken)
    {
        _loginCallback = loginCallback;
        _heartbeatCallback = heartbeatCallback;
        _cancellationToken = cancellationToken;
    }

    public void Initiate()
    {
        _loginTimer = new Timer(_loginCallback, null, LoginTimeout, Timeout.InfiniteTimeSpan);
        _heartbeatTimer = new Timer(HeartbeatTimerCb, null, LoginTimeout, HeartbeatTimeout);
    }

    private void HeartbeatTimerCb(object? o)
    {
        if (++_heartbeatCnt > MaxRetryTimes && !_cancellationToken.IsCancellationRequested)
            _heartbeatCallback();
    }
}