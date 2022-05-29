namespace NsbDeviceSimulator.Type;

public class CronTask
{
    public int Index { get; set; }
    public int StartHour { get; set; }
    public int StartMinute { get; set; }
    public int EndHour { get; set; }
    public int EndMinute { get; set; }
    public int Volume { get; set; }
    public bool Relay { get; set; }
    public int Audio { get; set; }
    public List<int> Weekdays { get; set; }

    public CronTask(int index, int startHour, int startMinute, int endHour, int endMinute, int volume, bool relay, int audio, List<int> weekdays)
    {
        Index = index;
        StartHour = startHour;
        StartMinute = startMinute;
        EndHour = endHour;
        EndMinute = endMinute;
        Volume = volume;
        Relay = relay;
        Audio = audio;
        Weekdays = weekdays;
    }

    public CronTask()
    {
        Weekdays = new List<int>();
    }

    public static bool TryParse(byte[] data, out CronTask cronTask)
    {
        if (data.Length < 8)
        {
            cronTask = new CronTask();
            return false;
        }

        var index = (int)data[(int)Offset.Index];
        var startHour = (int)data[(int)Offset.StartHour] + 8;
        if (startHour > 23) startHour -= 24;
        var startMinute = (int)data[(int)Offset.StartMinute];
        var endHour = (int)data[(int)Offset.EndHour] + 8;
        if (endHour > 23) endHour -= 24;
        var endMinute = (int)data[(int)Offset.EndMinute];
        var volume = (int)data[(int)Offset.Volume];
        var relay = data[(int)Offset.Relay] == 1;
        var audio = (int)data[(int)Offset.Audio];
        var weekdays = data.Skip((int)Offset.Weekdays).Select(d => (int)d - 1).ToList();

        if (startHour is < 0 or > 23 ||
            startMinute is < 0 or > 59 ||
            endHour is < 0 or > 23 ||
            endMinute is < 0 or > 59 ||
            volume is < 0 or > 30 ||
            audio < 0 || 
            weekdays.Any(d => d is < 0 or > 6))
        {
            cronTask = new CronTask();
            return false;
        }

        cronTask = new CronTask(index, startHour, startMinute, endHour, endMinute, volume, relay, audio, weekdays);
        return true;
    }
}

internal enum Offset
{
    Index,
    StartHour,
    StartMinute,
    EndHour,
    EndMinute,
    Volume,
    Relay,
    Audio,
    Weekdays
}