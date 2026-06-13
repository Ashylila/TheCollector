using TheCollector.CollectableManager;
using TheCollector.ScripShopManager;

namespace TheCollector.Data.ScripSystem;

public sealed class NormalScripSystem : IScripSystem
{
    public ScripSystemId Id => ScripSystemId.Normal;
    public string DisplayName => "Normal Scrips";
    public ITurnInPipeline TurnIn { get; }
    public IBuyPipeline Buy { get; }

    public NormalScripSystem(CollectableAutomationHandler turnIn, ScripShopAutomationHandler buy)
    {
        TurnIn = turnIn;
        Buy = buy;
    }
}
