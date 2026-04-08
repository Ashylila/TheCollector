using System;
using ECommons.EzIpcManager;

namespace TheCollector.Ipc;

public static class Deliveroo_IPCSubscriber
{
    private static readonly EzIPCDisposalToken[] _disposalToken = EzIPC.Init(typeof(Deliveroo_IPCSubscriber), "Deliveroo", SafeWrapper.IPCException);

    [EzIPC("Deliveroo.IsTurnInRunning")]
    internal static readonly Func<bool> IsTurnInRunning;

    public static void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalToken);
    }
}
