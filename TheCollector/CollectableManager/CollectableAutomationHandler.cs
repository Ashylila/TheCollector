using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;
using ECommons.Logging;
using Lumina.Excel.Sheets;
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
    
    
    private TaskManagerConfiguration _config = new  TaskManagerConfiguration()
    {
        ExecuteDefaultConfigurationEvents = true,
        
    };

    public bool IsRunning = false;
    private string _currentItem;
    private List<Item> _currentCollectables = new();
    
    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);

    public CollectableAutomationHandler( IPluginLog log, CollectableWindowHandler collectibleWindowHandler, IDataManager data, Configuration config)
    {
        _taskManager = new TaskManager(_config);
        _config.OnTaskTimeout += OnTaskTimeout;
        _log = log;
        _collectibleWindowHandler = collectibleWindowHandler;
        _dataManager = data;
        _configuration = config;
    }

    public void Start()
    {
        IsRunning = true;
        _log.Debug(GetCollectablesInInventory().Count.ToString());
        if (GetCollectablesInInventory().Count == 0) return;
        _currentCollectables = GetCollectablesInInventory();
        _taskManager.Enqueue(()=>PlayerHelper.CanAct);
        _taskManager.Enqueue(()=>TeleportToCollectableShop());
        _taskManager.EnqueueDelay(700);
        _taskManager.Enqueue(()=>PlayerHelper.CanAct);
        _taskManager.Enqueue(()=>MoveToCollectableShop());
    }
//TODO:Teleporting to collectable shop

    private void MoveToCollectableShop()
    {
        throw new NotImplementedException();
    }
    private void TeleportToCollectableShop()
    {
        if (TeleportHelper.TryFindAetheryteByName(_configuration.PreferredCollectableShop.ToString(), out var aetheryte,
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
