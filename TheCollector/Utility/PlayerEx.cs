using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace TheCollector.Utility;

public static class PlayerEx
{
    public static bool CanAct
    {
        get
        {
            if (Svc.Objects.LocalPlayer == null)
                return false;
            if (Svc.Condition[ConditionFlag.BetweenAreas]
                || Svc.Condition[ConditionFlag.BetweenAreas51]
                || Svc.Condition[ConditionFlag.OccupiedInQuestEvent]
                || Svc.Condition[ConditionFlag.OccupiedSummoningBell]
                || Svc.Condition[ConditionFlag.BeingMoved]
                || Svc.Condition[ConditionFlag.Casting]
                || Svc.Condition[ConditionFlag.Casting87]
                || Svc.Condition[ConditionFlag.Jumping]
                || Svc.Condition[ConditionFlag.Jumping61]
                || Svc.Condition[ConditionFlag.LoggingOut]
                || Svc.Condition[ConditionFlag.Occupied]
                || Svc.Condition[ConditionFlag.Occupied39]
                || Svc.Condition[ConditionFlag.Unconscious]
                || Svc.Condition[ConditionFlag.ExecutingGatheringAction]
                || (Svc.Condition[85] && !Svc.Condition[ConditionFlag.Gathering])
                || Svc.Objects.LocalPlayer.IsDead
                || Player.IsAnimationLocked
                || Svc.Condition[ConditionFlag.Crafting]
                || Svc.Condition[ConditionFlag.ExecutingCraftingAction]
                || Svc.Condition[ConditionFlag.PreparingToCraft])
                return false;

            return true;
        }
    }

    public static bool IsInDuty
    {
        get
        {
            if (Svc.Objects.LocalPlayer == null)
                return false;
            if (Player.TerritoryIntendedUseEnum.EqualsAny(
                    TerritoryIntendedUseEnum.City_Area,
                    TerritoryIntendedUseEnum.Open_World,
                    TerritoryIntendedUseEnum.Inn,
                    TerritoryIntendedUseEnum.Barracks,
                    TerritoryIntendedUseEnum.Gold_Saucer,
                    TerritoryIntendedUseEnum.Island_Sanctuary,
                    TerritoryIntendedUseEnum.Housing_Instances,
                    TerritoryIntendedUseEnum.Residential_Area))
                return false;
            return true;
        }
    }

    public static uint GetGrandCompanyTerritoryType(uint grandCompany) => grandCompany switch
    {
        1 => 128u,
        2 => 132u,
        _ => 130u,
    };

    public static short GetLevelForCollectableJob(sbyte jobId)
    {
        if (jobId < 0) return 0;
        return GetClassJobLevel((uint)(jobId + 8));
    }

    private static unsafe short GetClassJobLevel(uint classJobRowId)
    {
        var sheet = Svc.Data.GetExcelSheet<ClassJob>();
        if (sheet == null) return 0;
        try
        {
            var row = sheet.GetRow(classJobRowId);
            var idx = row.ExpArrayIndex;
            if (idx < 0 || idx >= 32) return 0;
            return PlayerState.Instance()->ClassJobLevels[idx];
        }
        catch
        {
            return 0;
        }
    }
}
