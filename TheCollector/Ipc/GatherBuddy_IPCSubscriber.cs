using System;
using ECommons.EzIpcManager;

namespace TheCollector.Ipc;

public static class GatherbuddyReborn_IPCSubscriber
{
    public static event Action<bool> OnAutoGatherStatusChanged;
    private static readonly EzIPCDisposalToken[] _disposalTokens;
    static GatherbuddyReborn_IPCSubscriber()
    {
        _disposalTokens = EzIPC.Init(typeof(GatherbuddyReborn_IPCSubscriber),"GatherBuddyReborn");
    }


    [EzIPC]
    internal static readonly Func<bool> IsAutoGatherEnabled;
    [EzIPC]
    internal static readonly Action<bool> SetAutoGatherEnabled;
    [EzIPC]
    internal static readonly Func<bool> IsAutoGatherWaiting;

    [EzIPCEvent]
    public static void AutoGatherEnabledChanged(bool enabled)
    {
        OnAutoGatherStatusChanged.Invoke(enabled);
    }

    internal static bool IsEnabled => IPCSubscriber_Common.IsReady("GatherbuddyReborn");

    internal static void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
}
