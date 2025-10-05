using System.Threading.Tasks;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
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
    private readonly TimeSpan _uiInteractDelay = TimeSpan.FromMilliseconds(500);
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
                if (ok) OnFinishCollecting?.Invoke();
                Plugin.State = PluginState.Idle;
            });

        var shopName = _configuration.PreferredCollectableShop.Name;
        var target = _configuration.PreferredCollectableShop.Location;

        var steps = new[]
        {
            new FrameRunner.Step(
                "EnsureNotInDuty",
                () => PlayerHelper.IsInDuty ? StepStatus.Failed : StepStatus.Succeeded,
                TimeSpan.FromSeconds(1),
                (() => PrimeTurnIn())
            ),
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(2)),

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
                (() => PlayerHelper.CanAct ? StepStatus.Succeeded : StepStatus.Continue ),
                TimeSpan.FromSeconds(10)
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
            FrameRunner.Delay("UiBuffer", _uiLoadDelay),

            new FrameRunner.Step(
                "TurnInAllCollectables",
                () => MakeTurnInTick(),
                TimeSpan.FromSeconds(150)
            ),
            FrameRunner.Delay("PostTurnInBuffer", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step(
                "CloseCollectablesShop",
                () =>
                {
                    _collectibleWindowHandler.CloseWindow();
                    _targetManager.Target = null;
                    return StepStatus.Succeeded;
                },
                TimeSpan.FromSeconds(5)
            ),
            FrameRunner.Delay("FinalDelay", TimeSpan.FromSeconds(1)),
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
                VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destination, false);
            _lastMove = DateTime.UtcNow;
        }

        if (PlayerHelper.GetDistanceToPlayer(destination) <= 0.4f)
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            return StepStatus.Succeeded;
        }

        return StepStatus.Continue;
    }

    private bool IsNearShop(Vector3 destination)
    {
        if (PlayerHelper.GetDistanceToPlayer(destination) <= 0.4f) //TODO: things
        {
            return true;
        }

        return false;
    }
    private StepStatus LifestreamCheck()
    {
        if (IsNearShop(_configuration.PreferredCollectableShop.Location))return StepStatus.Succeeded;
        if (_configuration.PreferredCollectableShop.IsLifestreamRequired)
        {
            _lifestreamIpc.ExecuteCommand(_configuration.PreferredCollectableShop.LifestreamCommand);
        }
        return StepStatus.Succeeded;
    }
    private StepStatus WaitForLifestream()
    {
        if (_configuration.PreferredCollectableShop.IsLifestreamRequired)
        {
            if (_lifestreamIpc.IsBusy())
                return StepStatus.Continue;
        }
        return StepStatus.Succeeded;
    }
    private (string name, int left, int jobIndex)[] _turnInQueue = Array.Empty<(string, int, int)>();
    private DateTime _lastTurnIn;
    private int _turnInPhase;

    private void PrimeTurnIn()
    {
        _turnInQueue = ItemHelper.GetLuminaItemsFromInventory()
            .Where(i => i.Name.ExtractText().Contains("Rarefied", StringComparison.OrdinalIgnoreCase) || FishingCollectables.Contains(i.Name.ExtractText())) 
            .GroupBy(i => i.Name)
            .Select(g => (g.Key.ExtractText(), g.Count(), int.MinValue))
            .ToArray();
        

        for (var i = 0; i < _turnInQueue.Length; i++)
        {
            var item = _turnInQueue[i];
            var jobId = ItemJobResolver.GetJobIdForItem(item.name, _dataManager);
            if (jobId != -1)
            {
                item.jobIndex = jobId; 
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
        _log.Debug($"found id{h.jobIndex.ToString()} for item {h.name.ToString()}");
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
