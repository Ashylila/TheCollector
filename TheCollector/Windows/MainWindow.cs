using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using TheCollector.Data;
using TheCollector.Data.Models;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private string comboFilter = "";
    private Configuration configuration;

    private readonly PlogonLog _log;
    private ScripShopItem? SelectedScripItem = null;

    public MainWindow(Plugin plugin, IDalamudPluginInterface pluginInterface, PlogonLog log)
        : base("The Collector##CollectorMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(340, 0),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _log = log;
        configuration = plugin.Configuration;
        this.pluginInterface = pluginInterface;
    }

    public void Dispose() { }

    public override void PreDraw() { }

    public override void Draw()
    {
        if (ScripShopItemManager.IsLoading)
        {
            ImGui.TextDisabled("Loading items...");
            return;
        }

        DrawStatusBar();
        DrawAddItem();
        DrawItemsList();
    }

    private void DrawStatusBar()
    {
        ImGuiHelper.Panel("StatusBar", () =>
        {
            var (color, label) = Plugin.State switch
            {
                PluginState.Teleporting             => (new Vector4(0.95f, 0.75f, 0.10f, 1f), "Teleporting..."),
                PluginState.MovingToCollectableVendor => (new Vector4(0.95f, 0.75f, 0.10f, 1f), "Moving to vendor..."),
                PluginState.ExchangingItems          => (new Vector4(0.30f, 0.85f, 0.30f, 1f), "Exchanging items..."),
                PluginState.SpendingScrip            => (new Vector4(0.30f, 0.85f, 0.30f, 1f), "Spending scrip..."),
                PluginState.AutoRetainer             => (new Vector4(0.40f, 0.65f, 1.00f, 1f), "AutoRetainer running..."),
                _                                    => (new Vector4(0.55f, 0.55f, 0.55f, 1f), "Idle")
            };

            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted($"● {label}");
            ImGui.PopStyleColor();

            if (configuration.ItemsToPurchase.Count > 0)
            {
                int completed = configuration.ItemsToPurchase.Count(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity);
                ImGui.SameLine();
                ImGui.TextDisabled($"   {completed}/{configuration.ItemsToPurchase.Count} items done");
            }
        });
    }

    private void DrawAddItem()
    {
        ImGuiHelper.Panel("AddItem", () =>
        {
            ImGui.TextDisabled("Add Item");
            ImGui.Separator();
            ImGui.Spacing();

            bool alreadyAdded = SelectedScripItem != null &&
                                configuration.ItemsToPurchase.Any(i => i.Item.ItemId == SelectedScripItem.ItemId);

            var style = ImGui.GetStyle();
            float buttonWidth = ImGui.CalcTextSize("+").X + style.FramePadding.X * 2;
            float textWidth = alreadyAdded ? ImGui.CalcTextSize("Already in list").X + style.ItemSpacing.X : 0;
            float reservedWidth = buttonWidth + textWidth + style.ItemSpacing.X;
            float comboWidth = Math.Min(200f, ImGui.GetContentRegionAvail().X - reservedWidth);

            ImGui.PushItemWidth(comboWidth);
            if (ImGui.BeginCombo("##ItemCombo", SelectedScripItem?.Name ?? "Select an item..."))
            {
                ImGui.InputTextWithHint("##ComboFilter", "Filter...", ref comboFilter, 100);
                ImGui.Separator();

                foreach (var item in ScripShopItemManager.ShopItems
                             .Where(i => string.IsNullOrEmpty(comboFilter) ||
                                         i.Name.Contains(comboFilter, StringComparison.OrdinalIgnoreCase)))
                {
                    bool isSelected = item == SelectedScripItem;
                    ImGui.Image(item.IconTexture.GetWrapOrEmpty().Handle, new Vector2(20, 20));
                    ImGui.SameLine();
                    if (ImGui.Selectable(item.Name, isSelected))
                    {
                        SelectedScripItem = item;
                        comboFilter = "";
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();

            ImGui.BeginDisabled(SelectedScripItem == null || alreadyAdded);
            if (ImGui.Button("+##AddBtn"))
            {
                configuration.ItemsToPurchase.Add(new ItemToPurchase
                {
                    Item = SelectedScripItem!,
                    Quantity = 1
                });
                configuration.Save();
            }
            ImGui.EndDisabled();

            if (alreadyAdded)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("Already in list");
            }
        });
    }

    private void DrawItemsList()
    {
        if (configuration.ItemsToPurchase.Count == 0)
        {
            ImGui.TextDisabled("No items added yet.");
            return;
        }

        ImGuiHelper.Panel("ItemsList", () =>
        {
            ImGui.TextDisabled("Purchase List");
            ImGui.Separator();
            ImGui.Spacing();

            var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX;
            if (!ImGui.BeginTable("##ItemsTable", 5, tableFlags))
                return;

            ImGui.TableSetupColumn("##Remove",  ImGuiTableColumnFlags.WidthFixed,   22f);
            ImGui.TableSetupColumn("Item",      ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Progress",  ImGuiTableColumnFlags.WidthFixed,   100f);
            ImGui.TableSetupColumn("Qty",       ImGuiTableColumnFlags.WidthFixed,   52f);
            ImGui.TableSetupColumn("##Reset",   ImGuiTableColumnFlags.WidthFixed,   58f);
            ImGui.TableHeadersRow();

            for (int i = 0; i < configuration.ItemsToPurchase.Count; i++)
            {
                var item = configuration.ItemsToPurchase[i];
                bool done = item.Quantity > 0 && item.AmountPurchased >= item.Quantity;

                ImGui.TableNextRow();

                // Remove
                ImGui.TableSetColumnIndex(0);
                ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.55f, 0.10f, 0.10f, 0.70f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.80f, 0.15f, 0.15f, 1.00f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(1.00f, 0.20f, 0.20f, 1.00f));
                if (ImGui.Button($"-##Remove{i}", new Vector2(22, 22)))
                {
                    configuration.ItemsToPurchase.RemoveAt(i);
                    configuration.Save();
                    ImGui.PopStyleColor(3);
                    i--;
                    continue;
                }
                ImGui.PopStyleColor(3);

                // Icon + Name
                ImGui.TableSetColumnIndex(1);
                ImGui.Image(item.Item.IconTexture.GetWrapOrEmpty().Handle, new Vector2(20, 20));
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                if (done)
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.90f, 0.35f, 1f));
                ImGui.TextUnformatted(item.Item.Name);
                if (done)
                    ImGui.PopStyleColor();
                if (item.Item.ItemCost > 0 && ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Cost: {item.Item.ItemCost} scrips each");

                // Progress bar
                ImGui.TableSetColumnIndex(2);
                float progress = item.Quantity > 0
                    ? Math.Clamp((float)item.AmountPurchased / item.Quantity, 0f, 1f)
                    : 0f;
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram,
                    done
                        ? new Vector4(0.20f, 0.70f, 0.20f, 0.85f)
                        : new Vector4(0.20f, 0.50f, 0.85f, 0.85f));
                ImGui.ProgressBar(progress, new Vector2(-1, ImGui.GetFrameHeight()), $"{item.AmountPurchased}/{item.Quantity}");
                ImGui.PopStyleColor();

                // Quantity
                ImGui.TableSetColumnIndex(3);
                bool isEquippable = item.Item.Item.EquipSlotCategory.RowId != 0;
                int qty = item.Quantity;
                ImGui.PushItemWidth(52);
                if (isEquippable)
                {
                    ImGui.BeginDisabled();
                    ImGui.InputInt($"##Qty{i}", ref qty, 0, 0);
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Equippable items can only be purchased one at a time");
                }
                else if (ImGui.InputInt($"##Qty{i}", ref qty, 0, 0))
                {
                    item.Quantity = Math.Max(0, qty);
                    configuration.ItemsToPurchase[i] = item;
                    configuration.Save();
                }
                ImGui.PopItemWidth();

                // Reset
                ImGui.TableSetColumnIndex(4);
                if (ImGui.Button($"Reset##R{i}", new Vector2(56, 0)))
                {
                    item.ResetQuantity();
                    configuration.Save();
                }
            }

            ImGui.EndTable();
        });
    }
}
