using System.Collections.Generic;

namespace TheCollector.Data.ScripSystem;

public sealed class ScripSystemSelector
{
    private readonly Configuration _config;
    public IScripSystem Normal { get; }
    public IScripSystem Firmament { get; }

    public ScripSystemSelector(Configuration config, NormalScripSystem normal, FirmamentScripSystem firmament)
    {
        _config = config;
        Normal = normal;
        Firmament = firmament;
    }

    public IScripSystem Active =>
        _config.ActiveSystem == ScripSystemId.Firmament ? Firmament : Normal;

    public IReadOnlyList<IScripSystem> All => new[] { Normal, Firmament };
}
