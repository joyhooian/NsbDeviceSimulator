namespace NsbDeviceSimulator.Type.Model.Send;

public class HeartbeatSendMsg : BaseMessage
{
    public static readonly Command Command = Command.Heartbeat;
    public HeartbeatSendMsg()
    {
        Header = new Header(Command.Heartbeat, 0);
        Body = new Body(null);
    }
}