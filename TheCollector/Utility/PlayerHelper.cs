using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.GameHelpers;

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
                || Svc.Condition[ConditionFlag.Gathering42]
                //Node is open? Fades off shortly after closing the node, can't use items (but can mount) while it's set
                || Svc.Condition[85] && !Svc.Condition[ConditionFlag.Gathering]
                || Svc.ClientState.LocalPlayer.IsDead
                || Player.IsAnimationLocked)
                return false;

            return true;
        }
    }
}