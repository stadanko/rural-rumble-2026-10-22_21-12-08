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
    /// <see cref="OnGrapplePressed"/> / <see cref="OnIrishWhipPressed"/>.
    /// When the timer runs out, PairedMoveStarted(BreakLockUp) is raised; when the break's timing ends both
    /// return to Standing or Recovering by health.</para>
    ///
    /// <para><b>The bodyslam (step 2).</b> Grapple during the lock-up window: A = Grappling, B = Grappled,
    /// CurrentMove = Bodyslam, both roots stay where the lock-up put them, PairedMoveStarted(Bodyslam) with
    /// <see cref="CombatTuning.BodyslamTimingFor"/> the initiator (D-033: Startup per initiator so each pair's
    /// clips play at native speed). End of Startup = impact: bodyslam damage to B (HitLanded, HealthChanged); a
    /// knockout ends the pair at once. End of Recovery: B is Knocked Down (length from health then), A returns
    /// to Standing or Recovering. Neither wrestler has any control during the bodyslam.</para>
    ///
    /// <para><b>The bodyslam throw (D-044).</b> When the slammer has an enabled
    /// <see cref="CombatTuning.TryGetBodyslamThrow"/> entry (both wrestlers in the project asset, D-045), the entry and the landing time (the slammer's bodyslam
    /// Startup) are fixed at the Grapple press and <c>random01</c> is drawn once for the distance. PairedMoveStarted(Bodyslam)
    /// carries Timing (landing, 0, 0) and ThrowDirection T = -Facing (the way B faced in the lock-up). No root moves before
    /// the release; from the release to the landing B's root moves on x, linearly, from releaseOffsetPx to the drawn
    /// distance past A (swept and clamped by the arena like the D-041 slide, no wrestler blocking; a truck/pool contact
    /// raises ObstacleHit once and adds obstacle collision damage at the landing). Landing: phases Active, Recovery for
    /// both, one HitLanded(Bodyslam), B Knocked Down (or Out), A free facing T, all on the same tick. A slam with no
    /// enabled entry is the step 2 bodyslam above, unchanged (no random draw).</para>
    ///
    /// <para><b>The Irish Whip (step 3, D-028, D-039).</b> Irish Whip (B) during the lock-up window. Direction W: the
    /// sign of the held Move x outside the deadzone, else the way the receiver faces (toward the whipper, so the
    /// runner passes the whipper: "through"). A = Grappling, CurrentMove = IrishWhip, rooted, facing locked; B =
    /// Running (a whip-origin run, no control for <see cref="CombatTuning.whipNoControlTime"/>), facing W,
    /// CurrentMove = IrishWhip while on the whip path. PairedMoveStarted(IrishWhip) carries W, through/away and
    /// the whipper's choreography as a MoveTiming (<see cref="CombatTuning.IrishWhipFor"/>). For the whole whip
    /// this controller places B's root at whipper x + W x the keyed offset (f1, f2, f3 held for their frame, then
    /// the f4 offset + runSpeed from the release), at B's depth, swept through the arena like a run: a forward key
    /// stopped by an arena end ends the run there (D-027); one stopped by the truck or pool is an Irish Whip
    /// impact. Either releases B from the path early. No wrestler blocking or run collision between the two while
    /// the whip plays (CombatMatch). When f4 ends: A faces W and returns to Standing or Recovering; B leaves the
    /// path and keeps running under the normal run rules.</para>
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

        // Irish Whip (D-039).
        private int whipDirection;
        private bool whipThrough;
        private float whipTime;
        private bool runnerOnPath;

        // Bodyslam throw (D-044). Fixed at the Grapple press for the slam playing.
        private const float PixelsPerUnit = 60f;
        private bool throwing;
        private int throwDirection;
        private float throwTime;
        private float throwRelease;
        private float throwLanding;
        private float throwReleaseOffset; // units
        private float throwDistance;      // units, the drawn root-to-root landing distance
        private bool throwBlocked;
        private bool throwHitObstacle;

        /// <param name="random01">Returns a value in [0, 1); below 0.5 the player wins a simultaneous lock-up.</param>
        internal LockUpController(CombatTuning tuning, CombatEvents events, ArenaGeometry arena, Func<float> random01)
        {
            this.tuning = tuning;
            this.events = events;
            this.arena = arena;
            this.random01 = random01;
        }

        /// <summary>A paired move (lock-up, bodyslam, Irish Whip or break) is playing.</summary>
        public bool IsActive => initiator != null;
        public WrestlerId? Initiator => initiator?.Id;
        public WrestlerId? Receiver => receiver?.Id;
        /// <summary>The paired move playing (LockUp, Bodyslam, IrishWhip or BreakLockUp), or None.</summary>
        public MoveId CurrentMove => move;
        /// <summary>During an Irish Whip: the whip direction, +1 right / -1 left; 0 otherwise.</summary>
        public int WhipDirection => move == MoveId.IrishWhip ? whipDirection : 0;
        /// <summary>During an Irish Whip: whether the runner passes the whipper (D-039 "through").</summary>
        public bool WhipThrough => move == MoveId.IrishWhip && whipThrough;
        /// <summary>Seconds since the Irish Whip press while one plays; 0 otherwise.</summary>
        public float WhipElapsed => move == MoveId.IrishWhip ? whipTime : 0f;
        /// <summary>During an Irish Whip: the runner is still on the whip path (its root is placed by this controller).</summary>
        public bool RunnerOnWhipPath => move == MoveId.IrishWhip && runnerOnPath;

        /// <summary>D-044: while a throwing bodyslam plays, the throw direction (+1 right / -1 left); 0 otherwise.</summary>
        public int BodyslamThrowDirection => move == MoveId.Bodyslam && throwing ? throwDirection : 0;
        /// <summary>D-044: the drawn root-to-root landing distance of the current (or, after it, the most recent) thrown
        /// slam, world units; 0 before any throw in this match.</summary>
        public float BodyslamThrowDistance => throwDistance;
        /// <summary>D-044: the current (or most recent) throw's flight was clamped short (arena end, truck or pool).</summary>
        public bool BodyslamThrowBlocked => throwBlocked;
        /// <summary>D-044: the current (or most recent) throw's flight was stopped by the truck or pool (extra damage).</summary>
        public bool BodyslamThrowHitObstacle => throwHitObstacle;

        /// <summary>
        /// True when this controller writes the wrestler's position this tick, so CombatMatch must not move it: both
        /// wrestlers during a lock-up, bodyslam or break; during an Irish Whip, the whipper (rooted) and the runner
        /// while it is on the whip path.
        /// </summary>
        internal bool HoldsPosition(CombatWrestler w)
        {
            if (!IsActive) return false;
            if (move != MoveId.IrishWhip) return w == initiator || w == receiver;
            return w == initiator || (w == receiver && runnerOnPath);
        }
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
            if (move == MoveId.IrishWhip)
            {
                AdvanceWhip(dt);
                return;
            }

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

            if (move == MoveId.Bodyslam && throwing)
            {
                AdvanceThrow(dt);
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
            // The throw's distance / blocked / obstacle flags stay readable after the slam (reset at the next throw).
            throwing = false;
            throwDirection = 0;
            throwTime = 0f;
            whipDirection = 0;
            whipThrough = false;
            whipTime = 0f;
            runnerOnPath = false;
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
            if (tuning.TryGetBodyslamThrow(initiator.Definition, out BodyslamThrow entry))
                StartThrowingSlam(entry);
            else
                StartPaired(MoveId.Bodyslam); // D-044: no throw (disabled or missing entry, no definitions): step 2 unchanged
            return true;
        }

        // ================================================================ Bodyslam throw (D-044)

        /// <summary>
        /// D-044 press tick: as the step 2 bodyslam (A Grappling, B Grappled, CurrentMove Bodyslam, no snap, LockUp None),
        /// with the throw entry and landing time fixed now and one random draw for the distance. PairedMoveStarted(Bodyslam)
        /// carries Timing (landing, 0, 0) and ThrowDirection = -Facing.
        /// </summary>
        private void StartThrowingSlam(in BodyslamThrow entry)
        {
            float landing = Mathf.Max(0f, tuning.BodyslamTimingFor(initiator.Definition).Startup);
            float variance = Mathf.Clamp01(entry.distanceVariance);
            float r = Mathf.Clamp01(random01()); // exactly one draw per thrown slam
            float distancePx = entry.landingDistancePx * (1f + variance * (2f * r - 1f));
            int facing = initiator.HorizontalFacing;

            throwing = true;
            throwDirection = facing >= 0 ? -1 : 1; // B's lock-up facing: B is thrown past A
            throwTime = 0f;
            throwLanding = landing;
            throwRelease = Mathf.Clamp(entry.releaseTime, 0f, landing);
            throwReleaseOffset = entry.releaseOffsetPx / PixelsPerUnit;
            throwDistance = distancePx / PixelsPerUnit;
            throwBlocked = false;
            throwHitObstacle = false;

            move = MoveId.Bodyslam;
            phase = MovePhase.Startup;
            phaseTime = 0f;
            initiator.SwitchPairedMove(MoveId.Bodyslam, CombatState.Grappling);
            receiver.SwitchPairedMove(MoveId.Bodyslam, CombatState.Grappled);
            Vector2 anchor = (initiator.Position + receiver.Position) * 0.5f;
            events.RaisePairedMoveStarted(new PairedMoveInfo(initiator.Id, receiver.Id, MoveId.Bodyslam, anchor, facing,
                new MoveTiming(landing, 0f, 0f), throwDirection: throwDirection));
        }

        /// <summary>
        /// One tick of a thrown slam: before the release nothing moves; from the release B's root follows the linear path
        /// (stopped for good once clamped short); on the first tick at or after the landing time, the landing.
        /// </summary>
        private void AdvanceThrow(float dt)
        {
            throwTime += dt;
            if (!throwBlocked && throwTime >= throwRelease)
                PlaceThrown();
            if (throwTime >= throwLanding)
                LandThrow();
        }

        /// <summary>
        /// Moves B's root toward A.x + T x lerp(release offset, drawn distance, fraction) at B's depth, swept and clamped
        /// through the arena like the D-041 knockback slide (no wrestler blocking, no run collision). Clamped short =
        /// blocked: B stays there for the rest of the flight; the truck or pool raises ObstacleHit once (an arena end nothing).
        /// </summary>
        private void PlaceThrown()
        {
            float span = throwLanding - throwRelease;
            float f = span > 0f ? Mathf.Clamp01((Mathf.Min(throwTime, throwLanding) - throwRelease) / span) : 1f;
            float offset = Mathf.Lerp(throwReleaseOffset, throwDistance, f);
            Vector2 from = receiver.Position;
            float dx = initiator.Position.x + throwDirection * offset - from.x;
            if (dx == 0f)
                return;
            ArenaMoveResult r = arena.Move(from, new Vector2(dx, 0f), receiver.FeetHalfExtents);
            receiver.SetPosition(r.Position);
            if (!r.BlockedX)
                return;
            throwBlocked = true;
            if (r.HitObstacleX && dx * throwDirection > 0f)
            {
                throwHitObstacle = true;
                events.RaiseObstacleHit(receiver.Id,
                    new Vector2(r.Position.x + throwDirection * tuning.feetHalfWidth, r.Position.y));
            }
        }

        /// <summary>
        /// The landing (D-044): Bodyslam Active then Recovery for both (zero length), one HitLanded(Bodyslam) with bodyslam
        /// damage (+ obstacle collision damage if the truck or pool stopped the flight), HealthChanged; B Knocked Down (or
        /// Knocked Out); A Standing or Recovering facing the throw direction, free from the next tick. On a knockout A's move
        /// still ends now, so the frozen A is standing.
        /// </summary>
        private void LandThrow()
        {
            CombatWrestler a = initiator, b = receiver;
            int t = throwDirection;
            float damage = tuning.bodyslamDamage + (throwHitObstacle ? tuning.obstacleCollisionDamage : 0f);
            SetPhase(MovePhase.Active);
            SetPhase(MovePhase.Recovery);
            Clear();
            b.LandBodyslamThrow(a.Id, damage, b.Position);
            a.EndPairedFacing(t);
        }

        /// <summary>
        /// Wrestler A pressed Irish Whip (B) during the lock-up window (COMBAT_SPEC "Wrestler A presses Irish Whip
        /// button", D-039). Direction: the left/right of the held Move outside the deadzone (up/down alone counts as
        /// none); otherwise the direction Wrestler B faces, which is toward A, so B runs past A ("through").
        /// </summary>
        private bool OnIrishWhipPressed(in WrestlerIntent intent)
        {
            int lockUpFacing = initiator.HorizontalFacing;
            int w = WhipDirectionFor(intent.Move.x, tuning.moveDeadzone, receiver.HorizontalFacing);
            CombatWrestler a = initiator, b = receiver;

            move = MoveId.IrishWhip;
            phase = MovePhase.Startup;
            phaseTime = 0f;
            whipDirection = w;
            whipThrough = w != lockUpFacing;
            whipTime = 0f;
            runnerOnPath = true;

            a.SwitchPairedMove(MoveId.IrishWhip, CombatState.Grappling); // rooted, facing locked
            b.BeginWhipRun(w);
            IrishWhipChoreography c = tuning.IrishWhipFor(a.Definition);
            PlaceRunner(c); // the f1 key, snapped on the press tick

            Vector2 anchor = (a.Position + b.Position) * 0.5f;
            events.RaisePairedMoveStarted(new PairedMoveInfo(a.Id, b.Id, MoveId.IrishWhip, anchor,
                lockUpFacing, c.Timing, w, w != lockUpFacing));
            return true;
        }

        /// <summary>D-039 whip direction: held left/right (outside the deadzone), else the receiver's facing.</summary>
        public static int WhipDirectionFor(float moveX, float deadzone, int receiverFacing)
        {
            if (Mathf.Abs(moveX) > deadzone)
                return moveX > 0f ? 1 : -1;
            return receiverFacing >= 0 ? 1 : -1;
        }

        /// <summary>
        /// One tick of the Irish Whip. Choreography and timing are read now (Play Mode edits apply at once). Phases
        /// for both (the runner only while on the path): Startup until the release (f1 + f2 + f3), then Active (zero
        /// length) and Recovery (f4), then None when the whip ends.
        /// </summary>
        private void AdvanceWhip(float dt)
        {
            if (initiator.State != CombatState.Grappling)
            {
                // Nothing can take the whipper out of the whip; if something ever does, drop the pair.
                ReleaseRunner();
                initiator.LeavePair();
                Clear();
                return;
            }
            if (runnerOnPath && (receiver.State != CombatState.Running || receiver.CurrentMove != MoveId.IrishWhip))
                ReleaseRunner(); // the runner left the path itself (only with a no-control time shorter than the whip)

            whipTime += dt;
            IrishWhipChoreography c = tuning.IrishWhipFor(initiator.Definition);
            MoveTiming t = c.Timing;

            if (runnerOnPath)
                PlaceRunner(c);

            if (phase == MovePhase.Startup && whipTime >= t.Startup)
                SetWhipPhase(MovePhase.Active);
            if (phase == MovePhase.Active && whipTime >= t.Startup + t.Active)
                SetWhipPhase(MovePhase.Recovery);
            if (phase == MovePhase.Recovery && whipTime >= t.Total)
            {
                CombatWrestler a = initiator;
                int w = whipDirection;
                ReleaseRunner(); // keeps running: a whip run under the normal run rules
                Clear();
                a.EndWhip(w); // D-039: free, facing the whip direction
            }
        }

        /// <summary>
        /// Puts the runner's root on the whip path for the current whip time: whipper x + W x the keyed offset, at
        /// the runner's depth. The move is swept through the arena like a run. A key that moves the runner forward
        /// (in W) and is stopped by an arena end ends the run there (D-027); stopped by the truck or pool, it is an
        /// Irish Whip impact (COMBAT_SPEC). Both release the runner from the path; the whipper's whip plays on. A key
        /// that moves the runner backward (the through f1 snap, a few px) is only clamped.
        /// </summary>
        private void PlaceRunner(in IrishWhipChoreography c)
        {
            float offset = c.RunnerOffset(whipThrough, Mathf.Min(whipTime, c.Timing.Total), tuning.runSpeed);
            Vector2 from = receiver.Position;
            float dx = initiator.Position.x + whipDirection * offset - from.x;
            ArenaMoveResult r = arena.Move(from, new Vector2(dx, 0f), receiver.FeetHalfExtents);
            receiver.SetPosition(r.Position);

            if (!r.BlockedX || dx * whipDirection <= 0f)
                return;
            ReleaseRunner();
            if (r.HitObstacleX)
                receiver.HitObstacle(new Vector2(r.Position.x + whipDirection * tuning.feetHalfWidth, r.Position.y));
            else
                receiver.EndRun();
        }

        private void ReleaseRunner()
        {
            if (!runnerOnPath)
                return;
            runnerOnPath = false;
            receiver.LeaveWhipPath();
        }

        private void SetWhipPhase(MovePhase next)
        {
            phase = next;
            initiator.SetPairedPhase(next);
            if (runnerOnPath)
                receiver.SetPairedPhase(next);
        }
    }
}
