using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using TheCollector.Data.Firmament;

namespace TheCollector.Utility;

public sealed class FirmamentCatalog
{
    public readonly record struct Placement(uint DataId, Vector3 Position);

    public readonly record struct FirmamentWare(uint ItemId, uint Cost, uint ShopId, uint CategoryId)
    {
        public int TabIndex => CategoryId >= 1 ? (int)CategoryId - 1 : 0;
    }

    private readonly PlogonLog _log;
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public uint ScripItemId => FirmamentAnchors.ScripItemId;
    public int HoldingCap { get; private set; }
    public uint TerritoryId { get; private set; }

    public IReadOnlyList<Placement> Appraisers { get; private set; } = Array.Empty<Placement>();
    public IReadOnlyList<Placement> Exchanges { get; private set; } = Array.Empty<Placement>();
    public IReadOnlySet<uint> TurnInItemIds { get; private set; } = new HashSet<uint>();
    public IReadOnlyDictionary<uint, int> CrafterJobByItemId { get; private set; } = new Dictionary<uint, int>();
    public IReadOnlyList<FirmamentWare> Wares { get; private set; } = Array.Empty<FirmamentWare>();
    private IReadOnlyDictionary<uint, FirmamentWare> _wareById = new Dictionary<uint, FirmamentWare>();
    private IReadOnlyList<uint> _shopOrder = Array.Empty<uint>();

    public IReadOnlyList<uint> ShopOrder => _shopOrder;

    public int PredictEntryIndex(uint shopId)
    {
        for (var i = 0; i < _shopOrder.Count; i++)
            if (_shopOrder[i] == shopId) return i;
        return -1;
    }

    public bool TryGetPlacement(uint itemId, out uint shopId, out int tabIndex)
    {
        if (_wareById.TryGetValue(itemId, out var w))
        {
            shopId = w.ShopId;
            tabIndex = w.TabIndex;
            return true;
        }
        shopId = 0;
        tabIndex = -1;
        return false;
    }

    public uint IdentifyShop(IEnumerable<uint> visibleItemIds)
    {
        foreach (var id in visibleItemIds)
            if (_wareById.TryGetValue(id, out var w))
                return w.ShopId;
        return 0;
    }

    public uint[] AppraiserDataIds => Appraisers.Select(p => p.DataId).ToArray();
    public uint[] ExchangeDataIds => Exchanges.Select(p => p.DataId).ToArray();
    public Vector3 AppraiserPosition => Appraisers.Count > 0 ? Appraisers[0].Position : Vector3.Zero;
    public Vector3 ExchangePosition => Exchanges.Count > 0 ? Exchanges[0].Position : Vector3.Zero;

    public FirmamentCatalog(PlogonLog log)
    {
        _log = log;
        Task.Run(() =>
        {
            try { Build(); }
            catch (Exception ex) { _log.Error(ex, "FirmamentCatalog: build failed; catalog will be empty."); }
            finally { _isReady = true; }
        });
    }

    private void Build()
    {
        var itemSheet = Svc.Data.GetExcelSheet<Item>();
        var npcBase = Svc.Data.GetExcelSheet<ENpcBase>();
        var levelSheet = Svc.Data.GetExcelSheet<Level>();
        var crafterSheet = Svc.Data.GetExcelSheet<HWDCrafterSupply>();
        var gathererSheet = Svc.Data.GetExcelSheet<HWDGathererInspection>();
        var specialSheet = Svc.Data.GetExcelSheet<SpecialShop>();
        if (itemSheet == null || npcBase == null || levelSheet == null ||
            crafterSheet == null || gathererSheet == null || specialSheet == null)
        {
            _log.Error("FirmamentCatalog: required Lumina sheets unavailable; catalog will be empty.");
            return;
        }

        var stack = itemSheet.GetRow(FirmamentAnchors.ScripItemId).StackSize;
        HoldingCap = stack > int.MaxValue ? int.MaxValue : (int)stack;

        var appraiserNpcIds = new HashSet<uint>();
        var exchangeNpcIds = new HashSet<uint>();
        foreach (var npc in npcBase)
        {
            if (npc.ENpcData.Any(e => e.RowId == FirmamentAnchors.AppraiserTalkId)) appraiserNpcIds.Add(npc.RowId);
            if (npc.ENpcData.Any(e => e.RowId == FirmamentAnchors.ExchangeTalkId)) exchangeNpcIds.Add(npc.RowId);
        }

        var appraisers = new List<Placement>();
        var exchanges = new List<Placement>();
        var territoryVotes = new Dictionary<uint, int>();
        foreach (var lv in levelSheet)
        {
            var npcId = lv.Object.RowId;
            if (npcId == 0) continue;
            var pos = new Vector3(lv.X, lv.Y, lv.Z);
            if (appraiserNpcIds.Contains(npcId))
            {
                appraisers.Add(new Placement(npcId, pos));
                var t = lv.Territory.RowId;
                if (t != 0) territoryVotes[t] = territoryVotes.GetValueOrDefault(t) + 1;
            }
            if (exchangeNpcIds.Contains(npcId))
                exchanges.Add(new Placement(npcId, pos));
        }

        TerritoryId = territoryVotes.Count > 0
            ? territoryVotes.OrderByDescending(kv => kv.Value).First().Key
            : 0;

        var turnInItems = new HashSet<uint>();
        foreach (var row in crafterSheet)
            foreach (var p in row.HWDCrafterSupplyParams)
                if (p.ItemTradeIn.RowId != 0) turnInItems.Add(p.ItemTradeIn.RowId);
        foreach (var row in gathererSheet)
            foreach (var d in row.HWDGathererInspectionData)
                if (d.RequiredItem.RowId != 0) turnInItems.Add(d.RequiredItem.RowId);

        var recipeSheet = Svc.Data.GetExcelSheet<Recipe>();
        var jobByItem = new Dictionary<uint, int>();
        if (recipeSheet != null)
            foreach (var rec in recipeSheet)
            {
                var rid = rec.ItemResult.RowId;
                if (rid != 0 && turnInItems.Contains(rid))
                    jobByItem.TryAdd(rid, (int)rec.CraftType.RowId);
            }

        var wares = new List<FirmamentWare>();
        var wareById = new Dictionary<uint, FirmamentWare>();
        foreach (var shop in specialSheet)
        {
            foreach (var entry in shop.Item)
            {
                uint cost = 0;
                foreach (var c in entry.ItemCosts)
                {
                    if (c.ItemCost.RowId == FirmamentAnchors.ScripItemId && c.CurrencyCost != 0)
                    {
                        cost = (uint)c.CurrencyCost;
                        break;
                    }
                }
                if (cost == 0) continue;
                var categoryId = entry.Category.Count > 0 ? entry.Category[0].RowId : 0;
                foreach (var receive in entry.ReceiveItems)
                {
                    var itemId = receive.Item.RowId;
                    if (itemId == 0 || wareById.ContainsKey(itemId)) continue;
                    var ware = new FirmamentWare(itemId, cost, shop.RowId, categoryId);
                    wares.Add(ware);
                    wareById[itemId] = ware;
                }
            }
        }

        Appraisers = appraisers;
        Exchanges = exchanges;
        TurnInItemIds = turnInItems;
        CrafterJobByItemId = jobByItem;
        Wares = wares;
        _wareById = wareById;
        _shopOrder = wares.Select(w => w.ShopId).Where(s => s != 0).Distinct().OrderBy(s => s).ToList();

        _log.Debug($"FirmamentCatalog: territory={TerritoryId} cap={HoldingCap} appraisers={appraisers.Count} " +
                   $"exchanges={exchanges.Count} turnInItems={turnInItems.Count} wares={wares.Count}.");
    }
}
