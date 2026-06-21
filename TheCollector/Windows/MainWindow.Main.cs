using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TheCollector.Data;
using TheCollector.Data.Models;
using TheCollector.Data.ScripSystem;
using TheCollector.Utility;

namespace TheCollector.Windows;

public partial class MainWindow
{
    private void DrawMainTab()
    {
        if (configuration.ActiveSystem.IsFirmamentLike())
        {
            DrawFirmamentMainTab();
            return;
        }

        if (ImGui.BeginTabBar("##PurchaseListTabs", ImGuiTabBarFlags.NoTooltip))
        {
            DrawPurchaseListTab("Gathering##GatherList", RunSource.Gathering);
            DrawPurchaseListTab("Crafting##CraftList",   RunSource.Crafting);
            ImGui.EndTabBar();
        }
    }

    private void DrawPurchaseListTab(string label, RunSource source)
    {
        if (!ImGui.BeginTabItem(label)) return;

        if (configuration.ActiveRunSource != source)
        {
            configuration.ActiveRunSource = source;
            configuration.Save();
            SelectedScripItem = null;
        }

        ImGui.Spacing();
        DrawAddItem(source);
        DrawItemsList(source);
        ImGui.EndTabItem();
    }

    private void DrawAddItem(RunSource source)
    {
        ImGuiHelper.Panel($"AddItem_{source}", () =>
        {
            ImGuiHelper.SectionHeader("Add Item");

            bool alreadyAdded = SelectedScripItem != null &&
                                configuration.Goal.ItemsToPurchase.Any(i => i.Item.ItemId == SelectedScripItem.ItemId);

            var style = ImGui.GetStyle();
            float buttonWidth = ImGui.CalcTextSize("+").X + style.FramePadding.X * 2;
            float textWidth = alreadyAdded ? ImGui.CalcTextSize("Already in list").X + style.ItemSpacing.X : 0;
            float reservedWidth = buttonWidth + textWidth + style.ItemSpacing.X;
            float comboWidth = Math.Max(140f, ImGui.GetContentRegionAvail().X - reservedWidth);

            ImGui.PushItemWidth(comboWidth);
            if (ImGui.BeginCombo("##ItemCombo", SelectedScripItem?.Name ?? "Select an item..."))
            {
                ImGui.InputTextWithHint("##ComboFilter", "Filter...", ref comboFilter, 100);
                ImGui.Separator();

                foreach (var item in ScripShopItemManager.ShopItems
                             .Where(i => CurrencyHelper.GetRunSource(i.CurrencyId) == source)
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
                configuration.Goal.ItemsToPurchase.Add(new ItemToPurchase
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
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("Already in list");
            }
        });
    }

    private void DrawItemsList(RunSource source)
    {
        var rows = configuration.Goal.ItemsToPurchase
            .Select((item, index) => (item, index))
            .Where(t => CurrencyHelper.GetRunSource(CurrencyHelper.GetCurrencyIdForItem(t.item.Item.ItemId)) == source)
            .ToList();

        if (rows.Count == 0)
        {
            ImGui.TextDisabled("No items added yet.");
            return;
        }

        ImGuiHelper.Panel($"ItemsList_{source}", () =>
        {
            ImGuiHelper.SectionHeader("Purchase List");

            var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX;
            if (!ImGui.BeginTable($"##ItemsTable_{source}", 5, tableFlags))
                return;

            ImGui.TableSetupColumn("##Remove",  ImGuiTableColumnFlags.WidthFixed,   22f);
            ImGui.TableSetupColumn("Item",      ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Progress",  ImGuiTableColumnFlags.WidthFixed,   100f);
            ImGui.TableSetupColumn("Qty",       ImGuiTableColumnFlags.WidthFixed,   52f);
            ImGui.TableSetupColumn("##Reset",   ImGuiTableColumnFlags.WidthFixed,   58f);
            ImGui.TableHeadersRow();

            foreach (var (item, originalIndex) in rows)
            {
                bool done = item.Quantity > 0 && item.AmountPurchased >= item.Quantity;

                ImGui.TableNextRow();

                // Remove
                ImGui.TableSetColumnIndex(0);
                if (ImGuiHelper.DangerButton($"x##Remove{originalIndex}", new Vector2(22, 22)))
                {
                    configuration.Goal.ItemsToPurchase.RemoveAt(originalIndex);
                    configuration.Save();
                    break;
                }

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
                var barW = ImGui.GetContentRegionAvail().X;
                if (barW < 1f) barW = 100f;
                var fromCol = done ? UiTheme.WithAlpha(UiTheme.Success, 0.55f) : UiTheme.WithAlpha(UiTheme.Accent, 0.55f);
                var toCol   = done ? UiTheme.Success                          : UiTheme.AccentHover;
                ImGuiHelper.GradientProgressBar(
                    progress,
                    new Vector2(barW, ImGui.GetFrameHeight()),
                    $"{item.AmountPurchased}/{item.Quantity}",
                    fromCol, toCol);

                // Quantity
                ImGui.TableSetColumnIndex(3);
                bool isUnique = item.Item.Item.IsUnique;
                int qty = item.Quantity;
                ImGui.PushItemWidth(52);
                if (isUnique)
                {
                    ImGui.BeginDisabled();
                    ImGui.InputInt($"##Qty{originalIndex}", ref qty, 0, 0);
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Unique items can only be owned one at a time");
                }
                else if (ImGui.InputInt($"##Qty{originalIndex}", ref qty, 0, 0))
                {
                    item.Quantity = Math.Max(0, qty);
                    configuration.Goal.ItemsToPurchase[originalIndex] = item;
                    configuration.Save();
                }
                ImGui.PopItemWidth();

                // Reset
                ImGui.TableSetColumnIndex(4);
                if (ImGui.Button($"Reset##R{originalIndex}", new Vector2(56, 0)))
                {
                    item.ResetQuantity();
                    configuration.Save();
                }
            }

            ImGui.EndTable();
        });
    }

    private List<ScripShopItem>? _firmamentWareCache;

    // Header-tab names by SpecialShopItemCategory row id (index 0 unused / invalid).
    private static readonly string[] FirmamentTabNames = { "", "Weapons", "Armor", "Accessories", "Others" };

    private string? FirmamentTabLabel(uint itemId)
    {
        if (!_firmamentCatalog.TryGetPlacement(itemId, out _, out var tabIndex)) return null;
        var categoryId = tabIndex + 1; // tabIndex = categoryId - 1
        return categoryId >= 1 && categoryId < FirmamentTabNames.Length ? FirmamentTabNames[categoryId] : null;
    }

    private List<ScripShopItem> FirmamentWares()
    {
        if (_firmamentWareCache != null) return _firmamentWareCache;
        if (!_firmamentCatalog.IsReady) return new List<ScripShopItem>();
        _firmamentWareCache = _firmamentCatalog.Wares
            .Select(w => new ScripShopItem { ItemId = w.ItemId, ItemCost = w.Cost, CurrencyId = _firmamentCatalog.ScripItemId })
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _firmamentWareCache;
    }

    private void DrawFirmamentMainTab()
    {
        ImGui.Spacing();
        DrawFirmamentAddItem();
        DrawFirmamentItemsList();
    }

    private void DrawFirmamentAddItem()
    {
        ImGuiHelper.Panel("AddItem_Firmament", () =>
        {
            ImGuiHelper.SectionHeader("Add Item");

            bool alreadyAdded = SelectedScripItem != null &&
                                configuration.FirmamentGoal.ItemsToPurchase.Any(i => i.Item.ItemId == SelectedScripItem.ItemId);

            var style = ImGui.GetStyle();
            float buttonWidth = ImGui.CalcTextSize("+").X + style.FramePadding.X * 2;
            float textWidth = alreadyAdded ? ImGui.CalcTextSize("Already in list").X + style.ItemSpacing.X : 0;
            float reservedWidth = buttonWidth + textWidth + style.ItemSpacing.X;
            float comboWidth = Math.Max(140f, ImGui.GetContentRegionAvail().X - reservedWidth);

            ImGui.PushItemWidth(comboWidth);
            if (ImGui.BeginCombo("##FirmItemCombo", SelectedScripItem?.Name ?? "Select an item..."))
            {
                ImGui.InputTextWithHint("##FirmComboFilter", "Filter...", ref comboFilter, 100);
                ImGui.Separator();
                foreach (var item in FirmamentWares()
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
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();
            ImGui.BeginDisabled(SelectedScripItem == null || alreadyAdded);
            if (ImGui.Button("+##FirmAddBtn"))
            {
                configuration.FirmamentGoal.ItemsToPurchase.Add(new ItemToPurchase
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
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("Already in list");
            }
        });
    }

    private void DrawFirmamentItemsList()
    {
        var items = configuration.FirmamentGoal.ItemsToPurchase;
        if (items.Count == 0)
        {
            ImGui.TextDisabled("No items added yet.");
            return;
        }

        ImGuiHelper.Panel("ItemsList_Firmament", () =>
        {
            ImGuiHelper.SectionHeader("Purchase List");

            var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX;
            if (!ImGui.BeginTable("##FirmItemsTable", 5, tableFlags)) return;

            ImGui.TableSetupColumn("##Remove",  ImGuiTableColumnFlags.WidthFixed,   22f);
            ImGui.TableSetupColumn("Item",      ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Progress",  ImGuiTableColumnFlags.WidthFixed,   100f);
            ImGui.TableSetupColumn("Qty",       ImGuiTableColumnFlags.WidthFixed,   52f);
            ImGui.TableSetupColumn("##Reset",   ImGuiTableColumnFlags.WidthFixed,   58f);
            ImGui.TableHeadersRow();

            for (int idx = 0; idx < items.Count; idx++)
            {
                var item = items[idx];
                bool done = item.Quantity > 0 && item.AmountPurchased >= item.Quantity;
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                if (ImGuiHelper.DangerButton($"x##FirmRemove{idx}", new Vector2(22, 22)))
                {
                    items.RemoveAt(idx);
                    configuration.Save();
                    break;
                }

                ImGui.TableSetColumnIndex(1);
                ImGui.Image(item.Item.IconTexture.GetWrapOrEmpty().Handle, new Vector2(20, 20));
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                if (done) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.90f, 0.35f, 1f));
                ImGui.TextUnformatted(item.Item.Name);
                if (done) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    var lines = new List<string>();
                    if (item.Item.ItemCost > 0) lines.Add($"Cost: {item.Item.ItemCost} scrips each");
                    var tab = FirmamentTabLabel(item.Item.ItemId);
                    if (tab != null) lines.Add($"Shop tab: {tab}");
                    if (lines.Count > 0) ImGui.SetTooltip(string.Join("\n", lines));
                }

                ImGui.TableSetColumnIndex(2);
                float progress = item.Quantity > 0
                    ? Math.Clamp((float)item.AmountPurchased / item.Quantity, 0f, 1f)
                    : 0f;
                var barW = ImGui.GetContentRegionAvail().X;
                if (barW < 1f) barW = 100f;
                var fromCol = done ? UiTheme.WithAlpha(UiTheme.Success, 0.55f) : UiTheme.WithAlpha(UiTheme.Accent, 0.55f);
                var toCol   = done ? UiTheme.Success                          : UiTheme.AccentHover;
                ImGuiHelper.GradientProgressBar(progress, new Vector2(barW, ImGui.GetFrameHeight()),
                    $"{item.AmountPurchased}/{item.Quantity}", fromCol, toCol);

                ImGui.TableSetColumnIndex(3);
                bool isUnique = item.Item.Item.IsUnique;
                int qty = item.Quantity;
                ImGui.PushItemWidth(52);
                if (isUnique)
                {
                    ImGui.BeginDisabled();
                    ImGui.InputInt($"##FirmQty{idx}", ref qty, 0, 0);
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Unique items can only be owned one at a time");
                }
                else if (ImGui.InputInt($"##FirmQty{idx}", ref qty, 0, 0))
                {
                    item.Quantity = Math.Max(0, qty);
                    configuration.Save();
                }
                ImGui.PopItemWidth();

                ImGui.TableSetColumnIndex(4);
                if (ImGui.Button($"Reset##FirmR{idx}", new Vector2(56, 0)))
                {
                    item.ResetQuantity();
                    configuration.Save();
                }
            }

            ImGui.EndTable();
        });
    }
}
