using System.Collections.Generic;

namespace TheCollector.Data.ScripSystem;

public sealed class ScripSystemSelector
{
    private readonly Configuration _config;
    public IScripSystem Normal { get; }
    public IScripSystem Firmament { get; }
    public IScripSystem Inspection { get; }

    public ScripSystemSelector(Configuration config, NormalScripSystem normal, FirmamentScripSystem firmament, ResourceInspectionScripSystem inspection)
    {
        _config = config;
        Normal = normal;
        Firmament = firmament;
        Inspection = inspection;
    }

    public IScripSystem Active => _config.ActiveSystem switch
    {
        ScripSystemId.Firmament => Firmament,
        ScripSystemId.Inspection => Inspection,
        _ => Normal,
    };

    public IReadOnlyList<IScripSystem> All => new[] { Normal, Firmament, Inspection };
}
