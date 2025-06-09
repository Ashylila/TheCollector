using System.Linq;
using Dalamud.Interface.Textures;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace TheCollector.Data.Models;

public class ScripShopItem
{
    public string Name { get; set; }
    public int Index { get; set; }
    public uint ItemCost { get; set; }
    public int Page { get; set; }
    public int SubPage { get; set; }
    public ScripType ScripType { get; set; }
    
    [JsonIgnore]
    private Item? _itemCache;
    [JsonIgnore]
    public Item Item => _itemCache ??= Svc.Data.GetExcelSheet<Item>().FirstOrDefault(i => i.Name == Name);
    [JsonIgnore]
    private ISharedImmediateTexture? _iconTextureCache;
    [JsonIgnore]
    public ISharedImmediateTexture IconTexture => _iconTextureCache ??=Svc.Texture.GetFromGameIcon(new GameIconLookup((uint)Item.Icon));

}
