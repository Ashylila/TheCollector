using TheCollector.FirmamentManager;

namespace TheCollector.Data.ScripSystem;

public sealed class FirmamentScripSystem : IScripSystem
{
    public ScripSystemId Id => ScripSystemId.Firmament;
    public string DisplayName => "Firmament";
    public ITurnInPipeline TurnIn { get; }
    public IBuyPipeline Buy { get; }

    public FirmamentScripSystem(FirmamentTurnInHandler turnIn, FirmamentShopHandler buy)
    {
        TurnIn = turnIn;
        Buy = buy;
    }
}
