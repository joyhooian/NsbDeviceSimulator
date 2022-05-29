using System.Text;

namespace NsbDeviceSimulator.Type.Model.Send;

public class LoginSendMsg : BaseMessage
{
    public static readonly Command Command = Command.Login;
    private readonly string _sn;
    private readonly DeviceType _deviceType;

    public LoginSendMsg(string sn, DeviceType type)
    {
        _sn = sn;
        _deviceType = type;

        var data = new byte[sn.Length + 1];
        Array.Copy(Encoding.ASCII.GetBytes(sn), data, sn.Length);
        data[^1] = (byte)type;
        
        Body = new Body(data);
        Header = new Header(Command.Login, (short)Body.Data.Length);
    }
}