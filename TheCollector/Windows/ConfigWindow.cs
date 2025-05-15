using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using TheCollector.CollectableManager;

namespace TheCollector.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private string[] _collectableShops = new []
    {
        "Collectable Appraiser",
        "Collectable Appraiser (Sharlayan)",
        "Collectable Appraiser (Radz-at-Han)",
        "Collectable Appraiser (Old Sharlayan)",
        "Collectable Appraiser (Thavnair)",
        "Collectable Appraiser (Rhalgr's Reach)",
        "Collectable Appraiser (Mor Dhona)",
        "Collectable Appraiser (Ishgard)"
    };
    
    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("A Wonderful Configuration Window###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
    }

    public override void Draw()
    {
        DrawShopSelection();
    }

    public void DrawShopSelection()
    {
        ImGui.TextUnformatted("Select your preferred collectable shop:");
        ImGui.SameLine();
        int selectedIndex = Configuration.PreferredCollectableShop;

        if (ImGui.BeginCombo("Shop", _collectableShops[selectedIndex]))
        {
            for (int i = 0; i < _collectableShops.Length; i++)
            {
                bool isSelected = selectedIndex == i;
                if (ImGui.Selectable(_collectableShops[i], isSelected))
                {
                    Configuration.PreferredCollectableShop = i;
                    Configuration.Save();
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }
}
