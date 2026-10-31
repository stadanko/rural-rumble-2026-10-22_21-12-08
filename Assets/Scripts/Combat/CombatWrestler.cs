using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// One wrestler's combat state. Plain C#; ticked only by <see cref="CombatMatch"/>, which runs the
    /// same rules for both wrestlers. Combat-core slice states: Standing, Walking, Running, Striking,
    /// Struck, Knocked Down, Recovering, Knocked Out (COMBAT_SPEC State Definitions; lock-up states come
    /// with the lock-up slice).
    ///
    /// A tick has three steps, each run for both wrestlers before the next (see CombatMatch.Tick):
    /// <list type="number">
    /// <item><see cref="Think"/>: timers, button presses and the movement this wrestler wants.</item>
    /// <item>Movement (CombatMatch): arena, wrestler blocking, obstacle hits, run collisions.</item>
    /// <item>Hits (CombatMatch): strikes whose hit window was open this tick are checked and applied.</item>
    /// </list>
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

        // Current move.
        private MoveId move = MoveId.None;
        private MovePhase phase = MovePhase.None;
        private float phaseTime;
        private bool moveHasHit;

        // Combo (COMBAT_SPEC Striking/Combo Rules). comboStep = number of the last combo strike thrown (0 = none).
        private int comboStep;
        private float sinceComboStrike = float.MaxValue;
        private bool bufferedStrike;

        private float stompCooldownLeft;
        private float struckLeft;
        private float knockdownElapsed;
        private float knockdownLeft;

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

        // ---------------------------------------------------------------- IWrestlerView
        public WrestlerId Id { get; }
        public CombatState State => state;
        public MoveId CurrentMove => move;
        public MovePhase Phase => phase;
        public float Health => health;
        public float MaxHealth => tuning.maxHealth;
        public Vector2 Position => position;
        public Vector2 Facing => facing;
        public int HorizontalFacing => horizontalFacing;

        // ---------------------------------------------------------------- Extra read-only state (tests, debugging)
        /// <summary>True when facing up, up-left or up-right (STYLE_GUIDE "Walking Back Facing").</summary>
        public bool IsBackFacing => facing.y > 0.01f;
        /// <summary>Seconds left in the current knockdown (after mashing); 0 when not Knocked Down.</summary>
        public float KnockdownTimeRemaining => state == CombatState.KnockedDown ? knockdownLeft : 0f;
        /// <summary>Seconds left in Struck; 0 when not Struck.</summary>
        public float StruckTimeRemaining => state == CombatState.Struck ? struckLeft : 0f;
        /// <summary>Seconds until this wrestler may stomp again (D-025).</summary>
        public float StompCooldownRemaining => stompCooldownLeft;
        /// <summary>Number of the last combo strike thrown (0 none, 1-3).</summary>
        public int ComboStep => comboStep;

        // ---------------------------------------------------------------- Per-tick data read by CombatMatch
        /// <summary>Movement this wrestler wants this tick (set by Think).</summary>
        internal Vector2 PendingDelta { get; private set; }
        /// <summary>A strike's hit window was open at some point this tick and it has not hit yet.</summary>
        internal bool HasHitCandidate { get; private set; }
        internal MoveId HitCandidateMove { get; private set; }
        /// <summary>Seconds since the candidate's hit window opened, at the end of this tick. Larger = opened earlier.</summary>
        internal float HitCandidateAge { get; private set; }

        internal Vector2 FeetHalfExtents => new Vector2(tuning.feetHalfWidth, tuning.feetHalfDepth);
        internal bool IsRunning => state == CombatState.Running;
        internal int RunDirection => runDirection;
        internal bool IsDown => state == CombatState.KnockedDown || state == CombatState.KnockedOut;

        private CombatState BaseState => health < tuning.recoveringThreshold ? CombatState.Recovering : CombatState.Standing;

        internal void PlaceAt(Vector2 feet) => position = feet;
        internal void SetPosition(Vector2 feet) => position = feet;

        // ================================================================ Step 1: Think

        /// <summary>
        /// Advances timers and handles this tick's buttons. <paramref name="opponentState"/> and
        /// <paramref name="opponentPosition"/> are snapshots taken before either wrestler thinks, so the
        /// order the two wrestlers think in cannot matter.
        /// </summary>
        internal void Think(float dt, in WrestlerIntent intent, CombatState opponentState, Vector2 opponentPosition)
        {
            PendingDelta = Vector2.zero;
            HasHitCandidate = false;

            if (sinceComboStrike < float.MaxValue) sinceComboStrike += dt;
            stompCooldownLeft = Mathf.Max(0f, stompCooldownLeft - dt);

            Vector2Int dir = MoveDirection.Quantize(intent.Move, tuning.moveDeadzone);

            switch (state)
            {
                case CombatState.Standing:
                case CombatState.Walking:
                case CombatState.Recovering:
                    ThinkNeutral(dt, intent, dir, opponentState, opponentPosition);
                    return;

                case CombatState.Running:
                    ThinkRunning(dt, intent);
                    return;

                case CombatState.Striking:
                    // A press during a combo strike, inside the combo window, queues the next strike.
                    if (intent.Strike.Pressed && (move == MoveId.Strike1 || move == MoveId.Strike2)
                        && sinceComboStrike <= tuning.comboWindow)
                        bufferedStrike = true;
                    AdvanceMove(dt);
                    return;

                case CombatState.Struck:
                    struckLeft -= dt;
                    if (struckLeft <= 0f)
                    {
                        struckLeft = 0f;
                        SetState(BaseState);
                    }
                    return;

                case CombatState.KnockedDown:
                    ThinkKnockedDown(dt, intent);
                    return;

                // KnockedOut: nothing. Lock-up states: lock-up slice.
            }
        }

        private void ThinkNeutral(float dt, in WrestlerIntent intent, Vector2Int dir, CombatState opponentState, Vector2 opponentPosition)
        {
            bool recovering = state == CombatState.Recovering;

            if (intent.Strike.Pressed)
            {
                if (opponentState == CombatState.KnockedDown && IsStandingOver(opponentPosition))
                {
                    // D-025: Strike over a Knocked Down opponent is a Stomp. During the cooldown the press
                    // does nothing (it never turns into a combo strike while standing over them).
                    if (stompCooldownLeft <= 0f)
                    {
                        StartMove(MoveId.Stomp);
                        return;
                    }
                }
                else
                {
                    StartComboStrike();
                    return;
                }
            }

            if (intent.RunWhip.Pressed && !recovering)
            {
                StartRun();
                PendingDelta = RunDelta(dt);
                return;
            }

            if (dir == Vector2Int.zero)
            {
                if (recovering)
                    Regenerate(dt);
                else
                    SetState(CombatState.Standing);
                return;
            }

            SetFacing(dir);
            if (!recovering)
                SetState(CombatState.Walking);
            PendingDelta = WalkVelocity(dir, recovering) * dt;
        }

        private void ThinkRunning(float dt, in WrestlerIntent intent)
        {
            if (intent.RunWhip.Pressed)
            {
                // D-027: Run again cancels your own run (whip runs come with the lock-up slice).
                SetState(BaseState);
                return;
            }
            if (intent.Strike.Pressed)
            {
                // D-029: Strike ends the run and throws Strike 1 on the spot. Lock Up does nothing.
                comboStep = 0;
                StartComboStrike();
                return;
            }
            PendingDelta = RunDelta(dt);
        }

        private void ThinkKnockedDown(float dt, in WrestlerIntent intent)
        {
            knockdownElapsed += dt;
            knockdownLeft -= dt;

            // COMBAT_SPEC: each Strike or Grapple press removes a share of the remaining time, but the
            // whole knockdown can't get shorter than the shortest possible knockdown.
            int presses = (intent.Strike.Pressed ? 1 : 0) + (intent.LockUpGrapple.Pressed ? 1 : 0);
            for (int i = 0; i < presses && knockdownLeft > 0f; i++)
            {
                float floor = Mathf.Max(0f, tuning.shortestKnockdown - knockdownElapsed);
                knockdownLeft = Mathf.Max(floor, knockdownLeft * (1f - tuning.mashReductionPerPress));
            }

            if (knockdownLeft <= 0f)
            {
                knockdownLeft = 0f;
                SetState(BaseState);
            }
        }

        private void Regenerate(float dt)
        {
            // COMBAT_SPEC Damage / D-006: only while standing still, only up to the recovery cap.
            float exitLevel = Mathf.Min(tuning.recoveryCap, tuning.recoveringThreshold);
            if (health < tuning.recoveryCap && tuning.recoveryRate > 0f)
            {
                health = Mathf.Min(tuning.recoveryCap, health + tuning.recoveryRate * dt);
                events.RaiseHealthChanged(Id, health);
            }
            if (health >= exitLevel)
                SetState(CombatState.Standing);
        }

        private bool IsStandingOver(Vector2 opponentPosition)
        {
            Vector2 d = opponentPosition - position;
            return Mathf.Abs(d.x) <= tuning.stompRangeX && Mathf.Abs(d.y) <= tuning.stompRangeDepth;
        }

        // ================================================================ Moves

        private void StartComboStrike()
        {
            int next = (comboStep == 1 || comboStep == 2) && sinceComboStrike <= tuning.comboWindow ? comboStep + 1 : 1;
            comboStep = next;
            sinceComboStrike = 0f;
            StartMove(next == 1 ? MoveId.Strike1 : next == 2 ? MoveId.Strike2 : MoveId.Strike3);
        }

        private void StartMove(MoveId next)
        {
            // Rooted while striking (D-024); PendingDelta stays zero.
            if (next == MoveId.Stomp)
                comboStep = 0;
            bufferedStrike = false;
            move = next;
            phase = MovePhase.Startup;
            phaseTime = 0f;
            moveHasHit = false;
            SetState(CombatState.Striking);
            events.RaiseMoveStarted(Id, move, tuning.TimingFor(move));
        }

        private void AdvanceMove(float dt)
        {
            phaseTime += dt;
            // Loop so a very large dt (or a zero-length phase) still passes through every phase in order.
            for (int guard = 0; guard < 8 && move != MoveId.None; guard++)
            {
                MoveTiming t = tuning.TimingFor(move);
                if (phase == MovePhase.Startup)
                {
                    if (phaseTime < t.Startup) break;
                    phaseTime -= t.Startup;
                    SetPhase(MovePhase.Active);
                }
                else if (phase == MovePhase.Active)
                {
                    MarkHitCandidate();
                    if (phaseTime < t.Active) break;
                    phaseTime -= t.Active;
                    SetPhase(MovePhase.Recovery);
                }
                else if (phase == MovePhase.Recovery)
                {
                    if (phaseTime < t.Recovery) break;
                    FinishMove();
                }
            }
        }

        private void MarkHitCandidate()
        {
            if (moveHasHit || HasHitCandidate)
                return;
            HasHitCandidate = true;
            HitCandidateMove = move;
            // phaseTime is the time spent in Active so far; for a phase passed through in one tick the
            // remaining phases' time is added on as the loop continues, but the age only needs ordering.
            HitCandidateAge = phaseTime;
        }

        private void FinishMove()
        {
            MoveId finished = move;
            move = MoveId.None;
            phase = MovePhase.None;
            phaseTime = 0f;
            events.RaiseMovePhaseChanged(Id, finished, MovePhase.None);

            if (finished == MoveId.Stomp)
                stompCooldownLeft = tuning.stompCooldown;

            if (bufferedStrike && (finished == MoveId.Strike1 || finished == MoveId.Strike2))
            {
                bufferedStrike = false;
                StartComboStrikeBuffered();
                return;
            }
            bufferedStrike = false;
            SetState(BaseState);
        }

        private void StartComboStrikeBuffered()
        {
            // The press was accepted inside the window when it was buffered; continue the combo.
            comboStep = Mathf.Clamp(comboStep + 1, 1, 3);
            sinceComboStrike = 0f;
            StartMove(comboStep == 2 ? MoveId.Strike2 : MoveId.Strike3);
        }

        private void SetPhase(MovePhase next)
        {
            phase = next;
            events.RaiseMovePhaseChanged(Id, move, next);
        }

        /// <summary>Stops the current move at once (the wrestler was hit or knocked down).</summary>
        private void InterruptMove()
        {
            bufferedStrike = false;
            comboStep = 0;
            sinceComboStrike = float.MaxValue;
            HasHitCandidate = false;
            if (move == MoveId.None)
                return;
            MoveId cut = move;
            move = MoveId.None;
            phase = MovePhase.None;
            phaseTime = 0f;
            events.RaiseMovePhaseChanged(Id, cut, MovePhase.None);
        }

        // ================================================================ Movement

        private Vector2 WalkVelocity(Vector2Int dir, bool recovering)
        {
            Vector2 v;
            if (dir.x != 0 && dir.y != 0)
            {
                // D-029: the diagonal keeps the horizontal:vertical direction of travel; its speed is the
                // separate diagonal walk speed.
                v = new Vector2(dir.x * tuning.walkSpeedHorizontal, dir.y * tuning.walkSpeedDepth);
                v = v.sqrMagnitude > 1e-12f ? v.normalized * tuning.walkSpeedDiagonal : Vector2.zero;
            }
            else
            {
                v = new Vector2(dir.x * tuning.walkSpeedHorizontal, dir.y * tuning.walkSpeedDepth);
            }
            return recovering ? v * tuning.recoveringWalkSpeedMultiplier : v;
        }

        private void StartRun()
        {
            // D-020: run in the facing left/right direction; facing straight up/down uses the last
            // left/right facing, which HorizontalFacing already holds.
            runDirection = horizontalFacing;
            SetFacing(new Vector2Int(runDirection, 0));
            SetState(CombatState.Running);
        }

        private Vector2 RunDelta(float dt) => new Vector2(runDirection * tuning.runSpeed * dt, 0f);

        /// <summary>The run reached the back wall or an arena end: it just ends (D-027).</summary>
        internal void EndRun()
        {
            if (state == CombatState.Running)
                SetState(BaseState);
        }

        // ================================================================ Being hit

        /// <summary>A strike or stomp connected. Returns the damage actually removed.</summary>
        internal float ReceiveHit(WrestlerId attacker, MoveId hitMove, Vector2 contactPoint)
        {
            InterruptMove();
            float damage = TakeDamage(tuning.DamageFor(hitMove), raise: false);
            events.RaiseHitLanded(new HitInfo(attacker, Id, hitMove, damage, contactPoint));
            events.RaiseHealthChanged(Id, health);

            if (health <= 0f)
            {
                SetState(CombatState.KnockedOut);
            }
            else if (hitMove == MoveId.Stomp)
            {
                // D-025: damage only; the knockdown timer is unchanged.
            }
            else if (hitMove == MoveId.Strike3 || health < tuning.recoveringThreshold)
            {
                // COMBAT_SPEC Knocked Down; the threshold check is after the damage (D-029).
                KnockDown();
            }
            else
            {
                // D-024: Struck, restarted by every hit (no StateChanged for a restart).
                struckLeft = tuning.struckDuration;
                SetState(CombatState.Struck);
            }
            return damage;
        }

        /// <summary>A run into the truck or pool (D-027): obstacle collision damage, then Struck, or Knocked Down below the threshold (D-030).</summary>
        internal void HitObstacle(Vector2 contactPoint)
        {
            InterruptMove();
            events.RaiseObstacleHit(Id, contactPoint);
            TakeDamage(tuning.obstacleCollisionDamage, raise: true);
            if (health <= 0f)
            {
                SetState(CombatState.KnockedOut);
                return;
            }
            if (health < tuning.recoveringThreshold)
            {
                // D-030: below the Recovering threshold after the damage, a crash knocks down like a strike.
                KnockDown();
                return;
            }
            struckLeft = tuning.struckDuration;
            SetState(CombatState.Struck);
        }

        /// <summary>A runner met this wrestler, or this wrestler ran into the opponent (D-029): both go down.</summary>
        internal void RunCollision()
        {
            InterruptMove();
            TakeDamage(tuning.runCollisionDamage, raise: true);
            if (health <= 0f)
            {
                SetState(CombatState.KnockedOut);
                return;
            }
            KnockDown();
        }

        private float TakeDamage(float amount, bool raise)
        {
            float damage = Mathf.Clamp(amount, 0f, health);
            if (damage <= 0f)
                return 0f;
            health -= damage;
            if (health < 1e-4f) health = 0f;
            if (raise)
                events.RaiseHealthChanged(Id, health);
            return damage;
        }

        private void KnockDown()
        {
            // D-025: linear in health lost, from min (full health) to max (near 0).
            float lost = Mathf.Clamp01((tuning.maxHealth - health) / Mathf.Max(1e-4f, tuning.maxHealth));
            float duration = Mathf.Lerp(tuning.minKnockdownTime, tuning.maxKnockdownTime, lost);
            knockdownElapsed = 0f;
            knockdownLeft = duration;
            SetState(CombatState.KnockedDown);
            events.RaiseKnockedDown(Id, duration);
        }

        /// <summary>The hit candidate connected; the move can't hit again.</summary>
        internal void ConsumeHitCandidate()
        {
            HasHitCandidate = false;
            if (move == HitCandidateMove)
                moveHasHit = true;
        }

        internal void ClearHitCandidate() => HasHitCandidate = false;

        // ================================================================ State helpers

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
