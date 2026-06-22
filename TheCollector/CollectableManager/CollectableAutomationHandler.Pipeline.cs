using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Ipc;

namespace TheCollector.CollectableManager;

using System;
using System.Linq;
using System.Numerics;
using TheCollector.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons;
using ECommons.GameHelpers;

public partial class CollectableAutomationHandler : FrameRunnerPipelineBase
{
    public override string Key => AddonDelays.Collectables;
    private Dictionary<uint, CollectablesShopItem> _collectableByItemId = new();
    private const int ScripCap = 4000;
    private TimeSpan UiInteractDelay => TimeSpan.FromMilliseconds(_configuration.GetUiDelayMs(Key));
    public string? CurrentItemName { get; private set; }
    public uint? LastEarnedCurrency { get; private set; }
    private int _currentJobIndex = int.MinValue;

    public bool ScripCapReached { get; private set; }

    private bool _windowAlreadyOpen;

    public void StartFromOpenWindow()
    {
        if (IsRunning) return;
        _windowAlreadyOpen = true;
        Start();
    }

    protected override FrameRunner.Step[] BuildSteps()
    {
        ScripCapReached = false;

        if (_windowAlreadyOpen)
        {
            return new[]
            {
                new FrameRunner.Step("CollectableCheck", CollectableCheck, TimeSpan.FromSeconds(5), PrimeTurnIn),
                new FrameRunner.Step(
                    "WaitCollectablesReady",
                    () => Addons.Ready("CollectablesShop") ? StepResult.Success() : StepResult.Continue(),
                    TimeSpan.FromSeconds(5)),
                TurnInDriver(),
            };
        }

        var territoryId = _configuration.PreferredTerritoryId;
        var vendor = _vendorCatalog.GetCollectableVendor(territoryId);
        var target = vendor?.Position ?? Vector3.Zero;

        if (!HasCollectible)
        {
            Log.Debug("No collectables in inventory; skipping collectable run.");
            return new[]
            {
                new FrameRunner.Step("SkipCollectableRun", () => StepResult.Success(), TimeSpan.FromSeconds(1)),
            };
        }

        var steps = new[]
        {
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step(
                "CanActCheck",
                () => PlayerEx.CanAct ?  StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(120)),
            new FrameRunner.Step(
                "CollectableCheck",
                CollectableCheck,
                TimeSpan.FromSeconds(5),
                PrimeTurnIn),
            new FrameRunner.Step(
                "TeleportToPreferredShop",
                () => MakeTeleportTick(territoryId),
                TimeSpan.FromSeconds(20),
                () => _teleportAttempted = false
            ),

            new FrameRunner.Step(
                "WaitCanActAfterTeleport",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(20)
            ),
            new FrameRunner.Step(
                "LifestreamCheck",
                () => LifestreamCheck(),
                TimeSpan.FromSeconds(1)
            ),
            new FrameRunner.Step(
                "WaitForLifestream",
                () => WaitForLifestream(),
                TimeSpan.FromSeconds(30)
            ),
            FrameRunner.Delay("PostLifestreamBuffer", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step(
                "CanActCheck",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(10)
                ),
            new FrameRunner.Step(
                "MoveToPreferredShop",
                () => MakeMoveTick(target),
                TimeSpan.FromSeconds(60),
                ResetMoveThrottle
            ),

            new FrameRunner.Step(
                "OpenCollectablesShop",
                () => StepResult.Success(),
                TimeSpan.FromSeconds(2),
                () => OpenShop()
            ),

            new FrameRunner.Step(
                "WaitCollectablesReady",
                () => Addons.Ready("CollectablesShop") ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(5)
            ),

            TurnInDriver(),
            FrameRunner.Delay("PostTurnInBuffer", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step(
                "CloseCollectablesShop",
                () =>
                {
                    _collectibleWindowHandler.CloseWindow();
                    _targetManager.Target = null;
                    return StepResult.Success();
                },
                TimeSpan.FromSeconds(5)
            ),
            FrameRunner.Delay("FinalDelay", TimeSpan.FromSeconds(1)),
        };

        return steps;
    }
    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        var wasWindowRun = _windowAlreadyOpen;
        _windowAlreadyOpen = false;
        if (ok && !wasWindowRun) OnFinishCollecting?.Invoke();
    }
    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        VNavmesh_IPCSubscriber.Path_Stop();
        CurrentItemName = string.Empty;
        TurnInQueue = null;
        if (!_windowAlreadyOpen)
            _collectibleWindowHandler.CloseWindow();
        _windowAlreadyOpen = false;
    }
    private bool _teleportAttempted;
    private StepResult MakeTeleportTick(uint territoryId)
    {
        var terSheet = _dataManager.GetExcelSheet<TerritoryType>();
        var destName = terSheet.GetRow(territoryId).PlaceName.Value.Name.ExtractText();
        Status.Set(PluginState.Teleporting, $"to {destName}");

        if (_clientState.TerritoryType == territoryId || TerritoryRouting.RequiresAethernet(territoryId))
            return StepResult.Success();

        if (!_teleportAttempted)
        {
            var vendor = _vendorCatalog.GetCollectableVendor(territoryId);
            var anchor = vendor?.Position ?? System.Numerics.Vector3.Zero;
            if (TeleportHelper.TryFindAetheryteForTerritory(territoryId, anchor, out var aetheryte, out _))
            {
                TeleportHelper.Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
                _teleportAttempted = true;
            }
            else
            {
                return StepResult.Fail("Couldn't find an Aetheryte to teleport to");
            }
        }

        // The territory check at the top runs every tick and flips to Success once the
        // teleport lands; place names aren't unique across territories, so don't compare them.
        return StepResult.Continue();
    }

    private StepResult MakeMoveTick(Vector3 destination)
    {
        Status.Set(PluginState.MovingToCollectableVendor, $"at ({destination.X:0}, {destination.Y:0}, {destination.Z:0})");
        return MoveTowardsTick(destination);
    }

    private bool IsNearShop(Vector3 destination)
    {
        var territoryId = _configuration.PreferredTerritoryId;
        if (_clientState.TerritoryType != territoryId) return false;
        return Player.DistanceTo(destination) <= 40f;
    }

    private StepResult LifestreamCheck()
    {
        var territoryId = _configuration.PreferredTerritoryId;
        var vendor = _vendorCatalog.GetCollectableVendor(territoryId);
        if (vendor != null && IsNearShop(vendor.Position)) return StepResult.Success();

        if (TerritoryRouting.TryGet(territoryId, out var route))
            _lifestreamIpc.ExecuteCommand($"debug TaskAetheryteAethernetTeleport {route.RootAetheryteId} {route.AethernetId}");

        return StepResult.Success();
    }

    private StepResult WaitForLifestream()
    {
        if (TerritoryRouting.RequiresAethernet(_configuration.PreferredTerritoryId) && _lifestreamIpc.IsBusy())
            return StepResult.Continue();
        return StepResult.Success();
    }
    public List<(CollectablesShopItem item, string name, int left, int jobIndex)>? TurnInQueue { get; private set; } = null;

    private void PrimeTurnIn()
    {
        TurnInQueue = ItemHelper.GetLuminaItemsFromInventory()
                                .Where(i => i.IsCollectable && _collectableByItemId.ContainsKey(i.RowId))
                                .GroupBy(i => i.RowId)
                                .Where(g => _collectableByItemId[g.Key].CollectablesShopRewardScrip.ValueNullable is not null)
                                .Select(g => (_collectableByItemId[g.Key], _collectableByItemId[g.Key].Item.Value.Name.ExtractText(), g.Count(), int.MinValue))
                                .ToList();


        for (var i = 0; i < TurnInQueue.Count; i++)
        {
            var item = TurnInQueue[i];
            var jobId = ItemJobResolver.GetJobIdForItem(item.name, _dataManager);
            if (jobId != -1)
            {
                item.jobIndex = jobId;
                TurnInQueue[i] = item;
            }
        }
        CurrentItemName = null;
        LastEarnedCurrency = null;
        _currentJobIndex = int.MinValue;
    }
    private FrameRunner.Step TurnInDriver() => new(
        "TurnInAllCollectables",
        ctx =>
        {
            Status.Set(PluginState.ExchangingItems);
            if (TurnInQueue == null || TurnInQueue.Count == 0)
                return StepResult.Success();

            var h = TurnInQueue[0];

            var currencyId = h.item.CollectablesShopRewardScrip.Value.Currency;
            if (!_collectibleWindowHandler.TryGetScripCount(currencyId, out var current))
                return StepResult.Fail("Collectables window closed during turn-in.");
            var remaining = ScripCap - (int)current;
            if (h.item.CollectablesShopRewardScrip.Value.HighReward > remaining)
            {
                Log.Debug($"Scrip cap reached for currency {currencyId}; skipping {h.name}");
                ScripCapReached = true;
                TurnInQueue.RemoveAt(0);
                ctx.InjectNext(TurnInDriver());
                return StepResult.Success();
            }

            var steps = new List<FrameRunner.Step>();
            if (h.jobIndex != int.MinValue && _currentJobIndex != h.jobIndex)
                steps.Add(SelectJobStep(h.jobIndex));
            if (!string.Equals(CurrentItemName, h.name, StringComparison.Ordinal))
                steps.Add(SelectItemStep(h.name));
            steps.Add(SubmitItemStep(currencyId));
            steps.Add(TurnInDriver());

            ctx.InjectNext(steps);
            return StepResult.Success();
        },
        TimeSpan.FromSeconds(10));

    private FrameRunner.Step SelectJobStep(int jobIndex)
        => FireAndSettle(
            "SelectJob",
            () =>
            {
                _collectibleWindowHandler.SelectJob((uint)jobIndex);
                _currentJobIndex = jobIndex;
            },
            UiInteractDelay,
            TimeSpan.FromSeconds(5));

    private FrameRunner.Step SelectItemStep(string name)
    {
        var fired = false;
        DateTime until = default;
        return new FrameRunner.Step(
            "SelectItem",
            () =>
            {
                if (!fired)
                {
                    if (!_collectibleWindowHandler.SelectItem(name))
                        return StepResult.Fail($"Could not select item {name}");
                    CurrentItemName = name;
                    until = DateTime.UtcNow + UiInteractDelay;
                    fired = true;
                }
                return DateTime.UtcNow >= until ? StepResult.Success() : StepResult.Continue();
            },
            TimeSpan.FromSeconds(10));
    }

    private FrameRunner.Step SubmitItemStep(uint currencyId)
    {
        var submitted = false;
        uint scripsBefore = 0;
        DateTime until = default;
        return new FrameRunner.Step(
            "SubmitItem",
            () =>
            {
                if (!submitted)
                {
                    if (!_collectibleWindowHandler.TryGetScripCount(currencyId, out scripsBefore))
                        return StepResult.Fail("Collectables window closed before submitting.");
                    _collectibleWindowHandler.SubmitItem();
                    LastEarnedCurrency = CurrencyHelper.SpecialIdToItemId(currencyId);
                    until = DateTime.UtcNow + UiInteractDelay;
                    submitted = true;
                    return StepResult.Continue();
                }

                if (DateTime.UtcNow < until)
                    return StepResult.Continue();

                // Wait for the server to apply the trade, then credit the scrips actually
                // received — the tier may pay Mid/Low, and SubmitItem silently no-ops when
                // the addon isn't ready. Timing out fails the run instead of booking
                // phantom progress.
                if (!_collectibleWindowHandler.TryGetScripCount(currencyId, out var current) ||
                    current <= scripsBefore)
                    return StepResult.Continue();

                OnScripsEarned?.Invoke(LastEarnedCurrency.Value, (int)(current - scripsBefore));

                // TurnInQueue[0] is stable across this item's UI steps; re-read and write back.
                var h = TurnInQueue![0];
                h.left--;
                if (h.left <= 0)
                {
                    TurnInQueue.RemoveAt(0);
                    CurrentItemName = null;
                }
                else
                {
                    TurnInQueue[0] = h;
                }
                return StepResult.Success();
            },
            TimeSpan.FromSeconds(10));
    }

    private StepResult CollectableCheck()
    {
        // PrimeTurnIn has run by now; if nothing in the inventory is actually
        // turn-in eligible, cancel instead of teleporting to the shop for nothing.
        if (TurnInQueue == null || TurnInQueue.Count == 0)
            return StepResult.Cancel("No eligible collectables to turn in.");
        return StepResult.Success();
    }
}


