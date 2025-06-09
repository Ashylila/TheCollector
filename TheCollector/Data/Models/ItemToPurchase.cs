
using Newtonsoft.Json;
using TheCollector.Data.Models;

public class ItemToPurchase
{
    public ScripShopItem Item { get; set; }
    public string Name => Item.Name;
    public int Quantity { get; set; }
    public int AmountPurchased { get; set; } = 0;
}
