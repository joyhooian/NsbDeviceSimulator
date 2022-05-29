using Microsoft.Extensions.Configuration;

namespace NsbDeviceSimulator.Logic;

public class ConfigHelper
{
    private static IConfigurationRoot? _configurationRoot;
    private static ConfigHelper? _instance;
    
    public static IConfigurationRoot Configs
    {
        get
        {
            lock(ConfigLock) 
            {
                _instance ??= new ConfigHelper();
                return _configurationRoot!;
            }
        }
    }

    private static readonly object ConfigLock = new();

    private ConfigHelper()
    {
        _configurationRoot = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
    }
}