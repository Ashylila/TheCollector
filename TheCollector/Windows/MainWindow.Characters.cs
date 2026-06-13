using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using ECommons.GameHelpers;
using TheCollector.Utility;

namespace TheCollector.Windows;

public partial class MainWindow
{
    private readonly CharacterBalanceTracker _balanceTracker;

    private void DrawCharactersTab()
    {
        ImGuiHelper.Panel("CharsToolbar", () =>
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Last-known scrip balances per character.");
            ImGui.SameLine();
            if (ImGui.Button("Sample current"))
                _balanceTracker.SampleNow();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Read the active character's scrip balances now.\nBalances also refresh automatically after each turn-in and buy cycle.");
        });

        var rows = _balanceTracker.KnownCharacters;
        if (rows.Count == 0)
        {
            ImGuiHelper.Panel("CharsEmpty", () =>
            {
                ImGui.TextDisabled("No characters sampled yet — log in or press \"Sample current\".");
            });
            return;
        }

        ImGuiHelper.Panel("CharsTable", () =>
        {
            const ImGuiTableFlags flags =
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.RowBg;
            if (!ImGui.BeginTable("##CharsTable", 7, flags)) return;

            // Column headers come from Lumina so they always match the
            // game's current item names (e.g. "Purple Crafters' Scrip") and
            // survive any future SE renames without a code change.
            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("World",     ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn(CurrencyHelper.GetCurrencyName(CurrencyHelper.PurpleCrafterScripItemId),  ImGuiTableColumnFlags.WidthFixed, 95f);
            ImGui.TableSetupColumn(CurrencyHelper.GetCurrencyName(CurrencyHelper.PurpleGathererScripItemId), ImGuiTableColumnFlags.WidthFixed, 95f);
            ImGui.TableSetupColumn(CurrencyHelper.GetCurrencyName(CurrencyHelper.OrangeCrafterScripItemId),  ImGuiTableColumnFlags.WidthFixed, 95f);
            ImGui.TableSetupColumn(CurrencyHelper.GetCurrencyName(CurrencyHelper.OrangeGathererScripItemId), ImGuiTableColumnFlags.WidthFixed, 95f);
            ImGui.TableSetupColumn(CurrencyHelper.GetCurrencyName(Data.Firmament.FirmamentAnchors.ScripItemId), ImGuiTableColumnFlags.WidthFixed, 95f);
            ImGui.TableHeadersRow();

            var now = DateTime.UtcNow;
            var activeCid = Player.Available ? Player.CID : 0ul;

            foreach (var c in rows)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                bool isActive = c.ContentId == activeCid;
                if (isActive) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Success);
                ImGui.TextUnformatted(string.IsNullOrEmpty(c.LastSeenName) ? $"#{c.ContentId}" : c.LastSeenName);
                if (isActive) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    var age = now - c.LastSampledAt;
                    ImGui.SetTooltip($"Last sampled {FormatAge(age)} ago.");
                }

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(c.LastSeenWorld);

                DrawBalance(c, 2, CurrencyHelper.PurpleCrafterScripItemId);
                DrawBalance(c, 3, CurrencyHelper.PurpleGathererScripItemId);
                DrawBalance(c, 4, CurrencyHelper.OrangeCrafterScripItemId);
                DrawBalance(c, 5, CurrencyHelper.OrangeGathererScripItemId);
                DrawBalance(c, 6, Data.Firmament.FirmamentAnchors.ScripItemId);
            }

            ImGui.EndTable();
        });
    }

    private static void DrawBalance(Data.Models.CharacterBalance c, int col, uint itemId)
    {
        ImGui.TableSetColumnIndex(col);
        if (c.ScripBalances.TryGetValue(itemId, out var n))
            ImGui.TextUnformatted($"{n:N0}");
        else
            ImGui.TextDisabled("-");
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalSeconds < 60) return $"{(int)age.TotalSeconds}s";
        if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}m";
        if (age.TotalHours < 24)   return $"{(int)age.TotalHours}h";
        return $"{(int)age.TotalDays}d";
    }
}
