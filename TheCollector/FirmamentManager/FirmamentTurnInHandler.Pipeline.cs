using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ECommons;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.FirmamentManager;

public partial class FirmamentTurnInHandler
{
    private TimeSpan UiInteractDelay => TimeSpan.FromMilliseconds(_configuration.UiDelayMs);
    public string? CurrentItemName { get; private set; }

    private List<(uint itemId, string name, int left, int job)>? _queue;
    private int _currentJob = -1;

    protected override FrameRunner.Step[] BuildSteps()
    {
        CapReached = false;
        var territoryId = _catalog.TerritoryId;

        if (!HasCollectible)
        {
            Log.Debug("No Firmament collectables in inventory; skipping run.");
            return new[]
            {
                new FrameRunner.Step("SkipFirmamentRun", () => StepResult.Success(), TimeSpan.FromSeconds(1)),
            };
        }

        return new[]
        {
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step("CanActCheck",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(120)),
            new FrameRunner.Step("PrimeCheck", PrimeAndCheck, TimeSpan.FromSeconds(5)),
            new FrameRunner.Step("RouteToFirmament",
                () => MakeFirmamentRouteTick(territoryId),
                TimeSpan.FromSeconds(120),
                () => _firmamentRouteIssued = false),
            new FrameRunner.Step("WaitLifestreamDone",
                () => _lifestreamIpc.IsBusy() ? StepResult.Continue() : StepResult.Success(),
                TimeSpan.FromSeconds(30)),
            new FrameRunner.Step("WaitCanActAfterRoute",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(20)),
            FrameRunner.Delay("PostRouteBuffer", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step("MoveToAppraiser",
                MakeMoveToAppraiserTick,
                TimeSpan.FromSeconds(60),
                ResetMoveThrottle),
            new FrameRunner.Step("OpenAppraiser",
                () =>
                {
                    unsafe
                    {
                        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(FirmamentTurnInWindowHandler.AddonName, out var a) &&
                            GenericHelpers.IsAddonReady(a))
                            return StepResult.Success();
                    }
                    if (DateTime.UtcNow >= _nextInteract)
                    {
                        // Potkin shows a one-page Talk dialogue before HWDSupply opens;
                        // advance it if present, otherwise (re)interact to start the chain.
                        if (!_window.ProgressTalk())
                        {
                            VNavmesh_IPCSubscriber.Path_Stop();
                            TryInteractWithAnyAppraiser();
                        }
                        _nextInteract = DateTime.UtcNow + TimeSpan.FromMilliseconds(700);
                    }
                    return StepResult.Continue();
                },
                TimeSpan.FromSeconds(15),
                () => _nextInteract = DateTime.MinValue),
            TurnInDriver(),
            FrameRunner.Delay("PostTurnInBuffer", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("CloseAppraiser",
                () =>
                {
                    // Sample the Kupo voucher count while the window is still open so the
                    // post-turn-in flow can decide whether to play Kupo of Fortune.
                    _window.TryGetVoucherCount(out _);
                    _window.CloseWindow();
                    _targetManager.Target = null;
                    return StepResult.Success();
                },
                TimeSpan.FromSeconds(5)),
            FrameRunner.Delay("FinalDelay", TimeSpan.FromSeconds(1)),
        };
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        if (ok) OnFinishedTurnIn?.Invoke();
    }

    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        VNavmesh_IPCSubscriber.Path_Stop();
        CurrentItemName = null;
        _queue = null;
        _window.CloseWindow();
    }

    private StepResult PrimeAndCheck()
    {
        var itemSheet = _dataManager.GetExcelSheet<Item>();
        _queue = ItemHelper.GetCurrentInventoryItems()
            .Where(i => i.IsCollectable && _catalog.CrafterJobByItemId.ContainsKey(i.BaseItemId))
            .GroupBy(i => i.BaseItemId)
            .Select(g => (g.Key, itemSheet.GetRow(g.Key).Name.ExtractText(), g.Count(), _catalog.CrafterJobByItemId[g.Key]))
            .OrderBy(t => t.Item4)
            .ToList();
        CurrentItemName = null;
        LastEarnedCurrency = null;
        _currentJob = -1;

        if (_queue.Count == 0)
            return StepResult.Cancel("No eligible Firmament crafter collectables to turn in.");
        return StepResult.Success();
    }

    private FrameRunner.Step TurnInDriver() => new(
        "TurnInAllFirmament",
        ctx =>
        {
            Status.Set(PluginState.ExchangingItems);
            if (_queue == null || _queue.Count == 0) return StepResult.Success();

            var h = _queue[0];
            if (!_window.TryGetScripCount(_catalog.ScripItemId, out var current))
                return StepResult.Fail("Firmament appraiser window closed during turn-in.");
            if (current >= _catalog.HoldingCap)
            {
                Log.Debug("Skybuilders' Scrip cap reached; stopping turn-in.");
                CapReached = true;
                return StepResult.Success();
            }

            // Pause the batch when held Kupo vouchers reach the threshold so the orchestrator
            // can play them off (and we resume turning in afterwards) — otherwise vouchers
            // earned past the cap of 10 are wasted before the run ends.
            if (_configuration.KupoOfFortuneEnabled &&
                _window.TryGetVoucherCount(out var vouchers) &&
                vouchers >= _configuration.KupoOfFortuneThreshold)
            {
                Log.Debug($"Kupo voucher threshold reached ({vouchers}/{_configuration.KupoOfFortuneThreshold}); pausing turn-in to play.");
                return StepResult.Success();
            }

            var steps = new List<FrameRunner.Step>();
            if (_currentJob != h.job)
                steps.Add(SelectJobStep(h.job));
            steps.Add(SelectItemStep(h.itemId, h.name));
            steps.Add(HandOverStep());
            steps.Add(TurnInDriver());
            ctx.InjectNext(steps);
            return StepResult.Success();
        },
        TimeSpan.FromSeconds(10));

    private FrameRunner.Step SelectJobStep(int job)
    {
        var fired = false;
        DateTime until = default;
        return new FrameRunner.Step("SelectJob",
            () =>
            {
                if (!fired)
                {
                    _window.SelectJob(job);
                    _currentJob = job;
                    CurrentItemName = null;
                    until = DateTime.UtcNow + UiInteractDelay;
                    fired = true;
                }
                return DateTime.UtcNow >= until ? StepResult.Success() : StepResult.Continue();
            },
            TimeSpan.FromSeconds(10));
    }

    private FrameRunner.Step SelectItemStep(uint itemId, string name)
    {
        var fired = false;
        DateTime until = default;
        return new FrameRunner.Step("SelectItem",
            () =>
            {
                if (!fired)
                {
                    if (!_window.SelectItem(itemId))
                        return StepResult.Fail($"Could not select Firmament item {itemId}.");
                    CurrentItemName = name;
                    until = DateTime.UtcNow + UiInteractDelay;
                    fired = true;
                }
                return DateTime.UtcNow >= until ? StepResult.Success() : StepResult.Continue();
            },
            TimeSpan.FromSeconds(10));
    }

    private FrameRunner.Step HandOverStep()
    {
        uint scripsBefore = 0;
        var captured = false;
        var handedOver = false;
        DateTime nextAction = DateTime.MinValue;
        DateTime settle = default;
        return new FrameRunner.Step("HandOverFirmament",
            () =>
            {
                if (!handedOver)
                {
                    if (!_window.IsRequestOpen)
                        return StepResult.Continue();

                    if (!captured)
                    {
                        _window.TryGetScripCount(_catalog.ScripItemId, out scripsBefore);
                        captured = true;
                    }

                    if (DateTime.UtcNow < nextAction)
                        return StepResult.Continue();

                    if (_window.HandOverEnabled)
                    {
                        _window.HandOver();
                        handedOver = true;
                        LastEarnedCurrency = _catalog.ScripItemId;
                        settle = DateTime.UtcNow + UiInteractDelay;
                        return StepResult.Continue();
                    }

                    if (_window.IsPickerOpen)
                        _window.SelectFirstPickerEntry();
                    else
                        _window.OpenCollectablePicker();
                    nextAction = DateTime.UtcNow + UiInteractDelay;
                    return StepResult.Continue();
                }

                _window.ConfirmYesNo();
                if (DateTime.UtcNow < settle) return StepResult.Continue();

                if (!_window.TryGetScripCount(_catalog.ScripItemId, out var current) || current <= scripsBefore)
                    return StepResult.Continue();

                OnScripsEarned?.Invoke(_catalog.ScripItemId, (int)(current - scripsBefore));

                var h = _queue![0];
                h.left--;
                if (h.left <= 0) { _queue.RemoveAt(0); CurrentItemName = null; }
                else _queue[0] = h;
                return StepResult.Success();
            },
            TimeSpan.FromSeconds(30));
    }

    private bool _firmamentRouteIssued;
    private DateTime _nextInteract;

    private StepResult MakeFirmamentRouteTick(uint territoryId)
        => FirmamentRouting.RouteTick(_clientState, _lifestreamIpc, Status, territoryId, ref _firmamentRouteIssued);

    private StepResult MakeMoveToAppraiserTick()
    {
        Status.Set(PluginState.MovingToCollectableVendor, "to the Firmament appraiser");
        return MoveTowardsTick(FirmamentRouting.LivePosition(_catalog.AppraiserDataIds, _catalog.AppraiserPosition));
    }
}
