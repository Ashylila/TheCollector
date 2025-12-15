using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
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
    private Dictionary<uint, CollectablesShopItem> _collectableByItemId = new();
    private readonly IPlayerState _player;
    private readonly TimeSpan _uiLoadDelay = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _uiInteractDelay = TimeSpan.FromMilliseconds(500);
    private DateTime _uiLoadWaitUntil;
    private DateTime _cooldownUntil;
    public string? CurrentItemName { get; private set; }
    private int _currentJobIndex = int.MinValue;


    private void StartPipeline()
    {
        if (IsRunning) return;
        IsRunning = true;
        Plugin.State = PluginState.MovingToCollectableVendor;
        _runner ??= new FrameRunner(_framework,
            n => _log.Debug(n),
            (string name, StepStatus status, string? error) =>
            {
                if (StepStatus.Failed == status)
                {
                    _runner.Cancel(error);
                    Plugin.State = PluginState.Idle;
                }
                _log.Debug($"{name} -> {status}{(error is null ? "" : $" ({error})")}");
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
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step(
                "CanActCheck",
                () => PlayerHelper.CanAct ?  StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(20),
                        PrimeTurnIn),
            new FrameRunner.Step(
                "CollectableCheck",
                (() => CollectableCheck()),
                TimeSpan.FromSeconds(5)),
            new FrameRunner.Step(
                "TeleportToPreferredShop",
                () => MakeTeleportTick(shopName),
                TimeSpan.FromSeconds(20),
                () => _teleportAttempted = false
            ),

            new FrameRunner.Step(
                "WaitCanActAfterTeleport",
                () => PlayerHelper.CanAct ? StepResult.Success() : StepResult.Continue(),
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
                (() => PlayerHelper.CanAct ? StepResult.Success() : StepResult.Continue() ),
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
                () => StepResult.Success(),
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
                            return StepResult.Success();
                    }
                    return StepResult.Continue();
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
                    return StepResult.Success();
                },
                TimeSpan.FromSeconds(5)
            ),
            FrameRunner.Delay("FinalDelay", TimeSpan.FromSeconds(1)),
        };

        _runner.Start(steps);
    }

    public void StopPipeline() => _runner?.Cancel("Canceled");

    private bool _teleportAttempted;
    private StepResult MakeTeleportTick(string shopName)
    {
        Plugin.State = PluginState.Teleporting;
        if (_dataManager.GetExcelSheet<TerritoryType>()
                .FirstOrDefault(t => t.RowId == _clientState.TerritoryType)
                .PlaceName.Value.Name.ExtractText().Contains(_configuration.PreferredCollectableShop.Name) || _configuration.PreferredCollectableShop.IsLifestreamRequired)
            return StepResult.Success();

        if (!_teleportAttempted)
        {
            if (TeleportHelper.TryFindAetheryteByName(shopName, out var aetheryte, out _))
            {
                TeleportHelper.Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
                _teleportAttempted = true;
            }
            else
            {
                return StepResult.Fail("Couldn't find an Aetheryte to teleport to");
            }
        }

        var currentName = _dataManager.GetExcelSheet<TerritoryType>()
            .FirstOrDefault(t => t.RowId == _clientState.TerritoryType)
            .PlaceName.Value.Name.ExtractText();
        if (string.Equals(currentName, shopName, StringComparison.OrdinalIgnoreCase))
        {
            Plugin.State = PluginState.Idle;
            return StepResult.Success();
        }

        return StepResult.Continue();
    }

    private DateTime _lastMove;
    private StepResult MakeMoveTick(Vector3 destination)
    {
        Plugin.State = PluginState.MovingToCollectableVendor;
        if ((DateTime.UtcNow - _lastMove).TotalMilliseconds >= 200)
        {
            if (!VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destination, false);
            _lastMove = DateTime.UtcNow;
        }

        if (PlayerHelper.GetDistanceToPlayer(destination) <= 0.4f)
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            Plugin.State = PluginState.Idle;
            return StepResult.Success();
        }

        return StepResult.Continue();
    }

    private bool IsNearShop(Vector3 destination)
    {
        var playerTer = _clientState.TerritoryType;
        var ter = _dataManager.GetExcelSheet<TerritoryType>().FirstOrDefault(t => t.PlaceName.Value.Name.ToString().Equals(_configuration.PreferredCollectableShop.Name)).RowId;
        if (playerTer == ter && PlayerHelper.GetDistanceToPlayer(destination) <= 40f)
        {
            return true;
        }

        return false;
    }
    private StepResult LifestreamCheck()
    {
        if (IsNearShop(_configuration.PreferredCollectableShop.Location))return StepResult.Success();
        if (_configuration.PreferredCollectableShop.IsLifestreamRequired)
        {
            _lifestreamIpc.ExecuteCommand(_configuration.PreferredCollectableShop.LifestreamCommand);
        }
        return StepResult.Success();
    }
    private StepResult WaitForLifestream()
    {
        if (_configuration.PreferredCollectableShop.IsLifestreamRequired)
        {
            if (_lifestreamIpc.IsBusy())
                return StepResult.Continue();
        }
        return StepResult.Success();
    }
    public (CollectablesShopItem item,string name,int left, int jobIndex)[] TurnInQueue { get; private set; }
    private DateTime _lastTurnIn;
    private int _turnInPhase;

    private void PrimeTurnIn()
    {
        TurnInQueue = ItemHelper.GetLuminaItemsFromInventory()
                                .Where(i => _collectableByItemId.ContainsKey(i.RowId))
                                .GroupBy(i => i.RowId)
                                .Select(g => (_collectableByItemId[g.Key],_collectableByItemId[g.Key].Item.Value.Name.ExtractText(), g.Count(), int.MinValue))
                                .ToArray();
        

        for (var i = 0; i < TurnInQueue.Length; i++)
        {
            var item = TurnInQueue[i];
            var jobId = ItemJobResolver.GetJobIdForItem(item.name, _dataManager);
            if (jobId != -1)
            {
                item.jobIndex = jobId; 
                TurnInQueue[i] = item;
            }
        }
        _lastTurnIn = DateTime.MinValue;
        _cooldownUntil = DateTime.MinValue;
        _turnInPhase = 0;
        CurrentItemName = null;
        _currentJobIndex = int.MinValue;
    }
    
    private StepResult MakeTurnInTick()
    {
        Plugin.State = PluginState.ExchangingItems;
        if (TurnInQueue.Length == 0)
        {
            Plugin.State = PluginState.Idle;
            return StepResult.Success();
        };
        if (DateTime.UtcNow < _cooldownUntil) return StepResult.Continue();

        var h = TurnInQueue[0];
        _log.Debug($"found id{h.jobIndex.ToString()} for item {h.name}");
        if (_turnInPhase < 2)
        {
            if (h.jobIndex != int.MinValue && _currentJobIndex != h.jobIndex)
            {
                _collectibleWindowHandler.SelectJob((uint)h.jobIndex);
                _currentJobIndex = h.jobIndex;
                _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
                _turnInPhase = 1; 
                return StepResult.Continue();
            }
            
            if (!string.Equals(CurrentItemName, h.name, StringComparison.Ordinal))
            {
                _collectibleWindowHandler.SelectItem(h.name);
                CurrentItemName = h.name;
                _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
                _turnInPhase = 2;
                return StepResult.Continue();
            }
            
            _cooldownUntil = DateTime.UtcNow + _uiInteractDelay;
            _turnInPhase = 2;
            return StepResult.Continue();
        }
        var current = h.item.CollectablesShopRewardScrip.Value.Currency switch
        {
            6 or 7 => _collectibleWindowHandler.OrangeScripCount(),
            2 or 4 => _collectibleWindowHandler.PurpleScripCount(),
            _ => throw new InvalidOperationException($"Unknown scrip currency: {h.item.CollectablesShopRewardScrip.Value.Currency}")
        };


        var remaining = 4000 - current;

        if (h.item.CollectablesShopRewardScrip.Value.HighReward > remaining)
        {
            return StepResult.Success();
        }

        _collectibleWindowHandler.SubmitItem();
        _lastTurnIn = DateTime.UtcNow;
        _cooldownUntil = _lastTurnIn + _uiInteractDelay;
        _turnInPhase = 0;

        h.left--;
        if (h.left <= 0)
        {
            TurnInQueue = TurnInQueue.Skip(1).ToArray();
            CurrentItemName = null;
        }
        else
        {
            TurnInQueue[0] = h;
        }
        return StepResult.Continue();
    }

    private StepResult CollectableCheck()
    {
        if (!HasCollectible)
        {
            _log.Debug("No collectables found in inventory, cancelling");
            return StepResult.Fail("No collectables found in inventory, cancelling");
        }
        return StepResult.Success();
    }
}


