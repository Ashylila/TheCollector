using System;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using TheCollector.CollectableManager;
using TheCollector.FirmamentManager;

namespace TheCollector.Utility;

public sealed class CollectableTurnInWatcher : IDisposable
{
    private const string CollectablesShopAddon = "CollectablesShop";

    private readonly IFramework _framework;
    private readonly Configuration _config;
    private readonly PipelineRegistry _pipelines;
    private readonly CollectableAutomationHandler _collectables;
    private readonly FirmamentTurnInHandler _firmament;
    private readonly Stopwatch _watch = new();

    private bool _collectablesWasOpen;
    private bool _hwdWasOpen;

    public int PollInterval { get; set; } = 250;

    public CollectableTurnInWatcher(
        IFramework framework,
        Configuration config,
        PipelineRegistry pipelines,
        CollectableAutomationHandler collectables,
        FirmamentTurnInHandler firmament)
    {
        _framework = framework;
        _config = config;
        _pipelines = pipelines;
        _collectables = collectables;
        _firmament = firmament;
        _framework.Update += OnUpdate;
        _watch.Start();
    }

    private void OnUpdate(IFramework _)
    {
        if (_watch.ElapsedMilliseconds < PollInterval)
            return;
        _watch.Restart();

        if (!_config.AutoTurnInOnWindowOpen)
            return;

        var collectablesOpen = Addons.Ready(CollectablesShopAddon);
        var hwdOpen = Addons.Ready(FirmamentTurnInWindowHandler.AddonName);

        var collectablesJustOpened = collectablesOpen && !_collectablesWasOpen;
        var hwdJustOpened = hwdOpen && !_hwdWasOpen;
        _collectablesWasOpen = collectablesOpen;
        _hwdWasOpen = hwdOpen;

        if (AnyPipelineRunning())
            return;

        if (collectablesJustOpened && _collectables.HasCollectible)
            _collectables.StartFromOpenWindow();
        else if (hwdJustOpened && _firmament.HasCollectible)
            _firmament.StartFromOpenWindow();
    }

    private bool AnyPipelineRunning()
    {
        foreach (var p in _pipelines.All)
            if (p.IsRunning)
                return true;
        return false;
    }

    public void Dispose() => _framework.Update -= OnUpdate;
}
