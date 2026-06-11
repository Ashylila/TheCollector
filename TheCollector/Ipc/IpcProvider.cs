using System;
using ECommons.EzIpcManager;
using TheCollector.Data;

namespace TheCollector.Ipc;

public class IpcProvider : IDisposable
{
    private readonly AutomationHandler _automationHandler;
    private readonly StatusService _status;
    private readonly EzIPCDisposalToken[] _disposalTokens;

    public IpcProvider(AutomationHandler handler, StatusService status)
    {
        _automationHandler = handler;
        _status = status;
        _disposalTokens = EzIPC.Init(this, Plugin.InternalName);
    }

    [EzIPC]
    public void Collect() =>
        _automationHandler.Invoke();
    [EzIPC]
    public string GetStateText() =>
        _status.Current.ToString();

    [EzIPC]
    public bool IsRunning() =>
        _automationHandler.IsRunning;

    public void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
}
