using Newtonsoft.Json;
using NsbDeviceSimulator.Type;

namespace NsbDeviceSimulator.Logic;

public class TaskManager
{
    private readonly List<CronTask> _cronTasks;
    private readonly List<Timer> _timers;
    private readonly string _cronFilePath;
    private readonly AudioManager _audioManager;

    public TaskManager(string sn, AudioManager audioManager)
    {
        _cronTasks = new List<CronTask>();
        _timers = new List<Timer>();
        _audioManager = audioManager;
        var taskPath = $@"{ConfigHelper.Configs["RootPath"]}\{sn}\" + "task";
        if (!Directory.Exists(taskPath))
            Directory.CreateDirectory(taskPath);
        _cronFilePath = taskPath + "\\cronTask";
        if (!File.Exists(_cronFilePath))
        {
            using var fs = File.Create(_cronFilePath);
        }
        else
        {
            UpdateTask();
        }
    }

    private void UpdateTask()
    {
        _timers.ForEach(timer => timer.Dispose());
        _timers.Clear();
        _cronTasks.Clear();
        
        using var rs = File.OpenText(_cronFilePath);
        while (!rs.EndOfStream)
        {
            var taskLine = rs.ReadLine();
            if (string.IsNullOrEmpty(taskLine)) continue;

            var ct = JsonConvert.DeserializeObject<CronTask>(taskLine);
            if (ct != null)
                _cronTasks.Add(ct);
        }
        _cronTasks.ForEach(task =>
        {
            task.Weekdays.ForEach(day =>
            {
                var weekday = (int)DateTime.Now.ToLocalTime().DayOfWeek - 1;
                weekday = weekday == -1 ? 6 : weekday;
                var dayOffset = day - weekday;
                dayOffset = dayOffset < 0 ? dayOffset + 7 : dayOffset;
                var now = DateTime.Now;
                var executionTime = new DateTime(now.Year, now.Month, now.Day, task.StartHour, task.StartMinute, 0);
                if (executionTime < now) dayOffset = 7;
                executionTime = executionTime.AddDays(dayOffset);
                var startObj = new TimerStateObject(task.Volume, task.Relay, task.Audio);
                _timers.Add(new Timer(CronTimerCb, startObj, executionTime - now, new TimeSpan(7, 0, 0, 0)));
                var dueTime = executionTime.AddHours(task.EndHour - task.StartHour)
                    .AddMinutes(task.EndMinute - task.StartMinute) - now;
                _timers.Add(new Timer(CronTimerCb, new TimerStateObject(task.Relay), dueTime, new TimeSpan(7, 0, 0, 0)));
            });
        });
    }

    private void CronTimerCb(object? state)
    {
        var stateObj = state as TimerStateObject;
        if (stateObj == null) return;

        if (stateObj.Start && stateObj.Audio < _audioManager.Count())
        {
            _audioManager.Volume((int)stateObj.Volume!);
            _audioManager.Play(stateObj.Audio - 1);
            var relaySwitch = (bool)stateObj.Relay! ? "打开" : "关闭";
            Console.WriteLine($"继电器{relaySwitch}");
        }
        else
        {
            _audioManager.Stop();
            var relaySwitch = (bool)stateObj.Relay! ? "打开" : "关闭";
            Console.WriteLine($"继电器{relaySwitch}");
        }
    }

    public void AddCronTasks(List<CronTask> cronTasks)
    {
        var fs = File.Open(_cronFilePath, FileMode.Truncate, FileAccess.ReadWrite);
        fs.Close();
        var writeStream = File.AppendText(_cronFilePath);
        cronTasks.ForEach(task =>
        {
            writeStream.WriteLine(JsonConvert.SerializeObject(task));
        });
        writeStream.Close();
        UpdateTask();
    }
}

internal class TimerStateObject
{
    public int? Volume { get; }
    public bool? Relay { get; }
    public int? Audio { get; }
    public bool Start { get; }

    public TimerStateObject(int volume, bool relay, int audio)
    {
        Volume = volume;
        Relay = relay;
        Audio = audio;
        Start = true;
    }

    public TimerStateObject(bool relay)
    {
        Relay = !relay;
        Start = false;
    }
}