using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace TheCollector.Data;

public enum PluginState
{
    [Description("Moving to vendor")]     MovingToCollectableVendor,
    [Description("Teleporting")]          Teleporting,
    [Description("Exchanging items")]     ExchangingItems,
    [Description("Spending scrip")]       SpendingScrip,
    [Description("Idle")]                 Idle,
    [Description("AutoRetainer running")] AutoRetainer,
    [Description("Deliveroo running")]    Deliveroo,
    [Description("Crafting Artisan list")] ProcessingArtisanList,
}

public static class PluginStateExtensions
{
    // Display strings live on the enum members via [Description]; the reflected lookups
    // are cached once so the per-frame UI calls are just dictionary reads.
    private static readonly Dictionary<PluginState, string> Labels =
        Enum.GetValues<PluginState>()
            .ToDictionary(
                s => s,
                s => typeof(PluginState).GetField(s.ToString())
                         ?.GetCustomAttribute<DescriptionAttribute>()?.Description
                     ?? s.ToString());

    /// <summary>The human-readable label declared on the enum member's [Description].</summary>
    public static string Label(this PluginState state) => Labels[state];
}
