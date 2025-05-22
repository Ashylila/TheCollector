using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Lumina.Excel.Sheets;
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
        //if (GetCollectablesInInventory().Count == 0) return;
        _currentCollectables = GetCollectablesInInventory();
        _taskManager.Enqueue(()=>PlayerHelper.CanAct);
        _taskManager.Enqueue(()=>TeleportToCollectableShop(), nameof(TeleportToCollectableShop));
        _taskManager.EnqueueDelay(8000);
        _taskManager.Enqueue(()=>VNavmesh_IPCSubscriber.Nav_Rebuild());
        _taskManager.Enqueue(()=>PlayerHelper.CanAct);
        _taskManager.Enqueue(()=>VNavmesh_IPCSubscriber.Nav_IsReady());
        _taskManager.Enqueue(()=>MoveToCollectableShop(), nameof(MoveToCollectableShop));
    }
//TODO:Teleporting to collectable shop

    private unsafe void MoveToCollectableShop()
    {
        //_framework.Run(()=>_targetManager.Target = FindNearbyAetheryte());
        //TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);
        var loc = CollectableNpcLocations.CollectableNpcLocationVectors(_clientState.TerritoryType);
        _log.Debug($"vnav moveto {loc.X} {loc.Y.ToString().Replace(",", ".")} {loc.Z}");
        _framework.Run(() => Chat.ExecuteCommand($"/vnav moveto {loc.X} {loc.Y.ToString().Replace(",", ".")} {loc.Z}"));
        _taskManager.Enqueue(() =>
        {
            if (PlayerHelper.GetDistanceToPlayer(CollectableNpcLocations.CollectableNpcLocationVectors(_clientState.TerritoryType)) >= 4) return false;
            _log.Debug(PlayerHelper.GetDistanceToPlayer(CollectableNpcLocations.CollectableNpcLocationVectors(_clientState.TerritoryType)).ToString());
            return true;
        });
        _taskManager.Enqueue(() =>
        {
            _targetManager.Target = _objectTable.FirstOrDefault(a => a.Name.TextValue.Contains(
                                                                    "collectable", StringComparison.OrdinalIgnoreCase));
        });
        
    }
    
    private IGameObject findNearbyGameObject(string name)
    {
        var target = _objectTable.FirstOrDefault(i => i.Name.TextValue == name);
        if (target == null) return null;
        return target;
    }
    
    private void TeleportToCollectableShop()
    {
        if (TeleportHelper.TryFindAetheryteByName("nine", out var aetheryte,
                out var name))
        {
            TeleportHelper.Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
        }
        else
        {
            ForceStop("Error while finding aetheryte for collectable shop");
        }
    }
    public void TradeEachCollectable()
    {
        foreach (var item in _currentCollectables)
        {
            if (!ItemHelper.RarefiedItemToClassJob.TryGetValue(item.Name.ExtractText(), out var value))
            {
                PluginLog.Error($"error finding job for item: {item.Name.ExtractText()}");
                continue;
            }
            _log.Debug($"Collecting {value.ToString()}");
            
            _taskManager.Enqueue(()=>_collectibleWindowHandler.SelectJob((uint)value));
            _taskManager.EnqueueDelay(100);
            _taskManager.Enqueue(()=>_collectibleWindowHandler.SelectItem(item.Name.ExtractText()));
            _taskManager.EnqueueDelay(100);
            _taskManager.Enqueue(() => _collectibleWindowHandler.SubmitItem());
            _taskManager.EnqueueDelay(100);
        }
    }
    private void ForceStop(string reason)
    {
        _taskManager.Abort();
        IsRunning = false;
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
