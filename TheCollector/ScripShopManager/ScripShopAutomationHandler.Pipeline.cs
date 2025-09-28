using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using TheCollector.Utility;

namespace TheCollector.ScripShopManager;

using System;
using System.Linq;
using TheCollector.Automation;
using TheCollector.Data;

public partial class ScripShopAutomationHandler
{
    private FrameRunner? _runner;
    
    private readonly TimeSpan _uiLoadDelay = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _uiInteractDelay = TimeSpan.FromMilliseconds(300); 

    private DateTime _uiLoadWaitUntil;
    private DateTime _cooldownUntil;
    private int _buyPhase; 

    public void StartPipeline()
    {
        Plugin.State = PluginState.SpendingScrip;

        _runner ??= new FrameRunner(_framework,
            n => _log.Debug(n),
            (string name, StepStatus status, string? error) =>
            {
                _log.Debug($"{name} -> {status}{(error is null ? "" : $" ({error})")}");
                if (StepStatus.Failed == status)
                {
                    _runner.Cancel(error);
                }
            },
            e => OnError?.Invoke(e),
            ok =>
            {
                IsRunning = false;
                if (ok) OnFinishedTrading?.Invoke();
                Plugin.State = PluginState.Idle;
            });

        var steps = new []
        {
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step(
                "TargetShop",
                () => TargetShop(),
                TimeSpan.FromSeconds(1),
                () => _uiLoadWaitUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(500)
            ),
            FrameRunner.Delay("PostTargetDelay", TimeSpan.FromMilliseconds(500)),
            new FrameRunner.Step(
                "OpenScripShop",
                () => StepStatus.Succeeded,
                TimeSpan.FromSeconds(2),
                () => _scripShopWindowHandler.OpenShop()
            ),
            
            new FrameRunner.Step(
                "WaitScripShopReady",
                () =>
                {
                    unsafe
                    {
                        if (ECommons.GenericHelpers.TryGetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("CollectablesShop", out var a) &&
                            ECommons.GenericHelpers.IsAddonReady(a))
                            return StepStatus.Succeeded;
                    }
                    return StepStatus.Continue;
                },
                TimeSpan.FromSeconds(5)
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
                () => { _scripShopWindowHandler.CloseShop(); return StepStatus.Succeeded; },
                TimeSpan.FromSeconds(2)
            ),
            new FrameRunner.Step(
                "SetState",
                () => { Plugin.State = PluginState.Idle;
                    IsRunning = false; return StepStatus.Succeeded; },
                TimeSpan.FromSeconds(1))
        };

        _runner.Start(steps);
    }

    public void StopPipeline() => _runner?.Cancel("Canceled");

    private (int page, int subPage, int index, int remaining, int cost)[] _buyQueue = Array.Empty<(int,int,int,int,int)>();
    private DateTime _lastBuy; 
    private int _currentPurchaseAmount; 

    private void PrimeBuy()
{
    _buyQueue = _configuration.ItemsToPurchase
        .Where(i => i.Quantity > 0)
        .Select(i => (
            page:     i.Item.Page,
            subPage:  i.Item.SubPage,
            index:    i.Item.Index,
            remaining:(i.Quantity - i.AmountPurchased),
            cost:     (int)i.Item.ItemCost
        ))
        .Where(t => t.remaining > 0)
        .ToArray();

    _lastBuy = DateTime.MinValue;
    _cooldownUntil = DateTime.MinValue;
    _buyPhase = 0;
    _currentPurchaseAmount = 0;
}

private StepStatus MakeBuyTick()
{
    if (_buyQueue.Length == 0) return StepStatus.Succeeded;
    if (DateTime.UtcNow < _cooldownUntil) return StepStatus.Continue;

    var h = _buyQueue[0];

    switch (_buyPhase)
    {
        case 0:
            _scripShopWindowHandler.SelectPage(h.page);
            _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
            _buyPhase = 1;
            return StepStatus.Continue;
        
        case 1:
            _scripShopWindowHandler.SelectSubPage(h.subPage);
            _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
            _buyPhase = 2;
            return StepStatus.Continue;
        
        case 2:
        {
            var scrips = _scripShopWindowHandler.ScripCount();
            var maxByScrip = h.cost > 0 ? (scrips / h.cost) : h.remaining;  
            var amount = Math.Min(h.remaining, Math.Min(maxByScrip, 99));
            
            if (amount <= 0)
            {
                _buyQueue = _buyQueue.Skip(1).ToArray();
                _buyPhase = 0;
                return StepStatus.Continue;
            }

            _scripShopWindowHandler.SelectItem(h.index, amount);
            _currentPurchaseAmount = amount;
            _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
            _buyPhase = 3;
            return StepStatus.Continue;
        }
        
        case 3:
        {
            _scripShopWindowHandler.PurchaseItem();
            _lastBuy = DateTime.UtcNow;
            _cooldownUntil = _lastBuy + _uiInteractDelay;
            _buyPhase = 0;

            h.remaining -= _currentPurchaseAmount;
            _currentPurchaseAmount = 0;
            
            var cfgItem = _configuration.ItemsToPurchase.FirstOrDefault(x => x.Item.Index == h.index);
            if (cfgItem != null)
            {
                cfgItem.AmountPurchased += Math.Max(0, _currentPurchaseAmount);
                _configuration.Save();
            }

            if (h.remaining <= 0)
                _buyQueue = _buyQueue.Skip(1).ToArray();
            else
                _buyQueue[0] = h;

            return StepStatus.Continue;
        }
    }

    return StepStatus.Continue;
}

    public unsafe StepStatus TargetShop()
    {
        var attemptedTarget = false;
        if (!PlayerHelper.CanAct) return StepStatus.Continue;
        
        if (!attemptedTarget)
        {
            var gameObj = _objectTable.FirstOrDefault(a =>
                                                          a.Name.TextValue.Contains("scrip", StringComparison.OrdinalIgnoreCase));

            if (gameObj == null)
                return StepStatus.Continue;

            TargetSystem.Instance()->Target = (GameObject*)gameObj.Address;
            TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);

            attemptedTarget = true;
            return StepStatus.Continue;
        }

        if (DateTime.UtcNow >= _uiLoadWaitUntil)
        {
            attemptedTarget = false;
            return StepStatus.Succeeded;
        }

        return StepStatus.Continue;
    }
    

}
