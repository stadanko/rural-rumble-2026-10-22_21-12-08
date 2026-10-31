using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// One wrestler's combat state. Plain C#; ticked only by <see cref="CombatMatch"/>.
    /// Movement slice: Standing, Walking and Running (COMBAT_SPEC State Definitions, D-001, D-020).
    /// </summary>
    public sealed class CombatWrestler : IWrestlerView
    {
        private readonly CombatTuning tuning;
        private readonly CombatEvents events;

        private Vector2 position;
        private Vector2 facing;
        private int horizontalFacing;
        private int runDirection;
        private CombatState state = CombatState.Standing;
        private float health;

        public CombatWrestler(WrestlerId id, CombatTuning tuning, CombatEvents events, Vector2 startFeet, int startHorizontalFacing)
        {
            Id = id;
            this.tuning = tuning;
            this.events = events;
            position = startFeet;
            horizontalFacing = startHorizontalFacing >= 0 ? 1 : -1;
            facing = new Vector2(horizontalFacing, 0f);
            health = tuning.maxHealth;
        }

        public WrestlerId Id { get; }
        public CombatState State => state;
        public MoveId CurrentMove => MoveId.None;
        public MovePhase Phase => MovePhase.None;
        public float Health => health;
        public float MaxHealth => tuning.maxHealth;
        public Vector2 Position => position;
        public Vector2 Facing => facing;
        public int HorizontalFacing => horizontalFacing;
        /// <summary>True when facing up, up-left or up-right (STYLE_GUIDE "Walking Back Facing").</summary>
        public bool IsBackFacing => facing.y > 0.01f;

        internal Vector2 FeetHalfExtents => new Vector2(tuning.feetHalfWidth, tuning.feetHalfDepth);

        internal void PlaceAt(Vector2 feet) => position = feet;

        internal void Tick(float dt, in WrestlerIntent intent, ArenaGeometry arena)
        {
            Vector2Int dir = MoveDirection.Quantize(intent.Move, tuning.moveDeadzone);

            switch (state)
            {
                case CombatState.Standing:
                case CombatState.Walking:
                    if (intent.RunWhip.Pressed)
                    {
                        StartRun();
                        TickRun(dt, arena);
                        return;
                    }
                    TickWalk(dt, dir, arena);
                    return;

                case CombatState.Running:
                    // TODO(combat-core): "performs another action" ends the run (D-020). Strike and
                    // Lock Up from Running are not defined yet (COMBAT_SPEC lists Running only as a
                    // valid target state for strikes), so no button ends a run in this slice.
                    // Open question: Move does not steer depth while running.
                    TickRun(dt, arena);
                    return;
            }
        }

        private void TickWalk(float dt, Vector2Int dir, ArenaGeometry arena)
        {
            if (dir == Vector2Int.zero)
            {
                SetState(CombatState.Standing);
                return;
            }

            SetFacing(dir);
            SetState(CombatState.Walking);
            // Digital 8-way like classic brawlers: each axis moves at its own speed, so diagonals are
            // a little faster than straight lines.
            var delta = new Vector2(dir.x * tuning.walkSpeedHorizontal, dir.y * tuning.walkSpeedDepth) * dt;
            // Walking into a wall or obstacle keeps the wrestler Walking in place.
            position = arena.Move(position, delta, FeetHalfExtents).Position;
        }

        private void StartRun()
        {
            // D-020: run in the facing left/right direction; facing straight up/down uses the last
            // left/right facing, which HorizontalFacing already holds.
            runDirection = horizontalFacing;
            SetFacing(new Vector2Int(runDirection, 0));
            SetState(CombatState.Running);
        }

        private void TickRun(float dt, ArenaGeometry arena)
        {
            var delta = new Vector2(runDirection * tuning.runSpeed * dt, 0f);
            ArenaMoveResult result = arena.Move(position, delta, FeetHalfExtents);
            position = result.Position;
            if (!result.BlockedX)
                return;

            if (result.HitObstacleX)
            {
                // TODO(combat-core): COMBAT_SPEC Irish Whip / Tuning Values: a running wrestler that runs
                // into an arena object enters Struck and takes obstacle collision damage, and Combat
                // raises CombatEvents.ObstacleHit. Not in the movement slice: the run just stops.
                // Which edges/objects are damaging is ARCHITECTURE gap #10.
            }
            SetState(CombatState.Standing);
        }

        private void SetState(CombatState next)
        {
            if (next == state)
                return;
            CombatState prev = state;
            state = next;
            events.RaiseStateChanged(Id, prev, next);
        }

        private void SetFacing(Vector2Int dir)
        {
            if (dir.x != 0)
                horizontalFacing = dir.x;
            Vector2 next = MoveDirection.ToFacing(dir);
            if (next == facing)
                return;
            facing = next;
            events.RaiseFacingChanged(Id, facing);
        }
    }
}
