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
    // Cap on cards drained in one run: the game holds at most 10, plus a little slack so a
    // miscount can never spin the loop forever.
    private const int MaxCardsPerRun = 12;

    // Delay between UI interactions, driven by the shared, per-addon UI Delay setting just like
    // every other automation here. Kupo previously used its own hardcoded 700ms/600ms timings and
    // ignored this setting; routing it through the per-addon delay makes the pace consistent and
    // tunable from the Settings tab.
    private TimeSpan UiInteractDelay => TimeSpan.FromMilliseconds(_configuration.GetUiDelayMs(Key));
    // Dwell after scratching so the choice registers before we close the result.
    private TimeSpan ScratchSettle => UiInteractDelay;
    // Dialogue addons stay "ready" (visible) for a frame or two before their event Lua coroutine
    // goes live and after it dies; firing a callback in either window resumes a null/dead coroutine
    // and crashes the game (Common::Lua::LuaThread.Resume). Require an addon to be continuously ready
    // this long before we touch it, re-armed after each click so the gate covers every dialogue page.
    // Kept independent of UiDelayMs so a low UI Delay can't shrink it below the safe window.
    private static readonly TimeSpan DialogueSettle = TimeSpan.FromMilliseconds(300);
    // After interacting, how long to wait for a card to be offered before concluding there
    // are no vouchers left. The timer is held off while any dialogue/event is in progress.
    private static readonly TimeSpan OfferGrace = TimeSpan.FromSeconds(3);

    // Each card is a self-contained NPC conversation: interact -> intro -> "play?" yes/no ->
    // scratch card -> reward -> post-card lines -> conversation ends. We must re-interact for
    // every card, so the drain runs as a small state machine over these phases.
    private enum KupoPhase { Interact, AwaitOffer, Play, PostCard }

    private int _cardsPlayed;

    protected override FrameRunner.Step[] BuildSteps()
    {
        _cardsPlayed = 0;

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

    // Drains every held card as a state machine over one long-running step. Each card is its
    // own conversation, so we re-interact per card; we stop when interacting no longer yields
    // a "play?" prompt (no vouchers left) or the safety cap is hit.
    private FrameRunner.Step DrainCardsStep()
    {
        var phase = KupoPhase.Interact;
        var cardScratched = false;
        var settleUntil = DateTime.MinValue;
        var nextAction = DateTime.MinValue;
        var nextClose = DateTime.MinValue;
        var offerDeadline = DateTime.MinValue;
        // When each addon became ready, or when we last clicked it (DateTime.MinValue while closed);
        // DialogueSettle measures continuous readiness from here.
        var lotteryReadySince = DateTime.MinValue;
        var yesNoReadySince = DateTime.MinValue;
        var talkReadySince = DateTime.MinValue;
        return new FrameRunner.Step("DrainKupoCards",
            () =>
            {
                var now = DateTime.UtcNow;
                Status.Set(PluginState.ExchangingItems, "Kupo of Fortune");

                var lottery = _window.IsLotteryOpen;
                var yesNo = _window.IsYesNoOpen;
                var talk = _window.IsTalkOpen;
                var occupied = Svc.Condition[ConditionFlag.OccupiedInQuestEvent] ||
                               Svc.Condition[ConditionFlag.OccupiedInEvent];

                // Track readiness onset per addon and derive the "settled" gates. The onset is also
                // re-armed after each click below, so the gate re-applies for every dialogue page.
                lotteryReadySince = lottery ? (lotteryReadySince == DateTime.MinValue ? now : lotteryReadySince) : DateTime.MinValue;
                yesNoReadySince = yesNo ? (yesNoReadySince == DateTime.MinValue ? now : yesNoReadySince) : DateTime.MinValue;
                talkReadySince = talk ? (talkReadySince == DateTime.MinValue ? now : talkReadySince) : DateTime.MinValue;
                var lotterySettled = lottery && now - lotteryReadySince >= DialogueSettle;
                var yesNoSettled = yesNo && now - yesNoReadySince >= DialogueSettle;
                var talkSettled = talk && now - talkReadySince >= DialogueSettle;

                // A lottery card open always means "go play it", regardless of phase.
                if (lottery && phase != KupoPhase.Play)
                    phase = KupoPhase.Play;

                switch (phase)
                {
                    case KupoPhase.Interact:
                        if (now >= nextAction)
                        {
                            VNavmesh_IPCSubscriber.Path_Stop();
                            TryInteractWithLizbeth();
                            nextAction = now + UiInteractDelay;
                            offerDeadline = now + OfferGrace;
                            phase = KupoPhase.AwaitOffer;
                        }
                        return StepResult.Continue();

                    case KupoPhase.AwaitOffer:
                        // Accept the "play?" prompt or advance the intro dialogue. While any
                        // dialogue/event is up, hold off the no-offer deadline. The decision to
                        // play at all was already made by the caller (loop threshold gate or the
                        // manual test button), so here we always confirm.
                        if (yesNo)
                        {
                            offerDeadline = now + OfferGrace;
                            // occupied: only fire while the game reports an active event, so we never
                            // click a torn-down addon. Re-arm the settle timer on click; see DialogueSettle.
                            if (occupied && yesNoSettled && now >= nextAction)
                            {
                                _window.ConfirmYesNo();
                                nextAction = now + UiInteractDelay;
                                yesNoReadySince = now;
                            }
                            return StepResult.Continue();
                        }
                        if (talk)
                        {
                            offerDeadline = now + OfferGrace;
                            if (occupied && talkSettled && now >= nextAction)
                            {
                                _window.ProgressTalk();
                                nextAction = now + UiInteractDelay;
                                talkReadySince = now;
                            }
                            return StepResult.Continue();
                        }
                        if (occupied) { offerDeadline = now + OfferGrace; return StepResult.Continue(); }
                        if (now < offerDeadline) return StepResult.Continue();
                        // Interacted but nothing was offered -> no vouchers left.
                        Log.Debug($"Kupo of Fortune: no more vouchers; {_cardsPlayed} played, finishing.");
                        return StepResult.Success();

                    case KupoPhase.Play:
                        if (lottery)
                        {
                            if (!cardScratched)
                            {
                                // Don't scratch on the addon's first ready frame — same
                                // open-transition hazard as the dialogue clicks.
                                if (!lotterySettled) return StepResult.Continue();
                                var chestIndex = PickChestIndex();
                                _window.Scratch(chestIndex);
                                Log.Debug($"Kupo of Fortune: scratching chest index {chestIndex} ({_configuration.KupoChestPick}).");
                                cardScratched = true;
                                settleUntil = now + ScratchSettle;
                                return StepResult.Continue();
                            }
                            if (now < settleUntil) return StepResult.Continue();
                            if (!_window.IsRevealComplete) return StepResult.Continue();
                            if (now >= nextClose)
                            {
                                _window.CloseLottery();
                                nextClose = now + UiInteractDelay;
                            }
                            return StepResult.Continue();
                        }
                        // Lottery closed -> the card is done.
                        if (cardScratched)
                        {
                            _cardsPlayed++;
                            Log.Debug($"Kupo of Fortune: played card #{_cardsPlayed}.");
                            cardScratched = false;
                        }
                        if (_cardsPlayed >= MaxCardsPerRun)
                        {
                            Log.Debug($"Kupo of Fortune: hit the {MaxCardsPerRun}-card safety cap, finishing.");
                            return StepResult.Success();
                        }
                        phase = KupoPhase.PostCard;
                        return StepResult.Continue();

                    case KupoPhase.PostCard:
                        // Advance the post-card lines; once the conversation ends, re-interact
                        // for the next card until vouchers run out.
                        if (talk || yesNo)
                        {
                            // Same occupied gate + per-click re-arm as the offer dialogue above; this
                            // post-card line, clicked as it lingered after the event ended, was the crash.
                            if (occupied && now >= nextAction)
                            {
                                if (talk && talkSettled) { _window.ProgressTalk(); nextAction = now + UiInteractDelay; talkReadySince = now; }
                                else if (yesNo && yesNoSettled) { _window.ConfirmYesNo(); nextAction = now + UiInteractDelay; yesNoReadySince = now; }
                            }
                            return StepResult.Continue();
                        }
                        if (occupied) return StepResult.Continue();
                        nextAction = DateTime.MinValue;
                        phase = KupoPhase.Interact;
                        return StepResult.Continue();

                    default:
                        return StepResult.Success();
                }
            },
            TimeSpan.FromSeconds(300));
    }
}
