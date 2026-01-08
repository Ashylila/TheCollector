using System;
using ECommons.EzIpcManager;
using FFXIVClientStructs;

namespace TheCollector.Ipc;

public static class Autoretainer_IPCSubscriber
{
    private static readonly EzIPCDisposalToken[] _disposalToken = EzIPC.Init(typeof(Autoretainer_IPCSubscriber), "AutoRetainer", SafeWrapper.IPCException);

    [EzIPC("AutoRetainer.GetMultiModeEnabled")]
    internal static readonly Func<bool> GetMultiModeEnabled;

    [EzIPC("AutoRetainer.SetMultiModeEnabled")]
    internal static readonly Func<bool> SetMultiModeEnabled;
    [EzIPC] 
    internal static readonly Func<bool> AreAnyRetainersAvailableForCurrentChara;
    [EzIPC] 
    internal static readonly Func<bool> IsBusy;

    public static void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalToken);
    }
}