using System.Runtime.CompilerServices;

namespace NsbDeviceSimulator.Type;

public class BaseMessage
{
    public Header? Header { get; set; }
    public Body Body { get; set; }
    
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
    private static int StartByte = 0x7E;
    
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

    public static explicit operator Header?(byte[]? header)
    {
        if (header is not { Length: 4 }) return null;

        if (header[(int)HeaderOffset.StartByteOffset] != StartByte) return null;

        if (Enum.IsDefined(typeof(Command), (int)header[(int)HeaderOffset.Command]))
        {
            return new Header((Command)header[(int)HeaderOffset.Command],
                (short)(header[(int)HeaderOffset.LengthH] | header[(int)HeaderOffset.LengthL]));
        }

        return null;
    }
}

public class Body
{
    internal static int EndByte = 0xEF;
    public byte[] Data { get; set; }
    
    public byte[] RawData { get; set; }

    public Body(byte[]? data)
    {
        if (data != null)
        {
            Data = data;
        }
        RawData = new byte[Data.Length + 1];
        Array.Copy(Data, RawData, Data.Length);
        RawData[^1] = (byte)EndByte;
    }

    public static explicit operator Body?(byte[] body)
    {
        if (body[^1] != EndByte) return null;
        var data = new byte[body.Length - 1];
        Array.Copy(body, data, body.Length - 1);
        return new Body(data);
    }
}

internal enum HeaderOffset
{
    StartByteOffset,
    Command,
    LengthH,
    LengthL
}

public enum DataType
{
    Header,
    Body
}