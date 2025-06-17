using System;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using TheCollector.CollectableManager;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;

namespace TheCollector;

public class AutomationHandler : IDisposable
{
    private readonly IPluginLog _log;
    private readonly CollectableAutomationHandler _collectableAutomationHandler;
    private readonly Configuration _config;
    private readonly ScripShopAutomationHandler _scripShopAutomationHandler;
    private readonly IChatGui _chatGui;
    
    
    
    public AutomationHandler(
        IPluginLog log,CollectableAutomationHandler collectableAutomationHandler, Configuration config, ScripShopAutomationHandler scripShopAutomationHandler, IChatGui chatGui, GatherbuddyReborn_IPCSubscriber gatherbuddyReborn_IPCSubscriber)
    {
        _log = log;
        _collectableAutomationHandler = collectableAutomationHandler;
        _collectableAutomationHandler.OnScripsCapped += OnScripCapped;
        _collectableAutomationHandler.OnError += OnError;
        _config = config;
        _scripShopAutomationHandler = scripShopAutomationHandler;
        _scripShopAutomationHandler.OnError += OnError;
        _scripShopAutomationHandler.OnFinishedTrading += OnFinishedTrading;
        _chatGui = chatGui;
        gatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged += OnAutoGatherStatusChanged;
    }
    private void OnAutoGatherStatusChanged(bool enabled)
    {
        if (!enabled && _config.CollectOnAutogatherDisabled)
        {
            _collectableAutomationHandler.Start();
        }
    }
    public void Invoke()
    {
        _collectableAutomationHandler.Start();
    }
    private void OnFinishedTrading()
    {
        if (_collectableAutomationHandler.HasCollectible)
        {
            _collectableAutomationHandler.RestartAfterTrading();
        }
        
    }

    private void OnError(string reason)
    {
        _chatGui.Print($"Automation threw an error, {reason}", "TheCollector");
    }
    private void OnScripCapped(bool capped)
    {
        if (capped)
        {
            _scripShopAutomationHandler.Start();
        }
    }
    public void Dispose()
    {
        _collectableAutomationHandler.OnScripsCapped -= OnScripCapped;
        _collectableAutomationHandler.OnError -= OnError;
        _scripShopAutomationHandler.OnError -= OnError;
        _scripShopAutomationHandler.OnFinishedTrading -= OnFinishedTrading;
    }
}
