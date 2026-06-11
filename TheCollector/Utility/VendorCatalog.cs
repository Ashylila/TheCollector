using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ECommons.DalamudServices;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using TheCollector.Data.Models;

namespace TheCollector.Utility;


public class VendorCatalog
{
    public IReadOnlyList<VendorNpc> AllVendors { get; private set; } = new List<VendorNpc>();
    public FrozenDictionary<uint, IReadOnlyList<VendorNpc>> ByTerritory { get; private set; } =
        FrozenDictionary<uint, IReadOnlyList<VendorNpc>>.Empty;
    public IReadOnlyList<uint> ServedTerritoryIds { get; private set; } = new List<uint>();

    private readonly HashSet<uint> _canonicalScripNpcIds = new();
    private readonly PlogonLog _log;

    private volatile bool _isReady;
    public bool IsReady => _isReady;

    public VendorCatalog(PlogonLog log)
    {
        _log = log;

        Task.Run(() =>
        {
            try
            {
                Build();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "VendorCatalog: background build failed; catalog will be empty.");
            }
            finally
            {
                _isReady = true;
            }
        });
    }

    private void Build()
    {
        var npcBaseSheet     = Svc.Data.GetExcelSheet<ENpcBase>();
        var npcResidentSheet = Svc.Data.GetExcelSheet<ENpcResident>();
        var levelSheet       = Svc.Data.GetExcelSheet<Level>();
        var territorySheet   = Svc.Data.GetExcelSheet<TerritoryType>();
        var inclusionSheet   = Svc.Data.GetExcelSheet<InclusionShop>();
        var seriesSheet      = Svc.Data.GetSubrowExcelSheet<InclusionShopSeries>();
        var specialSheet     = Svc.Data.GetExcelSheet<SpecialShop>();
        var collectablesSheet= Svc.Data.GetExcelSheet<CollectablesShop>();
        if (npcBaseSheet == null || npcResidentSheet == null || levelSheet == null ||
            territorySheet == null || inclusionSheet == null || seriesSheet == null ||
            specialSheet == null || collectablesSheet == null)
        {
            _log.Error("VendorCatalog: required Lumina sheets unavailable; catalog will be empty.");
            return;
        }

        var (scripInclusionShopIds, canonicalScripInclusionShopId) =
            CollectScripInclusionShops(inclusionSheet, seriesSheet, specialSheet);
        var collectablesShopIds = collectablesSheet.Select(r => r.RowId).ToHashSet();

        var (scripNpcIds, canonicalScripNpcIds, collectableNpcIds) =
            ClassifyVendorNpcs(npcBaseSheet, scripInclusionShopIds, canonicalScripInclusionShopId, collectablesShopIds);
        _canonicalScripNpcIds.UnionWith(canonicalScripNpcIds);
        _log.Debug($"VendorCatalog: classified {scripNpcIds.Count} scrip NPCs ({canonicalScripNpcIds.Count} reach canonical InclusionShop #{canonicalScripInclusionShopId}), {collectableNpcIds.Count} collectable NPCs.");

        var interestingNpcIds = new HashSet<uint>(scripNpcIds);
        interestingNpcIds.UnionWith(collectableNpcIds);
        if (interestingNpcIds.Count == 0)
        {
            _log.Error("VendorCatalog: no vendor NPCs classified; catalog will be empty.");
            return;
        }

        var resolved = new Dictionary<uint, VendorNpc>();
        foreach (var level in levelSheet)
        {
            var npcId = level.Object.RowId;
            if (npcId is < 1_000_000u or >= 11_000_000u) continue;
            if (!interestingNpcIds.Contains(npcId)) continue;
            if (resolved.ContainsKey(npcId)) continue;

            var territoryId = level.Territory.RowId;
            if (territoryId == 0) continue;
            var mapId = level.Map.RowId != 0
                ? level.Map.RowId
                : level.Territory.ValueNullable?.Map.RowId ?? 0;

            resolved[npcId] = BuildVendor(
                npcId,
                LookupNpcName(npcResidentSheet, npcId),
                territoryId,
                mapId,
                new Vector3(level.X, level.Y, level.Z),
                scripNpcIds,
                collectableNpcIds);
        }
        var levelPassCount = resolved.Count;
        _log.Debug($"VendorCatalog: Level sheet pass resolved {levelPassCount}/{interestingNpcIds.Count}.");

        ResolveFromLgb(territorySheet, npcResidentSheet, interestingNpcIds, resolved, scripNpcIds, collectableNpcIds);
        _log.Debug($"VendorCatalog: LGB pass brought total to {resolved.Count}/{interestingNpcIds.Count}.");

        var vendors = resolved.Values.ToList();

        var grouped = vendors
            .GroupBy(v => v.TerritoryId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<VendorNpc>)g.ToList());

        AllVendors         = vendors;
        ByTerritory        = grouped.ToFrozenDictionary();
        ServedTerritoryIds = grouped
            .Where(kv => kv.Value.Any(v => v.IsScripVendor)
                      && kv.Value.Any(v => v.IsCollectableVendor))
            .Select(kv => kv.Key)
            .OrderBy(id => GetTerritoryDisplayName(id), StringComparer.OrdinalIgnoreCase)
            .ToList();

        _log.Debug($"VendorCatalog: {vendors.Count} placements across {ServedTerritoryIds.Count} territories.");
    }

    public VendorNpc? GetScripVendor(uint territoryId)
    {
        if (!ByTerritory.TryGetValue(territoryId, out var list)) return null;
        return list.FirstOrDefault(v => v.IsScripVendor && _canonicalScripNpcIds.Contains(v.DataId))
            ?? list.FirstOrDefault(v => v.IsScripVendor);
    }

    public VendorNpc? GetCollectableVendor(uint territoryId)
        => ByTerritory.TryGetValue(territoryId, out var list)
            ? list.FirstOrDefault(v => v.IsCollectableVendor)
            : null;

    public static string GetTerritoryDisplayName(uint territoryId)
    {
        var ter = Svc.Data.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId);
        return ter?.PlaceName.ValueNullable?.Name.ExtractText() ?? $"#{territoryId}";
    }

    private static (HashSet<uint> ScripNpcIds, HashSet<uint> CanonicalScripNpcIds, HashSet<uint> CollectableNpcIds) ClassifyVendorNpcs(
        ExcelSheet<ENpcBase> npcBaseSheet,
        HashSet<uint> scripInclusionShopIds,
        uint canonicalScripInclusionShopId,
        HashSet<uint> collectablesShopIds)
    {
        var preHandlerSheet  = Svc.Data.GetExcelSheet<PreHandler>();
        var topicSelectSheet = Svc.Data.GetExcelSheet<TopicSelect>();
        var customTalkSheet  = Svc.Data.GetExcelSheet<CustomTalk>();

        var scripNpcIds          = new HashSet<uint>();
        var canonicalScripNpcIds = new HashSet<uint>();
        var collectableNpcIds    = new HashSet<uint>();
        var shops                = new HashSet<uint>();
        var visited              = new HashSet<(string, uint)>();

        foreach (var npc in npcBaseSheet)
        {
            shops.Clear();
            visited.Clear();
            foreach (var menuEntry in npc.ENpcData)
                CollectReachableShops(menuEntry, shops, visited, preHandlerSheet, topicSelectSheet, customTalkSheet);

            if (shops.Count == 0) continue;
            if (shops.Overlaps(scripInclusionShopIds))
                scripNpcIds.Add(npc.RowId);
            if (canonicalScripInclusionShopId != 0 && shops.Contains(canonicalScripInclusionShopId))
                canonicalScripNpcIds.Add(npc.RowId);
            if (shops.Overlaps(collectablesShopIds))
                collectableNpcIds.Add(npc.RowId);
        }

        return (scripNpcIds, canonicalScripNpcIds, collectableNpcIds);
    }

    private static void CollectReachableShops(
        RowRef menuEntry,
        HashSet<uint> shops,
        HashSet<(string, uint)> visited,
        ExcelSheet<PreHandler>? preHandlerSheet,
        ExcelSheet<TopicSelect>? topicSelectSheet,
        ExcelSheet<CustomTalk>? customTalkSheet)
    {
        if (menuEntry.RowId == 0) return;

        if (menuEntry.Is<InclusionShop>())    { shops.Add(menuEntry.RowId); return; }
        if (menuEntry.Is<SpecialShop>())      { shops.Add(menuEntry.RowId); return; }
        if (menuEntry.Is<CollectablesShop>()) { shops.Add(menuEntry.RowId); return; }

        if (menuEntry.Is<PreHandler>())
        {
            if (!visited.Add(("PreHandler", menuEntry.RowId))) return;
            if (preHandlerSheet != null && preHandlerSheet.TryGetRow(menuEntry.RowId, out var pre))
                CollectReachableShops(pre.Target, shops, visited, preHandlerSheet, topicSelectSheet, customTalkSheet);
            return;
        }

        if (menuEntry.Is<TopicSelect>())
        {
            if (!visited.Add(("TopicSelect", menuEntry.RowId))) return;
            if (topicSelectSheet != null && topicSelectSheet.TryGetRow(menuEntry.RowId, out var ts))
                foreach (var child in ts.Shop)
                    CollectReachableShops(child, shops, visited, preHandlerSheet, topicSelectSheet, customTalkSheet);
            return;
        }

        if (menuEntry.Is<CustomTalk>())
        {
            if (!visited.Add(("CustomTalk", menuEntry.RowId))) return;
            if (customTalkSheet != null && customTalkSheet.TryGetRow(menuEntry.RowId, out var ct))
                CollectReachableShops(ct.SpecialLinks, shops, visited, preHandlerSheet, topicSelectSheet, customTalkSheet);
        }
    }

    private void ResolveFromLgb(
        ExcelSheet<TerritoryType> territorySheet,
        ExcelSheet<ENpcResident> npcResidentSheet,
        HashSet<uint> interestingNpcIds,
        Dictionary<uint, VendorNpc> resolved,
        HashSet<uint> scripNpcIds,
        HashSet<uint> collectableNpcIds)
    {
        foreach (var territory in territorySheet)
        {
            if (resolved.Count == interestingNpcIds.Count) return;

            try
            {
                var bg = territory.Bg.ExtractText();
                if (string.IsNullOrEmpty(bg)) continue;
                var levelIdx = bg.IndexOf("/level/", StringComparison.Ordinal);
                if (levelIdx < 0) continue;

                var lgbPath = $"bg/{bg.Substring(0, levelIdx + 1)}level/planevent.lgb";
                var lgb = Svc.Data.GetFile<LgbFile>(lgbPath);
                if (lgb == null) continue;

                var mapId = territory.Map.RowId;

                foreach (var layer in lgb.Layers)
                {
                    foreach (var obj in layer.InstanceObjects)
                    {
                        if (obj.AssetType != LayerEntryType.EventNPC) continue;
                        var npcId = ((LayerCommon.ENPCInstanceObject)obj.Object).ParentData.ParentData.BaseId;
                        if (!interestingNpcIds.Contains(npcId)) continue;
                        if (resolved.ContainsKey(npcId)) continue;

                        var position = new Vector3(
                            obj.Transform.Translation.X,
                            obj.Transform.Translation.Y,
                            obj.Transform.Translation.Z);

                        resolved[npcId] = BuildVendor(
                            npcId,
                            LookupNpcName(npcResidentSheet, npcId),
                            territory.RowId,
                            mapId,
                            position,
                            scripNpcIds,
                            collectableNpcIds);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Debug($"VendorCatalog: LGB read failed for territory {territory.RowId}: {ex.Message}");
            }
        }
    }

    private static VendorNpc BuildVendor(
        uint npcId,
        string name,
        uint territoryId,
        uint mapId,
        Vector3 position,
        HashSet<uint> scripNpcIds,
        HashSet<uint> collectableNpcIds)
        => new()
        {
            DataId              = npcId,
            Name                = name,
            TerritoryId         = territoryId,
            MapId               = mapId,
            Position            = position,
            IsScripVendor       = scripNpcIds.Contains(npcId),
            IsCollectableVendor = collectableNpcIds.Contains(npcId),
        };

    private static string LookupNpcName(ExcelSheet<ENpcResident> sheet, uint npcId)
        => sheet.TryGetRow(npcId, out var resident)
            ? resident.Singular.ExtractText()
            : $"#{npcId}";

    private static (HashSet<uint> ScripInclusionShopIds, uint CanonicalId) CollectScripInclusionShops(
        ExcelSheet<InclusionShop> inclusionSheet,
        SubrowExcelSheet<InclusionShopSeries> seriesSheet,
        ExcelSheet<SpecialShop> specialSheet)
    {
        var scripSpecialShopIds = new HashSet<uint>();
        foreach (var shop in specialSheet)
            if (ShopHasScripCost(shop))
                scripSpecialShopIds.Add(shop.RowId);

        var scripInclusionShopIds = new HashSet<uint>();
        uint canonicalId = 0;
        var bestScore = 0;
        foreach (var inclusion in inclusionSheet)
        {
            var score = CountReachableSpecialShopHits(inclusion, seriesSheet, scripSpecialShopIds);
            if (score == 0) continue;
            scripInclusionShopIds.Add(inclusion.RowId);
            if (score > bestScore)
            {
                bestScore = score;
                canonicalId = inclusion.RowId;
            }
        }
        return (scripInclusionShopIds, canonicalId);
    }

    private static int CountReachableSpecialShopHits(
        InclusionShop inclusion,
        SubrowExcelSheet<InclusionShopSeries> seriesSheet,
        HashSet<uint> targetSpecialShopIds)
    {
        var count = 0;
        for (var pageIndex = 0; pageIndex < inclusion.Category.Count; pageIndex++)
        {
            var categoryRef = inclusion.Category[pageIndex];
            if (categoryRef.RowId == 0) continue;
            if (categoryRef.ValueNullable is not { } category) continue;

            var seriesId = category.InclusionShopSeries.RowId;
            if (seriesId == 0) continue;
            if (!seriesSheet.TryGetRow(seriesId, out var seriesRow)) continue;

            for (var subRowIndex = 0; subRowIndex < seriesRow.Count; subRowIndex++)
            {
                var specialShopRowId = seriesRow[subRowIndex].SpecialShop.RowId;
                if (specialShopRowId != 0 && targetSpecialShopIds.Contains(specialShopRowId))
                    count++;
            }
        }
        return count;
    }

    private static bool ShopHasScripCost(SpecialShop shop)
    {
        foreach (var entry in shop.Item)
        {
            foreach (var cost in entry.ItemCosts)
            {
                if (cost.ItemCost.RowId == 0) continue;
                if (CurrencyHelper.NormalizeScripCurrencyId(cost.ItemCost.RowId) != 0)
                    return true;
            }
        }
        return false;
    }
}
