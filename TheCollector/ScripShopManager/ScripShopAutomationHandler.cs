using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using TheCollector.CollectableManager;
using TheCollector.Data;

namespace TheCollector.ScripShopManager;

public class ScripShopAutomationHandler
{
    private readonly TaskManager _taskManager;
    private readonly IPluginLog _log;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private TaskManagerConfiguration _config = new  TaskManagerConfiguration()
    {
        ExecuteDefaultConfigurationEvents = false,
        ShowDebug = true,
        
    };
    private readonly ScripShopWindowHandler _scripShopWindowHandler;
    private readonly CollectableAutomationHandler _collectableAutomationHandler;
    public bool IsRunning { get; private set; } = false;
    public ScripShopAutomationHandler(IPluginLog log, ITargetManager targetManager, IFramework framework, IClientState clientState, Configuration configuration, IObjectTable objectTable, ScripShopWindowHandler handler, CollectableAutomationHandler collectableAutomationHandler)
    {
        _taskManager = new TaskManager(_config);
        _config.OnTaskTimeout += OnTaskTimeout;
        _log = log;
        _targetManager = targetManager;
        _framework = framework;
        _clientState = clientState;
        _configuration = configuration;
        _objectTable = objectTable;
        _scripShopWindowHandler = handler;
        _collectableAutomationHandler = collectableAutomationHandler;
    }

    public unsafe void Start()
    {
        Plugin.State = PluginState.SpendingScrip;
        _taskManager.Enqueue(() =>
        {
            _targetManager.Target = _objectTable.FirstOrDefault(a => a.Name.TextValue.Contains(
                                                                    "scrip", StringComparison.OrdinalIgnoreCase));
        });
        _taskManager.Enqueue(() =>
        {
            TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);
        });
        _taskManager.Enqueue(()=>_scripShopWindowHandler.OpenShop());
        _taskManager.EnqueueDelay(1500);
        PurchaseItems();
    }

    public void PurchaseItems()
    {
        _log.Debug(_configuration.ItemsToPurchase.Count.ToString());
        foreach (var scripItem in _configuration.ItemsToPurchase)
        {
            _taskManager.Enqueue(() =>
            {
                _scripShopWindowHandler.SelectPage(scripItem.Item.Page);
            });
            _taskManager.EnqueueDelay(100);
            _taskManager.Enqueue(()=>
            {
                _log.Debug(scripItem.Item.SubPage.ToString());
                _scripShopWindowHandler.SelectSubPage(scripItem.Item.SubPage);
            });
            _taskManager.EnqueueDelay(100);
            
            var quantity = scripItem.Quantity - scripItem.AmountPurchased;
            if ((quantity * scripItem.Item.ItemCost) > _scripShopWindowHandler.ScripCount())
            {
                quantity = _scripShopWindowHandler.ScripCount() / (int)scripItem.Item.ItemCost;
            }

            _taskManager.Enqueue(() =>
            {
                _scripShopWindowHandler.SelectItem(scripItem.Item.Index, quantity);
            });
            _taskManager.EnqueueDelay(100);
            _taskManager.Enqueue(()=>
            {
                _scripShopWindowHandler.PurchaseItem();
                scripItem.AmountPurchased += quantity;
                _configuration.Save();
            });
        }
        _taskManager.Enqueue((() =>
                                 {
                                     if (_collectableAutomationHandler.HasCollectible)
                                     {
                                         _collectableAutomationHandler.RestartAfterTrading();
                                     }
                                 }));
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
    
}
