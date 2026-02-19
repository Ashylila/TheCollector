using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
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
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly AutoRetainerManager _autoretainerManager;
    public bool IsRunning => _pipelineRegistry.All.Any(p => p.IsRunning);
    
    
    
    public AutomationHandler(
        PlogonLog log,CollectableAutomationHandler collectableAutomationHandler, Configuration config, ScripShopAutomationHandler scripShopAutomationHandler, IChatGui chatGui, GatherbuddyReborn_IPCSubscriber gatherbuddyReborn_IPCSubscriber, ArtisanWatcher artisanWatcher, IFramework framework, FishingWatcher fishingWatcher, CraftingHandler craftingHandler, PipelineRegistry registry, AutoRetainerManager retainer)
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
        _pipelineRegistry = registry;
        _autoretainerManager = retainer;
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
        _autoretainerManager.OnRetainerFinish += OnAutoRetainerFinish;
        _autoretainerManager.OnError += OnError;
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
        if(_config.PreferredCollectableShop.TerritoryId == default)
        {
            _chatGui.PrintError("Please configure your preferred collectable shop in the settings tab!", "TheCollector");
            return;
        }
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
    public void OnAutoRetainerFinish()
    {
        if (_config.EnableAutogatherOnFinish)
        {
            _gatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(true);
        }
    }
    public void ForceStop(string reason)
    {
        _pipelineRegistry.StopAll(reason);
    }

    private void OnFinishedCollecting()
    {
        if (_config.BuyAfterEachCollect)
        {
            _scripShopAutomationHandler.Start();
            return;
        }
        if(_config.CheckForVenturesBetweenRuns && Autoretainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara())
        {
            _autoretainerManager.Start();
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
        _log.Debug(_config.CheckForVenturesBetweenRuns.ToString() + " " + Autoretainer_IPCSubscriber.GetClosestRetainerVentureSecondsRemaining(Svc.PlayerState.ContentId) );
        if (_config.CheckForVenturesBetweenRuns && Autoretainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara())
        {
            _autoretainerManager.Start();
            return;
        }
        if (_config.EnableAutogatherOnFinish)
        {
            _gatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(true);
        }
        
    }

    private void OnError(Exception ex)
    {
        _chatGui.Print($"Automation threw an error, {ex.Message}", "TheCollector");
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
        _autoretainerManager.OnError -= OnError;
        _autoretainerManager.OnRetainerFinish -= OnAutoRetainerFinish;
    }
}
