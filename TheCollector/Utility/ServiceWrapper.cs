using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.Havok.Animation.Rig;
using Microsoft.Extensions.DependencyInjection;
using TheCollector.CollectableManager;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Windows;

namespace TheCollector.Utility;

public static class ServiceWrapper
{
    public static IServiceProvider ServiceProvider;

    public static void Init(Plugin plugin)
    {
        var collection = new ServiceCollection();
        
        collection.AddSingleton(DalamudServices.Log);
        collection.AddSingleton(DalamudServices.GameData);
        collection.AddSingleton(DalamudServices.Objects);
        collection.AddSingleton(DalamudServices.Targets);
        collection.AddSingleton(DalamudServices.Framework);
        collection.AddSingleton(DalamudServices.ClientState);
        collection.AddSingleton(DalamudServices.PluginInterface);
        collection.AddSingleton(DalamudServices.Chat);
        collection.AddSingleton(DalamudServices.Conditions);
        collection.AddSingleton(DalamudServices.PlayerState);
        
        collection.AddSingleton<TaskManager>();
        
        collection.AddSingleton(plugin);
        collection.AddSingleton(plugin.Configuration);
        
        collection.AddSingleton<CollectableWindowHandler>();
        collection.AddSingleton<CollectableAutomationHandler>();
        collection.AddSingleton<ScripShopAutomationHandler>();
        collection.AddSingleton<ScripShopWindowHandler>();
        
        collection.AddSingleton<AutomationHandler>();

        collection.AddSingleton<GatherbuddyReborn_IPCSubscriber>();
        collection.AddSingleton<Artisan_IPCSubscriber>();
        collection.AddSingleton<Lifestream_IPCSubscriber>();
        collection.AddSingleton<IpcProvider>();
        
        collection.AddSingleton<ArtisanWatcher>();
        collection.AddSingleton<FishingWatcher>();
        
        collection.AddSingleton<PlogonLog>();
        collection.AddSingleton<ScripShopItemManager>();
        collection.AddSingleton<CraftingHandler>();

        collection.AddSingleton<MainWindow>();
        collection.AddSingleton<ConfigWindow>();
        collection.AddSingleton<ChangelogUi>();
        collection.AddSingleton<StopUi>();
        
        ServiceProvider = collection.BuildServiceProvider();
    }

    public static T Get<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();
    public static object Get(Type type) => ServiceProvider.GetRequiredService(type);
}
