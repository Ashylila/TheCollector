using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.Havok.Animation.Rig;
using Microsoft.Extensions.DependencyInjection;
using TheCollector.CollectibleManager;
using TheCollector.Windows;

namespace TheCollector.Utility;

public static class ServiceWrapper
{
    public static IServiceProvider ServiceProvider;

    public static void Init(Plugin plugin)
    {
        var collection = new ServiceCollection();
        
        collection.AddSingleton(Svc.Log);
        collection.AddSingleton(Svc.Data);
        
        collection.AddSingleton<TaskManager>();
        
        collection.AddSingleton(plugin);
        collection.AddSingleton(plugin.Configuration);
        
        collection.AddSingleton<CollectibleWindowHandler>();
        collection.AddSingleton<CollectableAutomationHandler>(sp =>
                                                                  new CollectableAutomationHandler(
                                                                      sp.GetRequiredService<TaskManager>(),
                                                                      sp.GetRequiredService<IPluginLog>(),
                                                                      sp.GetRequiredService<CollectibleWindowHandler>(),
                                                                      sp.GetRequiredService<IDataManager>()
                                                                  ));

        collection.AddSingleton<MainWindow>();
        collection.AddSingleton<ConfigWindow>();
        
        ServiceProvider = collection.BuildServiceProvider();
    }

    public static T Get<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();
    public static object Get(Type type) => ServiceProvider.GetRequiredService(type);
}
