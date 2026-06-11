using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.ScripShopManager;

using System;
using System.Collections.Generic;
using System.Linq;
using TheCollector.Data;

public partial class ScripShopAutomationHandler : FrameRunnerPipelineBase
{

    private TimeSpan UiInteractDelay => TimeSpan.FromMilliseconds(_configuration.UiDelayMs);

    private bool _attemptedTarget;

    protected override FrameRunner.Step[] BuildSteps()
    {

        _attemptedTarget = false;

        var steps = new[]
        {
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(1)),

            new FrameRunner.Step(
                "MoveToShop",
                () => MakeMoveTick(),
                TimeSpan.FromSeconds(60),
                () =>
                {
                    Status.Set(PluginState.SpendingScrip);
                    ResetMoveThrottle();
                }
            ),

            new FrameRunner.Step(
                "TargetShop",
                () => TargetShop(),
                TimeSpan.FromSeconds(5)
            ),

            FrameRunner.Delay("PostTargetDelay", TimeSpan.FromSeconds(1)),

            new FrameRunner.Step(
                "OpenScripShop",
                () => StepResult.Success(),
                TimeSpan.FromSeconds(2),
                () => _scripShopWindowHandler.OpenShop()
            ),

            new FrameRunner.Step(
                "WaitScripShopReady",
                () =>
                {
                    unsafe
                    {
                        if (ECommons.GenericHelpers.TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>(
                                "InclusionShop", out var a) &&
                            ECommons.GenericHelpers.IsAddonReady(a))
                            return StepResult.Success();
                    }

                    return StepResult.Continue();
                },
                TimeSpan.FromSeconds(10)
            ),

            new FrameRunner.Step(
                "PrimeBuy",
                () => StepResult.Success(),
                TimeSpan.FromSeconds(2),
                PrimeBuy
            ),

            BuyDriver(),

            FrameRunner.Delay("PostBuyDelay", TimeSpan.FromMilliseconds(600)),

            new FrameRunner.Step(
                "CloseScripShop",
                () =>
                {
                    _scripShopWindowHandler.CloseShop();
                    return StepResult.Success();
                },
                TimeSpan.FromSeconds(2)
            )
        };

        return steps;
    }
    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        if(ok) OnFinishedTrading?.Invoke(_scripsSpentThisCycle);
    }

    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        VNavmesh_IPCSubscriber.Path_Stop();
        _scripShopWindowHandler.CloseShop();
    }
    private List<(int page, int subPage, int remaining, int cost, uint itemId, uint currencyId, bool isEquippable)> _buyQueue = new();

    private readonly Dictionary<uint, int> _scripsSpentThisCycle = new();

    private void PrimeBuy()
    {
        var activeSource = _configuration.ActiveRunSource;
        _buyQueue =
            (from i in _configuration.Goal.ItemsToPurchase
             join s in ScripShopItemManager.ShopItems on i.Item.ItemId equals s.ItemId
             let remaining = i.Quantity - i.AmountPurchased
             where i.Quantity > 0 && remaining > 0
                && CurrencyHelper.GetRunSource(s.CurrencyId) == activeSource
             select (
                 page: s.Page,
                 subPage: s.SubPage,
                 remaining: remaining,
                 cost: (int)s.ItemCost,
                 itemId: s.ItemId,
                 currencyId: s.CurrencyId,
                 isEquippable: s.Item.EquipSlotCategory.RowId != 0
             ))
            .ToList();

        _scripsSpentThisCycle.Clear();
    }
    private FrameRunner.Step BuyDriver() => new(
        "BuyConfigured",
        ctx =>
        {
            if (_buyQueue.Count == 0) return StepResult.Success();

            var h = _buyQueue[0];

            if (!_scripShopWindowHandler.TryGetScripCount(h.currencyId, out var scrips))
                return StepResult.Fail("Scrip shop window closed while buying.");
            Log.Debug($"Scripcount: {scrips}");
            var spendable  = Math.Max(0, scrips - _configuration.ReserveScripAmount);
            var maxByScrip = h.cost > 0 ? (spendable / h.cost) : h.remaining;
            var amount = (int)(h.isEquippable
                ? Math.Min(1, maxByScrip)
                : Math.Min(h.remaining, Math.Min(maxByScrip, 99)));

            if (amount <= 0)
            {
                _buyQueue.RemoveAt(0);
                ctx.InjectNext(BuyDriver());
                return StepResult.Success();
            }

            if (ItemHelper.GetFreeInventorySlots() <= 0)
                return StepResult.Fail("No free inventory slots available for purchases.");

            ctx.InjectNext(
                UiStep("SelectPage",            () => _scripShopWindowHandler.SelectPage(h.page)),
                UiStep("SelectSubPage",         () => _scripShopWindowHandler.SelectSubPage(h.subPage)),
                SelectItemStep(h.itemId, amount),
                UiStep("ConfirmPurchaseDialog", () => _scripShopWindowHandler.ConfirmPurchaseDialog()),
                UiStep("ConfirmYesNo",          () => _scripShopWindowHandler.ConfirmYesNo()),
                RecordPurchaseStep(h.itemId, h.currencyId, h.cost, amount, scrips),
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
        return new FrameRunner.Step(
            "SelectItem",
            () =>
            {
                if (!selected)
                {
                    var r = _scripShopWindowHandler.SelectItem(itemId, amount);
                    if (r.Status == StepStatus.Continue) return StepResult.Continue();
                    if (r.Status == StepStatus.Failed) return r;
                    selected = true;
                    until = DateTime.UtcNow + UiInteractDelay;
                }
                return DateTime.UtcNow >= until ? StepResult.Success() : StepResult.Continue();
            },
            TimeSpan.FromSeconds(10));
    }

    private FrameRunner.Step RecordPurchaseStep(uint itemId, uint currencyId, int cost, int amount, uint scripsBefore) => new(
        "RecordPurchase",
        () =>
        {
            if (!_scripShopWindowHandler.TryGetScripCount(currencyId, out var current))
                return StepResult.Fail("Scrip shop window closed before the purchase could be verified.");
            if ((long)scripsBefore - current < (long)cost * amount)
                return StepResult.Continue();

            _scripsSpentThisCycle.TryGetValue(currencyId, out var prev);
            _scripsSpentThisCycle[currencyId] = prev + cost * amount;

            var cfgItem = _configuration.Goal.ItemsToPurchase.FirstOrDefault(x => x.Item.ItemId == itemId);
            if (cfgItem != null)
            {
                cfgItem.AmountPurchased += Math.Max(0, amount);
                _configuration.Save();
            }

        
            var h = _buyQueue[0];
            h.remaining -= amount;
            if (h.remaining <= 0)
                _buyQueue.RemoveAt(0);
            else
                _buyQueue[0] = h;

            return StepResult.Success();
        },
        TimeSpan.FromSeconds(10));

    private StepResult MakeMoveTick()
    {
        var vendor = _vendorCatalog.GetScripVendor(_configuration.PreferredTerritoryId);
        if (vendor == null)
            return StepResult.Fail("No scrip vendor known for the preferred territory.");

        return MoveTowardsTick(vendor.Position);
    }

    public StepResult TargetShop()
    {
        if (_attemptedTarget) return StepResult.Success();

        var vendor = _vendorCatalog.GetScripVendor(_configuration.PreferredTerritoryId);
        if (vendor == null) return StepResult.Fail("No scrip vendor known for the preferred territory.");

        if (!TryInteractWithNpc(vendor.DataId))
            return StepResult.Continue();

        _attemptedTarget = true;
        return StepResult.Success();
    }
}
