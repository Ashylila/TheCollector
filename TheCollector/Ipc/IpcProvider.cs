using System;
using ECommons.EzIpcManager;
using TheCollector.Data;

namespace TheCollector.Ipc;

public class IpcProvider : IDisposable
{
    private readonly AutomationHandler _automationHandler;
    private readonly Plugin _plugin;
    private readonly EzIPCDisposalToken[] _disposalTokens;

    public IpcProvider(AutomationHandler handler, Plugin plugin)
    {
        _automationHandler = handler;
        _plugin = plugin;
        _disposalTokens = EzIPC.Init(this, Plugin.InternalName);
    }

    [EzIPC]
    public void Collect() =>
        _automationHandler.Invoke();
    [EzIPC]
    public string GetStateText() =>
        Plugin.State.ToString();

    [EzIPC]
    public bool IsRunning() =>
        _automationHandler.IsRunning;

    public void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
}
