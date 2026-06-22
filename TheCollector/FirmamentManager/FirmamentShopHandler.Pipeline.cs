using System;
using System.Collections.Generic;
using System.Linq;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.FirmamentManager;

public partial class FirmamentShopHandler
{
    private TimeSpan UiInteractDelay => TimeSpan.FromMilliseconds(_configuration.GetUiDelayMs(Key));
    private bool _attemptedTarget;

    private List<(int remaining, int cost, uint itemId, bool isEquippable)> _buyQueue = new();
    private readonly Dictionary<uint, int> _scripsSpentThisCycle = new();

    protected override FrameRunner.Step[] BuildSteps()
    {
        _attemptedTarget = false;

        return new[]
        {
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("RouteToFirmament",
                () => MakeFirmamentRouteTick(_catalog.TerritoryId),
                TimeSpan.FromSeconds(120),
                () => _firmamentRouteIssued = false),
            new FrameRunner.Step("WaitLifestreamDone",
                () => _lifestreamIpc.IsBusy() ? StepResult.Continue() : StepResult.Success(),
                TimeSpan.FromSeconds(30)),
            new FrameRunner.Step("WaitCanActAfterRoute",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(20)),
            FrameRunner.Delay("PostRouteBuffer", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step("MoveToEnie",
                MakeMoveToEnieTick,
                TimeSpan.FromSeconds(60),
                () => { Status.Set(PluginState.SpendingScrip); ResetMoveThrottle(); }),
            new FrameRunner.Step("TargetEnie", TargetShop, TimeSpan.FromSeconds(5)),
            FrameRunner.Delay("PostTargetDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("OpenEnieShop",
                () => StepResult.Success(),
                TimeSpan.FromSeconds(2),
                () => _window.OpenShop()),
            new FrameRunner.Step("WaitShopReady",
                () => _window.IsReady ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(10)),
            new FrameRunner.Step("PrimeBuy", () => StepResult.Success(), TimeSpan.FromSeconds(2), PrimeBuy),
            BuyDriver(),
            FrameRunner.Delay("PostBuyDelay", TimeSpan.FromMilliseconds(600)),
            new FrameRunner.Step("CloseEnieShop",
                () => { _window.CloseShop(); return StepResult.Success(); },
                TimeSpan.FromSeconds(2)),
        };
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        if (ok) OnFinishedTrading?.Invoke(_scripsSpentThisCycle);
    }

    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        VNavmesh_IPCSubscriber.Path_Stop();
        _window.CloseShop();
    }

    private void PrimeBuy()
    {
        var costByItem = _catalog.Wares.ToDictionary(w => w.ItemId, w => (int)w.Cost);
        _buyQueue =
            (from i in _configuration.FirmamentGoal.ItemsToPurchase
             where i.Quantity > 0 && (i.Quantity - i.AmountPurchased) > 0
             where costByItem.ContainsKey(i.Item.ItemId)
             select (
                 remaining: i.Quantity - i.AmountPurchased,
                 cost: costByItem[i.Item.ItemId],
                 itemId: i.Item.ItemId,
                 isEquippable: i.Item.Item.EquipSlotCategory.RowId != 0
             )).ToList();
        _scripsSpentThisCycle.Clear();
    }

    private FrameRunner.Step BuyDriver() => new(
        "BuyConfigured",
        ctx =>
        {
            if (_buyQueue.Count == 0) return StepResult.Success();
            var h = _buyQueue[0];

            if (!_window.TryGetScripCount(_catalog.ScripItemId, out var scrips))
                return StepResult.Fail("Enie shop window closed while buying.");

            var spendable = Math.Max(0, (int)scrips - _configuration.ReserveScripAmount);
            var maxByScrip = h.cost > 0 ? spendable / h.cost : h.remaining;
            var amount = h.isEquippable ? Math.Min(1, maxByScrip) : Math.Min(h.remaining, Math.Min(maxByScrip, 99));

            if (amount <= 0)
            {
                _buyQueue.RemoveAt(0);
                ctx.InjectNext(BuyDriver());
                return StepResult.Success();
            }
            if (ItemHelper.GetFreeInventorySlots() <= 0)
                return StepResult.Fail("No free inventory slots available for purchases.");

            ctx.InjectNext(
                SelectItemStep(h.itemId, amount),
                UiStep("ConfirmPurchaseDialog", () => _window.ConfirmPurchaseDialog()),
                UiStep("ConfirmYesNo", () => _window.ConfirmYesNo()),
                RecordPurchaseStep(h.itemId, h.cost, amount, scrips),
                BuyDriver());
            return StepResult.Success();
        },
        TimeSpan.FromSeconds(5));

    private FrameRunner.Step UiStep(string name, Action act)
        => FireAndSettle(name, act, UiInteractDelay, UiInteractDelay + TimeSpan.FromSeconds(5));

    private FrameRunner.Step SelectItemStep(uint itemId, int amount)
    {
        var selected = false;
        DateTime until = default;
        return new FrameRunner.Step("SelectItem",
            () =>
            {
                if (!selected)
                {
                    var r = _window.SelectItem(itemId, amount);
                    if (r.Status == StepStatus.Continue) return StepResult.Continue();
                    if (r.Status == StepStatus.Failed) return r;
                    selected = true;
                    until = DateTime.UtcNow + UiInteractDelay;
                }
                return DateTime.UtcNow >= until ? StepResult.Success() : StepResult.Continue();
            },
            TimeSpan.FromSeconds(10));
    }

    private FrameRunner.Step RecordPurchaseStep(uint itemId, int cost, int amount, uint scripsBefore) => new(
        "RecordPurchase",
        () =>
        {
            if (!_window.TryGetScripCount(_catalog.ScripItemId, out var current))
                return StepResult.Fail("Enie shop window closed before the purchase could be verified.");
            if ((long)scripsBefore - current < (long)cost * amount)
                return StepResult.Continue();

            _scripsSpentThisCycle.TryGetValue(_catalog.ScripItemId, out var prev);
            _scripsSpentThisCycle[_catalog.ScripItemId] = prev + cost * amount;

            var cfgItem = _configuration.FirmamentGoal.ItemsToPurchase.FirstOrDefault(x => x.Item.ItemId == itemId);
            if (cfgItem != null)
            {
                cfgItem.AmountPurchased += Math.Max(0, amount);
                _configuration.Save();
            }

            var h = _buyQueue[0];
            h.remaining -= amount;
            if (h.remaining <= 0) _buyQueue.RemoveAt(0);
            else _buyQueue[0] = h;
            return StepResult.Success();
        },
        TimeSpan.FromSeconds(10));

    private StepResult TargetShop()
    {
        if (_attemptedTarget) return StepResult.Success();
        if (!_catalog.ExchangeDataIds.Any(TryInteractWithNpc)) return StepResult.Continue();
        _attemptedTarget = true;
        return StepResult.Success();
    }

    private bool _firmamentRouteIssued;

    private StepResult MakeFirmamentRouteTick(uint territoryId)
        => FirmamentRouting.RouteTick(_clientState, _lifestreamIpc, Status, territoryId, ref _firmamentRouteIssued);

    private StepResult MakeMoveToEnieTick()
    {
        Status.Set(PluginState.SpendingScrip);
        var dest = FirmamentRouting.LivePosition(_catalog.ExchangeDataIds, _catalog.ExchangePosition);
        if (dest == System.Numerics.Vector3.Zero)
            return StepResult.Fail("Firmament exchange position not resolved.");
        return MoveTowardsTick(dest);
    }
}
