using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.FirmamentManager;

public static class FirmamentRouting
{
    public static StepResult RouteTick(
        IClientState clientState,
        Lifestream_IPCSubscriber lifestream,
        StatusService status,
        uint territoryId,
        ref bool issued)
    {
        status.Set(PluginState.Teleporting, "to The Firmament");
        if (territoryId == 0) return StepResult.Fail("Firmament territory not resolved.");
        if (clientState.TerritoryType == territoryId) return StepResult.Success();

        if (!IPCSubscriber_Common.IsReady("Lifestream"))
            return StepResult.Fail("Lifestream is required to reach The Firmament.");

        if (!issued && !lifestream.IsBusy())
        {
            lifestream.ExecuteCommand("firmament");
            issued = true;
        }
        return StepResult.Continue();
    }

    public static Vector3 LivePosition(IReadOnlyList<uint> dataIds, Vector3 fallback)
    {
        foreach (var id in dataIds)
        {
            var obj = Svc.Objects.FirstOrDefault(o => o.BaseId == id);
            if (obj != null) return obj.Position;
        }
        return fallback;
    }
}
