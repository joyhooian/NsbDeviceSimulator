using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using NsbDeviceSimulator.Type;
using NsbDeviceSimulator.Type.Model.Send;

namespace NsbDeviceSimulator.Logic;

public class Connection
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _sn;
    private readonly DeviceType _type;
    private readonly CancellationToken _cancellationToken;
    private CancellationTokenSource? _localCancellationTokenSource;
    private readonly BlockingCollection<BaseMessage> _inboxQueue;
    private readonly BlockingCollection<BaseMessage> _outboxQueue;
    private readonly ConcurrentDictionary<Command, ReplyCallback> _replyDict;
    private ErrorCallback? _errorCallback;
    private int _heartbeatCnt;
    private readonly AudioManager _audioManager;
    private readonly TaskManager _taskManager;
    
    public delegate void ErrorCallback(string reason);
    public delegate void ReplyCallback(params object[] args);

    public Connection(string host, int port, string sn, DeviceType type, CancellationToken cancellationToken, AudioManager audioManager, TaskManager taskManager)
    {
        _host = host;
        _port = port;
        _sn = sn;
        _type = type;
        _cancellationToken = cancellationToken;
        _audioManager = audioManager;
        _taskManager = taskManager;
        _inboxQueue = new BlockingCollection<BaseMessage>();
        _outboxQueue = new BlockingCollection<BaseMessage>();
        _replyDict = new ConcurrentDictionary<Command, ReplyCallback>();
        _heartbeatCnt = 0;
    }
    
    public bool Start(ErrorCallback callback)
    {
        _localCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var connState = new ConnectionState(
            new TcpClient(AddressFamily.InterNetwork),
            new Semaphore(0, 1),
            _localCancellationTokenSource.Token);
        _errorCallback = callback;
        try
        {
            connState.Client.BeginConnect(_host, _port, OnConnect, connState);
            return connState.ConnLock.WaitOne(new TimeSpan(0, 0, 10)) && connState.Client.Connected;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
        finally
        {
            connState.ConnLock.Dispose();
        }
    }

    public void Stop()
    {
        _localCancellationTokenSource?.Cancel();
    }

    public void Login(ReplyCallback? callback)
    {
        try
        {
            if (callback != null)
            {
                if (_replyDict.ContainsKey(Command.Login))
                {
                    _replyDict.TryRemove(Command.Login, out _);
                }

                _replyDict.TryAdd(Command.Login, callback);
            }

            var msg = new LoginSendMsg(_sn, _type);
            _outboxQueue.TryAdd(msg);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void StartHeartbeat(CancellationToken cancellationToken)
    {
        var timer = new Timer(OnHeartbeatTimeout, cancellationToken, 20 * 1000, 20 * 1000);
        cancellationToken.WaitHandle.WaitOne();
        timer.Dispose();
    }

    private void OnConnect(IAsyncResult ar)
    {
        if (ar.AsyncState is not ConnectionState connState) return;
        if (!connState.Client.Connected) return;
        Console.WriteLine($"TcpClient({connState.Client.GetHashCode()}) connected");

        Task.Run(() => OutboxTask(connState.Client, connState.CancellationToken), connState.CancellationToken);
        Task.Run(() => InboxTask(connState.CancellationToken), connState.CancellationToken);

        var receiveState = new ReceiveState(connState.Client);
        connState.Client.GetStream()
            .BeginRead(receiveState.Data, 0, receiveState.Data.Length, OnHeaderReceive, receiveState);
        connState.ConnLock.Release();
        var client = connState.Client;
        var stream = client.GetStream();
        connState.CancellationToken.WaitHandle.WaitOne();
        try
        {
            stream.Close();
            client.Close();
            Console.WriteLine($"TcpClient({client.GetHashCode()}) closed");
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void OutboxTask(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            while (_outboxQueue.TryTake(out var msg, Timeout.Infinite, cancellationToken))
            {
                try
                {
                    var stream = client?.GetStream();
                    if (stream is { CanWrite: true })
                    {
                        stream.Write(msg.GetBytes());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void InboxTask(CancellationToken cancellationToken)
    {
        try
        {
            int? cronCount = null;
            List<CronTask> cronTasks = new List<CronTask>();
            while (_inboxQueue.TryTake(out var msg, Timeout.Infinite, cancellationToken))
            {
                if (msg.Header?.Command == null) continue;
                _heartbeatCnt = 0;
                _replyDict.TryRemove(msg.Header.Command, out var callback);
                var result = false;
                var needRpl = true;
                var rplDataLength = 0;
                byte[]? rplData = null;
                switch (msg.Header.Command)
                {
                    case Command.Login:
                        if (callback == null) break;
                        callback.Invoke();
                        Task.Run(() => StartHeartbeat(cancellationToken), cancellationToken);
                        needRpl = false;
                        break;
                    case Command.Heartbeat:
                        needRpl = false;
                        break;
                    case Command.None:
                        break;
                    case Command.Activation:
                        break;
                    case Command.Reboot:
                        break;
                    case Command.FactoryReset:
                        break;
                    case Command.LoopWhile:
                        break;
                    case Command.QueryTimingMode:
                        break;
                    case Command.QueryTimingSet:
                        break;
                    case Command.SetTimingAlarm:
                        if (!CronTask.TryParse(msg.Body.Data, out var cronTask) || cronCount == null)
                        {
                            cronCount = null;
                            cronTasks = new List<CronTask>();
                            break;
                        }
                        cronTasks.Add(cronTask);
                        if (cronTasks.Count == cronCount)
                        {
                            _taskManager.AddCronTasks(cronTasks);
                            cronTasks = new List<CronTask>();
                            result = true;
                        }
                        break;
                    case Command.SetTimingAfter:
                        break;
                    case Command.TimingReport:
                        break;
                    case Command.CronCount:
                        if (msg.Body.Data.Length != 1) break;
                        cronCount = msg.Body.Data[0];
                        result = true;
                        break;
                    case Command.FileTransReqWifi:
                        break;
                    case Command.FileTransProcWifi:
                        break;
                    case Command.FileTransErrWifi:
                        break;
                    case Command.FileTransRptWifi:
                        break;
                    case Command.FileTransReqCell:
                        if (msg.Body.Data.Length != 8) break;
                        var fileToken = Encoding.ASCII.GetString(msg.Body.Data);
                        if (string.IsNullOrEmpty(fileToken)) break;
                        _audioManager.AddAudio(fileToken);
                        result = true;
                        break;
                    case Command.FileTransRptCell:
                        break;
                    case Command.Play:
                        result = _audioManager.Play();
                        break;
                    case Command.Pause:
                        result = _audioManager.Pause();
                        break;
                    case Command.Next:
                        result = _audioManager.Next();
                        break;
                    case Command.Previous:
                        result = _audioManager.Previous();
                        break;
                    case Command.Volume:
                        if (msg.Body.Data.Length != 2) break;
                        var volume = msg.Body.Data[0] | msg.Body.Data[1];
                        result = _audioManager.Volume(volume);
                        break;
                    case Command.FastForward:
                        break;
                    case Command.FastBackward:
                        break;
                    case Command.PlayIndex:
                        if (msg.Body.Data.Length != 2) break;
                        var playIndex = msg.Body.Data[0] | msg.Body.Data[1] - 1;
                        result = _audioManager.Play(playIndex);
                        break;
                    case Command.ReadFilesList:
                        var count = _audioManager.Count();
                        rplData = new[] { (byte)(count & 0xFF00 >> 8), (byte)(count & 0x00FF) };
                        rplDataLength = rplData.Length;
                        result = true;
                        break;
                    case Command.DeleteFile:
                        if (msg.Body.Data.Length != 2) break;
                        var deleteIndex = msg.Body.Data[0] | msg.Body.Data[1] - 1;
                        result = _audioManager.DeleteAudio(deleteIndex);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (result && needRpl)
                {
                    _outboxQueue.Add(new BaseMessage()
                    {
                        Header = new Header(msg.Header.Command, (short)rplDataLength),
                        Body = new Body(rplData)
                    }, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void OnHeaderReceive(IAsyncResult ar)
    {
        try
        {
            if (ar.AsyncState is not ReceiveState rs || !rs.Stream.CanRead) return;

            var length = rs.Stream.EndRead(ar);
            if (length == 0)
            {
                _errorCallback?.Invoke("Socket closed by peer");
                return;
            }

            if (Header.TryParse(rs.Data, out var header))
            {
                rs.Message.Header = header;
                rs.Data = new byte[header!.DataLength + 1];
                rs.Stream.BeginRead(rs.Data, 0, rs.Data.Length, OnBodyReceive, rs);
            }
            else
            {
                rs.Stream.BeginRead(rs.Data, 0, rs.Data.Length, OnHeaderReceive, rs);
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
            if (ar.AsyncState is not ReceiveState rs) return;

            var length = rs.Stream.EndRead(ar);
            if (length == 0) return;

            if (Body.TryParse(rs.Data, out var body))
            {
                rs.Message.Body = body!;
                _inboxQueue.TryAdd(rs.Message);
            }

            var receiveState = new ReceiveState(rs.Client);
            rs.Stream
                .BeginRead(receiveState.Data, 0, receiveState.Data.Length, OnHeaderReceive, receiveState);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    private void OnHeartbeatTimeout(object? state)
    {
        if (state is not CancellationToken cancellationToken) return;
        if (cancellationToken.IsCancellationRequested) return;
        _outboxQueue.TryAdd(new HeartbeatSendMsg());
        if (++_heartbeatCnt > 3)
        {
            _errorCallback?.Invoke("Heartbeat timeout");
        }
    }
}

internal class ReceiveState
{
    public byte[] Data { get; set; }
    public BaseMessage Message { get; }
    public TcpClient Client { get; }
    public NetworkStream Stream { get; }

    public ReceiveState(TcpClient client)
    {
        Data = new byte[4];
        Message = new BaseMessage();
        Client = client;
        Stream = client.GetStream();
    }
}

internal class ConnectionState
{
    public TcpClient Client { get; }
    public Semaphore ConnLock { get; }
    public CancellationToken CancellationToken { get; }

    public ConnectionState(TcpClient client, Semaphore connLock, CancellationToken cancellationToken)
    {
        Client = client;
        ConnLock = connLock;
        CancellationToken = cancellationToken;
    }
}