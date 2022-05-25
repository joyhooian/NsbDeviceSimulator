using System;
using NsbDeviceSimulator;
using NsbDeviceSimulator.Logic;
using NsbDeviceSimulator.Type;

class Program
{
    static void Main()
    {
        var config = ConfigHelper.GetConfig;
        var host = config.Configs["Server:Local"];
        var port = Convert.ToInt32(config.Configs["Port"]);
        var agent = new Agent(host, port, DeviceType.Cellular, "02387448");
        agent.Start();
        while (true) ;
    }
}