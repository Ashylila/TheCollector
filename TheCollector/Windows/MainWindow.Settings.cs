using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using TheCollector.CollectableManager;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Utility;

namespace TheCollector.Windows;

public partial class MainWindow
{
    private Dictionary<int, string>? _artisanLists;
    private DateTime _artisanListsFetchedAt = DateTime.MinValue;
    private static readonly JsonSerializerOptions DebugJsonOpts = new() { WriteIndented = true };

    private void DrawSettingsTab()
    {
        DrawInstalledPlugins();

        bool vnavReady = IPCSubscriber_Common.IsReady("vnavmesh");
        ImGui.BeginDisabled(!vnavReady);
        if (!vnavReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("vnavmesh plugin is not installed or not ready.");

        if (ImGui.BeginTabBar("##SettingsTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                ImGui.Spacing();
                DrawSettingsGeneral();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Integrations"))
            {
                ImGui.Spacing();
                DrawSettingsIntegrations();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Goal"))
            {
                ImGui.Spacing();
                DrawSettingsGoal();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Timing"))
            {
                ImGui.Spacing();
                DrawSettingsTiming();
                ImGui.EndTabItem();
            }

            if (pluginInterface.IsDev && ImGui.BeginTabItem("Debug"))
            {
                ImGui.Spacing();
                DrawSettingsDebug();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.EndDisabled();
    }

    private static void DrawInstalledPlugins()
    {
        ImGuiHelper.Panel("InstalledPlgs", () =>
        {
            ImGuiHelper.SectionHeader("Required & Optional Plugins");

            if (!ImGui.BeginTable("##PluginGrid", 2, ImGuiTableFlags.SizingStretchSame))
                return;

            void Cell(string key, string label, bool required)
            {
                ImGui.TableNextColumn();
                DrawPluginStatus(key, label, required);
            }

            Cell("vnavmesh",          "vnavmesh",          required: true);
            Cell("GatherBuddyReborn", "GatherBuddyReborn", required: false);
            Cell("Artisan",           "Artisan",           required: false);
            Cell("AutoRetainer",      "AutoRetainer",      required: false);
            Cell("Deliveroo",         "Deliveroo",         required: false);
            Cell("Lifestream",        "Lifestream",        required: true);

            ImGui.EndTable();
        });
    }

    private static void DrawPluginStatus(string pluginKey, string displayName, bool required)
    {
        bool ready    = IPCSubscriber_Common.IsReady(pluginKey);
        var dotColor  = ready ? UiTheme.Success : UiTheme.Danger;

        ImGuiHelper.StatusDot(dotColor);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(displayName);
        ImGui.SameLine();
        ImGuiHelper.Chip(required ? "required" : "optional", required ? UiTheme.Accent : UiTheme.TextDim);
    }

    private void DrawSettingsGeneral()
    {
        if (configuration.ActiveSystem == TheCollector.Data.ScripSystem.ScripSystemId.Normal)
        {
            ImGuiHelper.SectionHeader("Collectable Shop");

            var catalog = ServiceWrapper.Get<VendorCatalog>();
            var currentTerritory = configuration.PreferredTerritoryId;
            var currentLabel = currentTerritory == 0
                ? "Select a territory"
                : VendorCatalog.GetTerritoryDisplayName(currentTerritory);

            ImGui.PushItemWidth(-1);
            if (ImGui.BeginCombo("##shopselection", currentLabel))
            {
                if (!catalog.IsReady)
                    ImGui.TextDisabled("Scanning vendor data...");

                foreach (var territoryId in catalog.ServedTerritoryIds)
                {
                    bool requiresLifestream = TerritoryRouting.RequiresAethernet(territoryId);
                    bool lifestreamMissing = requiresLifestream && !IPCSubscriber_Common.IsReady("Lifestream");
                    ImGui.BeginDisabled(lifestreamMissing);

                    var label = VendorCatalog.GetTerritoryDisplayName(territoryId);
                    if (lifestreamMissing) label = $"{label} (Lifestream required)";

                    if (ImGui.Selectable(label, territoryId == currentTerritory))
                    {
                        configuration.PreferredTerritoryId = territoryId;
                        configuration.Save();
                    }
                    if (lifestreamMissing && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Lifestream plugin is required to reach this territory.");

                    ImGui.EndDisabled();
                }

                ImGui.EndCombo();
            }
            ImGui.PopItemWidth();

            ImGui.Spacing();
        }
        else
        {
            ImGuiHelper.SectionHeader("Kupo of Fortune");

            var kupoEnabled = configuration.KupoOfFortuneEnabled;
            if (ImGui.Checkbox("Play Kupo of Fortune to spend held cards", ref kupoEnabled))
            {
                configuration.KupoOfFortuneEnabled = kupoEnabled;
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("After a Firmament turn-in, walk to Lizbeth and play any Kupo of Fortune\n" +
                                 "cards you hold so vouchers earned past the 10-card cap aren't wasted.");

            ImGui.BeginDisabled(!kupoEnabled);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Play when cards held reach:");
            ImGui.SameLine();
            var threshold = configuration.KupoOfFortuneThreshold;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderInt("##KupoThreshold", ref threshold, 1, 10))
            {
                configuration.KupoOfFortuneThreshold = Math.Clamp(threshold, 1, 10);
                configuration.Save();
            }

            const string leftLabel = "Left chest (2nd-4th prizes)";
            const string rightLabel = "Random right chest (all 5 prizes)";
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Chest to scratch:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            var pick = configuration.KupoChestPick;
            if (ImGui.BeginCombo("##KupoChest", pick == Data.KupoChestPick.RandomRight ? rightLabel : leftLabel))
            {
                if (ImGui.Selectable(leftLabel, pick == Data.KupoChestPick.Left))
                {
                    configuration.KupoChestPick = Data.KupoChestPick.Left;
                    configuration.Save();
                }
                if (ImGui.Selectable(rightLabel, pick == Data.KupoChestPick.RandomRight))
                {
                    configuration.KupoChestPick = Data.KupoChestPick.RandomRight;
                    configuration.Save();
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Left can only win 2nd-4th prize (avoids the jackpot and the\n" +
                                 "consolation). Random right can win any of the 5 prizes.");
            ImGui.EndDisabled();

            ImGui.Spacing();
        }

        ImGuiHelper.SectionHeader("Automation");

        var buyAfterEach = configuration.BuyAfterEachCollect;
        if (ImGui.Checkbox("Buy items after each trade instead of on capping scrips", ref buyAfterEach))
        {
            configuration.BuyAfterEachCollect = buyAfterEach;
            configuration.Save();
        }

        var resetOnComplete = configuration.ResetEachQuantityAfterCompletingList;
        if (ImGui.Checkbox("Reset each quantity after completing the list", ref resetOnComplete))
        {
            configuration.ResetEachQuantityAfterCompletingList = resetOnComplete;
            configuration.Save();
        }

        var collectOnFishing = configuration.CollectOnFinishedFishing;
        if (ImGui.Checkbox("Collect on finished fishing", ref collectOnFishing))
        {
            configuration.CollectOnFinishedFishing = collectOnFishing;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Reserve scrips:");
        ImGui.SameLine();
        var reserve = configuration.ReserveScripAmount;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##ReserveScrip", ref reserve, 0, Configuration.ScripCeiling))
        {
            configuration.ReserveScripAmount = Math.Clamp(reserve, 0, Configuration.ScripCeiling);
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Purchases will leave at least this many scrips of each currency unspent.");

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Troubleshooting");
        DrawCopyTroubleshootingButton();
    }

    private DateTime _troubleshootCopiedAt = DateTime.MinValue;

    private void DrawCopyTroubleshootingButton()
    {
        if (ImGui.Button("Copy troubleshooting info"))
        {
            ImGui.SetClipboardText(BuildTroubleshootingInfo());
            _troubleshootCopiedAt = DateTime.UtcNow;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copies plugin state, settings and dependency status to the clipboard.\nPaste it along with your issue report.");
        if ((DateTime.UtcNow - _troubleshootCopiedAt).TotalSeconds < 2)
        {
            ImGui.SameLine();
            ImGui.TextColored(UiTheme.Success, "Copied!");
        }
    }

    private string BuildTroubleshootingInfo()
    {
        var collectable  = ServiceWrapper.Get<CollectableAutomationHandler>();
        var scripShop    = ServiceWrapper.Get<ScripShopAutomationHandler>();
        var retainer     = ServiceWrapper.Get<AutoRetainerManager>();
        var deliveroo    = ServiceWrapper.Get<DeliverooManager>();
        var catalog      = ServiceWrapper.Get<VendorCatalog>();

        var sb = new StringBuilder();
        sb.AppendLine("=== TheCollector troubleshooting info ===");
        sb.AppendLine($"Generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Plugin version:  {Assembly.GetExecutingAssembly().GetName().Version} (dev: {pluginInterface.IsDev})");

        sb.AppendLine();
        sb.AppendLine("[State]");
        sb.AppendLine($"Active system:   {configuration.ActiveSystem}");
        sb.AppendLine($"Status:          {_status.Current}{(string.IsNullOrEmpty(_status.Detail) ? "" : $" ({_status.Detail})")}");
        sb.AppendLine($"Hard fail:       {configuration.HardFailReason ?? "<none>"}");
        if (_status.Errors.Count > 0)
        {
            var lastError = _status.Errors[^1];
            sb.AppendLine($"Last error:      [{lastError.Source}] {lastError.Message} at {lastError.LastAtUtc:HH:mm:ss} UTC");
        }
        else
            sb.AppendLine("Last error:      <none>");
        sb.AppendLine($"Running:         {_automationHandler.IsRunning} (collectable={collectable.IsRunning}, scripshop={scripShop.IsRunning}, autoretainer={retainer.IsRunning}, deliveroo={deliveroo.IsRunning})");
        var terId = Svc.ClientState.TerritoryType;
        sb.AppendLine($"Territory:       {terId} ({VendorCatalog.GetTerritoryDisplayName(terId)})");
        sb.AppendLine($"Has collectible: {collectable.HasCollectible}");
        sb.AppendLine($"Free inv slots:  {ItemHelper.GetFreeInventorySlots()}");
        sb.AppendLine($"Current item:    {collectable.CurrentItemName ?? "<none>"}");
        if (collectable.TurnInQueue is { Count: > 0 } queue)
            sb.AppendLine($"Turn-in queue:   {string.Join(", ", queue.Select(t => $"{t.name}×{t.left} (job {t.jobIndex})"))}");

        sb.AppendLine();
        sb.AppendLine("[Dependencies]");
        foreach (var (key, required) in new[]
                 {
                     ("vnavmesh", true), ("Lifestream", true), ("GatherBuddyReborn", false),
                     ("Artisan", false), ("AutoRetainer", false), ("Deliveroo", false),
                 })
            sb.AppendLine($"{key,-18} {(IPCSubscriber_Common.IsReady(key) ? "ready" : "NOT READY")}{(required ? " (required)" : "")}");

        sb.AppendLine();
        sb.AppendLine("[Settings]");
        var prefId = configuration.PreferredTerritoryId;
        sb.AppendLine($"Config version:        {configuration.Version}");
        sb.AppendLine($"Preferred territory:   {prefId} ({(prefId == 0 ? "<none>" : VendorCatalog.GetTerritoryDisplayName(prefId))})");
        sb.AppendLine($"Active source:         {configuration.ActiveRunSource}");
        sb.AppendLine($"UI delay:              {configuration.UiDelayMs}ms");
        sb.AppendLine($"Reserve scrips:        {configuration.ReserveScripAmount}");
        sb.AppendLine($"Buy after each:        {configuration.BuyAfterEachCollect}");
        sb.AppendLine($"Reset qty on complete: {configuration.ResetEachQuantityAfterCompletingList}");
        sb.AppendLine($"Stop when complete:    {configuration.Goal.StopGatheringWhenComplete}");
        sb.AppendLine($"Autogather on finish:  {configuration.EnableAutogatherOnFinish}");
        sb.AppendLine($"Collect on craft/fish/gather: {configuration.CollectOnFinishCraftingList}/{configuration.CollectOnFinishedFishing}/{configuration.CollectOnAutogatherFinish}");
        sb.AppendLine($"Craft on autogather:   {configuration.ShouldCraftOnAutogatherChanged} (Artisan list {configuration.ArtisanListId})");
        sb.AppendLine($"Artisan inv pause:     {configuration.PauseArtisanOnInventoryFull} (threshold {configuration.ArtisanInventoryFullThreshold})");
        sb.AppendLine($"AutoRetainer between:  {configuration.CheckForVenturesBetweenRuns}");
        sb.AppendLine($"Deliveroo between:     {configuration.CheckForDeliverooBetweenRuns}");
        var stop = configuration.Stop;
        sb.AppendLine($"Stop conditions:       scrips={stop.StopOnScripsEarnedEnabled}({stop.MaxScripsEarned}) cycles={stop.StopOnBuyCyclesEnabled}({stop.MaxBuyCycles}) time={stop.StopOnSessionTimeEnabled}({stop.MaxSessionMinutes}m)");

        sb.AppendLine();
        sb.AppendLine("[Catalogs]");
        sb.AppendLine($"Vendor catalog:  {(catalog.IsReady ? "ready" : "still building")}, {catalog.AllVendors.Count} placements, {catalog.ServedTerritoryIds.Count} served territories");
        var cv = catalog.GetCollectableVendor(prefId);
        var sv = catalog.GetScripVendor(prefId);
        sb.AppendLine($"Collectable NPC: {(cv == null ? "<none>" : $"{cv.Name} @ ({cv.Position.X:F1}, {cv.Position.Y:F1}, {cv.Position.Z:F1})")}");
        sb.AppendLine($"Scrip NPC:       {(sv == null ? "<none>" : $"{sv.Name} @ ({sv.Position.X:F1}, {sv.Position.Y:F1}, {sv.Position.Z:F1})")}");
        sb.AppendLine($"Scrip shop items: {ScripShopItemManager.ShopItems.Count}");

        sb.AppendLine();
        sb.AppendLine("[Firmament]");
        var firmCatalog = ServiceWrapper.Get<FirmamentCatalog>();
        var firmTurnIn  = ServiceWrapper.Get<FirmamentManager.FirmamentTurnInHandler>();
        var firmShop    = ServiceWrapper.Get<FirmamentManager.FirmamentShopHandler>();
        sb.AppendLine($"Catalog:         {(firmCatalog.IsReady ? "ready" : "still building")} (territory {firmCatalog.TerritoryId}, cap {firmCatalog.HoldingCap})");
        sb.AppendLine($"Appraisers/exch: {firmCatalog.Appraisers.Count}/{firmCatalog.Exchanges.Count}, turn-in items {firmCatalog.TurnInItemIds.Count}");
        sb.AppendLine($"Wares:           {firmCatalog.Wares.Count} across {firmCatalog.ShopOrder.Count} sub-shops [{string.Join(", ", firmCatalog.ShopOrder)}]");
        sb.AppendLine($"Pipelines:       turnIn={firmTurnIn.IsRunning}, shop={firmShop.IsRunning}");
        if (configuration.FirmamentGoal.ItemsToPurchase.Count == 0)
            sb.AppendLine("Purchase list:   <empty>");
        else
        {
            sb.AppendLine("Purchase list:");
            foreach (var item in configuration.FirmamentGoal.ItemsToPurchase)
            {
                var placement = firmCatalog.TryGetPlacement(item.Item.ItemId, out var shopId, out var tab)
                    ? $"shop {shopId}, tab {tab}"
                    : "unmapped";
                sb.AppendLine($"  {item.Name}: {item.AmountPurchased}/{item.Quantity} ({placement})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("[Purchase list]");
        if (configuration.Goal.ItemsToPurchase.Count == 0)
            sb.AppendLine("<empty>");
        else
            foreach (var item in configuration.Goal.ItemsToPurchase)
            {
                var currency = CurrencyHelper.GetCurrencyName(CurrencyHelper.GetCurrencyIdForItem(item.Item.ItemId));
                sb.AppendLine($"{item.Name}: {item.AmountPurchased}/{item.Quantity} ({currency})");
            }

        sb.AppendLine();
        sb.AppendLine("[Recent errors]");
        if (_status.Errors.Count == 0)
            sb.AppendLine("<none>");
        else
            foreach (var err in _status.Errors)
            {
                var when = err.Count == 1
                    ? $"{err.FirstAtUtc:HH:mm:ss}"
                    : $"{err.FirstAtUtc:HH:mm:ss}–{err.LastAtUtc:HH:mm:ss} ×{err.Count}";
                sb.AppendLine($"{when} UTC [{err.Source}] {err.Message}");
            }

        sb.AppendLine();
        sb.AppendLine("[Session]");
        sb.AppendLine($"Started (UTC):   {_automationHandler.SessionStarted?.ToString("yyyy-MM-dd HH:mm:ss") ?? "<not started>"}");
        sb.AppendLine($"Turn-ins:        {_automationHandler.SessionCollectablesTurnedIn}");
        sb.AppendLine($"Buy cycles:      {_automationHandler.SessionItemsPurchased}");
        foreach (var (currencyId, amount) in _automationHandler.SessionScripsEarned)
            sb.AppendLine($"Earned:          {amount:N0} {CurrencyHelper.GetCurrencyName(currencyId)}");
        foreach (var (currencyId, amount) in _automationHandler.SessionScripsSpent)
            sb.AppendLine($"Spent:           {amount:N0} {CurrencyHelper.GetCurrencyName(currencyId)}");

        return sb.ToString();
    }

    private void DrawSettingsIntegrations()
    {
        ImGuiHelper.SectionHeader("GatherBuddyReborn");

        bool gbrReady = IPCSubscriber_Common.IsReady("GatherBuddyReborn");
        ImGui.BeginDisabled(!gbrReady);

        var autogatherOnFinish = configuration.EnableAutogatherOnFinish;
        if (ImGui.Checkbox("Enable Autogather on finish", ref autogatherOnFinish))
        {
            configuration.EnableAutogatherOnFinish = autogatherOnFinish;
            configuration.Save();
        }
        if (!gbrReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("GatherbuddyReborn is not installed or not ready.");

        bool craftOnAutogatherActive = configuration.ShouldCraftOnAutogatherChanged;
        ImGui.BeginDisabled(craftOnAutogatherActive);
        var collectOnAutogather = configuration.CollectOnAutogatherFinish;
        if (ImGui.Checkbox("Turn in collectables on autogather finish", ref collectOnAutogather))
        {
            configuration.CollectOnAutogatherFinish = collectOnAutogather;
            configuration.Save();
        }
        if (craftOnAutogatherActive && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Disabled while \"Craft selected Artisan list on autogather finish\" is enabled (Artisan section).");
        else if (!gbrReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("GatherbuddyReborn is not installed or not ready.");
        ImGui.EndDisabled();

        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Artisan");

        bool artisanGbrReady = gbrReady;
        bool artisanReady = IPCSubscriber_Common.IsReady("Artisan");
        bool artisanSectionReady = artisanGbrReady && artisanReady;
        string? artisanDisabledReason = !artisanReady && !artisanGbrReady
            ? "Artisan and GatherbuddyReborn are not installed or not ready."
            : !artisanReady
                ? "Artisan is not installed or not ready."
                : !artisanGbrReady
                    ? "GatherbuddyReborn is not installed or not ready."
                    : null;

        ImGui.BeginDisabled(!artisanSectionReady);

        bool collectOnAutogatherActiveArtisan = configuration.CollectOnAutogatherFinish;
        ImGui.BeginDisabled(collectOnAutogatherActiveArtisan);
        var craftOnAutogather = configuration.ShouldCraftOnAutogatherChanged;
        if (ImGui.Checkbox("Craft selected Artisan list on autogather finish", ref craftOnAutogather))
        {
            configuration.ShouldCraftOnAutogatherChanged = craftOnAutogather;
            configuration.Save();
        }
        if (collectOnAutogatherActiveArtisan && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Disabled while \"Turn in collectables on autogather finish\" is enabled (GatherBuddyReborn section).");
        else if (artisanDisabledReason != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(artisanDisabledReason);
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!craftOnAutogather);

        DrawArtisanListPicker();

        var collectOnFinish = configuration.CollectOnFinishCraftingList;
        if (ImGui.Checkbox("Collect on finish crafting an Artisan list", ref collectOnFinish))
        {
            configuration.CollectOnFinishCraftingList = collectOnFinish;
            configuration.Save();
        }

        ImGui.EndDisabled();

        ImGui.Spacing();
        var pauseOnFull = configuration.PauseArtisanOnInventoryFull;
        if (ImGui.Checkbox("Pause Artisan when inventory is full, turn in, then resume", ref pauseOnFull))
        {
            configuration.PauseArtisanOnInventoryFull = pauseOnFull;
            configuration.Save();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("When the running Artisan list drops below the free-slot threshold and\n" +
                             "you have collectables, pause the list, run the turn-in cascade, then\n" +
                             "resume the list from where it left off.");

        ImGui.BeginDisabled(!pauseOnFull);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Pause when free slots ≤");
        ImGui.SameLine();
        var threshold = configuration.ArtisanInventoryFullThreshold;
        ImGui.PushItemWidth(80);
        if (ImGui.InputInt("##InvFullThreshold", ref threshold, 1, 5))
        {
            configuration.ArtisanInventoryFullThreshold = Math.Clamp(threshold, 0, 140);
            configuration.Save();
        }
        ImGui.PopItemWidth();
        ImGui.EndDisabled();

        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("AutoRetainer");

        bool arReady = IPCSubscriber_Common.IsReady("AutoRetainer");
        ImGui.BeginDisabled(!arReady);

        var checkVentures = configuration.CheckForVenturesBetweenRuns;
        if (ImGui.Checkbox("Check for available ventures between runs", ref checkVentures))
        {
            configuration.CheckForVenturesBetweenRuns = checkVentures;
            configuration.Save();
        }
        if (!arReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("AutoRetainer is not installed or not ready.");

        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Deliveroo");

        bool deliverooReady = IPCSubscriber_Common.IsReady("Deliveroo");
        bool isMaelstrom = (uint)Player.GrandCompany == 1;
        bool lifestreamReady = IPCSubscriber_Common.IsReady("Lifestream");
        bool deliverooDisabled = !deliverooReady || (isMaelstrom && !lifestreamReady);
        ImGui.BeginDisabled(deliverooDisabled);

        var checkDeliveroo = configuration.CheckForDeliverooBetweenRuns;
        if (ImGui.Checkbox("Run Deliveroo GC turn-ins between runs", ref checkDeliveroo))
        {
            configuration.CheckForDeliverooBetweenRuns = checkDeliveroo;
            configuration.Save();
        }
        if (deliverooDisabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            if (!deliverooReady)
                ImGui.SetTooltip("Deliveroo is not installed or not ready.");
            else
                ImGui.SetTooltip("Lifestream plugin is required for Maelstrom GC turn-ins.");
        }

        ImGui.EndDisabled();

        ImGui.Spacing();
        DrawSettingsDiscord();
    }

    private void DrawArtisanListPicker()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Artisan List:");
        ImGui.SameLine();

        var artisanIpc = ServiceWrapper.Get<Artisan_IPCSubscriber>();
        if (_artisanLists == null || (DateTime.UtcNow - _artisanListsFetchedAt).TotalSeconds >= 5)
        {
            _artisanLists = artisanIpc.IsEnabled
                ? artisanIpc.GetLists() ?? new Dictionary<int, string>()
                : new Dictionary<int, string>();
            _artisanListsFetchedAt = DateTime.UtcNow;
        }
        var lists = _artisanLists;

        if (lists.Count == 0)
        {
            var manualId = configuration.ArtisanListId;
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("##ArtisanListID", ref manualId, 0, 0))
            {
                configuration.ArtisanListId = manualId;
                configuration.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled(artisanIpc.IsEnabled ? "(no lists in Artisan)" : "(Artisan not ready)");
            return;
        }

        var currentId = configuration.ArtisanListId;
        var currentLabel = lists.TryGetValue(currentId, out var name)
            ? $"[{currentId}] {name}"
            : currentId == 0 ? "Select a list" : $"[{currentId}] (not found)";

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##ArtisanListID", currentLabel))
        {
            foreach (var (id, listName) in lists.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
            {
                var label = $"[{id}] {listName}";
                if (ImGui.Selectable(label, id == currentId))
                {
                    configuration.ArtisanListId = id;
                    configuration.Save();
                }
            }
            ImGui.EndCombo();
        }
    }

    private void DrawSettingsDiscord()
    {
        ImGuiHelper.SectionHeader("Discord Webhook");

        var enabled = configuration.Discord.Enabled;
        if (ImGui.Checkbox("Send Discord notifications", ref enabled))
        {
            configuration.Discord.Enabled = enabled;
            configuration.Save();
        }

        ImGui.BeginDisabled(!enabled);

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Webhook URL:");
        ImGui.SameLine();
        var url = configuration.Discord.WebhookUrl ?? "";
        ImGui.SetNextItemWidth(-90);
        if (ImGui.InputText("##discordurl", ref url, 256, ImGuiInputTextFlags.Password))
        {
            configuration.Discord.WebhookUrl = url;
            configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Test##discord", new Vector2(80, 0)))
            _ = _discord.TestAsync();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Posts a test message to verify the webhook works.");

        ImGui.Spacing();
        ImGui.TextDisabled("Notify on:");

        var notifyHardFail = configuration.Discord.NotifyOnHardFail;
        if (ImGui.Checkbox("Hard fail", ref notifyHardFail))
        {
            configuration.Discord.NotifyOnHardFail = notifyHardFail;
            configuration.Save();
        }

        var notifyGoal = configuration.Discord.NotifyOnGoalComplete;
        if (ImGui.Checkbox("Goal complete (purchase list done)", ref notifyGoal))
        {
            configuration.Discord.NotifyOnGoalComplete = notifyGoal;
            configuration.Save();
        }

        var notifyStop = configuration.Discord.NotifyOnStopCondition;
        if (ImGui.Checkbox("Stop condition met", ref notifyStop))
        {
            configuration.Discord.NotifyOnStopCondition = notifyStop;
            configuration.Save();
        }

        var notifyCap = configuration.Discord.NotifyOnScripCap;
        if (ImGui.Checkbox("Scrip cap reached", ref notifyCap))
        {
            configuration.Discord.NotifyOnScripCap = notifyCap;
            configuration.Save();
        }

        ImGui.EndDisabled();
    }

    private void DrawSettingsTiming()
    {
        ImGuiHelper.SectionHeader("UI Delay");

        ImGui.TextWrapped("Delay between UI interactions during automation. Lower values run faster but may misbehave on slower machines or with high latency.");
        ImGui.Spacing();

        var delay = configuration.UiDelayMs;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##UiDelay", ref delay, 50, 1500, "%d ms"))
        {
            configuration.UiDelayMs = delay;
            configuration.Save();
        }

        if (ImGui.Button($"Reset to default ({Configuration.DefaultUiDelayMs} ms)"))
        {
            configuration.UiDelayMs = Configuration.DefaultUiDelayMs;
            configuration.Save();
        }
    }

    private void DrawSettingsGoal()
    {
        ImGuiHelper.SectionHeader("Goal Automation");

        var stopOnComplete = configuration.Goal.StopGatheringWhenComplete;
        if (ImGui.Checkbox("Stop gathering when purchase list is complete", ref stopOnComplete))
        {
            configuration.Goal.StopGatheringWhenComplete = stopOnComplete;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When enabled, automation will stop instead of\nre-enabling autogather once all items are purchased.");

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Stop Conditions");
        ImGui.TextWrapped("Evaluated between turn-in and buy cycles. Counters reset when the plugin reloads.");
        ImGui.Spacing();

        {
            var en = configuration.Stop.StopOnScripsEarnedEnabled;
            var v  = configuration.Stop.MaxScripsEarned;
            if (DrawStopConditionRow("Stop after scrips earned", ref en, ref v, 100, 1_000_000, 500,
                "Total scrips estimated earned this session (using HighReward).\nApplies across currencies."))
            {
                configuration.Stop.StopOnScripsEarnedEnabled = en;
                configuration.Stop.MaxScripsEarned = v;
                configuration.Save();
            }
        }
        {
            var en = configuration.Stop.StopOnBuyCyclesEnabled;
            var v  = configuration.Stop.MaxBuyCycles;
            if (DrawStopConditionRow("Stop after buy cycles", ref en, ref v, 1, 1000, 1,
                "Number of times the shop has been visited and purchases completed."))
            {
                configuration.Stop.StopOnBuyCyclesEnabled = en;
                configuration.Stop.MaxBuyCycles = v;
                configuration.Save();
            }
        }
        {
            var en = configuration.Stop.StopOnSessionTimeEnabled;
            var v  = configuration.Stop.MaxSessionMinutes;
            if (DrawStopConditionRow("Stop after session minutes", ref en, ref v, 5, 1440, 5,
                "Real elapsed minutes since the current session started."))
            {
                configuration.Stop.StopOnSessionTimeEnabled = en;
                configuration.Stop.MaxSessionMinutes = v;
                configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Planner");

        var hideFish = configuration.Goal.HideFishingCollectables;
        if (ImGui.Checkbox("Hide fishing collectables from planner", ref hideFish))
        {
            configuration.Goal.HideFishingCollectables = hideFish;
            configuration.Save();
        }

        var hideUnobtainable = configuration.Goal.HideUnobtainableCollectables;
        if (ImGui.Checkbox("Hide collectables above your job level", ref hideUnobtainable))
        {
            configuration.Goal.HideUnobtainableCollectables = hideUnobtainable;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filters the planner to only show collectables you can\nactually gather or craft. Also gates the 'best' pick.");
    }

    private static bool DrawStopConditionRow(string label, ref bool enabled, ref int value,
        int min, int max, int step, string tooltip)
    {
        bool changed = false;

        if (ImGui.Checkbox($"##en_{label}", ref enabled))
            changed = true;
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        ImGui.BeginDisabled(!enabled);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt($"##v_{label}", ref value, step, step * 10))
        {
            value = Math.Clamp(value, min, max);
            changed = true;
        }
        ImGui.EndDisabled();

        return changed;
    }

    private void DrawSettingsDebug()
    {
        var collectable   = ServiceWrapper.Get<CollectableAutomationHandler>();
        var scripShop     = ServiceWrapper.Get<ScripShopAutomationHandler>();
        var retainer      = ServiceWrapper.Get<AutoRetainerManager>();
        var deliveroo     = ServiceWrapper.Get<DeliverooManager>();
        var vendorCatalog = ServiceWrapper.Get<VendorCatalog>();

        ImGuiHelper.SectionHeader("State");

        if (ImGui.BeginTable("##dbgstate", 2, ImGuiTableFlags.SizingStretchProp))
        {
            void Row(string label, string value)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextDisabled(label);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(value);
            }

            var terId   = Svc.ClientState.TerritoryType;
            var terName = VendorCatalog.GetTerritoryDisplayName(terId);
            var prefId  = configuration.PreferredTerritoryId;
            var prefName = prefId == 0 ? "<none>" : VendorCatalog.GetTerritoryDisplayName(prefId);

            Row("Plugin state",       string.IsNullOrEmpty(_status.Detail) ? _status.Current.ToString() : $"{_status.Current} ({_status.Detail})");
            Row("Territory",          $"{terId} ({terName})");
            Row("Preferred",          $"{prefId} ({prefName})");
            Row("Active source",      configuration.ActiveRunSource.ToString());
            Row("Hard fail",          configuration.HardFailReason ?? "<none>");
            Row("Config version",     configuration.Version.ToString());
            Row("Has collectible",    collectable.HasCollectible.ToString());
            Row("Free inv slots",     ItemHelper.GetFreeInventorySlots().ToString());
            Row("Automation running", _automationHandler.IsRunning.ToString());
            Row("  collectable",      collectable.IsRunning.ToString());
            Row("  scrip shop",       scripShop.IsRunning.ToString());
            Row("  autoretainer",     retainer.IsRunning.ToString());
            Row("  deliveroo",        deliveroo.IsRunning.ToString());
            Row("Session started",    _automationHandler.SessionStarted?.ToLocalTime().ToString("HH:mm:ss") ?? "-");
            Row("Turn-ins",           _automationHandler.SessionCollectablesTurnedIn.ToString());
            Row("Items purchased",    _automationHandler.SessionItemsPurchased.ToString());
            Row("Scrips earned",      _automationHandler.SessionScripsEarnedTotal.ToString());
            Row("Current item",       collectable.CurrentItemName ?? "-");

            string queueText;
            if (collectable.TurnInQueue is { Count: > 0 } q)
            {
                var preview = string.Join(", ", q.Take(3).Select(t => $"{t.name}×{t.left}"));
                queueText = q.Count > 3 ? $"{preview}, +{q.Count - 3} more" : preview;
            }
            else queueText = "<empty>";
            Row("Turn-in queue", queueText);

            var cv = vendorCatalog.GetCollectableVendor(prefId);
            var sv = vendorCatalog.GetScripVendor(prefId);
            Row("Collectable NPC", cv == null ? "<none>" : $"{cv.Name} @ ({cv.Position.X:F1}, {cv.Position.Y:F1}, {cv.Position.Z:F1})");
            Row("Scrip NPC",       sv == null ? "<none>" : $"{sv.Name} @ ({sv.Position.X:F1}, {sv.Position.Y:F1}, {sv.Position.Z:F1})");

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Pipelines");
        ImGui.TextDisabled("Starts the pipeline directly, bypassing the automation cascade.");
        ImGui.Spacing();

        if (ImGui.Button("Start collectable")) collectable.Start();
        ImGui.SameLine();
        if (ImGui.Button("Start scrip shop")) scripShop.Start();
        ImGui.SameLine();
        if (ImGui.Button("Start autoretainer")) retainer.Start();
        ImGui.SameLine();
        if (ImGui.Button("Start deliveroo")) deliveroo.Start();

        if (ImGui.Button("Start Kupo of Fortune")) _automationHandler.InvokeKupo();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Walk to Lizbeth in the Firmament and play any held Kupo of Fortune\n" +
                             "cards. Runs standalone — won't trigger the buy/cascade flow.");

        if (ImGui.Button("Force stop all"))
            _automationHandler.ForceStop("Stopped from debug panel");

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Reset");

        bool ctrl = ImGui.GetIO().KeyCtrl;
        if (!ctrl)
            ImGui.TextDisabled("Hold Ctrl to enable destructive buttons.");
        else
            ImGui.TextColored(UiTheme.Danger, "Ctrl held — buttons are armed.");

        ImGui.BeginDisabled(!ctrl);

        if (ImGui.Button("Clear hard fail"))
        {
            configuration.HardFailReason = null;
            configuration.Save();
            _log.Debug("Debug: cleared HardFailReason.");
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset session counters"))
        {
            ResetSessionCountersDebug();
            _log.Debug("Debug: reset session counters.");
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset purchased amounts"))
        {
            foreach (var i in configuration.Goal.ItemsToPurchase)
                i.AmountPurchased = 0;
            configuration.Save();
            _log.Debug("Debug: reset AmountPurchased on every Goal item.");
        }

        if (ImGui.Button("Clear character balances"))
        {
            configuration.CharacterBalances.Clear();
            configuration.Save();
            _log.Debug("Debug: cleared CharacterBalances.");
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear total scrips spent"))
        {
            configuration.TotalScripsSpent.Clear();
            configuration.Save();
            _log.Debug("Debug: cleared TotalScripsSpent.");
        }

        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Dump to log");

        if (ImGui.Button("Vendor catalog"))
        {
            var payload = vendorCatalog.AllVendors.Select(v => new
            {
                v.DataId, v.Name, v.TerritoryId, v.MapId,
                Position = new { v.Position.X, v.Position.Y, v.Position.Z },
                v.IsScripVendor, v.IsCollectableVendor,
            });
            _log.Debug($"VendorCatalog ({vendorCatalog.AllVendors.Count} entries):\n{JsonSerializer.Serialize(payload, DebugJsonOpts)}");
        }
        ImGui.SameLine();
        if (ImGui.Button("Turn-in queue"))
        {
            if (collectable.TurnInQueue is null) _log.Debug("TurnInQueue: null");
            else if (collectable.TurnInQueue.Count == 0) _log.Debug("TurnInQueue: empty");
            else _log.Debug($"TurnInQueue ({collectable.TurnInQueue.Count}):\n" +
                string.Join("\n", collectable.TurnInQueue.Select(t => $"  {t.name} ×{t.left} (job {t.jobIndex})")));
        }
        ImGui.SameLine();
        if (ImGui.Button("Scrip shop items"))
        {
            var payload = ScripShopItemManager.ShopItems.Select(s => new
            {
                s.ItemId, name = s.Name, s.Page, s.SubPage, s.ItemCost, s.CurrencyId,
            });
            _log.Debug($"ScripShopItems ({ScripShopItemManager.ShopItems.Count}):\n{JsonSerializer.Serialize(payload, DebugJsonOpts)}");
        }
        ImGui.SameLine();
        if (ImGui.Button("Full config"))
        {
            _log.Debug($"Configuration:\n{JsonSerializer.Serialize(configuration, DebugJsonOpts)}");
        }
    }

    private void ResetSessionCountersDebug()
    {
        var t = typeof(AutomationHandler);
        foreach (var name in new[]
        {
            nameof(AutomationHandler.SessionCollectablesTurnedIn),
            nameof(AutomationHandler.SessionItemsPurchased),
        })
            t.GetProperty(name)?.GetSetMethod(nonPublic: true)?.Invoke(_automationHandler, new object[] { 0 });

        t.GetProperty(nameof(AutomationHandler.SessionStarted))
            ?.GetSetMethod(nonPublic: true)
            ?.Invoke(_automationHandler, new object?[] { null });

        _automationHandler.SessionScripsSpent.Clear();
        _automationHandler.SessionScripsEarned.Clear();
    }
}
