using System;
using ECommons.EzIpcManager;

namespace TheCollector.Ipc;

public class Artisan_IPCSubscriber
{
    private readonly EzIPCDisposalToken[] _disposalTokens;
    
    public Artisan_IPCSubscriber()
    {
        _disposalTokens = EzIPC.Init(this, "Artisan", SafeWrapper.IPCException);
    }

    [EzIPC("IsListRunning")]
    public readonly Func<bool> IsListRunning;

    [EzIPC("IsListPaused")]
    public  readonly Func<bool> IsListPaused;

    [EzIPC("GetStopRequest")]
    public readonly Func<bool> GetStopRequest;

    [EzIPC("SetStopRequest")]
    public readonly Action<bool> SetStopRequest;

    [EzIPC("SetListPause")]
    public readonly Action<bool> SetListPause;

    [EzIPC("CraftItem")]
    public readonly Action<ushort, int> CraftItem;

    [EzIPC("IsBusy")]
    public readonly Func<bool> IsBusy;

    public bool IsEnabled => IPCSubscriber_Common.IsReady("Artisan");

    public void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
}
