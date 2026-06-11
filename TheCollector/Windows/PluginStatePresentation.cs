using System.Numerics;
using TheCollector.Data;
using TheCollector.Utility;

namespace TheCollector.Windows;

internal static class PluginStatePresentation
{
    public static (Vector4 Color, string Label, bool IsActive) Describe(PluginState state, string? detail = null)
    {
        var color = state switch
        {
            PluginState.Teleporting               => UiTheme.Warning,
            PluginState.MovingToCollectableVendor => UiTheme.Warning,
            PluginState.ExchangingItems           => UiTheme.Success,
            PluginState.SpendingScrip             => UiTheme.Success,
            PluginState.AutoRetainer              => UiTheme.Info,
            PluginState.Deliveroo                 => UiTheme.Info,
            _                                     => UiTheme.Idle,
        };

        var label = state.Label();
        if (!string.IsNullOrEmpty(detail))
            label = $"{label} {detail}";

        return (color, label, IsActive: state != PluginState.Idle);
    }
}
