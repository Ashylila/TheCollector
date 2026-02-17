using System.Linq;
using Dalamud.Interface.Textures;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace TheCollector.Data.Models;

public class ScripShopItem
{
    public int Index { get; set; }
    public uint ItemCost { get; set; }
    public int Page { get; set; }
    public int SubPage { get; set; }
    public uint ItemId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    [JsonIgnore]
    public string Name => Item.Name.ToString();
    [System.Text.Json.Serialization.JsonIgnore]
    [JsonIgnore]
    public uint CurrencyId { get; set; }
    
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    private Item? _itemCache;
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public Item Item => _itemCache ??= Svc.Data.GetExcelSheet<Item>().GetRow(ItemId);
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    private ISharedImmediateTexture? _iconTextureCache;
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public ISharedImmediateTexture IconTexture => _iconTextureCache ??=Svc.Texture.GetFromGameIcon(new GameIconLookup((uint)Item.Icon));

}
