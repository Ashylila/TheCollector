using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
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
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Havok.Common.Serialize.Util;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Data.Models;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Utility;

namespace TheCollector.CollectableManager;

public class CollectableAutomationHandler
{
    #pragma warning disable
    
    private readonly TaskManager _taskManager;
    private readonly PlogonLog _log;
    private readonly CollectableWindowHandler _collectibleWindowHandler;
    private readonly IDataManager _dataManager;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyService;
    private List<CollectableShopItem> _collectableShopItems = new();
    public event Action<String>? OnError;
    public event Action<bool>? OnScripsCapped;
    public event System.Action? OnFinishTrading;
    
    private TaskManagerConfiguration _config = new  TaskManagerConfiguration()
    {
        ExecuteDefaultConfigurationEvents = false,
        ShowDebug = false,
    };

    public bool IsRunning = false;
    private string _currentItem;
    private List<Item> _currentCollectables = new();
    
    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);
    
    internal static CollectableAutomationHandler? Instance { get; private set; }

    public CollectableAutomationHandler( PlogonLog log, CollectableWindowHandler collectibleWindowHandler, IDataManager data, Configuration config, IObjectTable objectTable, ITargetManager targetManager, IFramework frameWork, IClientState clientState, GatherbuddyReborn_IPCSubscriber gatherbuddyService )
    {
        _config.OnTaskTimeout += OnTaskTimeout;
        _taskManager = new TaskManager(_config);
        _log = log;
        _collectibleWindowHandler = collectibleWindowHandler;
        _dataManager = data;
        _configuration = config;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _framework = frameWork;
        _clientState = clientState;
        _gatherbuddyService = gatherbuddyService;
        Instance = this;
        
        LoadItems();
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
        _taskManager.Enqueue(()=>PlayerHelper.CanAct);
        _taskManager.Enqueue(()=>VNavmesh_IPCSubscriber.Nav_IsReady());
        _taskManager.Enqueue(()=>MoveToCollectableShop(), nameof(MoveToCollectableShop));
        _taskManager.Enqueue(()=>OpenShop(), nameof(OpenShop));
        _taskManager.Enqueue(()=>TradeEachCollectable(), nameof(TradeEachCollectable));
    }
    
    
    private unsafe void MoveToCollectableShop()
    {
        Plugin.State = PluginState.MovingToCollectableVendor;
        var loc = _configuration.PreferredCollectableShop.Location;
        VNavmesh_IPCSubscriber.Path_MoveTo([loc], false);
        var tasks = new[]
        {
            new TaskManagerTask(() =>
            {
                VNavmesh_IPCSubscriber.Path_MoveTo([loc], false);
            }),
            new TaskManagerTask(() =>
            {
                if (PlayerHelper.GetDistanceToPlayer(_configuration.PreferredCollectableShop.Location) > 0.4f)
                    return false;
                return true;
            })
        };

        _taskManager.EnqueueMulti(tasks);
        _taskManager.EnqueueDelay(500);
    }

    private unsafe void OpenShop()
    {
        var tasks = new []
        {
            new TaskManagerTask(() =>
            {
                var gameObj = _objectTable.FirstOrDefault(a => a.Name.TextValue.Contains(
                                                              "collectable", StringComparison.OrdinalIgnoreCase));
                TargetSystem.Instance()->Target = (GameObject*)gameObj.Address;
            }),
            new TaskManagerTask(() =>
            {
                VNavmesh_IPCSubscriber.Path_Stop();
            }),
            new TaskManagerTask(() =>
            {
                TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);
            }),
        };
        _taskManager.EnqueueMulti(tasks);
        _taskManager.EnqueueDelay(2000);
    }
    
    private void TeleportToCollectableShop()
    {
        Plugin.State = PluginState.Teleporting;
        if (_dataManager.GetExcelSheet<TerritoryType>().FirstOrDefault(t => t.RowId == _clientState.TerritoryType).PlaceName.Value.Name.ExtractText() == _configuration.PreferredCollectableShop.Name) return;
        _taskManager.InsertDelay(5000);
        if (TeleportHelper.TryFindAetheryteByName(_configuration.PreferredCollectableShop.Name, out var aetheryte,
                out var name))
        {
            TeleportHelper.Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
            _taskManager.Insert(() =>
            {
                if (_dataManager.GetExcelSheet<TerritoryType>()
                                .FirstOrDefault(t => t.RowId == _clientState.TerritoryType).PlaceName.Value.Name
                                .ExtractText() == _configuration.PreferredCollectableShop.Name) return true;
                return false;
            });
        }
        else
        {
            ForceStop("Error while finding aetheryte for collectable shop");
        }
    }
    public unsafe void TradeEachCollectable()
    {
        string currentItem = string.Empty;
        Plugin.State = PluginState.ExchangingItems;
        foreach (var item in _currentCollectables)
        {
            _log.Debug(_currentCollectables.Count.ToString());
            if (!_collectableShopItems.TryGetFirst(i=> i.Name.Contains(item.Name.ExtractText(), StringComparison.OrdinalIgnoreCase), out var value))
            {
                PluginLog.Error($"error finding job for item: {item.Name.ExtractText()}");
                continue;
            }
            
            if (!IsRunning) return;
            _taskManager.EnqueueDelay(300);
            if (currentItem != item.Name.ExtractText())
            {
                currentItem = item.Name.ExtractText();
                _log.Debug(value.Class.ToString());
                _taskManager.Enqueue(() => _collectibleWindowHandler.SelectJob((uint)value.Class));
                _taskManager.Enqueue(() => _collectibleWindowHandler.SelectJob((uint)value.Class));
                _taskManager.EnqueueDelay(300);
                _taskManager.Enqueue(() => _collectibleWindowHandler.SelectItem(item.Name.ExtractText()));
                _taskManager.EnqueueDelay(300);
            }
            else
            {
                _taskManager.EnqueueDelay(300);
            }
            _taskManager.Enqueue(() =>
            {
                if ( 200 >(4000 -(value.ScripType == ScripType.Purple
                                      ? _collectibleWindowHandler.PurpleScripCount()
                                      : _collectibleWindowHandler.OrangeScripCount())))
                {
                    _collectibleWindowHandler.CloseWindow();
                    _log.Debug("Max scrips reached, stopping automatic turn-in");
                    _targetManager.Target = null;
                    IsRunning = false;
                    Plugin.State = PluginState.Idle;
                    _taskManager.Abort();
                    OnScripsCapped?.Invoke(true);
                }
            });
            
            _taskManager.Enqueue(()=>_log.Debug($"Collecting {value.Name}"));
            _taskManager.Enqueue(() => _collectibleWindowHandler.SubmitItem());
            _taskManager.EnqueueDelay(300);
        }
        _taskManager.EnqueueDelay(500);
        _taskManager.Enqueue(() =>
        {
            _targetManager.Target = null;
            _collectibleWindowHandler.CloseWindow();
            IsRunning = false;
            Plugin.State = PluginState.Idle;
            OnFinishTrading?.Invoke();
        });
    }
    
    public void ForceStop(string reason)
    {
        OnError?.Invoke(reason);
        _taskManager.Abort();
        IsRunning = false;
        Plugin.State = PluginState.Idle;
        _log.Error(new Exception("TheCollector has stopped unexpectedly."), reason);
    }
    public void OnTaskTimeout(TaskManagerTask task, ref long remainingTime)
    {
        ForceStop($"Task {task.Name} timed out.");
    }
    private List<Item> GetCollectablesInInventory()
    {
        return ItemHelper.GetLuminaItemsFromInventory().Where(i => i.IsCollectable).ToList().OrderBy(i => i.Name.ExtractText()).ToList();
    }

    private void LoadItems()
    {
        var fileName = "CollectableShopItems.json";
        var path = Svc.PluginInterface.AssemblyLocation.DirectoryName;
        var fullPath = System.IO.Path.Combine(path, fileName);
        try
        {
            var text = File.ReadAllText(fullPath);
            _collectableShopItems = JsonSerializer.Deserialize<List<CollectableShopItem>>(text) ?? new List<CollectableShopItem>();
            _log.Debug($"Loaded {_collectableShopItems.Count} items");
        }
        catch (Exception e)
        {
            _log.Error(e.Message);
        }
    }
}
