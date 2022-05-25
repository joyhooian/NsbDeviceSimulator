using System.Text;

namespace NsbDeviceSimulator.Type.Model.Send;

public class LoginSendMsg : BaseMessage
{
    public static readonly Command Command = Command.Login;
    public string Sn { get; set; }
    public DeviceType DeviceType { get; set; }

    public LoginSendMsg(string sn, DeviceType type)
    {
        Sn = sn;
        DeviceType = type;

        var data = new byte[sn.Length + 1];
        Array.Copy(Encoding.ASCII.GetBytes(sn), data, sn.Length);
        data[^1] = (byte)type;
        
        Body = new Body(data);
        Header = new Header(Command.Login, (short)Body.Data.Length);
    }


}