using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using Microsoft.Extensions.DependencyInjection;
using TheCollector.CollectableManager;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Windows;

namespace TheCollector.Utility;

public static class ServiceWrapper
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    public static void Init(Plugin plugin)
    {
        var collection = new ServiceCollection();
        
        collection.AddSingleton(Svc.Log);
        collection.AddSingleton(Svc.Data);
        collection.AddSingleton(Svc.Objects);
        collection.AddSingleton(Svc.Targets);
        collection.AddSingleton(Svc.Framework);
        collection.AddSingleton(Svc.ClientState);
        collection.AddSingleton(Svc.PluginInterface);
        collection.AddSingleton(Svc.Chat);
        collection.AddSingleton(Svc.Condition);
        collection.AddSingleton(Plugin.PlayerState);
        
        collection.AddSingleton<TaskManager>();
        
        collection.AddSingleton(plugin);
        collection.AddSingleton(plugin.Configuration);

        collection.AddSingleton<StatusService>();
        
        collection.AddSingleton<CollectableWindowHandler>();
        collection.AddSingleton<ScripShopWindowHandler>();
        
        collection.AddSingleton<AutomationHandler>();

        collection.AddSingleton<GatherbuddyReborn_IPCSubscriber>();
        collection.AddSingleton<Artisan_IPCSubscriber>();
        collection.AddSingleton<Lifestream_IPCSubscriber>();
        collection.AddSingleton<IpcProvider>();
        
        collection.AddSingleton<ArtisanWatcher>();
        collection.AddSingleton<FishingWatcher>();
        
        collection.AddSingleton<PlogonLog>();
        collection.AddSingleton<DiscordWebhookService>();
        collection.AddSingleton<CharacterBalanceTracker>();
        collection.AddSingleton<VendorCatalog>();
        collection.AddSingleton<FirmamentCatalog>();
        collection.AddSingleton<FirmamentManager.FirmamentTurnInWindowHandler>();
        collection.AddSingleton<FirmamentManager.FirmamentShopWindowHandler>();

        collection.AddSingleton<FirmamentManager.FirmamentTurnInHandler>();
        collection.AddSingleton<IPipeline>(sp => sp.GetRequiredService<FirmamentManager.FirmamentTurnInHandler>());

        collection.AddSingleton<FirmamentManager.FirmamentShopHandler>();
        collection.AddSingleton<IPipeline>(sp => sp.GetRequiredService<FirmamentManager.FirmamentShopHandler>());

        collection.AddSingleton<FirmamentManager.KupoOfFortuneWindowHandler>();
        collection.AddSingleton<FirmamentManager.KupoOfFortuneHandler>();
        collection.AddSingleton<IPipeline>(sp => sp.GetRequiredService<FirmamentManager.KupoOfFortuneHandler>());

        collection.AddSingleton<ResourceInspectionManager.ResourceInspectionWindowHandler>();
        collection.AddSingleton<ResourceInspectionManager.ResourceInspectionHandler>();
        collection.AddSingleton<IPipeline>(sp => sp.GetRequiredService<ResourceInspectionManager.ResourceInspectionHandler>());

        collection.AddSingleton<Data.ScripSystem.NormalScripSystem>();
        collection.AddSingleton<Data.ScripSystem.FirmamentScripSystem>();
        collection.AddSingleton<Data.ScripSystem.ResourceInspectionScripSystem>();
        collection.AddSingleton<Data.ScripSystem.ScripSystemSelector>();
        collection.AddSingleton<ScripShopItemManager>();
        collection.AddSingleton<CraftingHandler>();
        collection.AddSingleton<ScripPlannerService>();
        collection.AddSingleton<FirmamentPlannerService>();

        collection.AddSingleton<CollectableAutomationHandler>();
        collection.AddSingleton<IPipeline>(sp => sp.GetRequiredService<CollectableAutomationHandler>());

        collection.AddSingleton<ScripShopAutomationHandler>();
        collection.AddSingleton<IPipeline>(sp => sp.GetRequiredService<ScripShopAutomationHandler>());

        collection.AddSingleton<AutoRetainerManager>();
        collection.AddSingleton<IPipeline>(sp => sp.GetRequiredService<AutoRetainerManager>());

        collection.AddSingleton<DeliverooManager>();
        collection.AddSingleton<IPipeline>(sp => sp.GetRequiredService<DeliverooManager>());

        collection.AddSingleton<PipelineRegistry>();

        collection.AddSingleton<MainWindow>();
        collection.AddSingleton<ChangelogUi>();
        collection.AddSingleton<StopUi>();
        
        ServiceProvider = collection.BuildServiceProvider();
    }

    public static T Get<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();
    public static object Get(Type type) => ServiceProvider.GetRequiredService(type);
}
