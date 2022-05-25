using System.Collections.Concurrent;
using System.Net.Sockets;
using NsbDeviceSimulator.Type;
using NsbDeviceSimulator.Type.Model;
using NsbDeviceSimulator.Type.Model.Send;

namespace NsbDeviceSimulator.Logic;

public class Connection
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _sn;
    private readonly DeviceType _type;
    private readonly CancellationToken _cancellationToken;
    private readonly BlockingCollection<BaseMessage> _handler;
    private bool _isLogin;

    private Timer _loginTimer;
    private Timer _heartbeatTimer;

    private bool _reconnecting;
    private static int _maxRetryTimes = 3;
    private int _heartbeatCnt;
    
    private TcpClient _client;
    
    public Connection(string host, int port, string sn, DeviceType type, CancellationToken cancellationToken)
    {
        _host = host;
        _port = port;
        _sn = sn;
        _type = type;
        _cancellationToken = cancellationToken;
        _client = new TcpClient();
        _handler = new BlockingCollection<BaseMessage>();
        new Task(MessageHandle).Start();
    }

    public void Start()
    {
        Connect();
    }

    public void Stop()
    {
        if (_client?.Client == null) return;
        try
        {
            _client.GetStream().Close();
            _client.Close();
        }
        catch (Exception)
        {
            // ignored
        }

    }

    private void Reconnect()
    {
        if (_reconnecting) return;
        _reconnecting = true;
        Stop();
        if (_cancellationToken.IsCancellationRequested) return;
        Start();
    }
    
    private void Connect()
    {
        _client = new TcpClient(AddressFamily.InterNetwork);
        _client.BeginConnect(_host, _port, OnConnect, null);
    }

    #region Client Callback
    private void OnConnect(IAsyncResult ar)
    {
        _reconnecting = false;
        var loginMsg = new LoginSendMsg(_sn, _type);
        _loginTimer = new Timer(OnLoginTimeout, null, 10 * 1000, Timeout.Infinite);
        _heartbeatTimer = new Timer(OnHeartbeatTimeout, null, 10 * 1000, 60 * 1000);
        _isLogin = false;

        if (!SendMessage(loginMsg)) return;
            
        var so = new StateObject()
        {
            Data = new byte[4],
            Message = new BaseMessage()
        };
        _client.GetStream().BeginRead(so.Data, 0, 4, OnHeaderReceive, so);
    }
    
    private void OnHeaderReceive(IAsyncResult ar)
    {
        try
        {
            var so = ar.AsyncState as StateObject;

            if (!_client.Connected) return;
            var length = _client.GetStream().EndRead(ar);
            if (length == 0 || so?.Data == null || so.Message == null)
            {
                Reconnect();
            }
            else
            {
                so.Message.Header = (Header)so.Data!;
                if (so.Message.Header != null)
                {
                    so.Data = new byte[so.Message.Header.DataLength + 1];
                    _client.GetStream().BeginRead(so.Data, 0, so.Message.Header.DataLength + 1, OnBodyReceive, so);
                }
                else
                {
                    _client.GetStream().BeginRead(so.Data, 0, 4, OnHeaderReceive, so);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void OnBodyReceive(IAsyncResult ar)
    {
        try
        {
            var so = ar.AsyncState as StateObject;
            var length = _client.GetStream().EndRead(ar);
            if (length == 0 || so?.Data == null || so.Message == null)
            {
                Reconnect();
            }
            else
            {
                so.Message.Body = (Body)so.Data!;
                _handler.Add(so.Message, _cancellationToken);
                so.Message = new BaseMessage();
                so.Data = new byte[4];
                _client.GetStream().BeginRead(so.Data, 0, 4, OnBodyReceive, so);
                Heartbeat();
            }
        }
        catch (Exception e)
        {
            if (_client?.Client != null)
            {
                _client.GetStream().Close();
                _client.Close();
            }
            Console.WriteLine(e);
        }
    }

    private bool SendMessage(BaseMessage msg)
    {
        if (_client?.Client == null || 
            !_client.Connected || 
            !_client.GetStream().CanWrite) 
            return false;

        try
        {
            _client.GetStream().WriteAsync(msg.GetBytes(), _cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return true;
    }
    #endregion

    #region Timer

    private void OnLoginTimeout(object? state)
    {
        if (_isLogin) return;
        Console.WriteLine($"{DateTime.Now.ToLocalTime()} OnLoginFail is invoked");
        Reconnect();
    }

    private void OnHeartbeatTimeout(object? state)
    {
        try
        {
            if (_client is { Client: { }, Connected: true })
            {
                _client.GetStream().WriteAsync(new HeartbeatSendMsg().GetBytes(), _cancellationToken);
            }
        }
        catch (Exception)
        {
            // ignored
        }

        if (++_heartbeatCnt <= _maxRetryTimes) return;
        _heartbeatCnt = 0;
        Reconnect();
    }
    
    private void Heartbeat()
    {
        _heartbeatCnt = 0;
    }

    #endregion

    private void MessageHandle()
    {
        Console.WriteLine("Message Handler is running");
        while (!_cancellationToken.IsCancellationRequested)
        {
            var msg = _handler.Take();
            switch (msg.Header.Command)
            {
                case Command.Login:
                     _isLogin = true;
                    break;
                case Command.Heartbeat:
                    SendMessage(new HeartbeatSendMsg());
                    break;
            }
        }
    }
}

internal class StateObject
{
    public byte[]? Data { get; set; }
    public BaseMessage? Message { get; set; }
}