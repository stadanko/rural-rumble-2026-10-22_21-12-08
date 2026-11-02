using System;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.AI
{
    /// <summary>
    /// The CPU opponent (COMBAT_SPEC "CPU AI", ARCHITECTURE 4.1). A deterministic finite-state machine
    /// that only produces <see cref="WrestlerIntent"/>s; Combat applies them under the same rules as the
    /// player's input. Plain C#: reads only the two read-only views, the tuning asset and a clock.
    ///
    /// States:
    /// <list type="bullet">
    /// <item><b>Idle</b> (Idle / Evaluate): stands still; each decision picks the next state.</item>
    /// <item><b>Approach</b>: walks 8-way to a point beside the player (strike distance), or to stomp range
    /// of a Knocked Down player.</item>
    /// <item><b>Attack</b>: throws Strike 1, then presses again on later decisions to continue the combo
    /// (Strike 2, Strike 3), or stomps a Knocked Down player.</item>
    /// <item><b>LockUp</b> (Lock Up/Grapple): when it would strike a player who can be locked up, it goes for
    /// a lock-up instead at <see cref="CombatTuning.cpuLockUpChance"/>: it presses Lock Up if the player is
    /// in lock-up reach, otherwise it steps in closer (Approach with a lock-up goal, kept until it presses
    /// or the player can no longer be locked up). Holding a lock-up it started, it presses Grapple (bodyslam)
    /// at <see cref="CombatTuning.cpuBodyslamChance"/> on each decision tick (the first one interval in);
    /// otherwise the lock-up breaks on the timer. A whiff and its own bodyslam (Grappling) take no input.
    /// No Irish Whip yet (lock-up slice step 3).</item>
    /// <item><b>Stunned</b> (Stunned / Knocked Down): while Struck, Locked Up or Grappled, no input; while Knocked
    /// Down, mashes Strike at the CPU mash rate (D-025).</item>
    /// <item><b>Recover</b>: while Recovering, stands still to regenerate, with a chance to strike back
    /// at a player in range.</item>
    /// <item><b>Knockout</b>: terminal; either wrestler is Knocked Out. No input.</item>
    /// </list>
    /// Stunned, Recover and Knockout are entered as soon as the CPU's own combat state calls for them.
    /// All other choices are made on decision ticks (every <see cref="CombatTuning.cpuDecisionInterval"/>
    /// seconds of combat time), not every frame. Between decisions the chosen state is held; Approach
    /// re-aims its walk at the moving target each read.
    /// </summary>
    public sealed class OpponentBrain : IWrestlerInput
    {
        public enum BrainState
        {
            Idle,
            Approach,
            Attack,
            Stunned,
            Recover,
            Knockout,
            LockUp
        }

        private enum ApproachGoal { Strike, Stomp, LockUp }

        private readonly CombatTuning tuning;
        private readonly Func<float> clock;
        private readonly System.Random rng;

        private IWrestlerView self;
        private IWrestlerView opponent;

        private bool started;
        private float lastDecision;
        private bool forceDecision;
        private bool pendingStrike;
        private bool pendingLockUp;
        private bool holdingLockUp;
        private ApproachGoal goal;
        private int comboPresses;
        private float lastAttackPress;
        private float nextMash;

        /// <param name="clock">Combat seconds (<c>() => match.ElapsedSeconds</c> in the game).</param>
        /// <param name="seed">Seed for the configurable probabilities; the same seed and inputs give the same choices.</param>
        public OpponentBrain(IWrestlerView self, IWrestlerView opponent, CombatTuning tuning, Func<float> clock, int seed = 1)
            : this(tuning, clock, seed)
        {
            Bind(self, opponent);
        }

        /// <summary>Creates a brain whose views are bound later with <see cref="Bind"/>. Reads idle until then.</summary>
        public OpponentBrain(CombatTuning tuning, Func<float> clock, int seed = 1)
        {
            this.tuning = tuning != null ? tuning : throw new ArgumentNullException(nameof(tuning));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            rng = new System.Random(seed);
        }

        public BrainState State { get; private set; } = BrainState.Idle;
        public bool IsBound => self != null && opponent != null;

        /// <summary>Binds the CPU's own view and the player's view (once the CombatMatch exists).</summary>
        public void Bind(IWrestlerView selfView, IWrestlerView opponentView)
        {
            self = selfView;
            opponent = opponentView;
        }

        public WrestlerIntent Read()
        {
            if (!IsBound)
                return WrestlerIntent.None;

            float now = clock();
            if (!started)
            {
                started = true;
                forceDecision = true;
            }

            // ---- Forced by the CPU's own combat state (not choices).
            if (self.State == CombatState.KnockedOut || opponent.State == CombatState.KnockedOut)
            {
                State = BrainState.Knockout;
                return WrestlerIntent.None;
            }

            if (self.State == CombatState.KnockedDown)
                return Mash(now);

            if (self.State == CombatState.Struck || self.State == CombatState.LockedUp || self.State == CombatState.Grappled)
            {
                // Locked Up / Grappled: Wrestler B has no control (D-031).
                EnterStunned(now);
                return WrestlerIntent.None;
            }

            if (self.State == CombatState.LockUp || self.State == CombatState.Grappling)
            {
                // Lock Up/Grapple. A whiff or a bodyslam plays out with no input. Holding a lock-up it started,
                // the CPU rolls for a bodyslam on each decision tick, the first one a full interval in.
                State = BrainState.LockUp;
                pendingStrike = false;
                pendingLockUp = false;
                comboPresses = 0;
                return HoldLockUp(now);
            }
            holdingLockUp = false;

            if (State == BrainState.LockUp)
            {
                State = self.State == CombatState.Recovering ? BrainState.Recover : BrainState.Idle;
                forceDecision = true;
            }

            if (State == BrainState.Stunned)
            {
                // Back on its feet: Recover or re-evaluate straight away.
                State = self.State == CombatState.Recovering ? BrainState.Recover : BrainState.Idle;
                forceDecision = true;
            }

            // ---- Decisions on the interval.
            if (forceDecision || now - lastDecision >= tuning.cpuDecisionInterval)
            {
                forceDecision = false;
                lastDecision = now;
                Decide(now);
            }

            return Execute();
        }

        // ================================================================ Stunned / mash

        private void EnterStunned(float now)
        {
            if (State != BrainState.Stunned)
            {
                State = BrainState.Stunned;
                comboPresses = 0;
                pendingStrike = false;
                pendingLockUp = false;
                nextMash = float.NaN;
            }
        }

        private WrestlerIntent Mash(float now)
        {
            EnterStunned(now);
            if (tuning.cpuMashRate <= 0f)
                return WrestlerIntent.None;

            float period = 1f / tuning.cpuMashRate;
            if (float.IsNaN(nextMash))
                nextMash = now + period;
            if (now < nextMash)
                return WrestlerIntent.None;

            nextMash += period;
            if (nextMash <= now)
                nextMash = now + period; // never burst to catch up
            return StrikePress();
        }

        // ================================================================ Lock-up hold

        /// <summary>
        /// COMBAT_SPEC CPU AI Lock Up/Grapple: "if successful, performs a grapple". While the CPU holds a lock-up
        /// it started (Lock Up, partner Locked Up, lock-up window), each decision tick presses Grapple at
        /// <see cref="CombatTuning.cpuBodyslamChance"/>. The interval restarts when the hold begins, so the
        /// first roll is one decision interval after the lock-up. Otherwise (whiff, bodyslam, break) no input.
        /// </summary>
        private WrestlerIntent HoldLockUp(float now)
        {
            bool holding = self.State == CombatState.LockUp && self.CurrentMove == MoveId.LockUp
                && opponent.State == CombatState.LockedUp;
            if (!holding)
            {
                holdingLockUp = false;
                return WrestlerIntent.None;
            }
            if (!holdingLockUp)
            {
                holdingLockUp = true;
                lastDecision = now;
                return WrestlerIntent.None;
            }
            if (now - lastDecision < tuning.cpuDecisionInterval)
                return WrestlerIntent.None;
            lastDecision = now;
            return Roll(tuning.cpuBodyslamChance) ? GrapplePress() : WrestlerIntent.None;
        }

        // ================================================================ Decide

        private void Decide(float now)
        {
            Vector2 d = opponent.Position - self.Position;

            // Mid-strike: only the combo continuation is a choice; otherwise wait for the move to end.
            if (State == BrainState.Attack && comboPresses > 0)
            {
                bool comboOpen = comboPresses < 3 && now - lastAttackPress <= tuning.comboWindow
                    && CombatMatch.CanBeStruck(opponent.State);
                if (comboOpen && Roll(tuning.cpuComboContinueChance))
                {
                    Press(now);
                    return;
                }
                comboPresses = 0;
                if (self.State == CombatState.Striking)
                    return;
                State = BrainState.Idle;
            }
            if (self.State == CombatState.Striking)
                return;

            bool recovering = self.State == CombatState.Recovering;

            // Knocked Down player: stomp (a normal rule available to both wrestlers, D-025).
            if (opponent.State == CombatState.KnockedDown)
            {
                comboPresses = 0;
                if (recovering)
                {
                    State = BrainState.Recover;
                    return;
                }
                if (Roll(tuning.cpuStompChance))
                {
                    if (InStompRange(d))
                    {
                        State = BrainState.Attack;
                        pendingStrike = true; // Combat turns it into a Stomp
                        lastAttackPress = now;
                    }
                    else
                    {
                        State = BrainState.Approach;
                        goal = ApproachGoal.Stomp;
                    }
                }
                else
                {
                    State = BrainState.Idle;
                }
                return;
            }

            bool hittable = CombatMatch.CanBeStruck(opponent.State);
            bool inRange = hittable && InStrikeRange(d);

            // Committed to a lock-up: keep stepping in until in reach, then press.
            if (!recovering && State == BrainState.Approach && goal == ApproachGoal.LockUp)
            {
                if (LockUpController.CanBeLockedUp(opponent.State, opponent.CurrentMove, opponent.Phase))
                {
                    if (InLockUpRange(d) && FacingOpponent(d))
                        PressLockUp();
                    return;
                }
                State = BrainState.Idle;
            }

            if (recovering)
            {
                // COMBAT_SPEC Recover: finish recovering first, with a configurable chance to fight back.
                State = BrainState.Recover;
                if (inRange && FacingOpponent(d) && Roll(tuning.cpuRecoverCounterChance))
                {
                    if (TryLockUp(d, allowStepIn: false))
                        return;
                    State = BrainState.Attack;
                    comboPresses = 0;
                    Press(now);
                }
                return;
            }

            if (inRange)
            {
                if (!FacingOpponent(d))
                {
                    State = BrainState.Approach; // Execute turns toward the player
                    goal = ApproachGoal.Strike;
                }
                else if (TryLockUp(d, allowStepIn: true))
                {
                    // Lock Up/Grapple chosen instead of striking.
                }
                else if (Roll(tuning.cpuAttackChance))
                {
                    State = BrainState.Attack;
                    comboPresses = 0;
                    Press(now);
                }
                else
                {
                    State = BrainState.Idle;
                }
                return;
            }

            if (Roll(tuning.cpuApproachChance))
            {
                State = BrainState.Approach;
                goal = ApproachGoal.Strike;
            }
            else
            {
                State = BrainState.Idle;
            }
        }

        /// <summary>
        /// COMBAT_SPEC CPU AI Lock Up/Grapple: with the lock-up chance, go for a lock-up instead of striking,
        /// only when the player can be locked up. In lock-up reach it presses Lock Up; otherwise, with
        /// <paramref name="allowStepIn"/>, it steps in closer first (so it never whiffs on purpose).
        /// </summary>
        private bool TryLockUp(Vector2 d, bool allowStepIn)
        {
            if (!LockUpController.CanBeLockedUp(opponent.State, opponent.CurrentMove, opponent.Phase))
                return false;
            bool inReach = InLockUpRange(d);
            if (!inReach && !allowStepIn)
                return false;
            if (!Roll(tuning.cpuLockUpChance))
                return false;
            comboPresses = 0;
            if (inReach)
            {
                PressLockUp();
            }
            else
            {
                State = BrainState.Approach;
                goal = ApproachGoal.LockUp;
            }
            return true;
        }

        private void PressLockUp()
        {
            State = BrainState.LockUp;
            pendingLockUp = true;
        }

        private void Press(float now)
        {
            pendingStrike = true;
            comboPresses++;
            lastAttackPress = now;
        }

        // ================================================================ Execute

        private WrestlerIntent Execute()
        {
            if (pendingStrike)
            {
                // No stick on the press tick, so the strike goes the way the CPU already faces.
                pendingStrike = false;
                return StrikePress();
            }

            if (pendingLockUp)
            {
                pendingLockUp = false;
                return LockUpPress();
            }

            if (State != BrainState.Approach)
                return WrestlerIntent.None;

            Vector2 d = opponent.Position - self.Position;
            float side = self.Position.x >= opponent.Position.x ? 1f : -1f;
            float distance = goal == ApproachGoal.Stomp ? tuning.stompRangeX * 0.5f
                : goal == ApproachGoal.LockUp ? LockUpApproachDistance()
                : tuning.cpuStrikeDistance;
            Vector2 target = opponent.Position + new Vector2(side * distance, 0f);
            Vector2 toTarget = target - self.Position;

            float tolX = tuning.cpuArriveTolerance;
            float tolY = Mathf.Min(tuning.cpuArriveTolerance, tuning.strikeDepthTolerance * 0.5f);
            float mx = Mathf.Abs(toTarget.x) > tolX ? Mathf.Sign(toTarget.x) : 0f;
            float my = Mathf.Abs(toTarget.y) > tolY ? Mathf.Sign(toTarget.y) : 0f;

            if (mx == 0f && my == 0f && goal != ApproachGoal.Stomp && !FacingOpponent(d))
                mx = Mathf.Sign(d.x); // one step toward the player turns the CPU to face them

            return new WrestlerIntent(new Vector2(mx, my), ButtonState.None, ButtonState.None, ButtonState.None);
        }

        // ================================================================ Helpers

        private bool InStrikeRange(Vector2 d)
        {
            // The shortest combo reach, so every strike of the combo can land from here.
            float reach = Mathf.Min(tuning.strike1Reach, Mathf.Min(tuning.strike2Reach, tuning.strike3Reach)) + tuning.hurtHalfWidth;
            return Mathf.Abs(d.x) <= reach && Mathf.Abs(d.y) <= tuning.strikeDepthTolerance;
        }

        /// <summary>
        /// Where the CPU stands to lock up: midway between the closest the feet boxes allow and the far edge of
        /// lock-up reach (no tuning value of its own).
        /// </summary>
        private float LockUpApproachDistance() =>
            0.5f * (2f * tuning.feetHalfWidth + tuning.lockUpReach + tuning.hurtHalfWidth);

        /// <summary>The lock-up reach test as Combat makes it (grab box vs. hurt box), from the feet offset.</summary>
        private bool InLockUpRange(Vector2 d) =>
            Mathf.Abs(d.x) <= tuning.lockUpReach + tuning.hurtHalfWidth && Mathf.Abs(d.y) <= tuning.strikeDepthTolerance;

        private bool InStompRange(Vector2 d) =>
            Mathf.Abs(d.x) <= tuning.stompRangeX && Mathf.Abs(d.y) <= tuning.stompRangeDepth;

        private bool FacingOpponent(Vector2 d) => Mathf.Abs(d.x) < 1e-3f || Mathf.Sign(d.x) == self.HorizontalFacing;

        private bool Roll(float chance)
        {
            if (chance >= 1f) return true;
            if (chance <= 0f) return false;
            return rng.NextDouble() < chance;
        }

        private static WrestlerIntent LockUpPress() =>
            new WrestlerIntent(Vector2.zero, new ButtonState(true, true), ButtonState.None, ButtonState.None);

        /// <summary>Grapple is the Lock Up button (A) pressed during a lock-up.</summary>
        private static WrestlerIntent GrapplePress() => LockUpPress();

        private static WrestlerIntent StrikePress() =>
            new WrestlerIntent(Vector2.zero, ButtonState.None, ButtonState.None, new ButtonState(true, true));
    }
}
