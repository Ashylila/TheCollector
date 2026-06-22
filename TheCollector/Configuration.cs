using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ECommons.DalamudServices;
using TheCollector.Data;
using TheCollector.Data.Models;
using TheCollector.Utility;
using TheCollector.Windows;

namespace TheCollector;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // Legacy field, read only by Migrate_MoveItemsToGoal. After v5 migration it
    // is emptied and the shopping list lives at Goal.ItemsToPurchase.
    public List<ItemToPurchase> ItemsToPurchase { get; set; } = new List<ItemToPurchase>();
    public bool EnableAutogatherOnFinish { get; set; } = false;
    public bool CollectOnFinishCraftingList { get; set; } = false;
    public bool ShouldCraftOnAutogatherChanged { get; set; } = false;
    public bool CollectOnAutogatherFinish { get; set; } = false;
    public bool BuyAfterEachCollect { get; set; } = false;
    public bool ResetEachQuantityAfterCompletingList { get; set; } = false;
    public bool CollectOnFinishedFishing { get; set; } = false;

    // When set, manually opening a collectables turn-in window (CollectablesShop or the
    // Firmament HWDSupply appraiser) auto-runs a turn-in-only pass on whatever is in inventory.
    public bool AutoTurnInOnWindowOpen { get; set; } = false;
    public int ArtisanListId { get; set; } = 0;
    public bool PauseArtisanOnInventoryFull { get; set; } = true;
    public int ArtisanInventoryFullThreshold { get; set; } = 3;
    public int LastSeenVersion { get; set; } = ChangelogUi.LastChangelogVersion;
    public bool CheckForVenturesBetweenRuns { get; set; } = false;
    public bool CheckForDeliverooBetweenRuns { get; set; } = false;
    public ChangeLogDisplayType ChangeLogDisplayType { get; set; } = ChangeLogDisplayType.New;

    // Legacy field kept only so v5 configs can be read and migrated to
    // PreferredTerritoryId in v6. New code reads PreferredTerritoryId.
    public CollectableShop PreferredCollectableShop { get; set; } = new();

    public uint PreferredTerritoryId { get; set; } = 0;

    public ScripGoal Goal { get; set; } = new();
    public StopConditions Stop { get; set; } = new();
    public DiscordNotificationSettings Discord { get; set; } = new();
    public Dictionary<ulong, CharacterBalance> CharacterBalances { get; set; } = new();
    public Dictionary<uint, int> TotalScripsSpent { get; set; } = new();

    public const int DefaultUiDelayMs = 300;

    // Legacy single global delay. No longer read by handlers or shown in the UI; kept only so
    // Migrate_SeedPerAddonDelays can fold a previously-customised value into UiDelayMsByAddon.
    public int UiDelayMs { get; set; } = DefaultUiDelayMs;

    // Per-addon interact interval overrides, keyed by AddonDelays.* / pipeline Key.
    // A missing key falls back to DefaultUiDelayMs (see GetUiDelayMs).
    public Dictionary<string, int> UiDelayMsByAddon { get; set; } = new();

    public int GetUiDelayMs(string addonKey)
        => UiDelayMsByAddon.TryGetValue(addonKey, out var ms) ? ms : DefaultUiDelayMs;

    public const int ScripCeiling = 4000;
    public int ReserveScripAmount { get; set; } = 0;

    public string? HardFailReason { get; set; }

    public RunSource ActiveRunSource { get; set; } = RunSource.Gathering;

    public Data.ScripSystem.ScripSystemId ActiveSystem { get; set; } = Data.ScripSystem.ScripSystemId.Normal;

    public ScripGoal FirmamentGoal { get; set; } = new();

    public void Save()
    {
        Svc.PluginInterface.SavePluginConfig(this);
    }
    public bool Migrate()
    {
        var changed = false;

        if (Version < 4)
        {
            changed |= Migrate_ScripsSpentKeys();
            Version = 4;
        }
        if (Version < 5)
        {
            changed |= Migrate_MoveItemsToGoal();
            Version = 5;
        }
        if (Version < 6)
        {
            changed |= Migrate_FlattenPreferredTerritoryId();
            Version = 6;
        }
        if (Version < 7)
        {
            changed |= Migrate_SeedPerAddonDelays();
            Version = 7;
        }
        if (changed) Save();
        return changed;
    }

    private bool Migrate_SeedPerAddonDelays()
    {
        // Fold a previously-customised global delay into per-addon overrides so existing
        // users keep their pacing. A default (300) global just falls through to DefaultUiDelayMs.
        if (UiDelayMs == DefaultUiDelayMs) return false;
        if (UiDelayMsByAddon.Count > 0) return false;
        foreach (var def in AddonDelays.All)
            UiDelayMsByAddon[def.Key] = UiDelayMs;
        return true;
    }

    private bool Migrate_FlattenPreferredTerritoryId()
    {
        if (PreferredTerritoryId != 0) return false;
        if (PreferredCollectableShop?.TerritoryId is not (uint t and not 0)) return false;
        PreferredTerritoryId = t;
        return true;
    }

    private bool Migrate_MoveItemsToGoal()
    {
        if (ItemsToPurchase.Count == 0) return false;
        if (Goal.ItemsToPurchase.Count > 0) { ItemsToPurchase.Clear(); return true; }
        Goal.ItemsToPurchase = ItemsToPurchase;
        ItemsToPurchase = new List<ItemToPurchase>();
        return true;
    }

    private bool Migrate_ScripsSpentKeys()
    {
        if (TotalScripsSpent.Count == 0) return false;

        var migrated = new Dictionary<uint, int>(TotalScripsSpent.Count);
        var changed = false;
        foreach (var (key, value) in TotalScripsSpent)
        {
            var newKey = CurrencyHelper.NormalizeScripCurrencyId(key);
            if (newKey == 0) newKey = key;
            if (newKey != key) changed = true;
            migrated.TryGetValue(newKey, out var prev);
            migrated[newKey] = prev + value;
        }
        if (!changed) return false;
        TotalScripsSpent = migrated;
        return true;
    }

}
