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
    private uint[] itemIds = Array.Empty<uint>();

    public ConfigWindow(Plugin plugin, IDataManager data, ITargetManager target, ScripShopWindowHandler scripShop, AutoRetainerManager manager)
        : base("Configuration###CollectorConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.AlwaysAutoResize;

        SizeCondition = ImGuiCond.Appearing;

        /*        SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(380, 0),
                    MaximumSize = new Vector2(1200, 800)
                };
                */

        _dataManager = data;
        Configuration = plugin.Configuration;
        _retainer = manager;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
    }

    public override void Draw()
    {
        DrawInstalledPlugins();
        DrawOptions();
        DrawSupportButton();
    }
    
    private void DrawInstalledPlugins()
    {
        ImGuiHelper.Panel("InstalledPlgs", () =>
        {
            ImGui.TextUnformatted("Installed required/optional Plugins:");

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text,
                                 IPCSubscriber_Common.IsReady("vnavmesh")
                                     ? new Vector4(0, 1, 0, 1)
                                     : new Vector4(1, 0, 0, 1));
            ImGui.TextUnformatted("vnavmesh(required)");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text,
                                 IPCSubscriber_Common.IsReady("GatherbuddyReborn")
                                     ? new Vector4(0, 1, 0, 1)
                                     : new Vector4(1, 0, 0, 1));
            ImGui.TextUnformatted("GatherbuddyReborn(optional)");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text,
                                 IPCSubscriber_Common.IsReady("Artisan")
                                     ? new Vector4(0, 1, 0, 1)
                                     : new Vector4(1, 0, 0, 1));
            ImGui.TextUnformatted("Artisan(optional)");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text,
                                 IPCSubscriber_Common.IsReady("AutoRetainer")
                                     ? new Vector4(0, 1, 0, 1)
                                     : new Vector4(1, 0, 0, 1));
            ImGui.TextUnformatted("AutoRetainer(optional)");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text,
                                 IPCSubscriber_Common.IsReady("Lifestream")
                                     ? new Vector4(0, 1, 0, 1)
                                     : new Vector4(1, 0, 0, 1));
            ImGui.TextUnformatted("Lifestream(optional)");
            ImGui.PopStyleColor();
            ImGui.Spacing();
        });
    }

    public void DrawSupportButton()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.60f, 0.86f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.30f, 0.70f, 0.96f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.10f, 0.50f, 0.76f, 1.00f));

        float buttonWidth = ImGui.CalcTextSize("Support Me").X + ImGui.GetStyle().FramePadding.X * 2;
        float windowWidth = ImGui.GetWindowContentRegionMax().X;
        float cursorX = windowWidth - buttonWidth;

        ImGui.SetCursorPosX(cursorX);
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
            ImGui.TextUnformatted("Options:");
            if (ImGui.CollapsingHeader("Artisan"))
            {

                ImGui.BeginDisabled(!IPCSubscriber_Common.IsReady("GatherbuddyReborn") || !IPCSubscriber_Common.IsReady("Artisan"));

                var craftOnAutogatherDisabled = Configuration.ShouldCraftOnAutogatherChanged;
                if (ImGui.Checkbox("Craft selected Artisan list id on autogather finish", ref craftOnAutogatherDisabled))
                {
                    Configuration.ShouldCraftOnAutogatherChanged = craftOnAutogatherDisabled;
                    Configuration.Save();
                }
                ImGui.BeginDisabled(!craftOnAutogatherDisabled);
                var listId = Configuration.ArtisanListId;
                ImGui.Text("Artisan List ID:");
                if (ImGui.InputInt("##ArtisanListID", ref listId, 100))
                {
                    Configuration.ArtisanListId = listId;
                    Configuration.Save();
                }

                var toggleCollectOnFinishCraftingList = Configuration.CollectOnFinishCraftingList;
                if (ImGui.Checkbox("Collect on Finish Crafting an Artisan List", ref toggleCollectOnFinishCraftingList))
                {
                    Configuration.CollectOnFinishCraftingList = toggleCollectOnFinishCraftingList;
                    Configuration.Save();
                }

                ImGui.EndDisabled();
                ImGui.EndDisabled();
            }
            if (ImGui.CollapsingHeader("AutoRetainer"))
            {
                ImGui.BeginDisabled(!IPCSubscriber_Common.IsReady("AutoRetainer"));
                var checkForVentures = Configuration.CheckForVenturesBetweenRuns;
                if(ImGui.Checkbox("Check for available ventures between runs", ref checkForVentures))
                {
                    Configuration.CheckForVenturesBetweenRuns = checkForVentures;
                    Configuration.Save();
                }
            }
            if (ImGui.CollapsingHeader("Misc"))
            {
                ImGui.BeginDisabled(!IPCSubscriber_Common.IsReady("GatherbuddyReborn"));
                var toggleAutogatherOnFinish = Configuration.EnableAutogatherOnFinish;
                if (ImGui.Checkbox("Enable Autogather on finish", ref toggleAutogatherOnFinish))
                {
                    Configuration.EnableAutogatherOnFinish = toggleAutogatherOnFinish;
                    Configuration.Save();
                }

                ImGui.EndDisabled();


                var buyAfterEachCollect = Configuration.BuyAfterEachCollect;
                if (ImGui.Checkbox("Buy items after each trade instead of on capping scrips", ref buyAfterEachCollect))
                {
                    Configuration.BuyAfterEachCollect = buyAfterEachCollect;
                    Configuration.Save();
                }

                var resetEachQuantityAfterCompletingList = Configuration.ResetEachQuantityAfterCompletingList;
                if (ImGui.Checkbox("Reset each quantity after completing the list",
                                   ref resetEachQuantityAfterCompletingList))
                {
                    Configuration.ResetEachQuantityAfterCompletingList = resetEachQuantityAfterCompletingList;
                    Configuration.Save();
                }

                var invokeAfterFinishFishing = Configuration.CollectOnFinishedFishing;
                if (ImGui.Checkbox("Collect on Finished Fishing", ref invokeAfterFinishFishing))
                {
                    Configuration.CollectOnFinishedFishing = invokeAfterFinishFishing;
                    Configuration.Save();
                }
            }
            ImGui.TextUnformatted("Select your preferred collectable shop:");
            ImGui.SameLine();
            string currentShopName = Configuration.PreferredCollectableShop.DisplayName ?? "Select a shop";

            ImGui.Spacing();
            if (ImGui.BeginCombo("##shopselection", currentShopName))
            {
                for (int i = 0; i < CollectableNpcLocations.CollectableShops.Count; i++)
                {
                    ImGui.BeginDisabled(CollectableNpcLocations.CollectableShops[i].Disabled || (CollectableNpcLocations.CollectableShops[i].IsLifestreamRequired && !IPCSubscriber_Common.IsReady("Lifestream")));
                    var shop = CollectableNpcLocations.CollectableShops[i];
                    if (ImGui.Selectable(shop.IsLifestreamRequired && !IPCSubscriber_Common.IsReady("Lifestream") ? (shop.DisplayName + " (Lifestream required)") : shop.DisplayName))
                    {
                        Configuration.PreferredCollectableShop = CollectableNpcLocations.CollectableShops[i];
                        Configuration.Save();
                    }

                    ImGui.EndDisabled();
                }

                ImGui.EndCombo();
            }
            ImGui.EndDisabled();
            ImGui.Spacing();
        });
    }

}
