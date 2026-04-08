using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace TheCollector.Utility;

public class ImGuiHelper
{
    public static void Panel(string id, Action body)
    {
        var style = ImGui.GetStyle();
        var pad   = style.FramePadding;

        var startScreen = ImGui.GetCursorScreenPos();
        var availW      = ImGui.GetContentRegionAvail().X;

        ImGui.PushID(id);

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        ImGui.BeginGroup();
        body();
        ImGui.EndGroup();

        var endY = ImGui.GetItemRectMax().Y;

        var bgMin = new Vector2(startScreen.X - pad.X, startScreen.Y - pad.Y);
        var bgMax = new Vector2(startScreen.X + availW + pad.X, endY + pad.Y);

        dl.ChannelsSetCurrent(0);
        var bgCol  = ImGui.GetColorU32(ImGuiCol.ChildBg);
        var brdCol = ImGui.GetColorU32(ImGuiCol.Border);
        var round  = style.FrameRounding;

        dl.AddRectFilled(bgMin, bgMax, bgCol, round);
        dl.AddRect(bgMin, bgMax, brdCol, round);

        dl.ChannelsMerge();

        ImGui.PopID();

        ImGui.Dummy(new Vector2(0, style.ItemSpacing.Y));
    }

    public static void CollapsiblePanel(string id, string label, ref bool isOpen, Action body)
    {
        var style = ImGui.GetStyle();
        var pad   = style.FramePadding;

        var startScreen = ImGui.GetCursorScreenPos();
        var availW      = ImGui.GetContentRegionAvail().X;

        ImGui.PushID(id);

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        ImGui.BeginGroup();

        var arrow = isOpen ? "▼" : "►";
        if (ImGui.Selectable($"{arrow}  {label}##Header", false, ImGuiSelectableFlags.None, new Vector2(0, ImGui.GetTextLineHeight())))
            isOpen = !isOpen;

        if (isOpen)
        {
            ImGui.Separator();
            ImGui.Spacing();
            body();
        }

        ImGui.EndGroup();

        var endY = ImGui.GetItemRectMax().Y;

        var bgMin = new Vector2(startScreen.X - pad.X, startScreen.Y - pad.Y);
        var bgMax = new Vector2(startScreen.X + availW + pad.X, endY + pad.Y);

        dl.ChannelsSetCurrent(0);
        var bgCol  = ImGui.GetColorU32(ImGuiCol.ChildBg);
        var brdCol = ImGui.GetColorU32(ImGuiCol.Border);
        var round  = style.FrameRounding;

        dl.AddRectFilled(bgMin, bgMax, bgCol, round);
        dl.AddRect(bgMin, bgMax, brdCol, round);

        dl.ChannelsMerge();

        ImGui.PopID();

        ImGui.Dummy(new Vector2(0, style.ItemSpacing.Y));
    }
}
