namespace TheCollector.Utility;

public static class ItemFunc
{
    public static void ResetQuantity(this ItemToPurchase item)
    {
        item.AmountPurchased = 0;
    }
}
