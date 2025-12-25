using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using TheCollector.CollectableManager;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Utility;

namespace TheCollector;

public class AutomationHandler : IDisposable
{
    private readonly PlogonLog _log;
    private readonly CollectableAutomationHandler _collectableAutomationHandler;
    private readonly Configuration _config;
    private readonly ScripShopAutomationHandler _scripShopAutomationHandler;
    private readonly IChatGui _chatGui;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyReborn_IPCSubscriber;
    private readonly ArtisanWatcher _artisanWatcher;
    private readonly IFramework _framework;
    private readonly FishingWatcher _fishingWatcher;
    private readonly CraftingHandler  _craftingHandler;
    public bool IsRunning => _collectableAutomationHandler.IsRunning || _scripShopAutomationHandler.IsRunning;
    
    
    
    public AutomationHandler(
        PlogonLog log,CollectableAutomationHandler collectableAutomationHandler, Configuration config, ScripShopAutomationHandler scripShopAutomationHandler, IChatGui chatGui, GatherbuddyReborn_IPCSubscriber gatherbuddyReborn_IPCSubscriber, ArtisanWatcher artisanWatcher, IFramework framework, FishingWatcher fishingWatcher, CraftingHandler craftingHandler)
    {
        _log = log;
        _gatherbuddyReborn_IPCSubscriber = gatherbuddyReborn_IPCSubscriber;
        _collectableAutomationHandler = collectableAutomationHandler;
        _config = config;
        _scripShopAutomationHandler = scripShopAutomationHandler;
        _chatGui = chatGui;
        _artisanWatcher = artisanWatcher;
        _framework = framework;
        _fishingWatcher = fishingWatcher;
        _craftingHandler = craftingHandler;
    }

    public void Init()
    {
        _collectableAutomationHandler.OnScripsCapped += OnScripCapped;
        _collectableAutomationHandler.OnError += OnError;
        _collectableAutomationHandler.OnFinishCollecting += OnFinishedCollecting;
        _scripShopAutomationHandler.OnError += OnError;
        _scripShopAutomationHandler.OnFinishedTrading += OnFinishedTrading;
        _gatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged += OnAutoGatherStatusChanged;
        _artisanWatcher.OnCraftingFinished += OnFinishedWatching;
        _fishingWatcher.OnFishingFinished += OnFinishedWatching;
    }

    private void OnAutoGatherStatusChanged(bool enabled)
    {
        if (_config.ShouldCraftOnAutogatherChanged && !enabled)
        {
            _craftingHandler.ShouldStartCrafting();
        }
    }
    public void Invoke()
    {
        _collectableAutomationHandler.Start();
    }

    public void OnFinishedWatching(WatchType watchType)
    {
        switch (watchType)
        {
            case WatchType.Crafting:
                if(_config.CollectOnFinishCraftingList) Invoke();
                break;
            case WatchType.Fishing:
                if(_config.CollectOnFinishedFishing) Invoke();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(watchType), watchType, null);
        }
    }

    public void ForceStop(string reason)
    {
        if (_collectableAutomationHandler.IsRunning)
        {
            _collectableAutomationHandler.ForceStop(reason);
        }

        if (_scripShopAutomationHandler.IsRunning)
        {
            _scripShopAutomationHandler.ForceStop(reason);
        }
    }

    private void OnFinishedCollecting()
    {
        if (_config.BuyAfterEachCollect)
        {
            _scripShopAutomationHandler.Start();
            return;
        }
        if (_config.EnableAutogatherOnFinish){
            _gatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(true);
            return;
        }
    }
    private void OnFinishedTrading()
    {
        if (_config.ResetEachQuantityAfterCompletingList)
            ResetIfAllComplete(_config.ItemsToPurchase);
        if (_collectableAutomationHandler.HasCollectible)
        {
            _collectableAutomationHandler.Start();
            return;
        }
        if (_config.EnableAutogatherOnFinish)
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
    bool ResetIfAllComplete(IList<ItemToPurchase> items)
    {
        if (items == null || items.Count == 0) return false;

        for (int i = 0; i < items.Count; i++)
            if (items[i].AmountPurchased < items[i].Quantity) return false;

        for (int i = 0; i < items.Count; i++)
            items[i].AmountPurchased = 0;
        _config.Save();
        _log.Debug("Reset all quantities since the list is complete.");
        return true;
    }


    public void Dispose()
    {
        _collectableAutomationHandler.OnError -= OnError;
        _scripShopAutomationHandler.OnError -= OnError;
        _scripShopAutomationHandler.OnFinishedTrading -= OnFinishedTrading;
        _gatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged -= OnAutoGatherStatusChanged;
        _artisanWatcher.OnCraftingFinished -= OnFinishedWatching;
        _fishingWatcher.OnFishingFinished -= OnFinishedWatching;
        _collectableAutomationHandler.OnScripsCapped -= OnScripCapped;
        _collectableAutomationHandler.OnFinishCollecting -= OnFinishedCollecting;
    }
}
