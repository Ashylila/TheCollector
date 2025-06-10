using System;
using ECommons.EzIpcManager;

namespace TheCollector.Ipc;

public class GatherbuddyReborn_IPCSubscriber : IDisposable
{
    [EzIPC] internal Func<bool>? IsAutoGatherEnabled;
    [EzIPC] internal Action<bool>? SetAutoGatherEnabled;
    [EzIPC] internal Func<bool>? IsAutoGatherWaiting;

    public event Action<bool>? OnAutoGatherStatusChanged;

    private readonly EzIPCDisposalToken[] _disposalTokens;

    public GatherbuddyReborn_IPCSubscriber()
    {
        _disposalTokens = EzIPC.Init(this, "GatherBuddyReborn", SafeWrapper.IPCException);
    }

    public bool GetIsAutoGatherEnabled() => IsAutoGatherEnabled?.Invoke() ?? false;
    public void SetAutoGatherEnabledStatus(bool enabled) => SetAutoGatherEnabled?.Invoke(enabled);
    public bool GetIsAutoGatherWaiting() => IsAutoGatherWaiting?.Invoke() ?? false;

    [EzIPCEvent]
    public void AutoGatherEnabledChanged(bool enabled)
    {
        OnAutoGatherStatusChanged?.Invoke(enabled);
    }

    public void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
}


