using System;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using TheCollector.Data;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public class ArtisanWatcher : IDisposable
{
    private readonly IFramework _framework;
    private readonly Artisan_IPCSubscriber ArtisanIpc;
    private readonly Stopwatch UpdateWatch = new();
    private bool _wasCrafting;
    private readonly Configuration _configuration;
    
    public event Action<WatchType>? OnCraftingFinished;
    
    public int PollInterval { get; set; } = 250;

    public ArtisanWatcher(IFramework framework, Artisan_IPCSubscriber artisanIpc, Configuration config)
    {
        _framework = framework;
        ArtisanIpc = artisanIpc;
        _configuration = config;
        Init();
    }

    private void Init()
    {
        _framework.Update += OnUpdate;
        UpdateWatch.Start();
    }
    private void OnUpdate(IFramework framework)
    {
        if (UpdateWatch.ElapsedMilliseconds < PollInterval)
            return;

        UpdateWatch.Restart();
        if (PlayerHelper.IsInDuty)
            return;

        bool isCrafting = ArtisanIpc.IsListRunning();

        if (_wasCrafting && !isCrafting)
        {
            OnCraftingFinished?.Invoke(WatchType.Crafting);
        }

        _wasCrafting = isCrafting;
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
    }
}
