using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.Sheets;
using TheCollector.Utility;

namespace TheCollector.CollectibleManager;

public class CollectableAutomationHandler
{
    #pragma warning disable
    
    private readonly TaskManager _taskManager;
    private readonly IPluginLog _log;
    private readonly CollectibleWindowHandler _collectibleWindowHandler;
    private readonly IDataManager _dataManager;
    

    private string _currentItem;
    private List<Item> _currentCollectables = new();
    
    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);

    public CollectableAutomationHandler(TaskManager taskManager, IPluginLog log, CollectibleWindowHandler collectibleWindowHandler, IDataManager data)
    {
        _taskManager = taskManager;
        _log = log;
        _collectibleWindowHandler = collectibleWindowHandler;
        _dataManager = data;
    }

    public void Start()
    {
        _log.Debug(GetCollectablesInInventory().Count.ToString());
        if (GetCollectablesInInventory().Count == 0) return;
        _currentCollectables = GetCollectablesInInventory();
    }
//TODO:Teleporting to collectable shop

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
            _taskManager.EnqueueDelay(500);
            _taskManager.Enqueue(()=>_collectibleWindowHandler.SelectItem(item.Name.ExtractText()));
            _taskManager.EnqueueDelay(500);
            _taskManager.Enqueue(() => _collectibleWindowHandler.SubmitItem());
            _taskManager.EnqueueDelay(500);
        }
    }
    
    private List<Item> GetCollectablesInInventory()
    {
        return ItemHelper.GetLuminaItemsFromInventory().Where(i => i.IsCollectable).ToList() ?? new List<Item>();
    }
}
