using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Presentation
{
    /// <summary>
    /// Presentation for one wrestler (ARCHITECTURE 4.2, D-021). Sits on the wrestler prefab root and is
    /// bound at runtime to that wrestler's read-only <see cref="IWrestlerView"/> and, optionally, to the
    /// match's <see cref="CombatEvents"/>. Every frame it copies the view's state, move and facing into the
    /// child Animator and flips the child SpriteRenderer.
    ///
    /// Listens, never decides: it never changes combat state, and never writes the root transform
    /// (CombatMatchRunner owns the root position). Clips may offset/rotate/tint the child sprite only.
    ///
    /// Steady state comes from polling the view each frame, so a transition that goes out and back inside
    /// one combat tick (e.g. Standing -> Running -> Standing against a wall) never reaches the Animator.
    /// Events are used only for what polling cannot see: a move that restarts on the same tick the previous
    /// one ends (MoveStarted while staying Striking) and Struck restarting on every hit (HitLanded, D-024),
    /// alternating between the two hit reactions "Struck" and "StruckB" on each hit (presentation only).
    /// Event handlers only queue a restart; it is applied in <see cref="Update"/> after the state is read,
    /// and only if the view still shows that state, so an event from a tick that also changed state
    /// (Strike 3 -> Knocked Down) cannot flash the wrong clip.
    ///
    /// Lock-up slice (ARCHITECTURE 6, D-031): PairedMoveStarted marks the wrestler as paired (Animator
    /// "Paired"), which is the only way to tell a lock-up hold from a whiff (both are State LockUp, Move LockUp).
    /// Whiff and break clips are fitted to the move's total time (MoveAnimationTable contact time 0); the hold
    /// clips (no table entry) play at native speed. While paired, Wrestler A draws one sorting order in front of B.
    ///
    /// Bodyslam (ARCHITECTURE 6, D-033): Grappling + Move Bodyslam plays "Bodyslam" (A), Grappled + Move Bodyslam
    /// plays "Bodyslammed" (B). Both clips are in the MoveAnimationTable with their impact frame as contact time,
    /// so each lands its impact frame at the Bodyslam MoveTiming's Startup. A stays in front of B while paired.
    ///
    /// Clip offsets and rotations are authored for left-facing art. When the sprite is flipped,
    /// <see cref="LateUpdate"/> mirrors the child sprite's animated x offset and z rotation so a knock-back
    /// or a fall still goes away from the way the wrestler faces.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WrestlerView : MonoBehaviour
    {
        /// <summary>Animator int parameter: (int)CombatState.</summary>
        public static readonly int StateParam = Animator.StringToHash("State");
        /// <summary>Animator float parameter: 0 = front-facing, 1 = back-facing (blend trees use exact 0/1).</summary>
        public static readonly int BackParam = Animator.StringToHash("Back");
        /// <summary>Animator int parameter: (int)MoveId of the current move (None when not in a move).</summary>
        public static readonly int MoveParam = Animator.StringToHash("Move");
        /// <summary>Animator float parameter (default 1): playback speed of move states.</summary>
        public static readonly int MoveSpeedParam = Animator.StringToHash("MoveSpeed");
        /// <summary>Animator bool parameter: Recovering walk (true) or stand (false) pose. Visual only.</summary>
        public static readonly int MovingParam = Animator.StringToHash("Moving");

        /// <summary>
        /// Animator bool parameter: this wrestler is in a paired move (lock-up slice, ARCHITECTURE 6). Set from
        /// <see cref="CombatEvents.PairedMoveStarted"/>; cleared by a one-wrestler MoveStarted (a lock-up whiff,
        /// D-031) and whenever the state leaves Lock Up / Locked Up. Visual only: picks the Lock Up hold clip
        /// over the whiff clip, which share State = LockUp and Move = LockUp.
        /// </summary>
        public static readonly int PairedParam = Animator.StringToHash("Paired");

        /// <summary>
        /// Animator bool parameter: show the second hit reaction (state "StruckB") instead of "Struck". Presentation
        /// only: Combat has a single Struck state. Toggled on every hit this wrestler takes, so consecutive hits
        /// alternate the two reactions (first hit = Struck, second = StruckB, third = Struck, ...).
        /// </summary>
        public static readonly int StruckBParam = Animator.StringToHash("StruckB");

        /// <summary>Animator state names used for restarts (match WrestlerAnimationBuilder).</summary>
        public const string StruckStateName = "Struck";
        public const string StruckBStateName = "StruckB";
        public const string KnockedOutStateName = "KnockedOut";
        /// <summary>Lock-up slice states. Each plays the slot clip "Slot_" + state name.</summary>
        public const string LockUpStateName = "LockUp";                    // A, paired, holding
        public const string LockUpWhiffStateName = "LockUpWhiff";          // A, no partner (D-031)
        public const string LockedUpStateName = "LockedUp";                // B, held
        public const string BreakLockUpStateName = "BreakLockUp";          // A, break
        public const string BreakLockUpReceiverStateName = "BreakLockUpReceiver"; // B, break
        /// <summary>Bodyslam states (D-033). Each plays the slot clip "Slot_" + state name.</summary>
        public const string BodyslamStateName = "Bodyslam";                // A, Grappling
        public const string BodyslammedStateName = "Bodyslammed";          // B, Grappled

        /// <summary>Shared-controller slot clip for a move: "Slot_" + MoveId (e.g. Slot_Strike1, Slot_Stomp).</summary>
        public static string SlotNameFor(MoveId move) => "Slot_" + move;

        /// <summary>Shared-controller slot clip played by an Animator state: "Slot_" + state name.</summary>
        public static string SlotNameForState(string animatorState) => "Slot_" + animatorState;

        /// <summary>
        /// The Animator state that plays a timed move, or null if the state shows no move clip. Striking:
        /// the move's own state (Strike1-3, Stomp). Lock Up: hold when paired, whiff otherwise; Break for
        /// BreakLockUp. Locked Up: held, or the receiver's break. Grappling / Grappled: the bodyslam sides.
        /// </summary>
        public static string MoveStateName(CombatState state, MoveId move, bool paired)
        {
            switch (state)
            {
                case CombatState.Striking:
                    return move == MoveId.None ? null : move.ToString();
                case CombatState.LockUp:
                    if (move == MoveId.BreakLockUp) return BreakLockUpStateName;
                    if (move == MoveId.LockUp) return paired ? LockUpStateName : LockUpWhiffStateName;
                    return null;
                case CombatState.LockedUp:
                    if (move == MoveId.BreakLockUp) return BreakLockUpReceiverStateName;
                    if (move == MoveId.LockUp) return LockedUpStateName;
                    return null;
                case CombatState.Grappling:
                    return move == MoveId.Bodyslam ? BodyslamStateName : null;
                case CombatState.Grappled:
                    return move == MoveId.Bodyslam ? BodyslammedStateName : null;
                default:
                    return null;
            }
        }

        /// <summary>States whose clip follows the current move's timing (MoveSpeed is not forced to 1).</summary>
        public static bool IsTimedMoveState(CombatState state) =>
            state == CombatState.Striking || IsPairedState(state);

        /// <summary>States a paired move plays in: Lock Up / Locked Up (lock-up, break) and Grappling / Grappled (bodyslam).</summary>
        public static bool IsPairedState(CombatState state) =>
            state == CombatState.LockUp || state == CombatState.LockedUp ||
            state == CombatState.Grappling || state == CombatState.Grappled;

        /// <summary>The initiator's side of a paired move (drawn in front while paired).</summary>
        public static bool IsInitiatorState(CombatState state) =>
            state == CombatState.LockUp || state == CombatState.Grappling;

        /// <summary>Playback speed limits for move clips. Extreme values are reported by the Editor validator, not fixed here.</summary>
        public const float MinMoveSpeed = 0.1f;
        public const float MaxMoveSpeed = 10f;

        /// <summary>How long a Recovering wrestler keeps the walk pose after its position stops changing.</summary>
        public const float MovingHoldSeconds = 0.1f;

        [Tooltip("Child Animator using the shared Wrestler controller (or this wrestler's override controller).")]
        [SerializeField] private Animator animator;
        [Tooltip("Child SpriteRenderer the Animator drives. Flipped when the wrestler faces right.")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Which way this wrestler's source art faces. All Barry art faces left.")]
        [SerializeField] private bool artFacesLeft = true;
        [Tooltip("Contact time of each move clip at native speed (Assets/Animation/MoveAnimationTable.asset).")]
        [SerializeField] private MoveAnimationTable moveTable;
        [Tooltip("World units, set by WrestlerAnimationBuilder: left + right edge of the downed sprite's opaque pixels " +
                 "relative to its pivot (left-facing art). A knockdown that did not come from a bodyslam shows the downed " +
                 "sprite mirrored and shifted by this, so the body stays on the same ground with head and feet swapped.")]
        [SerializeField] private float downedMirrorShift;

        private IWrestlerView view;
        private CombatEvents events;
        private WrestlerId eventsId;

        // Queued by event handlers, applied in Apply().
        private bool pendingMoveRestart;
        private MoveId pendingMove;
        private MoveTiming pendingTiming;
        private bool pendingStruckRestart;
        // Which hit reaction the current / next hit shows (alternates per hit taken; presentation only).
        private bool struckB;
        private bool nextStruckB;

        // Lock-up slice: in a paired move (from PairedMoveStarted), and the sprite's sorting order outside it.
        private bool paired;
        private bool baseSortingOrderKnown;
        private int baseSortingOrder;

        private float moveSpeed = 1f;
        private CombatState? lastState;

        // Downed orientation (designer, 2026-11-12). The downed sprite is the last Bodyslammed frame, which lies head
        // toward the slammer. A knockdown that follows a bodyslam (entered from Grappled) shows it as is; every other
        // knockdown is a backward fall and shows it mirrored in place (feet toward the opponent).
        private bool downedFromSlam;
        private bool downedMirrored;
        private bool facingFlip;
        private Vector2 lastPosition;
        private bool hasLastPosition;
        private float movingHold;

        // Mirror bookkeeping: the values LateUpdate last wrote, to avoid mirroring a value twice if the
        // Animator did not write that frame.
        private bool mirrorWritten;
        private float lastWrittenX;
        private float lastWrittenRotZ;
        // The Animator's own (unmirrored) values behind the last write.
        private float lastAnimatorX;
        private float lastAnimatorRotZ;

        /// <summary>The bound combat view, or null before <see cref="Bind"/>.</summary>
        public IWrestlerView View => view;
        public Animator Animator => animator;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public MoveAnimationTable MoveTable => moveTable;
        /// <summary>Current move playback speed sent to the Animator (1 outside moves).</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>
        /// Binds this presentation to one wrestler's combat view. Call once after the CombatMatch is built
        /// (MatchBootstrap.Start). Applies the current state immediately so the first rendered frame is
        /// already correct. Passing null unbinds (the wrestler holds its last pose).
        /// </summary>
        public void Bind(IWrestlerView wrestlerView)
        {
            view = wrestlerView;
            hasLastPosition = false;
            lastState = null;
            Apply(0f);
        }

        /// <summary>
        /// Subscribes to the match's combat events for wrestler <paramref name="id"/> (call together with
        /// <see cref="Bind"/>, e.g. <c>view.BindEvents(match.Events, WrestlerId.Player)</c>). Needed for move
        /// restarts and MoveSpeed (MoveStarted), resetting MoveSpeed (MovePhaseChanged) and Struck restarts
        /// (HitLanded). Replaces any earlier subscription; null unsubscribes. Unsubscribes on destroy.
        /// Without it the wrestler still shows the right state, but same-state restarts are missed and
        /// moves play at native speed.
        /// </summary>
        public void BindEvents(CombatEvents combatEvents, WrestlerId id)
        {
            UnbindEvents();
            events = combatEvents;
            eventsId = id;
            if (events == null)
                return;
            events.MoveStarted += OnMoveStarted;
            events.MovePhaseChanged += OnMovePhaseChanged;
            events.HitLanded += OnHitLanded;
            events.PairedMoveStarted += OnPairedMoveStarted;
        }

        private void UnbindEvents()
        {
            if (events != null)
            {
                events.MoveStarted -= OnMoveStarted;
                events.MovePhaseChanged -= OnMovePhaseChanged;
                events.HitLanded -= OnHitLanded;
                events.PairedMoveStarted -= OnPairedMoveStarted;
            }
            events = null;
            pendingMoveRestart = false;
            pendingStruckRestart = false;
            paired = false;
        }

        private void OnDestroy() => UnbindEvents();

        private void OnMoveStarted(WrestlerId id, MoveId move, MoveTiming timing)
        {
            if (id != eventsId) return;
            // A one-wrestler move: never paired (a Lock Up MoveStarted is a whiff, D-031).
            paired = false;
            pendingMoveRestart = true;
            pendingMove = move;
            pendingTiming = timing;
        }

        // ARCHITECTURE 6: both wrestlers play their side of the paired clip from the same tick. Positions are
        // already snapped by Combat; facing comes from each wrestler's own HorizontalFacing as usual.
        private void OnPairedMoveStarted(PairedMoveInfo info)
        {
            if (info.Initiator != eventsId && info.Receiver != eventsId) return;
            paired = true;
            pendingMoveRestart = true;
            pendingMove = info.Move;
            pendingTiming = info.Timing;
        }

        private void OnMovePhaseChanged(WrestlerId id, MoveId move, MovePhase phase)
        {
            if (id != eventsId || phase != MovePhase.None) return;
            // The move ended or was cut. A MoveStarted later in the same tick re-queues a restart.
            pendingMoveRestart = false;
            SetMoveSpeed(1f);
        }

        private void OnHitLanded(HitInfo hit)
        {
            if (hit.Defender == eventsId)
                pendingStruckRestart = true;
        }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // Default execution order (0) runs after CombatMatchRunner.Update (-50), so this reads the state
        // the runner just ticked, and the Animator evaluates the new parameters in the same frame.
        private void Update()
        {
            Apply(Time.deltaTime);
        }

        private void Apply(float dt)
        {
            if (view == null)
                return;

            CombatState state = view.State;
            facingFlip = ShouldFlip(view.HorizontalFacing, artFacesLeft);
            bool downed = IsDownedState(state);
            if (downed && !(lastState.HasValue && IsDownedState(lastState.Value)))
                downedFromSlam = lastState == CombatState.Grappled; // KO while down keeps how they went down
            downedMirrored = downed && !downedFromSlam;
            bool flip = facingFlip ^ downedMirrored;

            // Recovering stand vs walk: did the combat position change since the last frame (ARCHITECTURE 4.2)?
            Vector2 position = view.Position;
            bool moved = hasLastPosition && (position - lastPosition).sqrMagnitude > 1e-8f;
            lastPosition = position;
            hasLastPosition = true;
            movingHold = moved ? MovingHoldSeconds : Mathf.Max(0f, movingHold - dt);
            bool moving = moved || movingHold > 0f;

            // Paired only while in a paired-move state; the receiver is always paired.
            if (!IsPairedState(state))
                paired = false;
            bool showPaired = paired || state == CombatState.LockedUp || state == CombatState.Grappled;

            bool animatorReady = animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled;
            if (animatorReady)
            {
                MoveId currentMove = view.CurrentMove;
                if (currentMove == MoveId.None || !IsTimedMoveState(state))
                    SetMoveSpeed(1f);

                animator.SetInteger(StateParam, (int)state);
                animator.SetInteger(MoveParam, (int)currentMove);
                animator.SetFloat(BackParam, IsBackFacing(view.Facing) ? 1f : 0f);
                animator.SetBool(MovingParam, moving);
                animator.SetBool(PairedParam, showPaired);

                // A new hit picks the other reaction than the previous hit, before the Animator evaluates.
                bool struckRestart = pendingStruckRestart && state == CombatState.Struck;
                if (struckRestart)
                {
                    struckB = nextStruckB;
                    nextStruckB = !nextStruckB;
                }
                animator.SetBool(StruckBParam, struckB);

                if (pendingMoveRestart && IsTimedMoveState(state) && currentMove == pendingMove)
                {
                    string moveState = MoveStateName(state, currentMove, showPaired);
                    if (moveState != null)
                    {
                        SetMoveSpeed(ComputeStateSpeed(moveState, pendingTiming));
                        PlayFromStart(moveState, 0f);
                    }
                }

                if (struckRestart)
                    PlayFromStart(struckB ? StruckBStateName : StruckStateName, 0f);

                // D-023: Knocked Out reuses the Knocked Down clip (the held downed sprite). Knocked out while
                // already down (a stomp) jumps to the clip's end instead of replaying it from the start.
                if (state == CombatState.KnockedOut && lastState == CombatState.KnockedDown)
                    PlayFromStart(KnockedOutStateName, 1f);
            }
            pendingMoveRestart = false;
            pendingStruckRestart = false;
            lastState = state;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = flip;
                // ARCHITECTURE 6: presentation may adjust sorting for the pair. Both feet share one depth during a
                // lock-up or bodyslam, so the Y sort ties; Wrestler A draws in front (their reaching arm over B; the
                // approved bodyslam pair previews draw A over B in every frame).
                if (!baseSortingOrderKnown)
                {
                    baseSortingOrder = spriteRenderer.sortingOrder;
                    baseSortingOrderKnown = true;
                }
                spriteRenderer.sortingOrder = baseSortingOrder + (IsInitiatorState(state) && paired ? 1 : 0);
            }
        }

        // After the Animator has written this frame's pose: mirror the clip's x offset and z rotation for a
        // flipped sprite (clips are authored for left-facing art), and shift a mirrored downed sprite back onto
        // the ground the unmirrored one covers.
        private void LateUpdate()
        {
            if (view == null || spriteRenderer == null)
                return;
            Transform t = spriteRenderer.transform;
            if (t == transform)
                return; // never touch the root

            Vector3 p = t.localPosition;
            Vector3 e = t.localEulerAngles;
            float currentX = p.x, currentRotZ = NormalizeAngle(e.z);
            float animX = currentX, animRotZ = currentRotZ;
            if (mirrorWritten && Mathf.Abs(currentX - lastWrittenX) < 1e-4f && Mathf.Abs(currentRotZ - lastWrittenRotZ) < 0.01f)
            {
                // The Animator did not write since our last write: start again from the values it last wrote, so
                // nothing is mirrored twice but a change of flip or downed offset (e.g. knocked down) still applies.
                animX = lastAnimatorX;
                animRotZ = lastAnimatorRotZ;
            }
            lastAnimatorX = animX;
            lastAnimatorRotZ = animRotZ;

            float x = animX, r = animRotZ;
            if (spriteRenderer.flipX)
            {
                x = -x;
                r = -r;
            }
            x += DownedMirrorOffset(downedMirrored, facingFlip, downedMirrorShift);

            bool changed = Mathf.Abs(x - currentX) > 1e-5f || Mathf.Abs(r - currentRotZ) > 1e-3f;
            if (changed)
            {
                p.x = x;
                t.localPosition = p;
                t.localEulerAngles = new Vector3(e.x, e.y, r);
            }
            // Bookkeeping only when our value differs from the Animator's (a later identical value means "not rewritten").
            mirrorWritten = Mathf.Abs(x - animX) > 1e-5f || Mathf.Abs(r - animRotZ) > 1e-3f;
            lastWrittenX = x;
            lastWrittenRotZ = r;
        }

        /// <summary>Knocked Down or Knocked Out: the states that show the downed sprite.</summary>
        public static bool IsDownedState(CombatState state) =>
            state == CombatState.KnockedDown || state == CombatState.KnockedOut;

        /// <summary>
        /// Child x offset that keeps a mirrored downed sprite on the same ground as the unmirrored one: the sprite's
        /// opaque span [a, b] (left-facing art, relative to the pivot) drawn with the facing flip covers [a, b] or
        /// [-b, -a]; drawn with the opposite flip and shifted by this it covers the same span. 0 when not mirrored.
        /// </summary>
        public static float DownedMirrorOffset(bool mirrored, bool facingFlip, float shift) =>
            mirrored ? (facingFlip ? -shift : shift) : 0f;

        /// <summary>See the Inspector tooltip; set by WrestlerAnimationBuilder.</summary>
        public float DownedMirrorShift => downedMirrorShift;

        /// <summary>True while the downed sprite is shown mirrored (a knockdown that did not follow a bodyslam).</summary>
        public bool DownedMirrored => downedMirrored;

        private void SetMoveSpeed(float speed)
        {
            moveSpeed = speed;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat(MoveSpeedParam, speed);
        }

        private void PlayFromStart(string stateName, float normalizedTime)
        {
            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash))
                animator.Play(hash, 0, normalizedTime);
        }

        /// <summary>
        /// Playback speed that lands the clip's contact frame at the move's tuned Startup (ARCHITECTURE 4.2):
        /// contact time (native) / Startup. 1 if the clip or its contact time is unknown.
        /// </summary>
        public float ComputeMoveSpeed(MoveId move, MoveTiming timing) => ComputeClipSpeed(ClipForMove(move), timing);

        /// <summary>Playback speed for the clip an Animator state plays (see <see cref="ComputeClipSpeed"/>).</summary>
        public float ComputeStateSpeed(string animatorState, MoveTiming timing) =>
            ComputeClipSpeed(ClipForSlot(SlotNameForState(animatorState)), timing);

        /// <summary>
        /// A clip with a contact time lands its contact frame at Startup (<see cref="SpeedFor"/>). A clip recorded
        /// with contact time 0 has no contact frame (lock-up whiff, break): its whole length is fitted to the
        /// move's total time (<see cref="FitSpeed"/>). 1 if the clip has no table entry (looping holds).
        /// </summary>
        public float ComputeClipSpeed(AnimationClip clip, MoveTiming timing)
        {
            if (moveTable == null || clip == null || !moveTable.TryGetContactTime(clip, out float contact))
                return 1f;
            return contact > 0f ? SpeedFor(contact, timing.Startup) : FitSpeed(clip.length, timing.Total);
        }

        /// <summary>clipLength / total, clamped to [MinMoveSpeed, MaxMoveSpeed]; 1 if either is not positive.</summary>
        public static float FitSpeed(float clipLength, float total)
        {
            if (clipLength <= 0f || total <= 0f) return 1f;
            return Mathf.Clamp(clipLength / total, MinMoveSpeed, MaxMoveSpeed);
        }

        /// <summary>contact / startup, clamped to [MinMoveSpeed, MaxMoveSpeed]; startup 0 = fastest.</summary>
        public static float SpeedFor(float contactTime, float startup)
        {
            if (contactTime <= 0f) return 1f;
            if (startup <= 0f) return MaxMoveSpeed;
            return Mathf.Clamp(contactTime / startup, MinMoveSpeed, MaxMoveSpeed);
        }

        /// <summary>This wrestler's clip for a move: the override for the move's slot, or the slot clip itself.</summary>
        public AnimationClip ClipForMove(MoveId move) => ClipForSlot(SlotNameFor(move));

        /// <summary>This wrestler's clip for a shared slot: its override, or the slot clip itself.</summary>
        public AnimationClip ClipForSlot(string slot)
        {
            if (animator == null) return null;
            RuntimeAnimatorController rac = animator.runtimeAnimatorController;
            if (rac is AnimatorOverrideController aoc)
            {
                AnimationClip clip = aoc[slot];
                if (clip != null) return clip;
            }
            if (rac == null) return null;
            foreach (AnimationClip c in rac.animationClips)
                if (c != null && c.name == slot) return c;
            return null;
        }

        private static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees < -180f) degrees += 360f;
            return degrees;
        }

        /// <summary>Back-facing = any facing with an upward (away from camera) component (STYLE_GUIDE: Up/Left, Up, Up/Right).</summary>
        public static bool IsBackFacing(Vector2 facing) => facing.y > 0.01f;

        /// <summary>Art drawn facing left is flipped when facing right (+1), and vice versa.</summary>
        public static bool ShouldFlip(int horizontalFacing, bool artFacesLeft) =>
            artFacesLeft ? horizontalFacing > 0 : horizontalFacing < 0;
    }
}
