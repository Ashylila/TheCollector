using OtterGui.Services;
using OtterGui.Widgets;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class ChangelogUi : IUiService
{
    public const int LastChangelogVersion = 0;
    public readonly Changelog Changelog;
    private readonly Configuration _config;
    private readonly PlogonLog _log;
    public ChangelogUi(Configuration config, PlogonLog log)
    {
        _log = log;
        _config = config;
        Changelog = new Changelog("TheCollector Changelog", ConfigData , Save );
        Changelog.ForceOpen = true;
        Add0_28(Changelog);
    }

    private static void Add0_28(Changelog log)=>
        log.NextVersion("Version 0.28")
           .RegisterHighlight("Added changelog window!")
           .RegisterEntry("Marked Solution nine teleport as not functional & made it not interactable and also set the Eulmore one as default")
           .RegisterEntry("Fixed a bug where it would fail to buy items if the quantity was set too high")
           .RegisterEntry("Made certain config settings not interactable if the required plugins are not installed");
    
    private (int, ChangeLogDisplayType) ConfigData()
        => (_config.LastSeenVersion, _config.ChangeLogDisplayType);

    private void Save(int version, ChangeLogDisplayType type)
    {
        if (_config.LastSeenVersion != version)
        {
            _config.LastSeenVersion = version;
            _config.Save();
        }

        if (_config.ChangeLogDisplayType != type)
        {
            _config.ChangeLogDisplayType = type;
            _config.Save();
        }
    }
}
