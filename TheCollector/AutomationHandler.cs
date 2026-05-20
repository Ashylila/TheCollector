using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using TheCollector.CollectableManager;
using TheCollector.Data;
using TheCollector.Data.Models;
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
    private readonly CraftingHandler _craftingHandler;
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly AutoRetainerManager _autoretainerManager;
    private readonly DeliverooManager _deliverooManager;
    private readonly ScripPlannerService _plannerService;
    private readonly DiscordWebhookService _discord;
    private readonly CharacterBalanceTracker _balanceTracker;
    public bool IsRunning => _pipelineRegistry.All.Any(p => p.IsRunning);

    public int SessionCollectablesTurnedIn { get; private set; }
    public int SessionItemsPurchased { get; private set; }
    public Dictionary<uint, int> SessionScripsSpent { get; } = new();
    public Dictionary<uint, int> SessionScripsEarned { get; } = new();
    public DateTime? SessionStarted { get; private set; }

    public int SessionScripsEarnedTotal => SessionScripsEarned.Values.Sum();

    private int _consecutiveEmptyBuyCycles;
    private const int HardFailThreshold = 2;

    public AutomationHandler(
        PlogonLog log, CollectableAutomationHandler collectableAutomationHandler, Configuration config, ScripShopAutomationHandler scripShopAutomationHandler, IChatGui chatGui, GatherbuddyReborn_IPCSubscriber gatherbuddyReborn_IPCSubscriber, ArtisanWatcher artisanWatcher, IFramework framework, FishingWatcher fishingWatcher, CraftingHandler craftingHandler, PipelineRegistry registry, AutoRetainerManager retainer, DeliverooManager deliveroo, ScripPlannerService plannerService, DiscordWebhookService discord, CharacterBalanceTracker balanceTracker)
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
        _deliverooManager = deliveroo;
        _plannerService = plannerService;
        _discord = discord;
        _balanceTracker = balanceTracker;
    }

    public void Init()
    {
        _collectableAutomationHandler.OnScripsCapped += OnScripCapped;
        _collectableAutomationHandler.OnError += OnError;
        _collectableAutomationHandler.OnFinishCollecting += OnFinishedCollecting;
        _collectableAutomationHandler.OnScripsEarned += OnScripsEarned;
        _scripShopAutomationHandler.OnError += OnError;
        _scripShopAutomationHandler.OnFinishedTrading += OnFinishedTrading;
        _gatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged += OnAutoGatherStatusChanged;
        _artisanWatcher.OnCraftingFinished += OnFinishedWatching;
        _fishingWatcher.OnFishingFinished += OnFinishedWatching;
        _autoretainerManager.OnRetainerFinish += OnAutoRetainerFinish;
        _autoretainerManager.OnError += OnError;
        _deliverooManager.OnDeliverooFinish += OnDeliverooFinish;
        _deliverooManager.OnError += OnError;
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
        if (_config.HardFailReason != null)
        {
            _chatGui.PrintError($"Automation halted: {_config.HardFailReason}. Acknowledge it in the main window before retrying.", "TheCollector");
            return;
        }
        if (_config.PreferredCollectableShop.TerritoryId == default)
        {
            _chatGui.PrintError("Please configure your preferred collectable shop in the settings tab!", "TheCollector");
            return;
        }
        if (PlayerHelper.InCombat)
        {
            _chatGui.PrintError("Cannot start automation while in combat.", "TheCollector");
            return;
        }
        if (PlayerHelper.IsInDuty)
        {
            _chatGui.PrintError("Cannot start automation while in a duty.", "TheCollector");
            return;
        }
        SessionStarted ??= DateTime.UtcNow;
        _consecutiveEmptyBuyCycles = 0;
        _collectableAutomationHandler.Start();
    }

    public void AcknowledgeHardFail()
    {
        if (_config.HardFailReason == null) return;
        _config.HardFailReason = null;
        _config.Save();
        _consecutiveEmptyBuyCycles = 0;
    }

    private void TripHardFail(string reason)
    {
        if (_config.HardFailReason != null) return;
        _config.HardFailReason = reason;
        _config.Save();
        _chatGui.PrintError($"Automation stopped: {reason}", "TheCollector");
        _discord.Notify(DiscordEvent.HardFail, $"❌ TheCollector hard-failed: {reason}");
        ForceStop(reason);
    }

    public void OnFinishedWatching(WatchType watchType)
    {
        switch (watchType)
        {
            case WatchType.Crafting:
                if (_config.CollectOnFinishCraftingList) Invoke();
                break;
            case WatchType.Fishing:
                if (_config.CollectOnFinishedFishing) Invoke();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(watchType), watchType, null);
        }
    }
    public void OnAutoRetainerFinish()
    {
        if (_config.CheckForDeliverooBetweenRuns
            && IPCSubscriber_Common.IsReady("Deliveroo"))
        {
            _deliverooManager.Start();
            return;
        }
        if (_config.EnableAutogatherOnFinish)
        {
            _gatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(true);
        }
    }

    public void OnDeliverooFinish()
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

    private void OnScripsEarned(uint currencyItemId, int amount)
    {
        if (amount <= 0) return;
        SessionScripsEarned.TryGetValue(currencyItemId, out var prev);
        SessionScripsEarned[currencyItemId] = prev + amount;
    }

    private string? EvaluateStopConditions()
    {
        var cond = _config.Stop;

        if (cond.StopOnScripsEarnedEnabled && cond.MaxScripsEarned > 0 &&
            SessionScripsEarnedTotal >= cond.MaxScripsEarned)
            return $"Reached scrips-earned limit ({SessionScripsEarnedTotal:N0}/{cond.MaxScripsEarned:N0}).";

        if (cond.StopOnBuyCyclesEnabled && cond.MaxBuyCycles > 0 &&
            SessionItemsPurchased >= cond.MaxBuyCycles)
            return $"Reached buy-cycle limit ({SessionItemsPurchased}/{cond.MaxBuyCycles}).";

        if (cond.StopOnSessionTimeEnabled && cond.MaxSessionMinutes > 0 && SessionStarted is { } start)
        {
            var elapsed = DateTime.UtcNow - start;
            if (elapsed.TotalMinutes >= cond.MaxSessionMinutes)
                return $"Reached session-time limit ({(int)elapsed.TotalMinutes}m/{cond.MaxSessionMinutes}m).";
        }

        return null;
    }

    private bool TryStopOnConditionMet()
    {
        var reason = EvaluateStopConditions();
        if (reason == null) return false;
        _chatGui.Print($"Stop condition met: {reason}", "TheCollector");
        _discord.Notify(DiscordEvent.StopCondition, $"🛑 TheCollector stopped: {reason}");
        ForceStop(reason);
        return true;
    }

    private void OnFinishedCollecting()
    {
        SessionCollectablesTurnedIn++;
        _balanceTracker.SampleNow();

        if (_collectableAutomationHandler.LastEarnedCurrency is { } earned)
        {
            var source = CurrencyHelper.GetRunSource(earned);
            if (_config.ActiveRunSource != source)
            {
                _config.ActiveRunSource = source;
                _config.Save();
            }
        }

        if (TryStopOnConditionMet()) return;

        if (_config.BuyAfterEachCollect)
        {
            _scripShopAutomationHandler.Start();
            return;
        }
        if (_config.CheckForVenturesBetweenRuns
            && IPCSubscriber_Common.IsReady("AutoRetainer")
            && Autoretainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara())
        {
            _autoretainerManager.Start();
            return;
        }
        if (_config.CheckForDeliverooBetweenRuns
            && IPCSubscriber_Common.IsReady("Deliveroo"))
        {
            _deliverooManager.Start();
            return;
        }

        if (_config.EnableAutogatherOnFinish)
        {
            _gatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(true);
            return;
        }
    }
    private void OnFinishedTrading(Dictionary<uint, int> scripsSpent)
    {
        SessionItemsPurchased++;
        _balanceTracker.SampleNow();
        int totalSpent = 0;
        foreach (var (currencyId, amount) in scripsSpent)
        {
            SessionScripsSpent.TryGetValue(currencyId, out var prev);
            SessionScripsSpent[currencyId] = prev + amount;

            _config.TotalScripsSpent.TryGetValue(currencyId, out var totalPrev);
            _config.TotalScripsSpent[currencyId] = totalPrev + amount;
            totalSpent += amount;
        }
        _config.Save();
        if (_config.ResetEachQuantityAfterCompletingList)
            ResetIfAllComplete(_config.Goal.ItemsToPurchase);
        if (_config.Goal.StopGatheringWhenComplete && _plannerService.IsGoalComplete())
        {
            _chatGui.Print("Purchase list complete! Stopping automation.", "TheCollector");
            _log.Debug("Goal complete — all items purchased. Stopping.");
            _discord.Notify(DiscordEvent.GoalComplete, "✅ TheCollector: purchase list complete.");
            return;
        }

        if (TryStopOnConditionMet()) return;

        if (_collectableAutomationHandler.HasCollectible)
        {
            if (totalSpent == 0)
            {
                _consecutiveEmptyBuyCycles++;
                if (_consecutiveEmptyBuyCycles >= HardFailThreshold)
                {
                    TripHardFail("Scrip-cap recovery spent nothing twice in a row — purchase list cannot drain the current currency.");
                    return;
                }
            }
            else
            {
                _consecutiveEmptyBuyCycles = 0;
            }

            _collectableAutomationHandler.Start();
            return;
        }
        _consecutiveEmptyBuyCycles = 0;
        if (_config.CheckForVenturesBetweenRuns
            && IPCSubscriber_Common.IsReady("AutoRetainer")
            && Autoretainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara())
        {
            _autoretainerManager.Start();
            return;
        }
        if (_config.CheckForDeliverooBetweenRuns
            && IPCSubscriber_Common.IsReady("Deliveroo"))
        {
            _deliverooManager.Start();
            return;
        }
        if (_config.EnableAutogatherOnFinish)
        {
            _gatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(true);
        }

    }

    private void OnError(Exception ex)
    {
        _chatGui.PrintError($"Automation threw an error: {ex.Message}", "TheCollector");
    }
    private void OnScripCapped(bool capped)
    {
        if (capped)
        {
            _discord.Notify(DiscordEvent.ScripCap, "💰 TheCollector: scrip cap reached, moving to shop.");
            _scripShopAutomationHandler.Start();
        }
    }
    bool ResetIfAllComplete(IList<ItemToPurchase> items)
    {
        if (items == null || items.Count == 0) return false;

        var activeSource = _config.ActiveRunSource;
        var subset = items
            .Where(i => CurrencyHelper.GetRunSource(CurrencyHelper.GetCurrencyIdForItem(i.Item.ItemId)) == activeSource)
            .ToList();
        if (subset.Count == 0) return false;

        if (subset.Any(i => i.AmountPurchased < i.Quantity)) return false;

        foreach (var item in subset)
            item.AmountPurchased = 0;
        _config.Save();
        _log.Debug("Reset all quantities for the active source since its list is complete.");
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
        _collectableAutomationHandler.OnScripsEarned -= OnScripsEarned;
        _autoretainerManager.OnError -= OnError;
        _autoretainerManager.OnRetainerFinish -= OnAutoRetainerFinish;
        _deliverooManager.OnDeliverooFinish -= OnDeliverooFinish;
        _deliverooManager.OnError -= OnError;
    }
}
