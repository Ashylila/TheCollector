using System;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public class GatherBuddyService : IDisposable
{
    public GatherBuddyService()
    {
        SetAutoGatherEnabled(false);
    }
    public bool IsEnabled => GatherbuddyReborn_IPCSubscriber.IsEnabled;
    public bool IsAutoGatherEnabled => GatherbuddyReborn_IPCSubscriber.IsAutoGatherEnabled();
    public void SetAutoGatherEnabled(bool enabled) => GatherbuddyReborn_IPCSubscriber.SetAutoGatherEnabled(enabled);
    public bool IsAutoGatherWaiting => GatherbuddyReborn_IPCSubscriber.IsAutoGatherWaiting();
    public event Action<bool> OnAutoGatherStatusChanged
    {
        add => GatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged += value;
        remove => GatherbuddyReborn_IPCSubscriber.OnAutoGatherStatusChanged -= value;
    }
    public void Dispose()
    {
        GatherbuddyReborn_IPCSubscriber.Dispose();
    }
}
