using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.CollectableManager;

public class CollectableAutomationHandler
{
    #pragma warning disable
    
    private readonly TaskManager _taskManager;
    private readonly IPluginLog _log;
    private readonly CollectableWindowHandler _collectibleWindowHandler;
    private readonly IDataManager _dataManager;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    
    
    private TaskManagerConfiguration _config = new  TaskManagerConfiguration()
    {
        ExecuteDefaultConfigurationEvents = true,
        ShowDebug = true
        
    };

    public bool IsRunning = false;
    private string _currentItem;
    private List<Item> _currentCollectables = new();
    
    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);

    public CollectableAutomationHandler( IPluginLog log, CollectableWindowHandler collectibleWindowHandler, IDataManager data, Configuration config, IObjectTable objectTable, ITargetManager targetManager, IFramework frameWork, IClientState clientState )
    {
        _taskManager = new TaskManager(_config);
        _config.OnTaskTimeout += OnTaskTimeout;
        _log = log;
        _collectibleWindowHandler = collectibleWindowHandler;
        _dataManager = data;
        _configuration = config;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _framework = frameWork;
        _clientState = clientState;
    }

    public void Start()
    {
        IsRunning = true;
        _log.Debug(GetCollectablesInInventory().Count.ToString());
        if (GetCollectablesInInventory().Count == 0)
        {
            _log.Debug("No collectables found in inventory, cancelling");
            IsRunning = false;
            return;
        };
        _currentCollectables = GetCollectablesInInventory();
        _taskManager.Enqueue(()=>PlayerHelper.CanAct);
        _taskManager.Enqueue(()=>TeleportToCollectableShop(), nameof(TeleportToCollectableShop));
        _taskManager.EnqueueDelay(10000);
        _taskManager.Enqueue(()=>PlayerHelper.CanAct);
        _taskManager.Enqueue(()=>VNavmesh_IPCSubscriber.Nav_IsReady());
        _taskManager.Enqueue(()=>MoveToCollectableShop(), nameof(MoveToCollectableShop));
        _taskManager.Enqueue(()=>TradeEachCollectable(), nameof(TradeEachCollectable));
    }

    private unsafe void MoveToCollectableShop()
    {
        Plugin.State = PluginState.MovingToCollectableVendor;
        var loc = _configuration.PreferredCollectableShop.Location;
        _log.Debug($"vnav moveto {loc.X} {loc.Y.ToString().Replace(",", ".")} {loc.Z}");
        VNavmesh_IPCSubscriber.Path_MoveTo([loc], false);
        _taskManager.Enqueue(() =>
        {
            if (PlayerHelper.GetDistanceToPlayer(_configuration.PreferredCollectableShop.Location) > 2) return false;
            return true;
        }, "DistanceToShopCheck");
        _taskManager.Enqueue(() =>
        {
            _targetManager.Target = _objectTable.FirstOrDefault(a => a.Name.TextValue.Contains(
                                                                    "collectable", StringComparison.OrdinalIgnoreCase));
        });
        _taskManager.Enqueue(() =>
        {
            TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);
        });
    }
    
    private IGameObject FindNearbyGameObject(string name)
    {
        var target = _objectTable.FirstOrDefault(i => i.Name.TextValue == name);
        if (target == null) return null;
        return target;
    }
    
    private void TeleportToCollectableShop()
    {
        Plugin.State = PluginState.Teleporting;
        if (TeleportHelper.TryFindAetheryteByName(_configuration.PreferredCollectableShop.Name, out var aetheryte,
                out var name))
        {
            TeleportHelper.Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
        }
        else
        {
            ForceStop("Error while finding aetheryte for collectable shop");
        }
    }
    public unsafe void TradeEachCollectable()
    {
        Plugin.State = PluginState.ExchangingItems;
        foreach (var item in _currentCollectables)
        {
            int currentJob = -2; // -2 means no job selected, not -1 in-case of index not found
            string currentItem = string.Empty;
            if (!ItemHelper.RarefiedItemToClassJob.TryGetValue(item.Name.ExtractText(), out var value))
            {
                PluginLog.Error($"error finding job for item: {item.Name.ExtractText()}");
                continue;
            }
            _log.Debug($"Collecting {value.ToString()}");
            if (currentJob != (int)value)
            {
                _taskManager.Enqueue(() => _collectibleWindowHandler.SelectJob((uint)value));
                _taskManager.EnqueueDelay(100);
                currentJob = (int)value;
            }
            if (currentItem != item.Name.ExtractText())
            {
                _taskManager.Enqueue(() => _collectibleWindowHandler.SelectItem(item.Name.ExtractText()));
                _taskManager.EnqueueDelay(100);
                currentItem = item.Name.ExtractText();
            }
            _taskManager.Enqueue(() => _collectibleWindowHandler.SubmitItem());
            _taskManager.EnqueueDelay(100);
            
            if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addon))
            {
                addon->Close(true);
                _log.Debug("Max scrips reached, stopping automatic turn-in");
                _taskManager.Abort();
                _collectibleWindowHandler.CloseWindow();
                IsRunning = false;
                Plugin.State = PluginState.Idle;
                return;
            }
        }
        _collectibleWindowHandler.CloseWindow();
        IsRunning = false;
        Plugin.State = PluginState.Idle;
    }
    private void ForceStop(string reason)
    {
        _taskManager.Abort();
        IsRunning = false;
        Plugin.State = PluginState.Idle;
        _log.Error("TheCollector has stopped unexpectedly.", reason);
    }
    public void OnTaskTimeout(TaskManagerTask task, ref long remainingTime)
    {
        ForceStop($"Task {task.Name} timed out.");
    }
    private List<Item> GetCollectablesInInventory()
    {
        return ItemHelper.GetLuminaItemsFromInventory().Where(i => i.IsCollectable).ToList() ?? new List<Item>();
    }
}
