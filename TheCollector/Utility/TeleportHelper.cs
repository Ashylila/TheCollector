using System;
using System.Linq;
using System.Numerics;

using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace TheCollector.Utility;

public static class TeleportHelper
{
    private static PlogonLog Logger = new();

    public static unsafe bool TryFindAetheryteByName(string name, out TeleportInfo info, out string aetherName)
    {
        info = new TeleportInfo();
        aetherName = string.Empty;
        try
        {
            var tp = Telepo.Instance();
            if (tp->UpdateAetheryteList() == null) return false;
            var tpInfos = tp->TeleportList;
            foreach (var tpInfo in tpInfos)
            {
                var aetheryteName = ServiceWrapper.Get<IDataManager>().GetExcelSheet<Aetheryte>()
                    .FirstOrDefault(x => x.RowId == tpInfo.AetheryteId).PlaceName.ValueNullable?.Name
                    .ToString();

                if (string.IsNullOrEmpty(aetheryteName)) continue;
                if (!aetheryteName.Contains(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                info = tpInfo;
                aetherName = aetheryteName;
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to find teleportInfo");
            return false;
        }
        Logger.Error("Failed to find teleportInfo");
        return false;
    }


    public static unsafe bool TryFindAetheryteForTerritory(
        uint territoryId, Vector3 targetPosition,
        out TeleportInfo info, out string aetherName)
    {
        info = new TeleportInfo();
        aetherName = string.Empty;
        if (territoryId == 0) return false;

        try
        {
            var tp = Telepo.Instance();
            if (tp->UpdateAetheryteList() == null) return false;

            var sheet = ServiceWrapper.Get<IDataManager>().GetExcelSheet<Aetheryte>();
            if (sheet == null) return false;

            var bestDistSq = float.MaxValue;
            var found = false;
            foreach (var tpInfo in tp->TeleportList)
            {
                if (!sheet.TryGetRow(tpInfo.AetheryteId, out var aetheryte)) continue;
                if (!aetheryte.IsAetheryte) continue;
                if (aetheryte.Territory.RowId != territoryId) continue;

                var levelRef = aetheryte.Level[0].ValueNullable;
                var aetheryteName = aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
                var distSq = float.MaxValue;
                if (levelRef is { } lvl)
                {
                    var d = new Vector3(lvl.X, lvl.Y, lvl.Z) - targetPosition;
                    distSq = d.LengthSquared();
                }

                if (!found || distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    info = tpInfo;
                    aetherName = aetheryteName;
                    found = true;
                }
            }
            if (!found)
                Logger.Debug($"No unlocked Aetheryte found for territory {territoryId}.");
            return found;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to resolve aetheryte for territory " + territoryId);
            return false;
        }
    }

    public static unsafe bool Teleport(uint aetheryteId, byte subIndex)
    {
        return Telepo.Instance()->Teleport(aetheryteId, subIndex);
    }
}
