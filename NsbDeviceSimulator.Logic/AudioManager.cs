using System.Collections.Concurrent;
using System.Net.Http;
using NAudio.Wave;

namespace NsbDeviceSimulator.Logic;

public class AudioManager
{
    private readonly DirectoryInfo _directory;
    private List<FileInfo> _files;
    private AudioStatus _status = AudioStatus.Idle;
    private readonly CancellationToken _cancellationToken;
    private float _volume = 0.5f;
    private int _index;
    private readonly BlockingCollection<AudioEventObject> _eventQueue;

    public AudioManager(string sn, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _eventQueue = new BlockingCollection<AudioEventObject>();
        var audioPath = @$"{ConfigHelper.Configs["RootPath"]}\{sn}\" + "audio";
        if (!Directory.Exists(audioPath))
            Directory.CreateDirectory(audioPath);
        _directory = new DirectoryInfo(audioPath);
        _files = UpdateAudios();
    }

    #region Public Function

    public bool Play(int? index = null)
    {
        if (index == null) return _status == AudioStatus.Playing || Handle(new AudioEventObject(AudioEvent.Play));
        if (index >= _files.Count) return false;
        _index = (int)index;
            
        if (_status is not AudioStatus.Idle)
            Handle(new AudioEventObject(AudioEvent.Stop));
        return Handle(new AudioEventObject(AudioEvent.Play));
    }

    public bool Pause()
    {
        return _status is not AudioStatus.Playing || Handle(new AudioEventObject(AudioEvent.Pause));
    }

    public bool Stop()
    {
        return _status is AudioStatus.Idle || Handle(new AudioEventObject(AudioEvent.Stop));
    }
    
    public bool Next()
    {
        if (++_index >= _files.Count)
            _index = 0;
        
        if (_status is AudioStatus.Idle) return true;
        
        var result = Handle(new AudioEventObject(AudioEvent.Stop));
        return result && Handle(new AudioEventObject(AudioEvent.Play));
    }

    public bool Previous()
    {
        if (--_index < 0)
            _index = _files.Count - 1;

        if (_status is AudioStatus.Idle) return true;

        var result = Handle(new AudioEventObject(AudioEvent.Stop));
        return result && Handle(new AudioEventObject(AudioEvent.Play));
    }

    public bool Volume(int inputVolume)
    {
        if (inputVolume is < 0 or > 30)
            return false;

        _volume = inputVolume / 30.0f;
        return Handle(new AudioEventObject(AudioEvent.Volume));
    }

    public bool AddAudio(string fileToken)
    {
        try
        {
            using var client = new HttpClient();
            var uri = ConfigHelper.Configs["AddFileUri"] + fileToken;
            var response = client.GetAsync(uri, _cancellationToken).Result;
            var fullFileName = response.Content.Headers.ContentDisposition?.FileName;
            if (string.IsNullOrEmpty(fullFileName) || !fullFileName.EndsWith(".mp3")) return false;

            var fileNameWithoutExtension = fullFileName[..^4];
            if (_files.Any(file => file.Name.Equals(fullFileName)))
            {
                var numberFix = 1;
                string tempFileName;
                do
                {
                    tempFileName = fileNameWithoutExtension + $"({numberFix++}).mp3";
                } while (_files.Any(file => file.Name.Equals(tempFileName)));

                fullFileName = tempFileName;
            }

            using var contentStream = response.Content.ReadAsStream();
            using var fileStream = File.OpenWrite(_directory.FullName + $@"\{fullFileName}");
            contentStream.CopyTo(fileStream);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
        finally
        {
            _files = UpdateAudios();
        }
    }

    public bool DeleteAudio(int index)
    {
        if (index >= _files.Count)
            return false;

        _files[index].Delete();
        _files = UpdateAudios();
        return true;
    }

    public int Count() => _files.Count;
    
    private List<FileInfo> UpdateAudios()
    {
        return (
            from file in _directory.GetFiles()
            where file.Extension.EndsWith("mp3")
            orderby file.CreationTimeUtc
            select file).ToList();
    }

    #endregion

    private void Processing()
    {
        try
        {
            var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
            using var audioFile = new AudioFileReader(_files[_index].FullName);
            using var outputDevice = new WaveOutEvent();

            outputDevice.Init(audioFile);
            outputDevice.Volume = _volume;
            outputDevice.Play();
            _status = AudioStatus.Playing;
            outputDevice.PlaybackStopped += (_, _) =>
            {
                _status = AudioStatus.Idle;
                cancellationSource.Cancel();
            };
            while (_eventQueue.TryTake(out var audioEvent, Timeout.Infinite, cancellationSource.Token)
                   && _status != AudioStatus.Idle)
            {
                Console.WriteLine($"Event: {audioEvent.Event.ToString()}, Status: {_status.ToString()}");
                switch (audioEvent.Event)
                {
                    case AudioEvent.Volume:
                        outputDevice.Volume = _volume;
                        break;
                    case AudioEvent.Play when _status is AudioStatus.Pausing:
                        outputDevice.Play();
                        _status = AudioStatus.Playing;
                        break;
                    case AudioEvent.Pause when _status is AudioStatus.Playing:
                        outputDevice.Pause();
                        _status = AudioStatus.Pausing;
                        break;
                    case AudioEvent.Stop:
                        outputDevice.Stop();
                        _status = AudioStatus.Idle;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                audioEvent.EventLock.Release();
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

    private bool Handle(AudioEventObject aeo)
    {
        Console.WriteLine($"{aeo.Event.ToString()}, {_status.ToString()}");
        try
        {
            switch (aeo.Event)
            {
                case AudioEvent.Play when _status is AudioStatus.Idle:
                    Task.Run(Processing, _cancellationToken);
                    aeo.EventLock.Release();
                    break;
                case AudioEvent.Play when _status is not AudioStatus.Idle:
                    _eventQueue.Add(aeo, _cancellationToken);
                    break;
                case AudioEvent.Pause when _status is not AudioStatus.Idle:
                    _eventQueue.Add(aeo, _cancellationToken);
                    break;
                case AudioEvent.Stop when _status is not AudioStatus.Idle:
                    _eventQueue.Add(aeo, _cancellationToken);
                    break;
                case AudioEvent.Volume when _status is not AudioStatus.Idle:
                    _eventQueue.Add(aeo, _cancellationToken);
                    break;
                case AudioEvent.Volume when _status is AudioStatus.Idle:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return aeo.EventLock.WaitOne(1000);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    #region State Object

    private enum AudioEvent
    {
        Play,
        Pause,
        Stop,
        Volume
    }

    private enum AudioStatus
    {
        Idle,
        Playing,
        Pausing
    }

    private class AudioEventObject
    {
        public AudioEvent Event { get; }
        
        public Semaphore EventLock { get; set; }

        public AudioEventObject(AudioEvent audioEvent)
        {
            Event = audioEvent;
            EventLock = new Semaphore(0, 1);
        }
    }

    #endregion
}