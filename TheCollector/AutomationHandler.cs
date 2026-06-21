using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using TheCollector.CollectableManager;
using TheCollector.Data;
using TheCollector.FirmamentManager;
using TheCollector.Data.Models;
using TheCollector.Data.ScripSystem;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Utility;

namespace TheCollector;

public class AutomationHandler : IDisposable
{
    private readonly PlogonLog _log;
    private readonly ScripSystemSelector _selector;
    private readonly Configuration _config;
    private readonly FirmamentCatalog _firmamentCatalog;
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
    private readonly FirmamentPlannerService _firmamentPlannerService;
    private readonly DiscordWebhookService _discord;
    private readonly CharacterBalanceTracker _balanceTracker;
    private readonly VendorCatalog _vendorCatalog;
    private readonly KupoOfFortuneHandler _kupo;
    private readonly FirmamentTurnInWindowHandler _firmamentTurnInWindow;
    public bool IsRunning => _pipelineRegistry.All.Any(p => p.IsRunning);

    public int SessionCollectablesTurnedIn { get; private set; }
    public int SessionItemsPurchased { get; private set; }
    public int SessionFullLoops { get; private set; }
    public Dictionary<uint, int> SessionScripsSpent { get; } = new();
    public Dictionary<uint, int> SessionScripsEarned { get; } = new();
    public DateTime? SessionStarted { get; private set; }

    public int SessionScripsEarnedTotal => SessionScripsEarned.Values.Sum();

    private int _consecutiveEmptyBuyCycles;
    private const int HardFailThreshold = 2;

    // True while a Kupo of Fortune run was started manually (debug/test button) rather than
    // by the turn-in loop, so its completion does not spill into the post-turn-in cascade.
    private bool _kupoStartedManually;

    public AutomationHandler(
        PlogonLog log, ScripSystemSelector selector, Configuration config, FirmamentCatalog firmamentCatalog, IChatGui chatGui, GatherbuddyReborn_IPCSubscriber gatherbuddyReborn_IPCSubscriber, ArtisanWatcher artisanWatcher, IFramework framework, FishingWatcher fishingWatcher, CraftingHandler craftingHandler, PipelineRegistry registry, AutoRetainerManager retainer, DeliverooManager deliveroo, ScripPlannerService plannerService, FirmamentPlannerService firmamentPlannerService, DiscordWebhookService discord, CharacterBalanceTracker balanceTracker, VendorCatalog vendorCatalog, KupoOfFortuneHandler kupo, FirmamentTurnInWindowHandler firmamentTurnInWindow)
    {
        _log = log;
        _gatherbuddyReborn_IPCSubscriber = gatherbuddyReborn_IPCSubscriber;
        _selector = selector;
        _config = config;
        _firmamentCatalog = firmamentCatalog;
        _chatGui = chatGui;
        _artisanWatcher = artisanWatcher;
        _framework = framework;
        _fishingWatcher = fishingWatcher;
        _craftingHandler = craftingHandler;
        _pipelineRegistry = registry;
        _autoretainerManager = retainer;
        _deliverooManager = deliveroo;
        _plannerService = plannerService;
        _firmamentPlannerService = firmamentPlannerService;
        _discord = discord;
        _balanceTracker = balanceTracker;
        _vendorCatalog = vendorCatalog;
        _kupo = kupo;
        _firmamentTurnInWindow = firmamentTurnInWindow;
    }

    public void Init()
    {
        // Distinct() because systems can share pipeline instances (Inspection reuses the
        // Firmament shop buy) — subscribing per-system would double-fire those events.
        foreach (var turnIn in _selector.All.Select(s => s.TurnIn).Distinct())
        {
            turnIn.OnError += OnError;
            turnIn.OnFinished += OnFinishedCollecting;
            turnIn.OnScripsEarned += OnScripsEarned;
        }
        foreach (var buy in _selector.All.Select(s => s.Buy).Distinct())
        {
            buy.OnError += OnError;
            buy.OnFinishedTrading += OnFinishedTrading;
        }
        _gatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged += OnAutoGatherStatusChanged;
        _artisanWatcher.OnCraftingFinished += OnFinishedWatching;
        _artisanWatcher.OnInventoryFullDuringCrafting += OnArtisanInventoryFull;
        _fishingWatcher.OnFishingFinished += OnFinishedWatching;
        _autoretainerManager.OnRetainerFinish += OnAutoRetainerFinish;
        _autoretainerManager.OnError += OnError;
        _deliverooManager.OnDeliverooFinish += OnDeliverooFinish;
        _deliverooManager.OnError += OnError;
        _kupo.OnFinishedPlaying += OnKupoFinished;
        _kupo.OnError += OnKupoError;
    }

    // Set when an autogather-triggered resource inspection should be followed by an Artisan
    // list (gather -> inspect -> craft). Consumed once at the inspection run's terminal so the
    // later post-craft turn-in doesn't restart crafting.
    private bool _pendingCraftAfterInspection;

    // When set, the loop drives this turn-in instead of the active system's. ActiveSystem stays
    // Firmament (so the buy, goal, and post-craft/inventory-full/`collect` turn-ins use the
    // appraiser); the Resource Inspection runs transiently — only after gathering or via
    // /collector inspect — through this override. Cleared whenever Invoke()/ForceStop runs the
    // active turn-in, so inspection never hijacks the other commands.
    private ITurnInPipeline? _turnInOverride;
    private ITurnInPipeline CurrentTurnIn => _turnInOverride ?? _selector.Active.TurnIn;

    private void OnAutoGatherStatusChanged(bool enabled)
    {
        if (enabled) return;
        if (_config.RunInspectionOnAutogatherFinish)
        {
            _pendingCraftAfterInspection = _config.CraftOnInspectionFinish;
            if (!InvokeInspection()) _pendingCraftAfterInspection = false;
        }
        else if (_config.CollectOnAutogatherFinish)
            Invoke();
    }
    public bool Invoke()
    {
        if (IsRunning)
        {
            _log.Debug("Automation is already running; ignoring start request.");
            return false;
        }
        if (_config.HardFailReason != null)
        {
            _chatGui.PrintError($"Automation halted: {_config.HardFailReason}. Acknowledge it in the main window before retrying.", "TheCollector");
            return false;
        }
        if (_config.ActiveSystem == ScripSystemId.Normal)
        {
            if (_config.PreferredTerritoryId == 0)
            {
                _chatGui.PrintError("Please configure your preferred shop territory in the Settings tab!", "TheCollector");
                return false;
            }
            if (!_vendorCatalog.IsReady)
            {
                _chatGui.PrintError("Still scanning vendor data — try again in a few seconds.", "TheCollector");
                return false;
            }
        }
        else if (!_firmamentCatalog.IsReady)
        {
            _chatGui.PrintError("Still scanning Firmament data — try again in a few seconds.", "TheCollector");
            return false;
        }
        if (Svc.Condition[ConditionFlag.InCombat])
        {
            _chatGui.PrintError("Cannot start automation while in combat.", "TheCollector");
            return false;
        }
        
        if (PlayerEx.IsInDuty && Svc.ClientState.TerritoryType != _firmamentCatalog.TerritoryId)
        {
            _chatGui.PrintError("Cannot start automation while in a duty.", "TheCollector");
            return false;
        }
        SessionStarted ??= DateTime.UtcNow;
        _consecutiveEmptyBuyCycles = 0;
        // A plain Invoke always runs the active system's turn-in (collect / post-craft /
        // inventory-full), so drop any lingering inspection override.
        _turnInOverride = null;
        _selector.Active.TurnIn.Start();
        return true;
    }

    // Runs the Resource Inspection as a transient turn-in. ActiveSystem is kept on Firmament so
    // the scrip cap -> buy uses the Firmament shop and every other turn-in (post-craft,
    // inventory-full, /collector collect) still goes to the appraiser.
    public bool InvokeInspection()
    {
        if (IsRunning)
        {
            _log.Debug("Automation is already running; ignoring inspection request.");
            return false;
        }
        if (_config.HardFailReason != null)
        {
            _chatGui.PrintError($"Automation halted: {_config.HardFailReason}. Acknowledge it in the main window before retrying.", "TheCollector");
            return false;
        }
        if (!_firmamentCatalog.IsReady)
        {
            _chatGui.PrintError("Still scanning Firmament data — try again in a few seconds.", "TheCollector");
            return false;
        }
        if (Svc.Condition[ConditionFlag.InCombat])
        {
            _chatGui.PrintError("Cannot start automation while in combat.", "TheCollector");
            return false;
        }
        if (PlayerEx.IsInDuty && Svc.ClientState.TerritoryType != _firmamentCatalog.TerritoryId)
        {
            _chatGui.PrintError("Cannot start automation while in a duty.", "TheCollector");
            return false;
        }
        // Inspection is Firmament content; align the persistent system so the buy/goal/appraiser flow matches.
        if (_config.ActiveSystem != ScripSystemId.Firmament)
        {
            _config.ActiveSystem = ScripSystemId.Firmament;
            _config.Save();
        }
        SessionStarted ??= DateTime.UtcNow;
        _consecutiveEmptyBuyCycles = 0;
        _turnInOverride = _selector.Inspection.TurnIn;
        _turnInOverride.Start();
        return true;
    }

    public bool InvokeBuy()
    {
        if (IsRunning)
        {
            _chatGui.PrintError("Automation is already running.", "TheCollector");
            return false;
        }
        if (_config.HardFailReason != null)
        {
            _chatGui.PrintError($"Automation halted: {_config.HardFailReason}. Acknowledge it in the main window before retrying.", "TheCollector");
            return false;
        }
        if (_config.ActiveSystem == ScripSystemId.Normal && !_vendorCatalog.IsReady)
        {
            _chatGui.PrintError("Still scanning vendor data — try again in a few seconds.", "TheCollector");
            return false;
        }
        if (_config.ActiveSystem.IsFirmamentLike() && !_firmamentCatalog.IsReady)
        {
            _chatGui.PrintError("Still scanning Firmament data — try again in a few seconds.", "TheCollector");
            return false;
        }
        SessionStarted ??= DateTime.UtcNow;
        _selector.Active.Buy.Start();
        return true;
    }

    // Standalone Kupo of Fortune run for testing — plays the minigame at Lizbeth without
    // running (or continuing into) the rest of the automation loop.
    public bool InvokeKupo()
    {
        if (IsRunning)
        {
            _chatGui.PrintError("Automation is already running.", "TheCollector");
            return false;
        }
        if (!_firmamentCatalog.IsReady)
        {
            _chatGui.PrintError("Still scanning Firmament data — try again in a few seconds.", "TheCollector");
            return false;
        }
        _kupoStartedManually = true;
        _kupo.Start();
        return true;
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

    private void OnArtisanInventoryFull()
    {
        // The watcher has already stopped Artisan and flagged the pause. If the turn-in
        // can't actually start (no shop configured, combat, duty, hard-fail), undo the
        // pause so Artisan keeps crafting and the watcher isn't stuck thinking it owns one.
        if (!Invoke())
        {
            _artisanWatcher.CancelPause();
            return;
        }
        _chatGui.Print("Inventory near full — pausing Artisan to turn collectables in.", "TheCollector");
    }


    private enum PostRunStage { ResumeArtisan, StartCraft, AutoRetainer, Deliveroo, Autogather }

    private bool TryStartStage(PostRunStage stage)
    {
        switch (stage)
        {
            case PostRunStage.ResumeArtisan:
                if (!_artisanWatcher.IsPausedByUs) return false;
                _artisanWatcher.ResumeAfterTurnIn();
                _chatGui.Print("Turn-in done — resuming Artisan list.", "TheCollector");
                return true;

            case PostRunStage.StartCraft:
                // After an autogather-triggered inspection, kick off the Artisan list once
                // (gather -> inspect -> craft). ResumeArtisan above already handled the
                // inventory-full-during-crafting case, so we only reach here for a fresh start.
                if (!_pendingCraftAfterInspection) return false;
                _pendingCraftAfterInspection = false;
                _craftingHandler.ShouldStartCrafting();
                _chatGui.Print("Resource inspection done — starting Artisan list.", "TheCollector");
                return true;

            case PostRunStage.AutoRetainer:
                if (!_config.CheckForVenturesBetweenRuns) return false;
                if (!IPCSubscriber_Common.IsReady("AutoRetainer")) return false;
                if (!Autoretainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara()) return false;
                _autoretainerManager.Start();
                return true;

            case PostRunStage.Deliveroo:
                if (!_config.CheckForDeliverooBetweenRuns) return false;
                if (!IPCSubscriber_Common.IsReady("Deliveroo")) return false;
                _deliverooManager.Start();
                return true;

            case PostRunStage.Autogather:
                if (!_config.EnableAutogatherOnFinish) return false;
                // Re-enabling autogather closes one full loop and starts the next. Count it, and
                // honour the iteration cap by stopping here instead of re-gathering (the current
                // cycle is already complete).
                SessionFullLoops++;
                var stop = _config.Stop;
                if (stop.StopOnFullLoopsEnabled && stop.MaxFullLoops > 0 && SessionFullLoops >= stop.MaxFullLoops)
                {
                    var reason = $"Reached full-loop limit ({SessionFullLoops}/{stop.MaxFullLoops}).";
                    _chatGui.Print($"Stop condition met: {reason}", "TheCollector");
                    _discord.Notify(DiscordEvent.StopCondition, $"🛑 TheCollector stopped: {reason}");
                    return true; // handled — stop without re-enabling autogather
                }
                _gatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(true);
                return true;

            default:
                return false;
        }
    }

    private void RunPostRunCascade(PostRunStage from)
    {
        for (var stage = from; stage <= PostRunStage.Autogather; stage++)
            if (TryStartStage(stage)) return;
    }

    public void OnAutoRetainerFinish() => RunPostRunCascade(PostRunStage.Deliveroo);

    public void OnDeliverooFinish() => RunPostRunCascade(PostRunStage.Autogather);
    public void ForceStop(string reason)
    {
        _pendingCraftAfterInspection = false;
        _turnInOverride = null;
        // If we're mid-turn-in on a self-pause, drop the bookkeeping (leaving Artisan
        // stopped, since everything is halting) so the watcher isn't permanently stuck.
        if (_artisanWatcher.IsPausedByUs)
            _artisanWatcher.AbandonPause();
        else
            _artisanWatcher.SuppressAutoInvoke();
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

        if (CurrentTurnIn.LastEarnedCurrency is { } earned)
        {
            var source = CurrencyHelper.GetRunSource(earned);
            if (_config.ActiveRunSource != source)
            {
                _config.ActiveRunSource = source;
                _config.Save();
            }
        }

        if (TryStopOnConditionMet()) return;

        // Drain Kupo of Fortune cards (Firmament-only) before the buy/cascade flow; it
        // resumes via ContinueAfterCollect on finish, or OnKupoError on failure.
        if (ShouldPlayKupo())
        {
            _kupoStartedManually = false;
            _kupo.Start();
            return;
        }

        ContinueAfterCollect();
    }

    private void OnKupoFinished()
    {
        // A manual test run ends here without spilling into the buy/cascade flow.
        if (_kupoStartedManually) { _kupoStartedManually = false; return; }

        // The turn-in pauses when vouchers hit the threshold; having drained them, resume
        // turning in if collectables remain (and we're not at the scrip cap). This interleaves
        // turn-in and Kupo so vouchers never pile up past the cap.
        if (!_selector.Active.TurnIn.CapReached && _selector.Active.TurnIn.HasCollectible)
        {
            _selector.Active.TurnIn.Start();
            return;
        }
        ContinueAfterCollect();
    }

    private bool ShouldPlayKupo()
    {
        // Firmament-only, gated on the voucher count sampled from the turn-in window so we
        // only detour to Lizbeth once the held vouchers reach the threshold (default = cap).
        return _config.KupoOfFortuneEnabled &&
               _selector.Active.Id == ScripSystemId.Firmament &&
               _firmamentTurnInWindow.LastVoucherCount >= _config.KupoOfFortuneThreshold;
    }

    private void OnKupoError(Exception ex)
    {
        // A failed minigame must not hard-fail the whole run — just log and carry on.
        _log.Error(ex, "Kupo of Fortune play failed; continuing with the post-turn-in flow.");
        if (_kupoStartedManually) { _kupoStartedManually = false; return; }
        ContinueAfterCollect();
    }

    private void ContinueAfterCollect()
    {
        if (CurrentTurnIn.CapReached || _config.BuyAfterEachCollect)
        {
            if (CurrentTurnIn.CapReached)
                _discord.Notify(DiscordEvent.ScripCap, "💰 TheCollector: scrip cap reached, moving to shop.");
            // Keep the override across the buy so OnFinishedTrading resumes the same turn-in.
            _selector.Active.Buy.Start();
            return;
        }
        // Turn-in fully done — drop any inspection override so the cascade and the next turn-in
        // (post-craft / inventory-full / collect) use the active system's appraiser.
        _turnInOverride = null;
        RunPostRunCascade(PostRunStage.ResumeArtisan);
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
        var activeIsFirmament = _selector.Active.Id.IsFirmamentLike();
        var activeGoal = activeIsFirmament ? _config.FirmamentGoal : _config.Goal;

        if (_config.ResetEachQuantityAfterCompletingList)
        {
            if (activeIsFirmament)
            {
                if (activeGoal.ItemsToPurchase.Count > 0 &&
                    activeGoal.ItemsToPurchase.All(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity))
                {
                    foreach (var item in activeGoal.ItemsToPurchase)
                        item.AmountPurchased = 0;
                    _config.Save();
                }
            }
            else
            {
                ResetIfAllComplete(_config.Goal.ItemsToPurchase);
            }
        }

        var goalComplete = activeIsFirmament
            ? _firmamentPlannerService.IsGoalComplete()
            : _plannerService.IsGoalComplete();
        if (activeGoal.StopGatheringWhenComplete && goalComplete)
        {
            _chatGui.Print("Purchase list complete! Stopping automation.", "TheCollector");
            _log.Debug("Goal complete — all items purchased. Stopping.");
            _discord.Notify(DiscordEvent.GoalComplete, "✅ TheCollector: purchase list complete.");
            // This run may have started from the Artisan inventory-full pause; drop that
            // bookkeeping or the watcher stays blind forever (Artisan stays stopped on purpose).
            if (_artisanWatcher.IsPausedByUs)
                _artisanWatcher.AbandonPause();
            _turnInOverride = null;
            return;
        }

        if (TryStopOnConditionMet()) return;

        if (CurrentTurnIn.HasCollectible)
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

            CurrentTurnIn.Start();
            return;
        }
        _consecutiveEmptyBuyCycles = 0;
        _turnInOverride = null;
        RunPostRunCascade(PostRunStage.ResumeArtisan);
    }

    private void OnError(Exception ex)
    {
        TripHardFail(ex.Message);
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
        foreach (var turnIn in _selector.All.Select(s => s.TurnIn).Distinct())
        {
            turnIn.OnError -= OnError;
            turnIn.OnFinished -= OnFinishedCollecting;
            turnIn.OnScripsEarned -= OnScripsEarned;
        }
        foreach (var buy in _selector.All.Select(s => s.Buy).Distinct())
        {
            buy.OnError -= OnError;
            buy.OnFinishedTrading -= OnFinishedTrading;
        }
        _gatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged -= OnAutoGatherStatusChanged;
        _artisanWatcher.OnCraftingFinished -= OnFinishedWatching;
        _artisanWatcher.OnInventoryFullDuringCrafting -= OnArtisanInventoryFull;
        _fishingWatcher.OnFishingFinished -= OnFinishedWatching;
        _autoretainerManager.OnError -= OnError;
        _autoretainerManager.OnRetainerFinish -= OnAutoRetainerFinish;
        _deliverooManager.OnDeliverooFinish -= OnDeliverooFinish;
        _deliverooManager.OnError -= OnError;
    }
}
