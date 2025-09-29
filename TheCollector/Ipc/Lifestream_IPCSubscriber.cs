using System;
using ECommons.EzIpcManager;

namespace TheCollector.Ipc;

public class Lifestream_IPCSubscriber : IDisposable
{
    private readonly EzIPCDisposalToken[] _disposalTokens;
    
    public Lifestream_IPCSubscriber()
    {
        _disposalTokens = EzIPC.Init(this, "Lifestream", SafeWrapper.IPCException);
    }

    [EzIPC]
    public Func<bool> IsBusy;

    [EzIPC]
    public Action<string> ExecuteCommand;
    
    public void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
}
