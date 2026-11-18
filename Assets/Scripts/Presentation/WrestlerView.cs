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
    /// one ends (MoveStarted while staying Striking) and Struck restarting on every hit (HitLanded, D-024).
    /// The hit reaction is picked from the move that landed (presentation only; Combat has one Struck state): a
    /// Strike 2 (combo hit 2) shows "StruckC"; every other hit, and a Struck entered with no HitLanded (an obstacle
    /// crash), shows "StruckB" (D-042). A Strike 2 landing during StruckB restarts into StruckC from its start.
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
    /// Irish Whip (ARCHITECTURE 6, D-039): Grappling + Move IrishWhip plays "IrishWhip" (the whipper), contact-scaled
    /// so its release sprite (f4) lands at the whip's Startup; the runner is State Running and plays its normal Run
    /// clip. The whipper's f1 is drawn facing the lock-up facing (Combat keeps HorizontalFacing there for the whole
    /// whip) and f2-f4 facing PairedMoveInfo.WhipDirection: <see cref="LateUpdate"/> compares the whip clip's current
    /// time (native) with the clip's MoveAnimationTable TurnTime (start of f2), so the flip changes on the same frame
    /// as the sprite. The runner draws one sorting order in front of the whipper until the whipper's
    /// MovePhaseChanged(IrishWhip, None); the whipper does not take the usual initiator-in-front order.
    ///
    /// Falling Down (D-041): a Knocked Down / Knocked Out that starts with a nonzero <see cref="IWrestlerView.FallSide"/>
    /// latches that side and plays "Fall" (struck_a, fall2, fall3, downed) from its start (Animator "Fall" bool). The fall
    /// frames are drawn head toward the fall side; from the clip's TurnTime (the downed sprite) LateUpdate draws the downed
    /// sprite mirrored with downedMirrorShift, so head and feet never swap sides and the roots line up. A bodyslam
    /// knockdown (FallSide 0) keeps the plain downed sprite; a stomp KO while down (FallSide 0) keeps the latched side.
    ///
    /// Get-up (D-043): while <see cref="IWrestlerView.IsGettingUp"/> (still Knocked Down) the reusable "Crouch" state shows
    /// (Animator "Crouch" bool), drawn by the combat facing at the root (no child offset, the same anchor as the stand; the
    /// designer removed the slide after playtesting). A Stomp knockout during the get-up goes back to the lying pose (end of
    /// the Fall clip, or the KnockedOut clip's end).
    ///
    /// Bodyslam throw (D-044, Barry slams Peaux; PairedMoveInfo.ThrowDirection T != 0): the slammer's S1-S2 face the lock-up
    /// facing and S3 on faces T; the receiver's R1-R3 follow the Locked Up facing at the lock-up spot, R4-R7 (stored for a
    /// throw to screen-right) are flipped for a throw left at the root Combat moves. Both switch at their clip's
    /// MoveAnimationTable TurnTime in <see cref="LateUpdate"/>. The receiver turns Knocked Down on the landing tick; the
    /// Animator bool "SlamTail" keeps the Bodyslammed clip (R6, R7, R6) playing to its end before the Knocked Down clip
    /// (the same R6, same orientation). A slam without T (Peaux slams Barry) keeps the D-033/D-038 path unchanged.
    ///
    /// Stomp bounce (D-047): a HitLanded(Stomp) on this wrestler while he lies down (Knocked Down, not getting up, or
    /// Knocked Out by that stomp) plays "StompBounce" from its start: R7, the bodyslam's bounce frame (17 px lift baked
    /// into the art), for its FrameTiming hold. It keeps the lying pose's flip, mirror and shifts (the downed-pose rules
    /// above), so R7 sits relative to R6 exactly as in the bodyslam tail, mirrored with it. When the clip ends the lying pose
    /// is played at its end (Fall / KnockedOut / KnockedDown, normalized time 1: no new fall). Animator bool "StompBounce"
    /// holds off the Any State downed transitions meanwhile. Every stomp restarts it; the get-up crouch always wins.
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
        /// Animator bool parameter: show hit reaction B (state "StruckB") instead of "Struck". Presentation only:
        /// Combat has a single Struck state. Always true since D-040 (designer, 2026-11-15): the lean-back "Struck"
        /// (StruckA) clip stays built but is no longer shown. <see cref="StruckCParam"/> wins over it.
        /// </summary>
        public static readonly int StruckBParam = Animator.StringToHash("StruckB");

        /// <summary>
        /// Animator bool parameter: show hit reaction C (state "StruckC"), the second hit of the combo (D-042). True while
        /// the Struck reaction in progress came from a Strike 2 hit; false otherwise (StruckB shows). Presentation only.
        /// </summary>
        public static readonly int StruckCParam = Animator.StringToHash("StruckC");

        /// <summary>Animator state names used for restarts (match WrestlerAnimationBuilder).</summary>
        public const string StruckStateName = "Struck";
        public const string StruckBStateName = "StruckB";
        public const string StruckCStateName = "StruckC";
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
        /// <summary>Irish Whip, the whipper's side (D-039). Plays the slot clip "Slot_IrishWhip".</summary>
        public const string IrishWhipStateName = "IrishWhip";              // A, Grappling
        private static readonly int IrishWhipStateHash = Animator.StringToHash(IrishWhipStateName);
        /// <summary>Falling Down (D-041): struck_a, fall2, fall3, downed. Plays the slot clip "Slot_Fall".</summary>
        public const string FallStateName = "Fall";
        private static readonly int FallStateHash = Animator.StringToHash(FallStateName);
        /// <summary>Animator bool parameter: this Knocked Down / Knocked Out plays the fall (D-041; nonzero fall side).</summary>
        public static readonly int FallParam = Animator.StringToHash("Fall");
        /// <summary>Crouch (D-043): one sprite held. Today shown for the get-up at the end of a knockdown (Animator
        /// "Crouch" bool, while <see cref="IWrestlerView.IsGettingUp"/>). Plays the slot clip "Slot_Crouch".</summary>
        public const string CrouchStateName = "Crouch";
        /// <summary>Animator bool parameter: show the crouch (D-043). True while the wrestler gets up.</summary>
        public static readonly int CrouchParam = Animator.StringToHash("Crouch");
        /// <summary>Animator bool parameter (D-044): a thrown receiver who is already Knocked Down / Knocked Out still plays
        /// the Bodyslammed clip's landing tail (R6, R7, R6); the downed transitions wait for it to be false.</summary>
        public static readonly int SlamTailParam = Animator.StringToHash("SlamTail");
        /// <summary>Stomp bounce (D-047): R7 held, then back to the lying pose. Plays the slot clip "Slot_StompBounce".</summary>
        public const string StompBounceStateName = "StompBounce";
        private static readonly int StompBounceStateHash = Animator.StringToHash(StompBounceStateName);
        /// <summary>Animator bool parameter (D-047): the stomp bounce is playing; the downed transitions wait for it to be false.</summary>
        public static readonly int StompBounceParam = Animator.StringToHash("StompBounce");
        private static readonly int BodyslamStateHash = Animator.StringToHash(BodyslamStateName);
        private static readonly int BodyslammedStateHash = Animator.StringToHash(BodyslammedStateName);

        /// <summary>Shared-controller slot clip for a move: "Slot_" + MoveId (e.g. Slot_Strike1, Slot_Stomp).</summary>
        public static string SlotNameFor(MoveId move) => "Slot_" + move;

        /// <summary>Shared-controller slot clip played by an Animator state: "Slot_" + state name.</summary>
        public static string SlotNameForState(string animatorState) => "Slot_" + animatorState;

        /// <summary>
        /// The Animator state that plays a timed move, or null if the state shows no move clip. Striking:
        /// the move's own state (Strike1-3, Stomp). Lock Up: hold when paired, whiff otherwise; Break for
        /// BreakLockUp. Locked Up: held, or the receiver's break. Grappling / Grappled: the bodyslam sides; Grappling
        /// + IrishWhip: the whipper's Irish Whip (the runner is Running and plays Run).
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
                    if (move == MoveId.Bodyslam) return BodyslamStateName;
                    if (move == MoveId.IrishWhip) return IrishWhipStateName;
                    return null;
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

        /// <summary>The initiator's side of a paired move (drawn in front while paired, except the Irish Whip).</summary>
        public static bool IsInitiatorState(CombatState state) =>
            state == CombatState.LockUp || state == CombatState.Grappling;

        /// <summary>
        /// Sorting-order offset over the wrestler's base order (ARCHITECTURE 6). Paired initiator: +1 (its reaching arm
        /// over B), except the Irish Whip whipper (Grappling + IrishWhip): 0, because the runner is drawn in front
        /// (D-039: the runner hides the whipper's pushing arm). The runner: +1 while <paramref name="inFrontOfWhipper"/>
        /// (from PairedMoveStarted(IrishWhip) until the whipper's MovePhaseChanged(IrishWhip, None)).
        /// </summary>
        public static int SortingOffset(CombatState state, MoveId move, bool paired, bool inFrontOfWhipper)
        {
            if (inFrontOfWhipper) return 1;
            if (state == CombatState.Grappling && move == MoveId.IrishWhip) return 0;
            return IsInitiatorState(state) && paired ? 1 : 0;
        }

        /// <summary>
        /// Left/right the whipper is drawn facing at a time in the whip clip (D-039): before <paramref name="turnTime"/>
        /// (f1) the lock-up facing Combat still reports, from it (f2-f4) the whip direction. Times are clip time at
        /// native speed. A whip direction of 0 (unknown) keeps the combat facing.
        /// </summary>
        public static int WhipperFacing(float clipTime, float turnTime, int combatFacing, int whipDirection) =>
            whipDirection != 0 && clipTime >= turnTime - TurnTimeEpsilon ? whipDirection : combatFacing;

        /// <summary>Tolerance for the clip time reaching the turn key (float rounding of normalized time).</summary>
        public const float TurnTimeEpsilon = 1e-4f;

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
        [Tooltip("World units, set by WrestlerAnimationBuilder (D-044): extra x shift toward the fall side of the downed " +
                 "sprite at the end of a fall, so a downed sprite whose pivot is the body centre (Peaux's v5 R6) lands with " +
                 "its feet end on fall3's. 0 = the D-041 landing (Barry).")]
        [SerializeField] private float fallLandingShift;

        private IWrestlerView view;
        private CombatEvents events;
        private WrestlerId eventsId;

        // Queued by event handlers, applied in Apply().
        private bool pendingMoveRestart;
        private MoveId pendingMove;
        private MoveTiming pendingTiming;
        private bool pendingStruckRestart;
        private MoveId pendingStruckMove;
        // The Struck reaction showing is StruckC (from a Strike 2 hit); false = StruckB.
        private bool showStruckC;

        // Lock-up slice: in a paired move (from PairedMoveStarted), and the sprite's sorting order outside it.
        private bool paired;
        private bool baseSortingOrderKnown;
        private int baseSortingOrder;

        // Irish Whip (D-039). Whipper: the whip direction from PairedMoveStarted (0 = not whipping). Runner: drawn in
        // front of the whipper until the whipper's MovePhaseChanged(IrishWhip, None).
        private int whipDirection;
        private bool inFrontOfWhipper;
        private WrestlerId whipperId;

        // Bodyslam throw (D-044), from PairedMoveStarted(Bodyslam) with a ThrowDirection: the throw direction T while this
        // wrestler plays a throwing slam (0 otherwise), which side it plays, and the receiver's root x at the press (the
        // lock-up spot, where R1-R3 are drawn even if the root has already moved on that rendered frame).
        private int throwDirection;
        private bool throwReceiver;
        private float throwPinX;
        private float throwPinOffset;
        // A receiver knocked down / out by a throw: T, so the landing tail and the downed R6 keep the throw's orientation.
        private int downedThrowDirection;
        private bool slamTail;
        // Stomp bounce (D-047): a stomp hit this wrestler (queued by HitLanded), and the bounce is playing.
        private bool pendingStompBounce;
        private bool stompBouncing;

        private float moveSpeed = 1f;
        private CombatState? lastState;

        // Downed orientation (designer, 2026-11-12). The downed sprite is the last Bodyslammed frame, which lies head
        // toward the slammer. A knockdown that follows a bodyslam (entered from Grappled) shows it as is; every other
        // knockdown is a backward fall (D-041: the Fall clip, oriented by the latched fall side) that ends on it mirrored
        // in place (feet toward what hit them). With no fall side it is mirrored by facing, as under D-038.
        private bool downedFromSlam;
        private bool downedMirrored;
        // Falling Down (D-041): the FallSide latched when this knockdown / knockout started (0 = no fall), kept while
        // down so a stomp KO (FallSide 0) keeps the orientation; and whether the fall clip has reached the downed sprite.
        private int downedFallSide;
        private bool fallLanded;
        private bool facingFlip;
        // Get-up (D-043): showing the crouch (drawn at the root, no offset).
        private bool gettingUp;
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
            gettingUp = false;
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
            pendingStompBounce = false;
            showStruckC = false;
            paired = false;
            whipDirection = 0;
            inFrontOfWhipper = false;
            throwDirection = 0;
            throwReceiver = false;
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
            bool whip = info.Move == MoveId.IrishWhip;
            // D-039: the whipper turns to the whip direction at f2 (LateUpdate); the runner draws in front of it.
            whipDirection = whip && info.Initiator == eventsId ? info.WhipDirection : 0;
            inFrontOfWhipper = whip && info.Receiver == eventsId;
            whipperId = info.Initiator;
            // D-044: a throwing slam (ThrowDirection != 0). Peaux slams Barry has none and keeps the old path untouched.
            bool thrown = info.Move == MoveId.Bodyslam && info.ThrowDirection != 0;
            throwDirection = thrown ? info.ThrowDirection : 0;
            throwReceiver = thrown && info.Receiver == eventsId;
            throwPinX = transform.position.x; // the receiver's root does not move before the release
        }

        private void OnMovePhaseChanged(WrestlerId id, MoveId move, MovePhase phase)
        {
            // The runner stays in front until the whipper's whip ends (ARCHITECTURE 6), even if it was released early.
            if (inFrontOfWhipper && id == whipperId && move == MoveId.IrishWhip && phase == MovePhase.None)
                inFrontOfWhipper = false;
            if (id != eventsId || phase != MovePhase.None) return;
            // The move ended or was cut. A MoveStarted later in the same tick re-queues a restart.
            pendingMoveRestart = false;
            SetMoveSpeed(1f);
        }

        private void OnHitLanded(HitInfo hit)
        {
            if (hit.Defender != eventsId) return;
            pendingStruckRestart = true;
            pendingStruckMove = hit.Move;
            // D-047: only a stomp hits a downed wrestler; Apply plays the bounce if he is still lying down.
            if (hit.Move == MoveId.Stomp)
                pendingStompBounce = true;
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
            bool combatFlip = ShouldFlip(view.HorizontalFacing, artFacesLeft);
            facingFlip = combatFlip;
            bool downed = IsDownedState(state);
            bool fallStarted = false;
            if (downed && !(lastState.HasValue && IsDownedState(lastState.Value)))
            {
                // KO while down keeps how they went down. D-041: Combat reports no fall side only for a bodyslam
                // knockdown / KO, so FallSide 0 on a fresh knockdown also means "as the bodyslam left them" (covers a
                // Grappled state that never reached a rendered frame).
                downedFallSide = System.Math.Sign(view.FallSide);
                downedFromSlam = lastState == CombatState.Grappled || downedFallSide == 0;
                if (downedFromSlam)
                    downedFallSide = 0;
                fallLanded = false;
                fallStarted = downedFallSide != 0;
                // D-044: knocked down / out by a throw's landing: keep the throw's orientation for the tail and the R6.
                downedThrowDirection = downedFromSlam && throwReceiver ? throwDirection : 0;
            }
            if (!downed)
            {
                downedFallSide = 0;
                downedThrowDirection = 0;
            }
            // The throw (D-044) lasts while this wrestler is Grappling / Grappled in the bodyslam.
            if (view.CurrentMove != MoveId.Bodyslam || (state != CombatState.Grappling && state != CombatState.Grappled))
            {
                throwDirection = 0;
                throwReceiver = false;
            }
            bool falling = downedFallSide != 0;
            if (falling)
            {
                // D-041: orientation comes from the fall side, not the facing. LateUpdate refines fallLanded from the clip.
                facingFlip = FallFacingFlip(downedFallSide);
                downedMirrored = fallLanded;
            }
            else
                downedMirrored = downed && !downedFromSlam;
            if (downed && downedThrowDirection != 0)
            {
                // D-044: R6 (and the tail before it) is stored for a throw to screen-right: head toward the slammer.
                facingFlip = ThrownReceiverLateFlip(downedThrowDirection, artFacesLeft);
                downedMirrored = false;
            }

            // D-043 get-up: the crouch, drawn by the combat facing (locked while down; the stand that follows uses it),
            // never mirrored, at the root with no offset (designer, after playtesting: no slide).
            bool wasGettingUp = gettingUp;
            gettingUp = state == CombatState.KnockedDown && view.IsGettingUp;
            if (gettingUp)
            {
                facingFlip = combatFlip;
                downedMirrored = false;
            }
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
            if (state != CombatState.Grappling || view.CurrentMove != MoveId.IrishWhip)
                whipDirection = 0; // the whip ended (or was never this wrestler's): plain combat facing again
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
                animator.SetBool(FallParam, falling);
                animator.SetBool(CrouchParam, gettingUp);
                // D-044: the thrown receiver turns Knocked Down on the landing tick, when R6 starts. Keep the Bodyslammed
                // clip (R6 -> R7 -> R6) until its end; the Knocked Down clip then shows the same R6, the same way round.
                slamTail = false;
                if (downed && downedThrowDirection != 0 && !gettingUp)
                {
                    AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                    slamTail = current.shortNameHash == BodyslammedStateHash && current.normalizedTime < 1f;
                }
                animator.SetBool(SlamTailParam, slamTail);

                // The reaction comes from the hit that landed, set before the Animator evaluates: Strike 2 (combo hit 2)
                // shows StruckC, any other hit StruckB (D-042). A Struck entered with no hit this frame (an obstacle
                // crash) shows StruckB; staying Struck with no new hit keeps the reaction already showing.
                bool struckRestart = pendingStruckRestart && state == CombatState.Struck;
                if (struckRestart)
                    showStruckC = pendingStruckMove == MoveId.Strike2;
                else if (state != CombatState.Struck || lastState != CombatState.Struck)
                    showStruckC = false;
                animator.SetBool(StruckBParam, true);
                animator.SetBool(StruckCParam, showStruckC);

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
                    PlayFromStart(showStruckC ? StruckCStateName : StruckBStateName, 0f);

                // D-041: a knockdown or knockout from standing with a fall side plays the fall from its start.
                // (Not when the first downed frame this view sees is already the get-up: the crouch shows instead.)
                if (fallStarted && !gettingUp)
                    PlayFromStart(FallStateName, 0f);
                // D-023: Knocked Out reuses the Knocked Down clip (the held downed sprite). Knocked out while
                // already down (a stomp) jumps to the clip's end instead of replaying it from the start. After a fall
                // the Fall state just carries on (no self-transition): lying pose and orientation are kept.
                else if (state == CombatState.KnockedOut && lastState == CombatState.KnockedDown && !falling)
                    PlayFromStart(KnockedOutStateName, 1f);
                // D-043: a Stomp knockout during the get-up (the crouch was showing) goes straight back to the lying
                // pose it got up from: the end of the Fall clip (downed sprite, latched orientation), not a new fall.
                else if (state == CombatState.KnockedOut && wasGettingUp && falling)
                    PlayFromStart(FallStateName, 1f);

                // D-047 stomp bounce. Ends when its clip has played out (back to the lying pose at its end, so a fall is not
                // replayed), when the wrestler stops lying down (the get-up crouch wins), or if something else replaced it.
                if (stompBouncing)
                {
                    AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                    bool inBounce = current.shortNameHash == StompBounceStateHash;
                    if (!downed || gettingUp || !inBounce)
                        stompBouncing = false;
                    else if (current.normalizedTime >= 1f)
                    {
                        stompBouncing = false;
                        PlayFromStart(LyingStateName(state, falling), 1f);
                    }
                }
                // A stomp that landed on a lying wrestler (also the stomp that knocks him out, which wins over the KO jump
                // above) bounces him from R7's start; a stomp during the get-up (crouch) does not.
                if (pendingStompBounce && downed && !gettingUp && animator.HasState(0, StompBounceStateHash))
                {
                    stompBouncing = true;
                    PlayFromStart(StompBounceStateName, 0f);
                }
                animator.SetBool(StompBounceParam, stompBouncing);
            }
            pendingMoveRestart = false;
            pendingStruckRestart = false;
            pendingStompBounce = false;
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
                spriteRenderer.sortingOrder = baseSortingOrder + SortingOffset(state, view.CurrentMove, paired, inFrontOfWhipper);
            }
        }

        // After the Animator has written this frame's pose: mirror the clip's x offset and z rotation for a
        // flipped sprite (clips are authored for left-facing art), and shift a mirrored downed sprite back onto
        // the ground the unmirrored one covers.
        private void LateUpdate()
        {
            if (view == null || spriteRenderer == null)
                return;
            ApplyWhipperFacing();
            ApplyFallOrientation();
            ApplyThrowSlamFacing();
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
            // D-044: a fall's landed downed sprite lines its feet up with fall3; the receiver's R1-R3 stay on the lock-up spot.
            if (downedMirrored && downedFallSide != 0)
                x += downedFallSide * fallLandingShift;
            x += throwPinOffset;

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

        // D-039: after the Animator has evaluated this frame, draw the whipper's f1 facing the lock-up facing and f2-f4
        // facing the whip direction, read from the whip clip's current time against its TurnTime (start of f2), so the
        // flip changes with the sprite at any playback speed. Combat facing is never touched.
        private void ApplyWhipperFacing()
        {
            if (whipDirection == 0 || animator == null || !animator.isActiveAndEnabled || view.State != CombatState.Grappling)
                return;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != IrishWhipStateHash)
                return;
            AnimationClip clip = ClipForSlot(SlotNameForState(IrishWhipStateName));
            if (clip == null || moveTable == null || !moveTable.TryGetTurnTime(clip, out float turnTime))
                return;
            float clipTime = Mathf.Clamp01(info.normalizedTime) * clip.length;
            int facing = WhipperFacing(clipTime, turnTime, view.HorizontalFacing, whipDirection);
            facingFlip = ShouldFlip(facing, artFacesLeft);
            spriteRenderer.flipX = facingFlip;
        }

        // D-041: after the Animator has evaluated this frame, pick the fall frames' or the downed sprite's orientation from
        // the Fall clip's current time against its MoveAnimationTable TurnTime (start of the downed sprite). Fall frames
        // (stored head-right) are drawn head toward the fall side; the downed sprite (stored head-left) is mirrored with
        // downedMirrorShift (LateUpdate), so its head stays on the same side and its root lines up. A fall clip with no
        // TurnTime (the downed-only PLACEHOLDER) counts as landed from the start.
        private void ApplyFallOrientation()
        {
            if (downedFallSide == 0 || gettingUp || animator == null || !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null)
                return; // D-043: the get-up crouch is drawn by the combat facing (Apply), not the fall side
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            bool landed = true;
            if (info.shortNameHash == FallStateHash)
            {
                AnimationClip clip = ClipForSlot(SlotNameForState(FallStateName));
                if (clip != null && moveTable != null && moveTable.TryGetTurnTime(clip, out float landTime))
                    landed = FallHasLanded(Mathf.Clamp01(info.normalizedTime) * clip.length, landTime);
            }
            fallLanded = landed;
            facingFlip = FallFacingFlip(downedFallSide);
            downedMirrored = landed;
            spriteRenderer.flipX = facingFlip ^ downedMirrored;
        }

        // D-044: after the Animator has evaluated this frame, orient a throwing slam's frames from the clip's current time
        // against its MoveAnimationTable TurnTime, like the whip turn (so the flip changes with the sprite at any speed):
        //  - slammer (Bodyslam: S1-S5, stand): before TurnTime (S1-S2) the lock-up facing Combat still reports, from it
        //    (S3 on) the throw direction T;
        //  - receiver (Bodyslammed: R1-R7, R6): before TurnTime (R1-R3, the release) the Locked Up facing (= T), drawn at
        //    the lock-up spot even if the root already moved this frame; from it (R4-R7, stored for a throw to
        //    screen-right) flipped for a throw to the left, at the root Combat moves.
        // Combat facing is never touched. Peaux slams Barry (no ThrowDirection) never gets here.
        private void ApplyThrowSlamFacing()
        {
            throwPinOffset = 0f;
            if (throwDirection == 0 || animator == null || !animator.isActiveAndEnabled || moveTable == null)
                return;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            string stateName = throwReceiver ? BodyslammedStateName : BodyslamStateName;
            if (info.shortNameHash != (throwReceiver ? BodyslammedStateHash : BodyslamStateHash))
                return;
            AnimationClip clip = ClipForSlot(SlotNameForState(stateName));
            if (clip == null || !moveTable.TryGetTurnTime(clip, out float turnTime))
                return;
            float clipTime = Mathf.Clamp01(info.normalizedTime) * clip.length;
            if (throwReceiver)
            {
                bool released = ThrowHasReleased(clipTime, turnTime);
                facingFlip = released ? ThrownReceiverLateFlip(throwDirection, artFacesLeft) : ShouldFlip(view.HorizontalFacing, artFacesLeft);
                if (!released)
                    throwPinOffset = throwPinX - transform.position.x;
            }
            else
                facingFlip = ShouldFlip(WhipperFacing(clipTime, turnTime, view.HorizontalFacing, throwDirection), artFacesLeft);
            spriteRenderer.flipX = facingFlip;
        }

        /// <summary>
        /// D-047: the Animator state that shows the lying pose after a stomp bounce: the Fall clip (its end = the landed
        /// downed sprite) for a wrestler who fell, else the Knocked Out / Knocked Down clip (the downed sprite held).
        /// </summary>
        public static string LyingStateName(CombatState state, bool fell) =>
            fell ? FallStateName : state == CombatState.KnockedOut ? KnockedOutStateName : CombatState.KnockedDown.ToString();

        /// <summary>True while the stomp bounce (R7) plays (D-047).</summary>
        public bool StompBouncing => stompBouncing;

        /// <summary>D-044: the throwing slam's clip has reached its turn key (slammer S3 / receiver R4, the release).</summary>
        public static bool ThrowHasReleased(float clipTime, float turnTime) => clipTime >= turnTime - TurnTimeEpsilon;

        /// <summary>
        /// D-044: flip of the receiver's R4-R7 and his downed R6 after a throw. Those frames are stored for a throw to
        /// screen-right (slammer on the left, R6 head left = toward the slammer), so they are flipped for a throw to the
        /// left: the stored art "faces" right while the receiver's facing is T.
        /// </summary>
        public static bool ThrownReceiverLateFlip(int throwDirection, bool artFacesLeft) => ShouldFlip(-throwDirection, artFacesLeft);

        /// <summary>The throw direction while this wrestler plays a throwing slam (D-044), else 0.</summary>
        public int ThrowDirection => throwDirection;

        /// <summary>The throw direction this wrestler was knocked down / out by (D-044), else 0.</summary>
        public int DownedThrowDirection => downedThrowDirection;

        /// <summary>True while a thrown receiver, already down, still plays the Bodyslammed landing tail (D-044).</summary>
        public bool PlayingSlamTail => slamTail;

        /// <summary>See the Inspector tooltip; set by WrestlerAnimationBuilder (D-044).</summary>
        public float FallLandingShift => fallLandingShift;

        /// <summary>
        /// D-041: the "facing flip" a fall is drawn with: flipped for a fall toward -x. The fall frames are stored head on
        /// the right (+x), so they show head toward the fall side with this flip; the downed sprite is stored head on the
        /// left, so it is drawn mirrored (this flip inverted, plus <see cref="DownedMirrorOffset"/>) and lies head toward
        /// the fall side too.
        /// </summary>
        public static bool FallFacingFlip(int fallSide) => fallSide < 0;

        /// <summary>D-041: the fall clip shows the downed sprite from its land time (clip time at native speed).</summary>
        public static bool FallHasLanded(float clipTime, float landTime) => clipTime >= landTime - TurnTimeEpsilon;

        /// <summary>The fall side latched for the current knockdown / knockout (D-041); 0 when not falling.</summary>
        public int DownedFallSide => downedFallSide;

        /// <summary>The whipper's whip direction while its Irish Whip plays (D-039), else 0.</summary>
        public int WhipDirection => whipDirection;

        /// <summary>True while this runner is drawn in front of its whipper (D-039).</summary>
        public bool InFrontOfWhipper => inFrontOfWhipper;

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

        /// <summary>True while the get-up crouch is shown (D-043).</summary>
        public bool ShowingGetUp => gettingUp;

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
