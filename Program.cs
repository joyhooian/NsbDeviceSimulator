using NsbDeviceSimulator.Logic;
using NsbDeviceSimulator.Type;

namespace NsbDeviceSimulator;

internal static class Program
{
    private static void Main()
    {
        _ = new Agent(ConfigHelper.Configs["Server:Dev"]!, Convert.ToInt32(ConfigHelper.Configs["Port"]), DeviceType.Cellular, "02387448", CancellationToken.None);
        _ = new Agent(ConfigHelper.Configs["Server:Dev"]!, Convert.ToInt32(ConfigHelper.Configs["Port"]), DeviceType.Cellular, "12345678", CancellationToken.None);
        while (true)
        {
        }
    }
}