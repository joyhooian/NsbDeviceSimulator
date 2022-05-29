using System.Runtime.CompilerServices;

namespace NsbDeviceSimulator.Type;

public class BaseMessage
{
    public Header? Header { get; set; }
    public Body Body { get; set; }
    public Semaphore? Semaphore { get; set; }
    
    public int HeaderLength
    {
        get => 4;
    }
    public int BodyLength
    {
        get => Body.Data.Length + 1;
    }
    
    public byte[] GetBytes()
    {
        var bytes = new byte[HeaderLength + BodyLength];
        
        Array.Copy(Header.RawData, bytes, HeaderLength);
        Array.Copy(Body.RawData, 0, bytes, HeaderLength, Body.RawData.Length);

        return bytes;
    }
}

public class Header
{
    private const int StartByte = 0x7E;

    public Command Command { get; set; }
    public short DataLength { get; set; }
    public byte[] RawData { get; set; }

    public Header(Command command, short dataLength)
    {
        RawData = new byte[4];
        RawData[(int)HeaderOffset.StartByteOffset] = (byte)StartByte;
        RawData[(int)HeaderOffset.Command] = (byte)command;
        RawData[(int)HeaderOffset.LengthH] = (byte)(dataLength >> 8);
        RawData[(int)HeaderOffset.LengthL] = (byte)(dataLength & 0x00FF);

        Command = command;
        DataLength = dataLength;
    }

    public static bool TryParse(byte[]? data, out Header? header)
    {
        header = null;
        if (data is not { Length: 4 }) return false;

        if (data[(int)HeaderOffset.StartByteOffset] != StartByte) return false;

        if (!Enum.IsDefined(typeof(Command), (int)data[(int)HeaderOffset.Command])) return false;

        header = new Header((Command)data[(int)HeaderOffset.Command],
            (short)(data[(int)HeaderOffset.LengthH] | data[(int)HeaderOffset.LengthL]));
        return true;
    }
}

public class Body
{
    private const int EndByte = 0xEF;
    public byte[] Data { get; set; }
    
    public byte[] RawData { get; set; }

    public Body(byte[]? data)
    {
        Data = data ?? Array.Empty<byte>();
        RawData = new byte[Data.Length + 1];
        Array.Copy(Data, RawData, Data.Length);
        RawData[^1] = EndByte;
    }

    public static bool TryParse(byte[]? data, out Body? body)
    {
        body = null;
        if (data == null) return false;

        if (data[^1] != EndByte) return false;

        body = new Body(data[..^1]);
        return true;
    }
}

internal enum HeaderOffset
{
    StartByteOffset,
    Command,
    LengthH,
    LengthL
}