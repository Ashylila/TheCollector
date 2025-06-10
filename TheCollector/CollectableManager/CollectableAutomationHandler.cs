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
    private readonly IPluginLog _log;
    private readonly CollectableWindowHandler _collectibleWindowHandler;
    private readonly IDataManager _dataManager;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyService;
    public ScripShopAutomationHandler ScripShopAutomationHandler { get; set; }
    private List<CollectableShopItem> _collectableShopItems = new();
    
    
    private TaskManagerConfiguration _config = new  TaskManagerConfiguration()
    {
        ExecuteDefaultConfigurationEvents = false,
        ShowDebug = true,
        
    };

    public bool IsRunning = false;
    private string _currentItem;
    private List<Item> _currentCollectables = new();
    
    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);

    public CollectableAutomationHandler( IPluginLog log, CollectableWindowHandler collectibleWindowHandler, IDataManager data, Configuration config, IObjectTable objectTable, ITargetManager targetManager, IFramework frameWork, IClientState clientState, GatherbuddyReborn_IPCSubscriber gatherbuddyService )
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
        _gatherbuddyService = gatherbuddyService;
        
        LoadItems();
        Init();
    }
    public void Init()
    {
        _gatherbuddyService.OnAutoGatherStatusChanged += Invoke;
        _log.Debug("TheCollector loaded and ready to collect.");
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

    public void Invoke(bool disabled) // enabled
    {
        _log.Debug("ibeencalled");
        if (_configuration.CollectOnAutogatherDisabled && !disabled)
        {
            _log.Debug("Gatherbuddy disabled, starting to collect");
            Start();
        }
    }
    
    private unsafe void MoveToCollectableShop()
    {
        Plugin.State = PluginState.MovingToCollectableVendor;
        var loc = _configuration.PreferredCollectableShop.Location;
        _log.Debug($"vnav moveto {loc.X} {loc.Y.ToString().Replace(",", ".")} {loc.Z}");
        VNavmesh_IPCSubscriber.Path_MoveTo([loc], false);
        var tasks = new[]
        {
            new TaskManagerTask(() =>
            {
                if (PlayerHelper.GetDistanceToPlayer(_configuration.PreferredCollectableShop.Location) > 1) return false;
                return true;
            }),
            new TaskManagerTask(() =>
            {
                _targetManager.Target = _objectTable.FirstOrDefault(a => a.Name.TextValue.Contains(
                                                                        "collectable", StringComparison.OrdinalIgnoreCase));
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

    public unsafe void RestartAfterTrading()
    {
        var tasks = new[]
        {
            new TaskManagerTask(() =>
            {
                if (PlayerHelper.GetDistanceToPlayer(_configuration.PreferredCollectableShop.Location) > 1) return false;
                return true;
            }),
            new TaskManagerTask(() =>
            {
                _targetManager.Target = _objectTable.FirstOrDefault(a => a.Name.TextValue.Contains(
                                                                        "collectable", StringComparison.OrdinalIgnoreCase));
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
        _taskManager.Enqueue(()=>TradeEachCollectable(), nameof(TradeEachCollectable));
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

            _taskManager.Enqueue(() =>
            {
                if (value.Amount > (4000 -(value.ScripType == 0
                                               ? _collectibleWindowHandler.PurpleScripCount()
                                               : _collectibleWindowHandler.OrangeScripCount())))
                {
                    _log.Debug("Max scrips reached, stopping automatic turn-in");
                    _targetManager.Target = null;
                    _taskManager.Abort();
                    _collectibleWindowHandler.CloseWindow();
                    IsRunning = false;
                    Plugin.State = PluginState.Idle;
                    ScripShopAutomationHandler.Start();
                    return;
                }
            });
            if (currentItem != item.Name.ExtractText())
            {
                currentItem = item.Name.ExtractText();
                _taskManager.Enqueue(() => _collectibleWindowHandler.SelectJob((uint)value.Class));
                _taskManager.EnqueueDelay(300);
                _taskManager.Enqueue(() => _collectibleWindowHandler.SelectItem(item.Name.ExtractText()));
                _taskManager.EnqueueDelay(300);
            }
            else
            {
                _taskManager.EnqueueDelay(300);
            }
            
            _taskManager.Enqueue(()=>_log.Debug($"Collecting {value.ToString()}"));
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
        });
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
