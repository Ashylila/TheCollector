using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace TheCollector.ScripShopManager;

public class ScripShopCache
{
    public ScripShopCache(IDataManager dataManager)
    {
        foreach (var item in dataManager.GetExcelSheet<CollectablesShopRewardItem>())
        {
            Svc.Log.Debug(item.Item.Value.Name.ExtractText());
        }
    }
}
