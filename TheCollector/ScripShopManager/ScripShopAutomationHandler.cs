using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using TheCollector.CollectableManager;
using TheCollector.Data;
using TheCollector.Utility;

namespace TheCollector.ScripShopManager;

public class ScripShopAutomationHandler
{
    private readonly TaskManager _taskManager;
    private readonly PlogonLog _log;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private TaskManagerConfiguration _config = new  TaskManagerConfiguration()
    {
        ExecuteDefaultConfigurationEvents = false,
        ShowDebug = false,
        
    };
    private readonly ScripShopWindowHandler _scripShopWindowHandler;
    public bool IsRunning { get; private set; } = false;
    
    internal static ScripShopAutomationHandler? Instance { get; private set; }
    public event Action? OnFinishedTrading;
    public event Action<string>? OnError;
    public ScripShopAutomationHandler(PlogonLog log, ITargetManager targetManager, IFramework framework, IClientState clientState, Configuration configuration, IObjectTable objectTable, ScripShopWindowHandler handler)
    {
        _config.OnTaskTimeout += OnTaskTimeout;
        _taskManager = new TaskManager(_config);
        _log = log;
        _targetManager = targetManager;
        _framework = framework;
        _clientState = clientState;
        _configuration = configuration;
        _objectTable = objectTable;
        _scripShopWindowHandler = handler;
        Instance = this;
    }

    public unsafe void Start()
    {
        IsRunning = true;
        Plugin.State = PluginState.SpendingScrip;
        _taskManager.Enqueue(() =>
        {
            var gameObj = _objectTable.FirstOrDefault(a => a.Name.TextValue.Contains("scrip", StringComparison.OrdinalIgnoreCase));
            if (gameObj == null) return false;
            TargetSystem.Instance()->Target = (GameObject*)gameObj.Address;
            return true;
        }, "Target Scrip Shop");

        _taskManager.EnqueueDelay(500); 
        
        _taskManager.Enqueue(() =>
        {
            TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);
        }, "Open Interaction with Scrip Shop");
        _taskManager.Enqueue(()=>_scripShopWindowHandler.OpenShop(), nameof(_scripShopWindowHandler.OpenShop));
        _taskManager.EnqueueDelay(1500);
        _taskManager.Enqueue(()=>PurchaseItems(), nameof(PurchaseItems));
    }

    public void PurchaseItems()
    {
        if (_configuration.ItemsToPurchase.Count == 0 || !_configuration.ItemsToPurchase.Any(i => i.Quantity > i.AmountPurchased))
        {
            ForceStop("no items available for purchase.");
        }
        foreach (var scripItem in _configuration.ItemsToPurchase)
        {
            _taskManager.Enqueue(() =>
            {
                _scripShopWindowHandler.SelectPage(scripItem.Item.Page);
            },"Select Page");
            _taskManager.EnqueueDelay(100);
            _taskManager.Enqueue(()=>
            {
                _log.Debug(scripItem.Item.SubPage.ToString());
                _scripShopWindowHandler.SelectSubPage(scripItem.Item.SubPage);
            }, "Select SubPage");
            _taskManager.EnqueueDelay(100);
            int quantity = 0;
            _taskManager.Enqueue(() =>
            {
                quantity = scripItem.Quantity - scripItem.AmountPurchased;
                if ((quantity * scripItem.Item.ItemCost) > _scripShopWindowHandler.ScripCount())
                {
                    quantity = _scripShopWindowHandler.ScripCount() / (int)scripItem.Item.ItemCost;
                }

                if (quantity > 99)
                {
                    quantity = 99;
                }
            }, "GetQuantity");

            _taskManager.Enqueue(() =>
            {
                _scripShopWindowHandler.SelectItem(scripItem.Item.Index, quantity);
            }, "Select Item, quantity");
            _taskManager.EnqueueDelay(100);
            _taskManager.Enqueue(()=>
            {
                if (quantity > 0)
                {
                    _scripShopWindowHandler.PurchaseItem();
                    scripItem.AmountPurchased += quantity;
                    _configuration.Save();
                }
            } ,"Purchase Item");
        }
        _taskManager.EnqueueDelay(500);
        _taskManager.Enqueue((() =>
                                 {
                                     Plugin.State = PluginState.Idle;
                                        IsRunning = false;
                                     _scripShopWindowHandler.CloseShop();
                                        OnFinishedTrading?.Invoke();
                                 }));
    }
    public void ForceStop(string reason)
    {
        _taskManager.Abort();
        _scripShopWindowHandler.CloseShop();
        IsRunning = false;
        Plugin.State = PluginState.Idle;
        _log.Error(new Exception(reason),"TheCollector has stopped unexpectedly.");
        OnError?.Invoke(reason);
    }
    public void OnTaskTimeout(TaskManagerTask task, ref long remainingTime)
    {
        ForceStop($"Task {task.Name} timed out.");
    }
    
}
