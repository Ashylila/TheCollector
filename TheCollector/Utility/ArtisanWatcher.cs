using System;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Havok.Animation.Rig;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public class ArtisanWatcher : IDisposable
{
    private readonly IFramework _framework;
    private bool _wasCrafting;
    private Artisan_IPCSubscriber ArtisanIpc;
    public Configuration _config;
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

    public ArtisanWatcher(IFramework framework, Artisan_IPCSubscriber artisanIpc, Configuration configuration)
    {
        _framework = framework;
        ArtisanIpc = artisanIpc;
        _config = configuration;
        Init();
    }
    private void Init()
    {
        _framework.Update += OnUpdate;
    }
    private void OnUpdate(IFramework framework)
    {
        if (!_notInDutyTerritories.Contains(Player.TerritoryIntendedUse) || !_config.CollectOnFinishCraftingList) 
            return;
        bool isCrafting = false;
        try { isCrafting = ArtisanIpc.IsListRunning(); } 
        catch (Exception ex) { Svc.Log.Debug($"Artisan IPC call failed: {ex.Message}"); }

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
