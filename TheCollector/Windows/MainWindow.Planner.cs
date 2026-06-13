using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using TheCollector.Data.ScripSystem;
using TheCollector.Utility;

namespace TheCollector.Windows;

public partial class MainWindow
{
    private string _collectableFilter = "";
    private bool _costBreakdownOpen = true;
    private bool _collectablesOpen = true;
    private bool _sessionOpen = true;
    private const int CollectablePageSize = 10;
    private readonly Dictionary<uint, int> _collectableVisibleCount = new();
    private PlanSummary? _planCache;
    private DateTime _planCacheAt = DateTime.MinValue;
    private Dictionary<uint, uint>? _recipeIdByItemId;

    private void DrawPlannerTab()
    {
        if (configuration.Goal.ItemsToPurchase.Count == 0)
        {
            ImGui.TextDisabled("Add items to your purchase list on the Main tab to see the plan.");
            return;
        }

        if (_planCache == null || (DateTime.UtcNow - _planCacheAt).TotalMilliseconds >= 500)
        {
            _planCache = _plannerService.Calculate();
            _planCacheAt = DateTime.UtcNow;
        }
        var plan = _planCache;

        DrawPlannerOverview(plan);
        DrawPlannerCostBreakdown(plan);
        DrawPlannerCollectables(plan);
        DrawPlannerSession();
    }

    private void DrawPlannerOverview(PlanSummary plan)
    {
        ImGuiHelper.Panel("PlanOverview", () =>
        {
            if (plan.IsListComplete)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.90f, 0.35f, 1f));
                ImGui.TextUnformatted("All items purchased!");
                ImGui.PopStyleColor();
                return;
            }

            foreach (var cs in plan.CurrencySummaries)
            {
                var currName = GetCurrencyName(cs.CurrencyId);
                ImGui.TextUnformatted($"{cs.TotalScripsNeeded:N0} {currName} needed");
                if (cs.BestCollectable != null)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"(~{cs.EstimatedTurnIns} turn-ins via {cs.BestCollectable.Name})");
                }
                if (cs.InventoryScripsValue > 0)
                {
                    var carried = Math.Min(cs.InventoryScripsValue, cs.TotalScripsNeeded);
                    ImGui.TextColored(UiTheme.Success,
                        $"  {carried:N0} {currName} already covered by inventory collectables");
                }
            }
        });
    }

    private static string GetCurrencyName(uint specialId)
        => CurrencyHelper.GetCurrencyName(specialId);

    private void DrawPlannerCostBreakdown(PlanSummary plan)
    {
        ImGuiHelper.CollapsiblePanel("CostBreakdown", "Scrip Cost Breakdown", ref _costBreakdownOpen, () =>
        {
            var grouped = plan.ItemBreakdowns
                .GroupBy(i => i.CurrencyId)
                .ToList();

            foreach (var group in grouped)
            {
                var currencyName = GetCurrencyName(group.Key);
                ImGuiHelper.Chip(currencyName, UiTheme.Accent);
                ImGui.Spacing();

                var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.RowBg;
                if (!ImGui.BeginTable($"##CostTable{group.Key}", 4, tableFlags))
                    continue;

                ImGui.TableSetupColumn("Item",      ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Unit Cost", ImGuiTableColumnFlags.WidthFixed,   70f);
                ImGui.TableSetupColumn("Remaining", ImGuiTableColumnFlags.WidthFixed,   70f);
                ImGui.TableSetupColumn("Total",     ImGuiTableColumnFlags.WidthFixed,   70f);
                ImGui.TableHeadersRow();

                int groupTotal = 0;
                foreach (var item in group)
                {
                    bool done = item.QuantityRemaining <= 0;
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    if (done) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.90f, 0.35f, 1f));
                    ImGui.TextUnformatted(item.Name);
                    if (done) ImGui.PopStyleColor();

                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted($"{item.UnitCost:N0}");

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextUnformatted(done ? "Done" : $"{item.QuantityRemaining}");

                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextUnformatted(done ? "-" : $"{item.TotalCost:N0}");

                    groupTotal += item.TotalCost;
                }

                ImGui.TableNextRow();
                for (int col = 0; col < 4; col++)
                {
                    ImGui.TableSetColumnIndex(col);
                    ImGui.Separator();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted("Subtotal");
                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted($"{groupTotal:N0}");

                ImGui.EndTable();
                ImGui.Spacing();
            }
        });
    }

    private void DrawPlannerCollectables(PlanSummary plan)
    {
        if (plan.IsListComplete) return;

        ImGuiHelper.CollapsiblePanel("CollectableOptions", "Collectable Turn-in Options", ref _collectablesOpen, () =>
        {
            ImGui.InputTextWithHint("##CollFilter", "Filter collectables...", ref _collectableFilter, 100);
            ImGui.Spacing();

            foreach (var cs in plan.CurrencySummaries)
            {
                if (cs.Collectables.Count == 0) continue;

                var currencyName = GetCurrencyName(cs.CurrencyId);
                ImGui.TextColored(new Vector4(0.80f, 0.70f, 0.30f, 1f), currencyName);
                ImGui.Spacing();

                var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX
                    | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable;
                bool hideFish = configuration.Goal.HideFishingCollectables;

                if (!ImGui.BeginTable($"##CollTable{cs.CurrencyId}", 5, tableFlags))
                    continue;

                ImGui.TableSetupColumn("Lv",                ImGuiTableColumnFlags.WidthFixed,   25f);
                ImGui.TableSetupColumn("Collectable",       ImGuiTableColumnFlags.WidthStretch, 2.5f);
                ImGui.TableSetupColumn("Scrips / Turn-in",  ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 1f);
                ImGui.TableSetupColumn("Turn-ins Needed",   ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("##Recipe",          ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 60f);
                ImGui.TableHeadersRow();

                bool hideUnobtainable = configuration.Goal.HideUnobtainableCollectables;
                var filtered = cs.Collectables
                    .Where(c => (!hideFish || !c.IsFish) &&
                                (!hideUnobtainable || ScripPlannerService.IsObtainable(c)) &&
                                (string.IsNullOrEmpty(_collectableFilter) ||
                                 c.Name.Contains(_collectableFilter, StringComparison.OrdinalIgnoreCase)));

                var sortSpecs = ImGui.TableGetSortSpecs();
                int sortCol = sortSpecs.Specs.ColumnIndex;
                bool ascending = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending;

                var scripsRemaining = Math.Max(0, cs.TotalScripsNeeded - cs.InventoryScripsValue);

                var sorted = (sortCol switch
                {
                    0 => ascending ? filtered.OrderBy(c => c.Level) : filtered.OrderByDescending(c => c.Level),
                    1 => ascending ? filtered.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase) : filtered.OrderByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase),
                    3 => ascending
                        ? filtered.OrderBy(c => c.HighReward > 0 ? Math.Ceiling((double)scripsRemaining / c.HighReward) : double.MaxValue)
                        : filtered.OrderByDescending(c => c.HighReward > 0 ? Math.Ceiling((double)scripsRemaining / c.HighReward) : 0),
                    _ => ascending ? filtered.OrderBy(c => c.HighReward) : filtered.OrderByDescending(c => c.HighReward),
                }).ToList();

                if (!_collectableVisibleCount.ContainsKey(cs.CurrencyId))
                    _collectableVisibleCount[cs.CurrencyId] = CollectablePageSize;

                int maxVisible = _collectableVisibleCount[cs.CurrencyId];
                int visibleCount = Math.Min(sorted.Count, maxVisible);

                for (int i = 0; i < visibleCount; i++)
                {
                    var col = sorted[i];
                    var turnIns = col.HighReward > 0
                        ? (int)Math.Ceiling((double)scripsRemaining / col.HighReward)
                        : 0;

                    bool isBest = col == cs.BestCollectable;

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    var playerLvl = PlayerEx.GetLevelForCollectableJob(col.JobId);
                    bool levelMet = col.JobId < 0 || (playerLvl > 0 && playerLvl >= col.Level);
                    if (levelMet)
                        ImGui.TextDisabled($"{col.Level}");
                    else
                    {
                        ImGui.TextColored(UiTheme.Danger, $"{col.Level}");
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(playerLvl > 0
                                ? $"Requires level {col.Level} (you are {playerLvl})."
                                : $"Requires level {col.Level} on the matching job.");
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.AlignTextToFramePadding();
                    if (isBest) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Success);
                    ImGui.TextUnformatted(col.Name);
                    if (isBest) ImGui.PopStyleColor();
                    if (isBest)
                    {
                        ImGui.SameLine();
                        ImGuiHelper.Chip("best", UiTheme.Success);
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Most efficient collectable for this currency.");
                    }
                    if (plan.InventoryByItemId.TryGetValue(col.ItemId, out var invCount) && invCount > 0)
                    {
                        ImGui.SameLine();
                        ImGuiHelper.Chip($"{invCount} in inv", UiTheme.Accent);
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"{invCount} held — will be turned in before gathering more.");
                    }

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextUnformatted($"{col.HighReward}");

                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextUnformatted($"{turnIns}");

                    ImGui.TableSetColumnIndex(4);
                    var recipeId = GetRecipeIdForItem(col.ItemId);
                    if (recipeId != 0)
                    {
                        if (ImGui.SmallButton($"Recipe##{col.ItemId}"))
                        {
                            unsafe
                            {
                                var agent = AgentRecipeNote.Instance();
                                agent->OpenRecipeByRecipeId(recipeId);
                            }
                        }
                    }
                }

                ImGui.EndTable();

                int remaining = sorted.Count - visibleCount;
                if (remaining > 0)
                {
                    if (ImGui.SmallButton($"Show more ({remaining} remaining)##{cs.CurrencyId}"))
                        _collectableVisibleCount[cs.CurrencyId] = maxVisible + CollectablePageSize;
                }
                else if (visibleCount > CollectablePageSize)
                {
                    if (ImGui.SmallButton($"Show less##{cs.CurrencyId}"))
                        _collectableVisibleCount[cs.CurrencyId] = CollectablePageSize;
                }

                ImGui.Spacing();
            }
        });
    }

    private uint GetRecipeIdForItem(uint itemId)
    {
        if (_recipeIdByItemId == null)
        {
            _recipeIdByItemId = new Dictionary<uint, uint>();
            var sheet = Svc.Data.GetExcelSheet<Recipe>();
            if (sheet != null)
                foreach (var r in sheet)
                    if (r.ItemResult.RowId != 0)
                        _recipeIdByItemId.TryAdd(r.ItemResult.RowId, r.RowId);
        }
        return _recipeIdByItemId.TryGetValue(itemId, out var id) ? id : 0u;
    }

    private void DrawPlannerSession()
    {
        if (_automationHandler.SessionStarted.HasValue)
        {
            var elapsed = DateTime.UtcNow - _automationHandler.SessionStarted.Value;

            ImGuiHelper.CollapsiblePanel("SessionStats", "Session", ref _sessionOpen, () =>
            {
                ImGui.TextUnformatted($"Turn-ins:    {_automationHandler.SessionCollectablesTurnedIn}");
                ImGui.TextUnformatted($"Buy cycles:  {_automationHandler.SessionItemsPurchased}");
                ImGui.TextUnformatted($"Scrips earned: {_automationHandler.SessionScripsEarnedTotal:N0}");

                if (_automationHandler.SessionScripsEarned.Count > 0)
                {
                    foreach (var (currencyId, amount) in _automationHandler.SessionScripsEarned)
                        ImGui.TextUnformatted($"  {GetCurrencyName(currencyId)} earned: {amount:N0}");
                }

                if (_automationHandler.SessionScripsSpent.Count > 0)
                {
                    foreach (var (currencyId, amount) in _automationHandler.SessionScripsSpent)
                        ImGui.TextUnformatted($"  {GetCurrencyName(currencyId)} spent: {amount:N0}");
                }

                ImGui.TextUnformatted($"Elapsed:     {elapsed.Hours}h {elapsed.Minutes}m");
            });
        }

        if (configuration.TotalScripsSpent.Count > 0)
        {
            ImGuiHelper.Panel("AllTimeScrips", () =>
            {
                ImGuiHelper.SectionHeader("All-Time Scrips Spent");
                foreach (var (currencyId, amount) in configuration.TotalScripsSpent)
                    ImGui.TextUnformatted($"{GetCurrencyName(currencyId)}: {amount:N0}");
            });
        }
    }
}
