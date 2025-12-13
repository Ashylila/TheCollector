using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace TheCollector.Utility;

public static class PlayerHelper
{
    public static bool CanAct
    {
        get
        {
            if (Svc.ClientState.LocalPlayer == null)
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
                || Svc.Condition[85] && !Svc.Condition[ConditionFlag.Gathering]
                || Svc.ClientState.LocalPlayer.IsDead
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
            if (Svc.ClientState.LocalPlayer == null)
                return false;
            if (Player.TerritoryIntendedUseEnum.EqualsAny(TerritoryIntendedUseEnum.City_Area,
                                                      TerritoryIntendedUseEnum.Open_World,
                                                      TerritoryIntendedUseEnum.Inn,
                                                      TerritoryIntendedUseEnum.Barracks,
                                                      TerritoryIntendedUseEnum.Gold_Saucer,
                                                      TerritoryIntendedUseEnum.Island_Sanctuary,
                                                      TerritoryIntendedUseEnum.Housing_Instances
                ))
                return false;
            return true;

    }
    }


    internal static bool IsValid => Svc.Condition.Any()
                                    && !Svc.Condition[ConditionFlag.BetweenAreas]
                                    && !Svc.Condition[ConditionFlag.BetweenAreas51]
                                    && Player.Available
                                    && Player.Interactable;

    internal static bool IsJumping => Svc.Condition.Any()
                                      && (Svc.Condition[ConditionFlag.Jumping]
                                          || Svc.Condition[ConditionFlag.Jumping61]);

    internal static unsafe bool IsAnimationLocked => ActionManager.Instance()->AnimationLock > 0;

    internal static bool IsReady => IsValid && !IsOccupied;

    internal static bool IsOccupied => GenericHelpers.IsOccupied() || Svc.Condition[ConditionFlag.Jumping61];

    internal static bool IsReadyFull => IsValid && !IsOccupiedFull;

    internal static bool IsOccupiedFull => IsOccupied || IsAnimationLocked;

    internal static unsafe bool IsCasting => Player.Character->IsCasting;

    internal static unsafe bool IsMoving => AgentMap.Instance()->IsPlayerMoving;

    internal static bool InCombat => Svc.Condition[ConditionFlag.InCombat];

    internal static uint GetGrandCompanyTerritoryType(uint grandCompany)
    {
        return grandCompany switch
        {
            1 => 128u,
            2 => 132u,
            _ => 130u
        };
    }
    internal static unsafe float GetDistanceToPlayer(IGameObject gameObject) => GetDistanceToPlayer(gameObject.Position);

    internal static unsafe float GetDistanceToPlayer(Vector3 v3) => Vector3.Distance(v3, Player.GameObject->Position);
    internal static unsafe uint GetGrandCompany()
    {
        return UIState.Instance()->PlayerState.GrandCompany;
    }

    internal static unsafe uint GetGrandCompanyRank()
    {
        return UIState.Instance()->PlayerState.GetGrandCompanyRank();
    }
    

    internal static unsafe float GetDesynthLevel(uint classJobId)
    {
        return PlayerState.Instance()->GetDesynthesisLevel(classJobId);
    }
    

    internal static unsafe short GetCurrentItemLevelFromGearSet(
        int gearsetId = -1, bool updateGearsetBeforeCheck = true)
    {
        var gearsetModule = RaptureGearsetModule.Instance();
        if (gearsetId < 0)
            gearsetId = gearsetModule->CurrentGearsetIndex;
        if (updateGearsetBeforeCheck)
            gearsetModule->UpdateGearset(gearsetId);
        return gearsetModule->GetGearset(gearsetId)->ItemLevel;
    }
    

    internal static bool HasStatus(uint statusID)
    {
        return Svc.ClientState.LocalPlayer != null && Player.Object.StatusList.Any(x => x.StatusId == statusID);
    }
}
