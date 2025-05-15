using System;
using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using ECommons.Reflection;

namespace TheCollector.Ipc;

public class IPCSubscriber_Common
{
    internal static bool IsReady(string pluginName)
    {
        return DalamudReflector.TryGetDalamudPlugin(pluginName, out _, false, true);
    }

    internal static Version Version(string pluginName)
    {
        return DalamudReflector.TryGetDalamudPlugin(pluginName, out var dalamudPlugin, false, true)
            ? dalamudPlugin.GetType().Assembly.GetName().Version
            : new Version(0, 0, 0, 0);
    }

    internal static void DisposeAll(EzIPCDisposalToken[] disposalTokens)
    {
        foreach (var disposalToken in disposalTokens)
            try
            {
                disposalToken.Dispose();
            }
            catch (Exception ex)
            {
                Svc.Log.Debug($"Error while unregistering IPC: {ex}");
            }
    }
}