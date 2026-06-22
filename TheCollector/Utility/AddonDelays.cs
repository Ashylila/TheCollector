using System.Collections.Generic;

namespace TheCollector.Utility;

public readonly record struct AddonDelayDef(string Key, string DisplayName);

public static class AddonDelays
{
    public const string Collectables       = "collectables";
    public const string ScripShop          = "scripshop";
    public const string FirmamentShop      = "firmament-shop";
    public const string FirmamentTurnIn    = "firmament-turnin";
    public const string ResourceInspection = "resource-inspection";
    public const string KupoOfFortune      = "kupo-of-fortune";

    public static readonly IReadOnlyList<AddonDelayDef> All = new[]
    {
        new AddonDelayDef(Collectables,       "Collectables turn-in"),
        new AddonDelayDef(ScripShop,          "Scrip exchange"),
        new AddonDelayDef(FirmamentShop,      "Firmament shop"),
        new AddonDelayDef(FirmamentTurnIn,    "Firmament turn-in"),
        new AddonDelayDef(ResourceInspection, "Resource inspection"),
        new AddonDelayDef(KupoOfFortune,      "Kupo of Fortune"),
    };
}
