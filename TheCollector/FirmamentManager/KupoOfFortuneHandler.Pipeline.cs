using System;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.FirmamentManager;

public partial class KupoOfFortuneHandler
{
    // Pace for ordinary interactions (interact, advance a line, close), from the per-addon UI Delay.
    private TimeSpan UiInteractDelay => TimeSpan.FromMilliseconds(_configuration.GetUiDelayMs(Key));

    // The two animated minigame beats that must not be raced: confirming "play?" opens a voucher
    // (~6s to reveal) and scratching a chest runs its animation/reward (~6s).
    private static readonly TimeSpan VoucherOpenDelay = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ChestRevealDelay = TimeSpan.FromSeconds(6);

    // How long to wait for the next prompt/line before concluding no vouchers remain.
    private static readonly TimeSpan OfferGrace = TimeSpan.FromSeconds(3);

    protected override FrameRunner.Step[] BuildSteps()
    {
        if (_clientState.TerritoryType != _catalog.TerritoryId)
        {
            Log.Debug("Not in The Firmament; skipping Kupo of Fortune.");
            return new[] { new FrameRunner.Step("SkipKupo", () => StepResult.Success(), TimeSpan.FromSeconds(1)) };
        }

        if (_catalog.LizbethDataIds.Length == 0)
            Log.Information("Kupo of Fortune: Lizbeth placement not resolved; will move to the appraiser spot and attempt to interact.");

        return new[]
        {
            new FrameRunner.Step("CanActCheck",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(30)),
            new FrameRunner.Step("MoveToLizbeth",
                MoveToLizbethTick,
                TimeSpan.FromSeconds(60),
                ResetMoveThrottle),
            DrainCardsStep(),
            FrameRunner.Delay("PostPlayBuffer", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("ClearTarget",
                () => { _targetManager.Target = null; return StepResult.Success(); },
                TimeSpan.FromSeconds(5)),
        };
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        if (ok) OnFinishedPlaying?.Invoke();
    }

    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        VNavmesh_IPCSubscriber.Path_Stop();
    }

    private StepResult MoveToLizbethTick()
    {
        Status.Set(PluginState.MovingToCollectableVendor, "to Lizbeth");
        return MoveTowardsTick(FirmamentRouting.LivePosition(_catalog.LizbethDataIds, _catalog.LizbethPosition));
    }

    // Reactive drain: interact once, then handle whatever addon the game shows next, looping until no
    // further play is offered. One throttle (nextAction) paces the flow — the two animated beats wait
    // several seconds, everything else uses the configured UI delay.
    private FrameRunner.Step DrainCardsStep()
    {
        var interacted = false;
        var cardScratched = false;
        var cardsPlayed = 0;
        var nextAction = DateTime.MinValue;
        var offerDeadline = DateTime.MinValue;
        return new FrameRunner.Step("DrainKupoCards",
            () =>
            {
                var now = DateTime.UtcNow;
                Status.Set(PluginState.ExchangingItems, "Kupo of Fortune");

                if (now < nextAction)
                    return StepResult.Continue();

                var lottery = _window.IsLotteryOpen;
                var yesNo = _window.IsYesNoOpen;
                var talk = _window.IsTalkOpen;

                // Voucher open: scratch a chest, then close it once the reveal has played out.
                if (lottery)
                {
                    if (!cardScratched)
                    {
                        var chestIndex = PickChestIndex();
                        _window.Scratch(chestIndex);
                        cardScratched = true;
                        Log.Debug($"Kupo of Fortune: scratched chest {chestIndex} ({_configuration.KupoChestPick}); waiting {ChestRevealDelay.TotalSeconds:0}s.");
                        nextAction = now + ChestRevealDelay;
                        return StepResult.Continue();
                    }
                    // No-ops until the Close button is live, so an early press just retries.
                    _window.CloseLottery();
                    nextAction = now + UiInteractDelay;
                    return StepResult.Continue();
                }

                // The scratched voucher has closed: count it (for logging only).
                if (cardScratched)
                {
                    cardScratched = false;
                    cardsPlayed++;
                    Log.Debug($"Kupo of Fortune: played card #{cardsPlayed}.");
                    offerDeadline = now + OfferGrace;
                    nextAction = now + UiInteractDelay;
                    return StepResult.Continue();
                }

                // "Play?" prompt: confirm; Yes opens a voucher.
                if (yesNo)
                {
                    _window.ConfirmYesNo();
                    Log.Debug($"Kupo of Fortune: confirmed play prompt; waiting {VoucherOpenDelay.TotalSeconds:0}s.");
                    offerDeadline = now + OfferGrace;
                    nextAction = now + VoucherOpenDelay;
                    return StepResult.Continue();
                }

                // Line of dialogue: advance it.
                if (talk)
                {
                    _window.ProgressTalk();
                    offerDeadline = now + OfferGrace;
                    nextAction = now + UiInteractDelay;
                    return StepResult.Continue();
                }

                // Nothing open: start the conversation if we haven't.
                if (!interacted)
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    TryInteractWithLizbeth();
                    interacted = true;
                    Log.Debug("Kupo of Fortune: interacted with Lizbeth.");
                    offerDeadline = now + OfferGrace;
                    nextAction = now + UiInteractDelay;
                    return StepResult.Continue();
                }

                // Between lines while the event runs: keep waiting.
                if (Svc.Condition[ConditionFlag.OccupiedInQuestEvent] || Svc.Condition[ConditionFlag.OccupiedInEvent])
                {
                    offerDeadline = now + OfferGrace;
                    return StepResult.Continue();
                }

                if (now < offerDeadline)
                    return StepResult.Continue();

                Log.Debug($"Kupo of Fortune: no more vouchers; {cardsPlayed} played, finishing.");
                return StepResult.Success();
            },
            TimeSpan.FromSeconds(300));
    }
}
