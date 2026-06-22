using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using TheCollector.Data;
using TheCollector.Utility;

namespace TheCollector.ScripShopManager;

public partial class ScripShopAutomationHandler : TheCollector.Data.ScripSystem.IBuyPipeline
{
    public override string Key => AddonDelays.ScripShop;
    private readonly Configuration _configuration;
    private readonly ScripShopWindowHandler _scripShopWindowHandler;
    private readonly VendorCatalog _vendorCatalog;

    public event Action<Dictionary<uint, int>>? OnFinishedTrading;

    public ScripShopAutomationHandler(
        PlogonLog log,
        IFramework framework,
        Configuration configuration,
        ScripShopWindowHandler handler,
        VendorCatalog vendorCatalog,
        StatusService status) : base(log, framework, status)
    {
        _configuration = configuration;
        _scripShopWindowHandler = handler;
        _vendorCatalog = vendorCatalog;
    }

}
