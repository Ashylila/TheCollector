using System;
using System.Linq;

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
                
                var result = aetheryteName.Contains(name, StringComparison.OrdinalIgnoreCase);
                if (!result && !aetheryteName.Equals(name, StringComparison.OrdinalIgnoreCase))
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

        return false;
    }

    public static unsafe bool Teleport(uint aetheryteId, byte subIndex)
    {
        return Telepo.Instance()->Teleport(aetheryteId, subIndex);
    }
}
