namespace NsbDeviceSimulator.Type;

public enum Command
{
    None = 0xFF,

    // Communication
    Activation = 0x00,
    Login = 0x01,
    Heartbeat = 0x02,

    // Device Control
    Reboot = 0x10,
    FactoryReset = 0x11,

    // Timing
    LoopWhile = 0x20,
    QueryTimingMode = 0x21,
    QueryTimingSet = 0x22,
    SetTimingAlarm = 0x23,
    SetTimingAfter = 0x24,
    TimingReport = 0x25,
    CronCount = 0x87,

    // File Progress
    FileTransReqWifi = 0xA0,
    FileTransProcWifi = 0xA1,
    FileTransErrWifi = 0xA2,
    FileTransRptWifi = 0xA3,
    FileTransReqCell = 0xA4,
    FileTransRptCell = 0xA5,

    // Play Control
    Play = 0xF0,
    Pause = 0xF1,
    Next = 0xF2,
    Previous = 0xF3,
    Volume = 0xF4,
    FastForward = 0xF5,
    FastBackward = 0xF6,
    PlayIndex = 0xF7,
    ReadFilesList = 0xF8,
    DeleteFile = 0xF9
}