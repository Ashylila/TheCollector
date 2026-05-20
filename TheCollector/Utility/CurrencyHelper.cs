using System.Collections.Generic;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using TheCollector.Data;

namespace TheCollector.Utility;

public static class CurrencyHelper
{

    public const uint PurpleCrafterScripItemId  = 33913;
    public const uint PurpleGathererScripItemId = 33914;
    public const uint OrangeCrafterScripItemId  = 41784;
    public const uint OrangeGathererScripItemId = 41785;

    private static readonly Dictionary<uint, uint> ScripIdAliases = new()
    {
        [2] = PurpleCrafterScripItemId,
        [4] = PurpleGathererScripItemId,
        [6] = OrangeCrafterScripItemId,
        [7] = OrangeGathererScripItemId,
        [PurpleCrafterScripItemId]  = PurpleCrafterScripItemId,
        [PurpleGathererScripItemId] = PurpleGathererScripItemId,
        [OrangeCrafterScripItemId]  = OrangeCrafterScripItemId,
        [OrangeGathererScripItemId] = OrangeGathererScripItemId,
    };

    private static readonly Dictionary<uint, RunSource> ScripRunSource = new()
    {
        [PurpleCrafterScripItemId]  = RunSource.Crafting,
        [OrangeCrafterScripItemId]  = RunSource.Crafting,
        [PurpleGathererScripItemId] = RunSource.Gathering,
        [OrangeGathererScripItemId] = RunSource.Gathering,
    };

    public static unsafe uint SpecialIdToItemId(uint specialId)
    {
        var cur = CurrencyManager.Instance();
        if (cur == null) return 0;
        return cur->GetItemIdBySpecialId((byte)specialId);
    }


    public static uint NormalizeScripCurrencyId(uint rawId)
        => ScripIdAliases.GetValueOrDefault(rawId, 0u);

    public static string GetCurrencyName(uint currencyItemId)
    {
        if (currencyItemId == 0) return "Scrip";
        var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(currencyItemId);
        return item?.Name.ExtractText() ?? $"Item {currencyItemId}";
    }

    public static uint GetCurrencyIdForItem(uint shopItemId)
    {
        foreach (var s in ScripShopItemManager.ShopItems)
        {
            if (s.ItemId == shopItemId)
                return s.CurrencyId;
        }
        return 0;
    }

    public static RunSource RunSourceFromJobIndex(int jobIndex)
        => jobIndex >= 0 && jobIndex <= 7 ? RunSource.Crafting : RunSource.Gathering;

    public static RunSource GetRunSource(uint currencyId)
        => ScripRunSource.GetValueOrDefault(NormalizeScripCurrencyId(currencyId), RunSource.Gathering);
}
