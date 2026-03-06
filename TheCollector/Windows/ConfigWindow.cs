using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using TheCollector.CollectableManager;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly IDataManager _dataManager;
    private Configuration Configuration;
    private AutoRetainerManager _retainer;

    public ConfigWindow(Plugin plugin, IDataManager data, ITargetManager target, ScripShopWindowHandler scripShop, AutoRetainerManager manager)
        : base("Configuration###CollectorConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.AlwaysAutoResize;

        SizeCondition = ImGuiCond.Appearing;

        _dataManager = data;
        Configuration = plugin.Configuration;
        _retainer = manager;
    }

    public void Dispose() { }

    public override void PreDraw() { }

    public override void Draw()
    {
        DrawInstalledPlugins();
        DrawOptions();
        DrawSupportButton();
    }

    private static void DrawInstalledPlugins()
    {
        ImGuiHelper.Panel("InstalledPlgs", () =>
        {
            ImGui.TextDisabled("Required & Optional Plugins");
            ImGui.Separator();
            ImGui.Spacing();

            DrawPluginStatus("vnavmesh",          "vnavmesh",          required: true);
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X / 2f);
            DrawPluginStatus("GatherbuddyReborn", "GatherbuddyReborn", required: false);

            DrawPluginStatus("Artisan",           "Artisan",           required: false);
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X / 2f);
            DrawPluginStatus("AutoRetainer",      "AutoRetainer",      required: false);

            DrawPluginStatus("Lifestream",        "Lifestream",        required: false);
        });
    }

    private static void DrawPluginStatus(string pluginKey, string displayName, bool required)
    {
        bool ready = IPCSubscriber_Common.IsReady(pluginKey);
        var dotColor = ready ? new Vector4(0.20f, 0.85f, 0.20f, 1f) : new Vector4(0.85f, 0.20f, 0.20f, 1f);
        var label = required ? $"{displayName} (required)" : $"{displayName} (optional)";

        ImGui.PushStyleColor(ImGuiCol.Text, dotColor);
        ImGui.TextUnformatted("●");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }

    public static void DrawSupportButton()
    {
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.20f, 0.60f, 0.86f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.30f, 0.70f, 0.96f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.10f, 0.50f, 0.76f, 1.00f));

        float buttonWidth = ImGui.CalcTextSize("Support Me").X + ImGui.GetStyle().FramePadding.X * 2;
        float windowWidth = ImGui.GetWindowContentRegionMax().X;
        ImGui.SetCursorPosX(windowWidth - buttonWidth);
        if (ImGui.Button("Support Me"))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/Ashylila",
                UseShellExecute = true
            });
        }

        ImGui.PopStyleColor(3);
    }

    public void DrawOptions()
    {
        ImGuiHelper.Panel("Options", () =>
        {
            ImGui.BeginDisabled(!IPCSubscriber_Common.IsReady("vnavmesh"));

            ImGui.TextDisabled("Options");
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.CollapsingHeader("Artisan"))
            {
                ImGui.BeginDisabled(!IPCSubscriber_Common.IsReady("GatherbuddyReborn") ||
                                    !IPCSubscriber_Common.IsReady("Artisan"));

                var craftOnAutogather = Configuration.ShouldCraftOnAutogatherChanged;
                if (ImGui.Checkbox("Craft selected Artisan list on autogather finish", ref craftOnAutogather))
                {
                    Configuration.ShouldCraftOnAutogatherChanged = craftOnAutogather;
                    Configuration.Save();
                }

                ImGui.BeginDisabled(!craftOnAutogather);

                ImGui.TextUnformatted("Artisan List ID:");
                ImGui.SameLine();
                var listId = Configuration.ArtisanListId;
                ImGui.PushItemWidth(80);
                if (ImGui.InputInt("##ArtisanListID", ref listId, 0, 0))
                {
                    Configuration.ArtisanListId = listId;
                    Configuration.Save();
                }
                ImGui.PopItemWidth();

                var collectOnFinish = Configuration.CollectOnFinishCraftingList;
                if (ImGui.Checkbox("Collect on finish crafting an Artisan list", ref collectOnFinish))
                {
                    Configuration.CollectOnFinishCraftingList = collectOnFinish;
                    Configuration.Save();
                }

                ImGui.EndDisabled();
                ImGui.EndDisabled();
            }

            if (ImGui.CollapsingHeader("AutoRetainer"))
            {
                ImGui.BeginDisabled(!IPCSubscriber_Common.IsReady("AutoRetainer"));

                var checkVentures = Configuration.CheckForVenturesBetweenRuns;
                if (ImGui.Checkbox("Check for available ventures between runs", ref checkVentures))
                {
                    Configuration.CheckForVenturesBetweenRuns = checkVentures;
                    Configuration.Save();
                }

                ImGui.EndDisabled();
            }

            if (ImGui.CollapsingHeader("Misc"))
            {
                ImGui.BeginDisabled(!IPCSubscriber_Common.IsReady("GatherbuddyReborn"));

                var autogatherOnFinish = Configuration.EnableAutogatherOnFinish;
                if (ImGui.Checkbox("Enable Autogather on finish", ref autogatherOnFinish))
                {
                    Configuration.EnableAutogatherOnFinish = autogatherOnFinish;
                    Configuration.Save();
                }

                ImGui.EndDisabled();

                var buyAfterEach = Configuration.BuyAfterEachCollect;
                if (ImGui.Checkbox("Buy items after each trade instead of on capping scrips", ref buyAfterEach))
                {
                    Configuration.BuyAfterEachCollect = buyAfterEach;
                    Configuration.Save();
                }

                var resetOnComplete = Configuration.ResetEachQuantityAfterCompletingList;
                if (ImGui.Checkbox("Reset each quantity after completing the list", ref resetOnComplete))
                {
                    Configuration.ResetEachQuantityAfterCompletingList = resetOnComplete;
                    Configuration.Save();
                }

                var collectOnFishing = Configuration.CollectOnFinishedFishing;
                if (ImGui.Checkbox("Collect on finished fishing", ref collectOnFishing))
                {
                    Configuration.CollectOnFinishedFishing = collectOnFishing;
                    Configuration.Save();
                }
            }

            ImGui.Spacing();
            ImGui.TextDisabled("Collectable Shop");
            ImGui.Separator();
            ImGui.Spacing();

            string currentShopName = Configuration.PreferredCollectableShop.DisplayName ?? "Select a shop";
            ImGui.PushItemWidth(-1);
            if (ImGui.BeginCombo("##shopselection", currentShopName))
            {
                for (int i = 0; i < CollectableNpcLocations.CollectableShops.Count; i++)
                {
                    var shop = CollectableNpcLocations.CollectableShops[i];
                    bool lifestreamMissing = shop.IsLifestreamRequired && !IPCSubscriber_Common.IsReady("Lifestream");
                    ImGui.BeginDisabled(shop.Disabled || lifestreamMissing);

                    string shopLabel = lifestreamMissing
                        ? $"{shop.DisplayName} (Lifestream required)"
                        : shop.DisplayName;

                    if (ImGui.Selectable(shopLabel))
                    {
                        Configuration.PreferredCollectableShop = CollectableNpcLocations.CollectableShops[i];
                        Configuration.Save();
                    }

                    ImGui.EndDisabled();
                }

                ImGui.EndCombo();
            }
            ImGui.PopItemWidth();

            ImGui.EndDisabled();
        });
    }
}
