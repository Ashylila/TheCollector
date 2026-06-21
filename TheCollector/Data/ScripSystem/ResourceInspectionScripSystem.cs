using TheCollector.FirmamentManager;
using TheCollector.ResourceInspectionManager;

namespace TheCollector.Data.ScripSystem;

// Pairs the Resource Inspection turn-in (Flotpassant) with the existing Firmament shop buy.
// Both earn/spend Skybuilders' Scrip at the same 10,000 cap, so reusing FirmamentShopHandler
// lets AutomationHandler's turn-in -> (cap) -> buy -> turn-in loop drive cap handling for free.
public sealed class ResourceInspectionScripSystem : IScripSystem
{
    public ScripSystemId Id => ScripSystemId.Inspection;
    public string DisplayName => "Resource Inspection";
    public ITurnInPipeline TurnIn { get; }
    public IBuyPipeline Buy { get; }

    public ResourceInspectionScripSystem(ResourceInspectionHandler turnIn, FirmamentShopHandler buy)
    {
        TurnIn = turnIn;
        Buy = buy;
    }
}
