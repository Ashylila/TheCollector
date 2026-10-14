using System;

namespace TheCollector.Data.Models;

public enum CommandHookTrigger
{
    BeforeCraftStart,
    BeforeAutogatherStart,
    AfterAutogatherFinish,
    AfterTurnIn,
    AfterBuy,
    AfterKupo,
    OnLoopIdle,
    OnHardFail,
    OnStopCondition,
}

public static class CommandHookTriggers
{
    public static readonly CommandHookTrigger[] All = (CommandHookTrigger[])Enum.GetValues(typeof(CommandHookTrigger));

    public static string Label(this CommandHookTrigger trigger) => trigger switch
    {
        CommandHookTrigger.BeforeCraftStart => "Before Artisan craft starts",
        CommandHookTrigger.BeforeAutogatherStart => "Before autogather restarts",
        CommandHookTrigger.AfterAutogatherFinish => "After autogather finishes",
        CommandHookTrigger.AfterTurnIn => "After a turn-in run",
        CommandHookTrigger.AfterBuy => "After a buy run",
        CommandHookTrigger.AfterKupo => "After Kupo of Fortune",
        CommandHookTrigger.OnLoopIdle => "When the loop goes idle",
        CommandHookTrigger.OnHardFail => "On hard fail",
        CommandHookTrigger.OnStopCondition => "On stop condition",
        _ => trigger.ToString(),
    };

    public static bool HasContinuation(this CommandHookTrigger trigger) => trigger switch
    {
        CommandHookTrigger.OnLoopIdle or CommandHookTrigger.OnHardFail or CommandHookTrigger.OnStopCondition => false,
        _ => true,
    };
}

[Serializable]
public class CommandHook
{
    public bool Enabled { get; set; } = true;
    public CommandHookTrigger Trigger { get; set; } = CommandHookTrigger.BeforeCraftStart;
    public string Command { get; set; } = string.Empty;

    public bool WaitUntilSettled { get; set; } = true;
}
