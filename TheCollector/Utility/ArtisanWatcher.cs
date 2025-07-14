using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using Lumina.Excel.Sheets;
using TheCollector.Ipc;
using Action = System.Action;

namespace TheCollector.Utility;

public class ArtisanWatcher : IDisposable
{
    private readonly IFramework _framework;
    private readonly Artisan_IPCSubscriber ArtisanIpc;
    private readonly Stopwatch UpdateWatch = new();
    private bool _wasCrafting;
    private readonly Configuration _configuration;

    private readonly TerritoryIntendedUseEnum[] _notInDutyTerritories =
    {
        TerritoryIntendedUseEnum.City_Area,
        TerritoryIntendedUseEnum.Open_World,
        TerritoryIntendedUseEnum.Inn,
        TerritoryIntendedUseEnum.Barracks,
        TerritoryIntendedUseEnum.Gold_Saucer,
        TerritoryIntendedUseEnum.Island_Sanctuary,
        TerritoryIntendedUseEnum.Housing_Instances
    };

    public event Action? OnCraftingFinished;
    
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
        if (!_notInDutyTerritories.Contains(Player.TerritoryIntendedUse) || !_configuration.CollectOnFinishCraftingList)
            return;

        bool isCrafting = ArtisanIpc.IsListRunning();

        if (_wasCrafting && !isCrafting)
        {
            OnCraftingFinished?.Invoke();
        }

        _wasCrafting = isCrafting;
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
    }
}
