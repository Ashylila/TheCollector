using System.Numerics;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.ScripShopManager;

using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using TheCollector.Data;

public partial class ScripShopAutomationHandler : FrameRunnerPipelineBase
{

    private readonly TimeSpan _uiLoadDelay = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _uiInteractDelay = TimeSpan.FromMilliseconds(300);

    private DateTime _uiLoadWaitUntil;
    private DateTime _cooldownUntil;
    private int _buyPhase;

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
                TimeSpan.FromSeconds(20),
                () => Plugin.State = PluginState.SpendingScrip
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

            FrameRunner.Delay("WaitForUI", _uiLoadDelay),

            new FrameRunner.Step(
                "BuyConfigured",
                () => MakeBuyTick(),
                TimeSpan.FromSeconds(90),
                PrimeBuy
            ),

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
        OnFinishedTrading?.Invoke();
    }
    private (int page, int subPage, int remaining, int cost, uint itemId, uint currencyId)[] _buyQueue =
        Array.Empty<(int, int, int, int, uint, uint)>();

    private DateTime _lastBuy;
    private int _currentPurchaseAmount;

    private void PrimeBuy()
    {
        _buyQueue =
            (from i in _configuration.ItemsToPurchase
             join s in ScripShopItemManager.ShopItems on i.Item.ItemId equals s.ItemId
             let remaining = i.Quantity - i.AmountPurchased
             where i.Quantity > 0 && remaining > 0
             select (
                 page: s.Page,
                 subPage: s.SubPage,
                 remaining: remaining,
                 cost: (int)s.ItemCost,
                 itemId: s.ItemId,
                 currencyId: s.CurrencyId

             ))
            .ToArray();

        _lastBuy = DateTime.MinValue;
        _cooldownUntil = DateTime.MinValue;
        _buyPhase = 0;
        _currentPurchaseAmount = 0;
    }

    private StepResult MakeBuyTick()
    {
        if (_buyQueue.Length == 0) return StepResult.Success();
        if (DateTime.UtcNow < _cooldownUntil) return StepResult.Continue();

        var h = _buyQueue[0];

        switch (_buyPhase)
        {
            case 0:
                _scripShopWindowHandler.SelectPage(h.page);
                _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
                _buyPhase = 1;
                return StepResult.Continue();

            case 1:
                _scripShopWindowHandler.SelectSubPage(h.subPage);
                _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
                _buyPhase = 2;
                return StepResult.Continue();

            case 2:
                {
                    
                    var scrips = _scripShopWindowHandler.ScripCount(h.currencyId);
                    _log.Debug($"Scripcount: {scrips}");
                    var maxByScrip = h.cost > 0 ? (scrips / h.cost) : h.remaining;
                    var amount = Math.Min(h.remaining, Math.Min(maxByScrip, 99));
                    if (amount <= 0)
                    {
                        _buyQueue = _buyQueue.Skip(1).ToArray();
                        _buyPhase = 0;
                        return StepResult.Continue();
                    }


                    _currentPurchaseAmount = (int)amount;
                    _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
                    _buyPhase = 69;
                    return StepResult.Continue();
                }
            case 69:
                {
                    var r = _scripShopWindowHandler.SelectItem(h.itemId, _currentPurchaseAmount);

                    if (r.Status == StepStatus.Continue)
                        return StepResult.Continue();

                    if (r.Status == StepStatus.Failed)
                        return r;

                    _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
                    _buyPhase = 3;
                    return StepResult.Continue();
                }
            case 3:
                {
                    _scripShopWindowHandler.PurchaseItem();
                    _lastBuy = DateTime.UtcNow;
                    _cooldownUntil = _lastBuy + _uiInteractDelay;
                    _buyPhase = 0;

                    h.remaining -= _currentPurchaseAmount;

                    var cfgItem = _configuration.ItemsToPurchase.FirstOrDefault(x => x.Item.ItemId == h.itemId);
                    if (cfgItem != null)
                    {
                        cfgItem.AmountPurchased += Math.Max(0, _currentPurchaseAmount);
                        _configuration.Save();
                    }

                    _currentPurchaseAmount = 0;

                    if (h.remaining <= 0)
                        _buyQueue = _buyQueue.Skip(1).ToArray();
                    else
                        _buyQueue[0] = h;

                    return StepResult.Continue();
                }
        }

        return StepResult.Continue();
    }

    private DateTime _lastMove;

    private StepResult MakeMoveTick()
    {
        
        var shop = _configuration.PreferredCollectableShop;

        if (shop.ScripShopLocation == shop.Location)
            return StepResult.Success();

        if ((DateTime.UtcNow - _lastMove).TotalMilliseconds >= 200)
        {
            if (!VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(shop.ScripShopLocation, false);

            _lastMove = DateTime.UtcNow;
        }

        if (PlayerHelper.GetDistanceToPlayer(shop.ScripShopLocation) <= 0.4f)
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            return StepResult.Success();
        }

        return StepResult.Continue();
    }

    public unsafe StepResult TargetShop()
    {
        if (_attemptedTarget) return StepResult.Success();

        var gameObj = _objectTable.FirstOrDefault(a =>
            a.Name.TextValue.Contains("scrip", StringComparison.OrdinalIgnoreCase));

        if (gameObj == null)
            return StepResult.Continue();

        TargetSystem.Instance()->Target = (GameObject*)gameObj.Address;
        TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);

        _attemptedTarget = true;
        return StepResult.Success();
    }
}
