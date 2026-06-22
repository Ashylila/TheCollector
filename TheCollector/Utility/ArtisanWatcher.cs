using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using TheCollector.Data;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public class ArtisanWatcher : IDisposable
{
    private readonly IFramework _framework;
    private readonly Artisan_IPCSubscriber ArtisanIpc;
    private readonly Stopwatch UpdateWatch = new();
    private bool _wasCrafting;
    private bool _pausedByUs;
    private DateTime _turnInRetryAt = DateTime.MinValue;
    private DateTime _suppressUntil = DateTime.MinValue;
    private readonly Configuration _configuration;
    private readonly PlogonLog _log;
    private readonly FirmamentCatalog _firmamentCatalog;
    private readonly StatusService _status;
    public event Action<WatchType>? OnCraftingFinished;
    public event Action? OnInventoryFullDuringCrafting;
    public int PollInterval { get; set; } = 250;
    public bool IsPausedByUs => _pausedByUs;

    // The Firmament reports as a duty (its territory intended-use isn't Open World/City),
    // so the bare IsInDuty guard would silence the watcher there. Mirror the exception
    // AutomationHandler.Invoke() already makes for the Firmament territory.
    private bool InFirmament => _firmamentCatalog.TerritoryId != 0
                               && Svc.ClientState.TerritoryType == _firmamentCatalog.TerritoryId;

    public ArtisanWatcher(IFramework framework, Artisan_IPCSubscriber artisanIpc, Configuration config, PlogonLog log, FirmamentCatalog firmamentCatalog, StatusService status)
    {
        _framework = framework;
        ArtisanIpc = artisanIpc;
        _configuration = config;
        _log = log;
        _firmamentCatalog = firmamentCatalog;
        _status = status;
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
        if (PlayerEx.IsInDuty && !InFirmament)
            return;

        if (_pausedByUs)
            return;

        if (DateTime.UtcNow < _suppressUntil)
        {
            _wasCrafting = ArtisanIpc.IsListRunning();
            return;
        }

        bool isCrafting = ArtisanIpc.IsListRunning();

        if (_wasCrafting && !isCrafting)
        {
            _turnInRetryAt = DateTime.MinValue;
            OnCraftingFinished?.Invoke(WatchType.Crafting);
        }
        else if (!_wasCrafting && isCrafting)
        {
            _turnInRetryAt = DateTime.MinValue;
        }
        else if (isCrafting
                 && DateTime.UtcNow >= _turnInRetryAt
                 && _configuration.PauseArtisanOnInventoryFull
                 && ShouldPauseForInventory())
        {
            ArtisanIpc.SetStopRequest(true);
            _pausedByUs = true;
            OnInventoryFullDuringCrafting?.Invoke();
        }

        UpdateCraftingStatus(isCrafting);
        _wasCrafting = isCrafting;
    }

    // Surface a dedicated status while a list crafts. Only ever toggle between Idle and
    // our own state so we never clobber a status a turn-in/buy pipeline currently owns.
    // (During our own inventory-full pause we return early above, leaving status to the
    // turn-in pipeline.)
    private void UpdateCraftingStatus(bool isCrafting)
    {
        if (isCrafting)
        {
            if (_status.Current == PluginState.Idle)
                _status.Set(PluginState.ProcessingArtisanList);
        }
        else if (_status.Current == PluginState.ProcessingArtisanList)
        {
            _status.SetIdle();
        }
    }

    private bool ShouldPauseForInventory()
    {
        if (!ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable))
            return false;

        var threshold = Math.Max(0, _configuration.ArtisanInventoryFullThreshold);
        return ItemHelper.GetFreeInventorySlots() <= threshold;
    }

    public void ResumeAfterTurnIn()
    {
        if (!_pausedByUs) return;
        _pausedByUs = false;
        _wasCrafting = false;
        if (!ArtisanIpc.IsEnabled) return;
        if (ArtisanIpc.IsListRunning() && ArtisanIpc.GetStopRequest())
            ArtisanIpc.SetStopRequest(false);
        else
            ArtisanIpc.StartListById(_configuration.ArtisanListId);
    }

    public void CancelPause()
    {
        if (!_pausedByUs) return;
        if (ArtisanIpc.IsEnabled)
            ArtisanIpc.SetStopRequest(false);
        _pausedByUs = false;
        _turnInRetryAt = DateTime.UtcNow.AddSeconds(60);
        _log.Debug("Inventory-full turn-in could not start; retrying in 60s.");
    }

    public void AbandonPause()
    {
        _pausedByUs = false;
        _wasCrafting = false;
        _turnInRetryAt = DateTime.MinValue;
        SuppressAutoInvoke();
    }

    public void SuppressAutoInvoke()
    {
        _suppressUntil = DateTime.UtcNow + TimeSpan.FromSeconds(90);
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
    }
}
