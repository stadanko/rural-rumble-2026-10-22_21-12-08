using System;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Lock-ups (COMBAT_SPEC Lock Up/Grappling/Irish Whip Rules, D-028, D-031, ARCHITECTURE 6). Plain C#,
    /// owned by <see cref="CombatMatch"/> and run once per tick right after both wrestlers think.
    ///
    /// <para><b>Resolving presses.</b> A wrestler who pressed Lock Up from Standing, Walking or Recovering
    /// (<see cref="CombatWrestler.LockUpRequested"/>) locks up the opponent if the opponent is a valid target
    /// (<see cref="CanBeLockedUp"/>: Standing, Walking, Recovering, or Striking before its hit window opens;
    /// a wrestler who pressed Lock Up this tick counts as their pre-press state, which was one of the first
    /// three) and is in reach: the lock-up grab box (<see cref="CombatTuning.lockUpReach"/> in front of the
    /// feet, <see cref="CombatTuning.strikeDepthTolerance"/> in depth) touches the opponent's hurt box, the
    /// same test as a strike. Otherwise it whiffs (D-031). If both pressed and each reaches the other, the
    /// winner is 50/50 (D-028) from the injected random source; if only one reaches, that one wins.</para>
    ///
    /// <para><b>The lock-up.</b> On success the receiver's move (a strike winding up) is cancelled, both are
    /// snapped to the pair anchor, A = Lock Up, B = Locked Up, and PairedMoveStarted(LockUp) is raised. The
    /// LockUp move's Active phase is the lock-up timer. Wrestler B has no control; Wrestler A's Strike and
    /// movement are ignored (D-031). Wrestler A's Grapple (A) and Irish Whip (B) presses reach
    /// <see cref="OnGrapplePressed"/> / <see cref="OnIrishWhipPressed"/> (the whip does nothing yet, step 3).
    /// When the timer runs out, PairedMoveStarted(BreakLockUp) is raised; when the break's timing ends both
    /// return to Standing or Recovering by health.</para>
    ///
    /// <para><b>The bodyslam (step 2).</b> Grapple during the lock-up window: A = Grappling, B = Grappled,
    /// CurrentMove = Bodyslam, both roots stay where the lock-up put them, PairedMoveStarted(Bodyslam) with
    /// <see cref="CombatTuning.BodyslamTimingFor"/> the initiator (D-033: Startup per initiator so each pair's
    /// clips play at native speed). End of Startup = impact: bodyslam damage to B (HitLanded, HealthChanged); a
    /// knockout ends the pair at once. End of Recovery: B is Knocked Down (length from health then), A returns
    /// to Standing or Recovering. Neither wrestler has any control during the bodyslam.</para>
    /// </summary>
    public sealed class LockUpController
    {
        private readonly CombatTuning tuning;
        private readonly CombatEvents events;
        private readonly ArenaGeometry arena;
        private readonly Func<float> random01;

        private CombatWrestler initiator;
        private CombatWrestler receiver;
        private MoveId move = MoveId.None;
        private MovePhase phase = MovePhase.None;
        private float phaseTime;

        /// <param name="random01">Returns a value in [0, 1); below 0.5 the player wins a simultaneous lock-up.</param>
        internal LockUpController(CombatTuning tuning, CombatEvents events, ArenaGeometry arena, Func<float> random01)
        {
            this.tuning = tuning;
            this.events = events;
            this.arena = arena;
            this.random01 = random01;
        }

        /// <summary>A paired move (lock-up, bodyslam or break) is playing.</summary>
        public bool IsActive => initiator != null;
        public WrestlerId? Initiator => initiator?.Id;
        public WrestlerId? Receiver => receiver?.Id;
        /// <summary>The paired move playing (LockUp, Bodyslam or BreakLockUp), or None.</summary>
        public MoveId CurrentMove => move;
        /// <summary>Seconds left on the lock-up timer while the LockUp move's window is open; 0 otherwise.</summary>
        public float LockUpTimeRemaining =>
            move == MoveId.LockUp && phase == MovePhase.Active ? Mathf.Max(0f, tuning.lockUpTimer - phaseTime) : 0f;

        /// <summary>
        /// COMBAT_SPEC "States where a Lock Up is allowed" for Wrestler B: Standing, Walking, Recovering, or
        /// Striking with a combo strike still winding up (before its hit window opens, so before it can land).
        /// </summary>
        public static bool CanBeLockedUp(CombatState state, MoveId currentMove, MovePhase phase)
        {
            switch (state)
            {
                case CombatState.Standing:
                case CombatState.Walking:
                case CombatState.Recovering:
                    return true;
                case CombatState.Striking:
                    return phase == MovePhase.Startup
                        && (currentMove == MoveId.Strike1 || currentMove == MoveId.Strike2 || currentMove == MoveId.Strike3);
                default:
                    return false;
            }
        }

        // ================================================================ Tick

        /// <summary>Runs after both wrestlers think. Advances an active pair, or resolves this tick's Lock Up presses.</summary>
        internal void Tick(float dt, CombatWrestler player, CombatWrestler cpu, in WrestlerIntent playerIntent, in WrestlerIntent cpuIntent)
        {
            if (IsActive)
            {
                Advance(dt, initiator == player ? playerIntent : cpuIntent);
                player.ClearLockUpRequest();
                cpu.ClearLockUpRequest();
                return;
            }
            Resolve(player, cpu);
        }

        private void Resolve(CombatWrestler player, CombatWrestler cpu)
        {
            bool playerTries = player.LockUpRequested;
            bool cpuTries = cpu.LockUpRequested;
            if (!playerTries && !cpuTries)
                return;

            bool playerReaches = playerTries && IsValidTarget(cpu) && InReach(player, cpu);
            bool cpuReaches = cpuTries && IsValidTarget(player) && InReach(cpu, player);

            CombatWrestler winner = null;
            if (playerReaches && cpuReaches)
                winner = random01() < 0.5f ? player : cpu; // D-028: 50/50
            else if (playerReaches)
                winner = player;
            else if (cpuReaches)
                winner = cpu;

            if (winner != null)
            {
                Begin(winner, winner == player ? cpu : player);
                return;
            }

            // D-031: nobody valid in range.
            if (playerTries) player.StartWhiff();
            if (cpuTries) cpu.StartWhiff();
        }

        private static bool IsValidTarget(CombatWrestler target) =>
            target.LockUpRequested || CanBeLockedUp(target.State, target.CurrentMove, target.Phase);

        /// <summary>The strike reach test with the lock-up reach: grab box in front of the feet vs. hurt box.</summary>
        private bool InReach(CombatWrestler a, CombatWrestler b)
        {
            Vector2 p = a.Position;
            float tol = tuning.strikeDepthTolerance;
            var grab = new ArenaRect(p.x, p.y - tol, p.x + a.HorizontalFacing * tuning.lockUpReach, p.y + tol);
            Vector2 q = b.Position;
            float hw = tuning.hurtHalfWidth;
            var hurt = new ArenaRect(q.x - hw, q.y, q.x + hw, q.y);
            return StrikeBoxes.Overlap(grab, hurt);
        }

        // ================================================================ The pair

        private void Begin(CombatWrestler a, CombatWrestler b)
        {
            int facing = a.HorizontalFacing;
            ComputeSnap(a.Position, b.Position, facing, a.FeetHalfExtents, out Vector2 aFeet, out Vector2 bFeet);

            initiator = a;
            receiver = b;
            move = MoveId.LockUp;
            phase = MovePhase.Startup;
            phaseTime = 0f;

            a.BeginPaired(MoveId.LockUp, CombatState.LockUp, aFeet, facing);
            b.BeginPaired(MoveId.LockUp, CombatState.LockedUp, bFeet, -facing);
            events.RaisePairedMoveStarted(new PairedMoveInfo(a.Id, b.Id, MoveId.LockUp, (aFeet + bFeet) * 0.5f,
                facing, tuning.TimingFor(MoveId.LockUp)));
        }

        /// <summary>
        /// Pair anchor (ARCHITECTURE 6): Wrestler A stays put and B is placed lockUpPairSpacing in front of A at
        /// A's depth. If that spot is outside the arena or inside the truck or pool, A is placed behind B
        /// instead (at B's depth); if that fails too, both stay where they are.
        /// </summary>
        private void ComputeSnap(Vector2 a, Vector2 b, int facing, Vector2 halfExtents, out Vector2 aFeet, out Vector2 bFeet)
        {
            float spacing = tuning.lockUpPairSpacing;
            Vector2 bSnap = new Vector2(a.x + facing * spacing, a.y);
            if (IsFree(bSnap, halfExtents))
            {
                aFeet = a;
                bFeet = bSnap;
                return;
            }
            Vector2 aSnap = new Vector2(b.x - facing * spacing, b.y);
            if (IsFree(aSnap, halfExtents))
            {
                aFeet = aSnap;
                bFeet = b;
                return;
            }
            aFeet = a;
            bFeet = b;
        }

        private bool IsFree(Vector2 feet, Vector2 halfExtents) =>
            (arena.ClampToBounds(feet, halfExtents) - feet).sqrMagnitude < 1e-8f && !arena.OverlapsObstacle(feet, halfExtents);

        private void Advance(float dt, in WrestlerIntent initiatorIntent)
        {
            // Nothing can knock a wrestler out of a paired move (strikes don't land on Locked Up, Grappling or
            // Grappled); if something ever does, drop the pair.
            ExpectedStates(move, out CombatState aState, out CombatState bState);
            if (initiator.State != aState || receiver.State != bState)
            {
                initiator.LeavePair();
                receiver.LeavePair();
                Clear();
                return;
            }

            // D-031: only Wrestler A's Grapple and Irish Whip act, for the whole lock-up window. The LockUp
            // move's Startup and Recovery are zero-length (LockUpHoldTiming), so a press on the first tick
            // after the lock-up (phase still Startup) counts too.
            if (move == MoveId.LockUp)
            {
                if (initiatorIntent.LockUpGrapple.Pressed && OnGrapplePressed(initiatorIntent))
                    return;
                if (initiatorIntent.RunWhip.Pressed && OnIrishWhipPressed(initiatorIntent))
                    return;
            }

            phaseTime += dt;
            for (int guard = 0; guard < 8 && IsActive; guard++)
            {
                MoveTiming t = CurrentTiming(); // read now: Play Mode edits apply at once
                if (phase == MovePhase.Startup)
                {
                    if (phaseTime < t.Startup) break;
                    phaseTime -= t.Startup;
                    SetPhase(MovePhase.Active);
                    if (move == MoveId.Bodyslam && Impact())
                        return; // knocked out: the pair is over
                }
                else if (phase == MovePhase.Active)
                {
                    if (phaseTime < t.Active) break;
                    phaseTime -= t.Active;
                    SetPhase(MovePhase.Recovery);
                }
                else
                {
                    if (phaseTime < t.Recovery) break;
                    phaseTime -= t.Recovery;
                    FinishCurrent();
                }
            }
        }

        private void FinishCurrent()
        {
            if (move == MoveId.LockUp)
            {
                // COMBAT_SPEC: no Grapple or Irish Whip in time -> Break Grapple animation.
                StartPaired(MoveId.BreakLockUp);
                return;
            }

            CombatWrestler a = initiator, b = receiver;
            bool bodyslam = move == MoveId.Bodyslam;
            Clear();
            a.EndPaired(); // Standing or Recovering by health
            if (bodyslam)
                b.EndPairedKnockedDown(); // COMBAT_SPEC Grapple: Wrestler B enters Knocked Down
            else
                b.EndPaired(); // break over: Standing or Recovering by health
        }

        /// <summary>Timing of the paired move playing, read now. The bodyslam's Startup depends on the initiator (D-033).</summary>
        private MoveTiming CurrentTiming() =>
            move == MoveId.Bodyslam ? tuning.BodyslamTimingFor(initiator.Definition) : tuning.TimingFor(move);

        /// <summary>The states Wrestler A and B are in during a paired move.</summary>
        private static void ExpectedStates(MoveId paired, out CombatState a, out CombatState b)
        {
            if (paired == MoveId.Bodyslam)
            {
                a = CombatState.Grappling;
                b = CombatState.Grappled;
                return;
            }
            a = CombatState.LockUp;
            b = CombatState.LockedUp;
        }

        /// <summary>
        /// Moves the pair on to the next paired move, keeping the snapped positions (the bodyslam art assumes
        /// both roots stay where the lock-up put them, D-033).
        /// </summary>
        private void StartPaired(MoveId next)
        {
            move = next;
            phase = MovePhase.Startup;
            phaseTime = 0f;
            ExpectedStates(next, out CombatState aState, out CombatState bState);
            initiator.SwitchPairedMove(next, aState);
            receiver.SwitchPairedMove(next, bState);
            Vector2 anchor = (initiator.Position + receiver.Position) * 0.5f;
            events.RaisePairedMoveStarted(new PairedMoveInfo(initiator.Id, receiver.Id, next, anchor,
                initiator.HorizontalFacing, CurrentTiming()));
        }

        /// <summary>
        /// The bodyslam's impact (end of its Startup): bodyslam damage to Wrestler B (COMBAT_SPEC Grapple). B stays
        /// Grappled, still on the clip, until the move ends and B is Knocked Down. If the damage knocks B out, B
        /// is Knocked Out at once (the match ends this tick and freezes); A is left in Grappling so its clip
        /// plays out over the frozen match. Returns true on a knockout (the pair is cleared).
        /// </summary>
        private bool Impact()
        {
            if (!receiver.ReceiveGrappleImpact(initiator.Id, MoveId.Bodyslam, receiver.Position))
                return false;
            initiator.LeavePair();
            Clear();
            return true;
        }

        private void SetPhase(MovePhase next)
        {
            phase = next;
            initiator.SetPairedPhase(next);
            receiver.SetPairedPhase(next);
        }

        private void Clear()
        {
            initiator = null;
            receiver = null;
            move = MoveId.None;
            phase = MovePhase.None;
            phaseTime = 0f;
        }

        // ================================================================ Grapple and Irish Whip

        /// <summary>
        /// Wrestler A pressed Grapple (A) during the lock-up window (COMBAT_SPEC: a Grapple only from Lock Up on a
        /// Locked Up Wrestler B). Starts the bodyslam: A = Grappling, B = Grappled, CurrentMove = Bodyslam for
        /// both, positions unchanged, PairedMoveStarted(Bodyslam) with the initiator's bodyslam timing.
        /// </summary>
        private bool OnGrapplePressed(in WrestlerIntent intent)
        {
            StartPaired(MoveId.Bodyslam);
            return true;
        }

        /// <summary>
        /// Wrestler A pressed Irish Whip (B) during the lock-up window; <paramref name="intent"/>.Move gives the
        /// whip direction. Lock-up slice step 3 starts the whip here and returns true. Step 1: ignored.
        /// </summary>
        private bool OnIrishWhipPressed(in WrestlerIntent intent) => false;
    }
}
