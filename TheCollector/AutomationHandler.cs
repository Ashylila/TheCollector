using System;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using TheCollector.CollectableManager;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Utility;

namespace TheCollector;

public class AutomationHandler : IDisposable
{
    private readonly IPluginLog _log;
    private readonly CollectableAutomationHandler _collectableAutomationHandler;
    private readonly Configuration _config;
    private readonly ScripShopAutomationHandler _scripShopAutomationHandler;
    private readonly IChatGui _chatGui;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyReborn_IPCSubscriber;
    private readonly ArtisanWatcher _artisanWatcher;
    private readonly IFramework _framework;
    
    
    
    public AutomationHandler(
        IPluginLog log,CollectableAutomationHandler collectableAutomationHandler, Configuration config, ScripShopAutomationHandler scripShopAutomationHandler, IChatGui chatGui, GatherbuddyReborn_IPCSubscriber gatherbuddyReborn_IPCSubscriber, ArtisanWatcher artisanWatcher, IFramework framework)
    {
        _log = log;
        _gatherbuddyReborn_IPCSubscriber = gatherbuddyReborn_IPCSubscriber;
        _collectableAutomationHandler = collectableAutomationHandler;
        _config = config;
        _scripShopAutomationHandler = scripShopAutomationHandler;
        _chatGui = chatGui;
        _artisanWatcher = artisanWatcher;
        _framework = framework;
    }

    public void Init()
    {
        _collectableAutomationHandler.OnScripsCapped += OnScripCapped;
        _collectableAutomationHandler.OnError += OnError;
        _collectableAutomationHandler.OnFinishTrading += OnFinishedTrading;
        _scripShopAutomationHandler.OnError += OnError;
        _scripShopAutomationHandler.OnFinishedTrading += OnFinishedTrading;
        _gatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged += OnAutoGatherStatusChanged;
        _artisanWatcher.OnCraftingFinished += OnFinishedCraftingList;
    }

    private void OnAutoGatherStatusChanged(bool enabled)
    {
        if (!enabled && _config.CollectOnAutogatherDisabled)
        {
            _collectableAutomationHandler.Start();
        }
    }
    public void Invoke()
    {
        _collectableAutomationHandler.Start();
    }

    public void OnFinishedCraftingList()
    {
        Svc.Log.Debug("Finished Crafting List, starting collectables automation");
        _framework.RunOnTick((async () => 
                                 {
                                     await Task.Delay(5000);
                                     Invoke();
                                 }));
    }
    private void OnFinishedTrading()
    {
        if (_collectableAutomationHandler.HasCollectible)
        {
            _collectableAutomationHandler.Start();
        }
        else if (_config.EnableAutogatherOnFinish)
        {
            _gatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(true);
        }
        
    }

    private void OnError(string reason)
    {
        _chatGui.Print($"Automation threw an error, {reason}", "TheCollector");
    }
    private void OnScripCapped(bool capped)
    {
        if (capped)
        {
            _scripShopAutomationHandler.Start();
        }
    }
    public void Dispose()
    {
        _collectableAutomationHandler.OnScripsCapped -= OnScripCapped;
        _collectableAutomationHandler.OnError -= OnError;
        _scripShopAutomationHandler.OnError -= OnError;
        _scripShopAutomationHandler.OnFinishedTrading -= OnFinishedTrading;
        _gatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged -= OnAutoGatherStatusChanged;
    }
}
