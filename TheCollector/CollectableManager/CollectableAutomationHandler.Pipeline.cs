using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Ipc;

namespace TheCollector.CollectableManager;

using System;
using System.Linq;
using System.Numerics;
using TheCollector.Automation;
using TheCollector.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons;

public partial class CollectableAutomationHandler
{
    private FrameRunner? _runner;
    
    private readonly TimeSpan _uiLoadDelay = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _uiInteractDelay = TimeSpan.FromMilliseconds(300);
    private DateTime _uiLoadWaitUntil;
    private DateTime _cooldownUntil;
    private string? _currentItemName;
    private int _currentJobIndex = int.MinValue;


    public void StartPipeline()
    {
        if (IsRunning) return;
        IsRunning = true;
        Plugin.State = PluginState.MovingToCollectableVendor;

        _runner ??= new FrameRunner(_framework,
            n => _log.Debug(n),
            (n, s, e) => _log.Debug($"{n} -> {s}{(e is null ? "" : $" ({e})")}"),
            e => OnError?.Invoke(e),
            ok =>
            {
                IsRunning = false;
                if (ok) OnFinishTrading?.Invoke();
                Plugin.State = PluginState.Idle;
            });

        var shopName = _configuration.PreferredCollectableShop.Name;
        var target = _configuration.PreferredCollectableShop.Location;

        var steps = new[]
        {
            new FrameRunner.Step(
                "EnsureNotInDuty",
                () => PlayerHelper.IsInDuty ? StepStatus.Failed : StepStatus.Succeeded,
                TimeSpan.FromSeconds(1)
            ),
            new FrameRunner.Step(
                "Initial delay",
                () => DateTime.UtcNow >= _uiLoadWaitUntil ? StepStatus.Succeeded : StepStatus.Continue,
                TimeSpan.FromSeconds(5),
                () => _uiLoadWaitUntil = DateTime.UtcNow + TimeSpan.FromSeconds(1)),

            new FrameRunner.Step(
                "TeleportToPreferredShop",
                () => MakeTeleportTick(shopName),
                TimeSpan.FromSeconds(20),
                () => _teleportAttempted = false
            ),

            new FrameRunner.Step(
                "WaitCanActAfterTeleport",
                () => PlayerHelper.CanAct ? StepStatus.Succeeded : StepStatus.Continue,
                TimeSpan.FromSeconds(20)
            ),

            new FrameRunner.Step(
                "MoveToPreferredShop",
                () => MakeMoveTick(target),
                TimeSpan.FromSeconds(60),
                () => _lastMove = DateTime.MinValue
            ),
            
            new FrameRunner.Step(
                "OpenCollectablesShop",
                () => StepStatus.Succeeded,
                TimeSpan.FromSeconds(2),
                () => OpenShop()
            ),
            
            new FrameRunner.Step(
                "WaitCollectablesReady",
                () =>
                {
                    unsafe
                    {
                        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var a) &&
                            GenericHelpers.IsAddonReady(a))
                            return StepStatus.Succeeded;
                    }
                    return StepStatus.Continue;
                },
                TimeSpan.FromSeconds(5)
            ),
            
            new FrameRunner.Step(
                "UiLoadBuffer",
                () => DateTime.UtcNow >= _uiLoadWaitUntil ? StepStatus.Succeeded : StepStatus.Continue,
                TimeSpan.FromSeconds(5),
                () => _uiLoadWaitUntil = DateTime.UtcNow + _uiLoadDelay
            ),

            new FrameRunner.Step(
                "TurnInAllCollectables",
                () => MakeTurnInTick(),
                TimeSpan.FromSeconds(90),
                PrimeTurnIn
            ),
            new FrameRunner.Step(
                "CloseCollectablesShop",
                () =>
                {
                    if(DateTime.UtcNow >= _uiLoadWaitUntil)
                    {
                        _collectibleWindowHandler.CloseWindow();
                        return StepStatus.Succeeded;
                    }
                    return StepStatus.Continue;
                    
                },
                TimeSpan.FromSeconds(5),
                () => _uiLoadWaitUntil = DateTime.UtcNow + _uiInteractDelay
            ),
        };

        _runner.Start(steps);
    }

    public void StopPipeline() => _runner?.Cancel("Canceled");

    private bool _teleportAttempted;
    private StepStatus MakeTeleportTick(string shopName)
    {
        if (_dataManager.GetExcelSheet<TerritoryType>()
                .FirstOrDefault(t => t.RowId == _clientState.TerritoryType)
                .PlaceName.Value.Name.ExtractText() == _configuration.PreferredCollectableShop.Name)
            return StepStatus.Succeeded;

        if (!_teleportAttempted)
        {
            if (TeleportHelper.TryFindAetheryteByName(shopName, out var aetheryte, out _))
            {
                TeleportHelper.Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
                _teleportAttempted = true;
            }
            else
            {
                return StepStatus.Failed;
            }
        }

        var currentName = _dataManager.GetExcelSheet<TerritoryType>()
            .FirstOrDefault(t => t.RowId == _clientState.TerritoryType)
            .PlaceName.Value.Name.ExtractText();

        if (string.Equals(currentName, shopName, StringComparison.OrdinalIgnoreCase))
            return StepStatus.Succeeded;

        return StepStatus.Continue;
    }

    private DateTime _lastMove;
    private StepStatus MakeMoveTick(Vector3 destination)
    {
        if ((DateTime.UtcNow - _lastMove).TotalMilliseconds >= 200)
        {
            if (!VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.Path_MoveTo([destination], false);
            _lastMove = DateTime.UtcNow;
        }

        if (PlayerHelper.GetDistanceToPlayer(destination) <= 0.4f)
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            return StepStatus.Succeeded;
        }

        return StepStatus.Continue;
    }

    private (string name, int left, int jobIndex)[] _turnInQueue = Array.Empty<(string, int, int)>();
    private DateTime _lastTurnIn;
    private int _turnInPhase;

    private void PrimeTurnIn()
    {
        _turnInQueue = ItemHelper.GetLuminaItemsFromInventory()
            .Where(i => i.IsCollectable)
            .GroupBy(i => i.Name)
            .Select(g => (g.Key.ExtractText(), g.Count(), int.MinValue))
            .ToArray();

        for (var i = 0; i < _turnInQueue.Length; i++)
        {
            var item = _turnInQueue[i];
            if (_collectableShopItems.TryGetFirst(c => c.Name.Contains(item.name, StringComparison.OrdinalIgnoreCase),
                                                  out var value))
            {
                item.jobIndex = (int)value.Class; 
                _turnInQueue[i] = item;
            }
        }

        _lastTurnIn = DateTime.MinValue;
        _cooldownUntil = DateTime.MinValue;
        _turnInPhase = 0;
        _currentItemName = null;
        _currentJobIndex = int.MinValue;

    }

    private StepStatus MakeTurnInTick()
    {
        if (_turnInQueue.Length == 0) return StepStatus.Succeeded;
        if (DateTime.UtcNow < _cooldownUntil) return StepStatus.Continue;

        var h = _turnInQueue[0];
        
        if (_turnInPhase < 2)
        {
            if (h.jobIndex != int.MinValue && _currentJobIndex != h.jobIndex)
            {
                _collectibleWindowHandler.SelectJob((uint)h.jobIndex);
                _currentJobIndex = h.jobIndex;
                _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
                _turnInPhase = 1; 
                return StepStatus.Continue;
            }
            
            if (!string.Equals(_currentItemName, h.name, StringComparison.Ordinal))
            {
                _collectibleWindowHandler.SelectItem(h.name);
                _currentItemName = h.name;
                _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
                _turnInPhase = 2;
                return StepStatus.Continue;
            }
            
            _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
            _turnInPhase = 2;
            return StepStatus.Continue;
        }
        
        _collectibleWindowHandler.SubmitItem();
        _lastTurnIn = DateTime.UtcNow;
        _cooldownUntil = _lastTurnIn + _uiInteractDelay;
        _turnInPhase = 0;

        h.left--;
        if (h.left <= 0)
        {
            _turnInQueue = _turnInQueue.Skip(1).ToArray();
            _currentItemName = null;
        }
        else
        {
            _turnInQueue[0] = h;
        }

        return StepStatus.Continue;
    }


}
