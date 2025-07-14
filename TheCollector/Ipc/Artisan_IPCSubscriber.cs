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

    [EzIPC("Artisan.IsListRunning")]
    public readonly Func<bool> IsListRunning;

    [EzIPC("Artisan.IsListPaused")]
    public  readonly Func<bool> IsListPaused;

    [EzIPC("Artisan.GetStopRequest")]
    public readonly Func<bool> GetStopRequest;

    [EzIPC("Artisan.SetStopRequest")]
    public readonly Action<bool> SetStopRequest;

    [EzIPC("Artisan.SetListPause")]
    public readonly Action<bool> SetListPause;

    [EzIPC("Artisan.CraftItem")]
    public readonly Action<ushort, int> CraftItem;

    [EzIPC("Artisan.IsBusy")]
    public readonly Func<bool> IsBusy;

    public bool IsEnabled => IPCSubscriber_Common.IsReady("Artisan");

    public void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
}
