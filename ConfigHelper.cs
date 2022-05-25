using Microsoft.Extensions.Configuration;

namespace NsbDeviceSimulator;

public class ConfigHelper
{
    private static IConfigurationRoot? _configurationRoot;
    private static ConfigHelper? _instance;

    private static readonly object ConfigLock = new();

    private ConfigHelper()
    {
        _configurationRoot = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
    }

    public static ConfigHelper GetConfig
    {
        get
        {
            lock (ConfigLock)
            {
                return _instance ??= new ConfigHelper();
            }
        }
    }
    
    public IConfigurationRoot? Configs => _configurationRoot;
}