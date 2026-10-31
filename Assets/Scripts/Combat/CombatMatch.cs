using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// One match's combat rules (ARCHITECTURE 4.2, 4.5). Plain C#: no Time, Input or scene access.
    /// Both wrestlers run the same rules; only their input source differs.
    ///
    /// Tick order. Every step runs for both wrestlers before the next step starts, and no step lets
    /// the first-processed wrestler see the other's result from the same step, so neither the player
    /// nor the CPU is favored by processing order:
    /// <list type="number">
    /// <item>Think: timers and buttons. Each wrestler sees a snapshot of the opponent's state and
    /// position taken before either thinks.</item>
    /// <item>Movement: each wrestler moves through the arena on its own, then <see cref="WrestlerBlocking"/>
    /// resolves the two feet boxes symmetrically. Obstacle hits, run ends and run collisions follow.</item>
    /// <item>Hits: each strike whose hit window was open this tick is checked against the other's hurt
    /// box. The one whose window opened earlier lands; the other is cancelled. An exact tie is a trade
    /// (both land).</item>
    /// <item>Knockout: if anyone is Knocked Out the match ends (MatchEnded, IsOver, Result); both on the same tick is a draw (D-030).</item>
    /// </list>
    /// </summary>
    public sealed class CombatMatch
    {
        private const float TieEpsilon = 1e-5f;

        private readonly CombatWrestler player;
        private readonly CombatWrestler cpu;

        public CombatMatch(CombatTuning tuning, ArenaGeometry arena, Vector2 playerStartFeet, Vector2 cpuStartFeet)
        {
            Tuning = tuning;
            Arena = arena ?? ArenaGeometry.Unbounded();
            Events = new CombatEvents();

            // Starting facing: each wrestler faces the other.
            int playerFacing = cpuStartFeet.x >= playerStartFeet.x ? 1 : -1;
            player = new CombatWrestler(WrestlerId.Player, tuning, Events, playerStartFeet, playerFacing);
            cpu = new CombatWrestler(WrestlerId.Cpu, tuning, Events, cpuStartFeet, -playerFacing);
            player.PlaceAt(Arena.ClampToBounds(playerStartFeet, player.FeetHalfExtents));
            cpu.PlaceAt(Arena.ClampToBounds(cpuStartFeet, cpu.FeetHalfExtents));
        }

        public CombatTuning Tuning { get; }
        public ArenaGeometry Arena { get; }
        public CombatEvents Events { get; }
        public IWrestlerView Player => player;
        public IWrestlerView Cpu => cpu;
        /// <summary>Seconds of combat simulated so far (sum of clamped dt). Stops when the match ends.</summary>
        public float ElapsedSeconds { get; private set; }
        /// <summary>True once a knockout has ended the match. Tick changes nothing afterwards.</summary>
        public bool IsOver { get; private set; }
        /// <summary>Winner, loser (or draw, D-030) and elapsed time. Valid only when <see cref="IsOver"/>.</summary>
        public MatchResult Result { get; private set; }

        public IWrestlerView Get(WrestlerId id) => id == WrestlerId.Player ? player : cpu;

        /// <summary>
        /// Advances the match. dt is clamped to [0, tuning.maxTickStep] (D-021). Each intent is the single
        /// Read() of that wrestler's input for this tick. Does nothing once the match is over.
        /// </summary>
        public void Tick(float dt, WrestlerIntent playerIntent, WrestlerIntent cpuIntent)
        {
            if (IsOver)
                return;

            dt = Mathf.Clamp(dt, 0f, Tuning.maxTickStep);
            ElapsedSeconds += dt;

            // 1. Think (opponent snapshots taken before either wrestler changes).
            CombatState playerStateBefore = player.State, cpuStateBefore = cpu.State;
            Vector2 playerPosBefore = player.Position, cpuPosBefore = cpu.Position;
            player.Think(dt, playerIntent, cpuStateBefore, cpuPosBefore);
            cpu.Think(dt, cpuIntent, playerStateBefore, playerPosBefore);

            // 2. Movement.
            ResolveMovement();

            // 3. Hits.
            ResolveHits();

            // 4. Knockout.
            CheckKnockout();
        }

        // ================================================================ Movement

        private void ResolveMovement()
        {
            Vector2 halfExtents = player.FeetHalfExtents;
            Vector2 aStart = player.Position, bStart = cpu.Position;
            ArenaMoveResult aArena = Arena.Move(aStart, player.PendingDelta, halfExtents);
            ArenaMoveResult bArena = Arena.Move(bStart, cpu.PendingDelta, halfExtents);

            // D-030: a runner passes over a downed (Knocked Down / Knocked Out) opponent. The downed wrestler
            // can't move, so skipping blocking for this tick affects only the runner. Walking still blocks.
            bool passOver = (player.IsRunning && cpu.IsDown) || (cpu.IsRunning && player.IsDown);
            var blockHalf = new Vector2(Tuning.feetHalfWidth, Tuning.wrestlerBlockHalfDepth);
            WrestlerBlocking.Result blocked = passOver
                ? new WrestlerBlocking.Result(aArena.Position, bArena.Position, false, false, false, false)
                : WrestlerBlocking.Resolve(aStart, aArena.Position, bStart, bArena.Position, blockHalf);

            player.SetPosition(KeepOutOfObstacles(aStart, aArena.Position, blocked.A, halfExtents));
            cpu.SetPosition(KeepOutOfObstacles(bStart, bArena.Position, blocked.B, halfExtents));

            // Run collisions (D-029): a runner whose run was stopped by the opponent's feet, running toward
            // them. Both wrestlers are knocked down once, even if both were running.
            bool playerRanIn = player.IsRunning && blocked.ABlockedX && RunsToward(player, cpu);
            bool cpuRanIn = cpu.IsRunning && blocked.BBlockedX && RunsToward(cpu, player);

            if (playerRanIn || cpuRanIn)
            {
                // Same order for both regardless of who ran in; events name each wrestler. A downed opponent
                // never gets here (passOver above), so both are on their feet.
                player.RunCollision();
                cpu.RunCollision();
                return;
            }

            HandleRunArenaStop(player, aArena, blocked.A);
            HandleRunArenaStop(cpu, bArena, blocked.B);
        }

        private static bool RunsToward(CombatWrestler runner, CombatWrestler other) =>
            runner.RunDirection > 0 ? other.Position.x > runner.Position.x : other.Position.x < runner.Position.x;

        private void HandleRunArenaStop(CombatWrestler w, in ArenaMoveResult arenaResult, Vector2 afterBlocking)
        {
            if (!w.IsRunning || !arenaResult.BlockedX)
                return;
            // If wrestler blocking cut the move shorter than the arena did, the arena was never reached.
            if (Mathf.Abs(afterBlocking.x - arenaResult.Position.x) > 1e-5f)
                return;

            if (arenaResult.HitObstacleX)
            {
                // D-027: truck or pool -> Struck + obstacle collision damage.
                Vector2 contact = new Vector2(w.Position.x + w.RunDirection * Tuning.feetHalfWidth, w.Position.y);
                w.HitObstacle(contact);
            }
            else
            {
                // D-027: back wall / arena ends just end the run.
                w.EndRun();
            }
        }

        /// <summary>
        /// Wrestler blocking can leave a feet point the arena sweep did not visit (x clipped, then the
        /// arena's depth move kept). If that point is inside an obstacle, drop the depth move; if still
        /// inside, stay put. Both fallbacks are points the arena already accepted.
        /// </summary>
        private Vector2 KeepOutOfObstacles(Vector2 start, Vector2 arenaEnd, Vector2 end, Vector2 halfExtents)
        {
            if (end == arenaEnd || !Arena.OverlapsObstacle(end, halfExtents))
                return end;
            var noDepth = new Vector2(end.x, start.y);
            if (!Arena.OverlapsObstacle(noDepth, halfExtents))
                return noDepth;
            return start;
        }

        // ================================================================ Hits

        private void ResolveHits()
        {
            bool playerHits = player.HasHitCandidate && Connects(player, cpu);
            bool cpuHits = cpu.HasHitCandidate && Connects(cpu, player);

            if (playerHits && cpuHits)
            {
                float diff = player.HitCandidateAge - cpu.HitCandidateAge;
                if (diff > TieEpsilon) cpuHits = false;        // player's hit window opened first
                else if (diff < -TieEpsilon) playerHits = false; // CPU's opened first
                // else: exact tie -> trade, both land.
            }

            // Read both moves before applying either: applying a hit cancels the defender's move.
            MoveId playerMove = player.HitCandidateMove;
            MoveId cpuMove = cpu.HitCandidateMove;
            Vector2 playerContact = ContactPoint(player, cpu);
            Vector2 cpuContact = ContactPoint(cpu, player);

            if (playerHits) player.ConsumeHitCandidate();
            if (cpuHits) cpu.ConsumeHitCandidate();
            if (playerHits) cpu.ReceiveHit(WrestlerId.Player, playerMove, playerContact);
            if (cpuHits) player.ReceiveHit(WrestlerId.Cpu, cpuMove, cpuContact);

            player.ClearHitCandidate();
            cpu.ClearHitCandidate();
        }

        /// <summary>Whether the attacker's open hit window reaches the defender this tick.</summary>
        private bool Connects(CombatWrestler attacker, CombatWrestler defender)
        {
            MoveId m = attacker.HitCandidateMove;
            if (m == MoveId.Stomp)
            {
                // D-025: only on a Knocked Down opponent the stomper is standing over.
                if (defender.State != CombatState.KnockedDown) return false;
                Vector2 d = defender.Position - attacker.Position;
                return Mathf.Abs(d.x) <= Tuning.stompRangeX && Mathf.Abs(d.y) <= Tuning.stompRangeDepth;
            }

            if (!CanBeStruck(defender.State))
                return false;
            return StrikeBoxes.Overlap(HitBox(attacker, m), HurtBox(defender));
        }

        /// <summary>COMBAT_SPEC Striking/Combo Rules: states a strike can land on (Wrestler B).</summary>
        public static bool CanBeStruck(CombatState s)
        {
            switch (s)
            {
                case CombatState.Standing:
                case CombatState.Walking:
                case CombatState.Running:
                case CombatState.Striking:
                case CombatState.Struck:
                case CombatState.LockUp:
                case CombatState.Recovering:
                    return true;
                default:
                    return false;
            }
        }

        internal ArenaRect HitBox(CombatWrestler attacker, MoveId m)
        {
            Vector2 p = attacker.Position;
            float reach = Tuning.ReachFor(m);
            float tol = Tuning.strikeDepthTolerance;
            return new ArenaRect(p.x, p.y - tol, p.x + attacker.HorizontalFacing * reach, p.y + tol);
        }

        internal ArenaRect HurtBox(CombatWrestler defender)
        {
            Vector2 p = defender.Position;
            float hw = Tuning.hurtHalfWidth;
            return new ArenaRect(p.x - hw, p.y, p.x + hw, p.y);
        }

        private Vector2 ContactPoint(CombatWrestler attacker, CombatWrestler defender)
        {
            if (!attacker.HasHitCandidate)
                return defender.Position;
            if (attacker.HitCandidateMove == MoveId.Stomp)
                return defender.Position;
            // The hurt-box edge facing the attacker, at the defender's depth.
            float x = defender.Position.x - attacker.HorizontalFacing * Tuning.hurtHalfWidth;
            float tip = attacker.Position.x + attacker.HorizontalFacing * Tuning.ReachFor(attacker.HitCandidateMove);
            x = attacker.HorizontalFacing > 0 ? Mathf.Min(Mathf.Max(x, attacker.Position.x), tip) : Mathf.Max(Mathf.Min(x, attacker.Position.x), tip);
            return new Vector2(x, defender.Position.y);
        }

        // ================================================================ Knockout

        private void CheckKnockout()
        {
            bool playerOut = player.State == CombatState.KnockedOut;
            bool cpuOut = cpu.State == CombatState.KnockedOut;
            if (!playerOut && !cpuOut)
                return;

            if (playerOut && cpuOut)
            {
                // D-030: both knocked out on the same tick is a draw.
                Result = MatchResult.Draw(ElapsedSeconds);
            }
            else
            {
                WrestlerId loser = playerOut ? WrestlerId.Player : WrestlerId.Cpu;
                WrestlerId winner = loser == WrestlerId.Player ? WrestlerId.Cpu : WrestlerId.Player;
                Result = new MatchResult(winner, loser, ElapsedSeconds);
            }

            IsOver = true;
            Events.RaiseMatchEnded(Result);
        }
    }

    /// <summary>Box overlap on the arena plane, inclusive (touching counts), for hit/hurt boxes.</summary>
    internal static class StrikeBoxes
    {
        public static bool Overlap(ArenaRect a, ArenaRect b) =>
            a.XMax >= b.XMin && a.XMin <= b.XMax && a.YMax >= b.YMin && a.YMin <= b.YMax;
    }
}
