using System;
using ECommons.EzIpcManager;
using TheCollector.Data;

namespace TheCollector.Ipc;

public class IpcProvider : IDisposable
{
    private readonly AutomationHandler _automationHandler;
    private readonly Plugin _plugin;
    
    public IpcProvider(AutomationHandler handler, Plugin plugin)
    {
        _automationHandler = handler;
        _plugin = plugin;
        EzIPC.Init(this, Plugin.InternalName);
    }

    public void Init() =>
        EzIPC.Init(this, Plugin.InternalName);
    
    [EzIPC]
    public void Collect() => 
        _automationHandler.Invoke();
    [EzIPC]
    public string GetStateText() =>
        Plugin.State.ToString();
    
    [EzIPC]
    public bool IsRunning() =>
        Plugin.State != PluginState.Idle;

    public void Dispose()
    {
        
    }
    
    
}
