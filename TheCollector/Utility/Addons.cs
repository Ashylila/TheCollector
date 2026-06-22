using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TheCollector.Utility;

public static unsafe class Addons
{
    public static bool Ready(string name)
        => GenericHelpers.TryGetAddonByName<AtkUnitBase>(name, out var addon) && GenericHelpers.IsAddonReady(addon);

    public static bool TryGetReady(string name, out AtkUnitBase* addon)
        => GenericHelpers.TryGetAddonByName<AtkUnitBase>(name, out addon) && GenericHelpers.IsAddonReady(addon);

    public static bool TryGet(string name, out AtkUnitBase* addon)
        => GenericHelpers.TryGetAddonByName<AtkUnitBase>(name, out addon);
}
