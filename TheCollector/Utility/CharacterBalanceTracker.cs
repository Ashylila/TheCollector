using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using TheCollector.Data.Models;

namespace TheCollector.Utility;

public class CharacterBalanceTracker : IDisposable
{
    private static readonly uint[] ScripCurrencies =
    {
        CurrencyHelper.PurpleCrafterScripItemId,
        CurrencyHelper.PurpleGathererScripItemId,
        CurrencyHelper.OrangeCrafterScripItemId,
        CurrencyHelper.OrangeGathererScripItemId,
    };

    private readonly Configuration _config;
    private readonly IClientState _clientState;
    private readonly PlogonLog _log;
    private ulong _lastSampledContentId;

    public CharacterBalanceTracker(Configuration config, IClientState clientState, PlogonLog log)
    {
        _config = config;
        _clientState = clientState;
        _log = log;
        _clientState.Login += OnLogin;
    }

    public IReadOnlyCollection<CharacterBalance> KnownCharacters =>
        _config.CharacterBalances.Values
            .OrderByDescending(c => c.LastSampledAt)
            .ToList();

    private void OnLogin()
    {
        try { SampleNow(); }
        catch (Exception ex) { _log.Error($"CharacterBalanceTracker login sample failed: {ex.Message}"); }
    }

    public unsafe void SampleNow()
    {
        if (!Player.Available) return;
        var contentId = Player.CID;
        if (contentId == 0) return;

        var cur = CurrencyManager.Instance();
        if (cur == null) return;

        if (!_config.CharacterBalances.TryGetValue(contentId, out var entry))
        {
            entry = new CharacterBalance { ContentId = contentId };
            _config.CharacterBalances[contentId] = entry;
        }

        entry.LastSeenName = Player.Name;
        entry.LastSeenWorld = Player.HomeWorld.ValueNullable?.Name.ExtractText() ?? "";
        entry.LastSampledAt = DateTime.UtcNow;

        foreach (var itemId in ScripCurrencies)
            entry.ScripBalances[itemId] = (int)cur->GetItemCount(itemId);

        _lastSampledContentId = contentId;
        _config.Save();
    }

    public void Dispose()
    {
        _clientState.Login -= OnLogin;
    }
}
