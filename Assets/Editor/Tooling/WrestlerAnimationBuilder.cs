using System;
using System.Collections.Generic;
using System.Linq;
using RuralRumble.Core;
using RuralRumble.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RuralRumble.EditorTooling
{
    /// <summary>
    /// Builds the shared wrestler Animator graph, one wrestler's clips + AnimatorOverrideController, and the
    /// wrestler's presentation prefab (ARCHITECTURE 4.2, D-021). Re-running updates assets in place so GUIDs
    /// and references survive. Standing, Walking, Running, the three Striking clips (Strike1-3), Stomp (approved
    /// sheets; a PLACEHOLDER stand-in only for a wrestler with none), Struck A / B / C (approved single sprites:
    /// WrestlerView shows C for a Strike 1 hit and B for every other non-knockdown hit, A is built but unused; run
    /// ImportStruckSheets first), and PLACEHOLDER stand-ins for
    /// Recovering (stand + walk) built from the stand/walk sprites. Knocked Down / Knocked Out
    /// hold the wrestler's downed sprite (last Bodyslammed sprite) after a bodyslam; every other knockdown / knockout
    /// from standing plays the Fall clip (D-041: struck_a, fall2, fall3, downed; see WriteFallClip). The get-up at the end
    /// of every knockdown plays the reusable Crouch state (D-043: {Name}_Crouch.anim, one approved sprite held).
    ///
    /// Menu: Rural Rumble/Tooling/Build Barry Animations + Prefab, Rural Rumble/Tooling/Build Peaux Animations + Prefab.
    /// A wrestler with no run or strike art gets PLACEHOLDER stand-ins for those too (WriteMissingArtStandIns).
    /// Lock Up / Locked Up use the approved pair sheets (D-032; run ImportLockUpSheets first); lock-up whiff and
    /// break stay PLACEHOLDER stand-ins. Bodyslam / Bodyslammed use the approved bodyslam pair sheets (D-033; run
    /// ImportBodyslamSheets first). IrishWhip (the whipper's side) uses the approved 4-frame whip sheets (D-039; run
    /// ImportIrishWhipSheets first); the whipped runner plays its normal Run clip.
    ///
    /// Timing: the key frames below (SourceFps frames) are only the defaults. Each wrestler has a
    /// WrestlerFrameTiming asset (Assets/Animation/{Name}/{Name}_FrameTiming.asset, created from these defaults if
    /// missing) with a designer-editable Hold (ms) for every sprite / pose step of every clip; the clips, their
    /// lengths and the MoveAnimationTable contact times are built from it. Menu: Rural Rumble/Rebuild Wrestler
    /// Animations (both wrestlers, clips only).
    /// </summary>
    public static class WrestlerAnimationBuilder
    {
        public const string SharedFolder = "Assets/Animation/Shared";
        public const string ControllerPath = SharedFolder + "/Wrestler.controller";
        public const string PrefabFolder = "Assets/Prefabs/Wrestlers";
        public const string MoveTablePath = "Assets/Animation/MoveAnimationTable.asset";

        /// <summary>Animator int parameter: (int)MoveId of the current move. WrestlerView sets it every frame.</summary>
        public const string MoveParam = "Move";
        /// <summary>Animator float parameter (default 1): speed multiplier on move states only, so presentation can
        /// land the contact frame on the tuned Startup (ARCHITECTURE 4.2). WrestlerView sets it on MoveStarted, 1 otherwise.</summary>
        public const string MoveSpeedParam = "MoveSpeed";

        /// <summary>Wrestler sprite import (ARCHITECTURE 3.1): pixels per unit.</summary>
        public const float WrestlerPpu = 60f;

        /// <summary>Clips sampled from real-time video at 24 fps; keys sit on the source video frame offsets.</summary>
        public const float SourceFps = 24f;

        // Slot clips: empty clips the shared graph references; each wrestler's override controller replaces them.
        public const string SlotStandFront = "Slot_StandFront";
        public const string SlotStandBack = "Slot_StandBack";
        public const string SlotWalkFront = "Slot_WalkFront";
        public const string SlotWalkBack = "Slot_WalkBack";
        public const string SlotRun = "Slot_Run";
        public const string SlotStrike1 = "Slot_Strike1";
        public const string SlotStrike2 = "Slot_Strike2";
        public const string SlotStrike3 = "Slot_Strike3";
        // Combat-core slice slots. Stomp uses the approved stomp sheets; a stand-in (PLACEHOLDER) only without art.
        public const string SlotStomp = "Slot_Stomp";
        public const string SlotStruck = "Slot_Struck";
        /// <summary>Hit reaction B (head snap): every non-knockdown hit except a Strike 1 (D-040; presentation only).</summary>
        public const string SlotStruckB = "Slot_" + WrestlerView.StruckBStateName;
        /// <summary>Animator bool parameter: show hit reaction B (StruckB). Set by WrestlerView on each hit.</summary>
        public const string StruckBParam = "StruckB";
        /// <summary>Hit reaction C: the second hit of the combo (Strike 2, D-042). Presentation only.</summary>
        public const string SlotStruckC = "Slot_" + WrestlerView.StruckCStateName;
        /// <summary>Animator bool parameter: show hit reaction C (StruckC); wins over StruckB. Set by WrestlerView.</summary>
        public const string StruckCParam = "StruckC";
        public const string SlotKnockedDown = "Slot_KnockedDown";
        public const string SlotKnockedOut = "Slot_KnockedOut";
        public const string SlotRecoveringStandFront = "Slot_RecoveringStandFront";
        public const string SlotRecoveringStandBack = "Slot_RecoveringStandBack";
        public const string SlotRecoveringWalkFront = "Slot_RecoveringWalkFront";
        public const string SlotRecoveringWalkBack = "Slot_RecoveringWalkBack";

        // Lock-up slice slots (ARCHITECTURE 6, D-031). PLACEHOLDER stand-ins until lock-up art exists.
        public const string SlotLockUp = "Slot_" + WrestlerView.LockUpStateName;
        public const string SlotLockUpWhiff = "Slot_" + WrestlerView.LockUpWhiffStateName;
        public const string SlotLockedUp = "Slot_" + WrestlerView.LockedUpStateName;
        public const string SlotBreakLockUp = "Slot_" + WrestlerView.BreakLockUpStateName;
        public const string SlotBreakLockUpReceiver = "Slot_" + WrestlerView.BreakLockUpReceiverStateName;
        public const string SlotBodyslam = "Slot_" + WrestlerView.BodyslamStateName;
        public const string SlotBodyslammed = "Slot_" + WrestlerView.BodyslammedStateName;
        /// <summary>Irish Whip, the whipper's side (D-039). The runner plays its normal Run clip.</summary>
        public const string SlotIrishWhip = "Slot_" + WrestlerView.IrishWhipStateName;
        /// <summary>Falling Down (D-041): struck_a, fall2, fall3, then the downed sprite held. Played by the "Fall"
        /// state for a Knocked Down / Knocked Out with a fall side (WrestlerView sets the Fall parameter).</summary>
        public const string SlotFall = "Slot_" + WrestlerView.FallStateName;
        /// <summary>Animator bool parameter: this knockdown / knockout plays the fall (D-041). Set by WrestlerView.</summary>
        public const string FallParam = "Fall";
        /// <summary>Crouch (D-043): one approved sprite held. Played by the "Crouch" state; today only for the get-up at
        /// the end of a knockdown (State == KnockedDown and Crouch). A reusable state: later uses (getting back in the
        /// ring, picking up an opponent) add their own transition into it.</summary>
        public const string SlotCrouch = "Slot_" + WrestlerView.CrouchStateName;
        /// <summary>Animator bool parameter: show the crouch (D-043). Set by WrestlerView while the wrestler gets up.</summary>
        public const string CrouchParam = "Crouch";
        /// <summary>Stomp bounce (D-047): R7 (the bodyslam bounce, baked 17 px lift) held, then back to the lying pose.
        /// Played by the "StompBounce" state, which WrestlerView starts from HitLanded(Stomp) on a downed wrestler.</summary>
        public const string SlotStompBounce = "Slot_" + WrestlerView.StompBounceStateName;
        /// <summary>Animator bool parameter: the stomp bounce is playing (D-047). Set by WrestlerView; the Any State -&gt;
        /// KnockedDown / KnockedOut / Fall transitions wait for it to be false.</summary>
        public const string StompBounceParam = "StompBounce";
        /// <summary>D-047 Starting point: R7 held 150 ms (the bodyslam tail's R7 hold, D-044/D-045). Tunable in FrameTiming.</summary>
        public const float StompBounceDefaultHoldMs = 150f;

        /// <summary>Animator bool parameter: in a paired move (set by WrestlerView from PairedMoveStarted). Tells the
        /// Lock Up hold from a whiff, which share State = LockUp and Move = LockUp.</summary>
        public const string PairedParam = "Paired";

        /// <summary>Animator bool parameter: Recovering walk (true) or stand (false). Set by WrestlerView from position change.</summary>
        public const string MovingParam = "Moving";
        public const string RecoveringStandState = "RecoveringStand";
        public const string RecoveringWalkState = "RecoveringWalk";

        /// <summary>One non-looping move clip: sprites, their key times and the clip length in SourceFps frames,
        /// and which sprite is the contact pose.</summary>
        public sealed class MoveClipSpec
        {
            public Sprite[] Sprites;
            public int[] KeyFrames;
            public int LengthFrames;
            public int ContactIndex;
            /// <summary>Optional built-in Hold (ms) per sprite, used instead of the SourceFps frame spacing when the
            /// default timing is not a whole number of 24 fps frames (Irish Whip: the combat frame times, D-039).
            /// KeyFrames / LengthFrames then only give the step structure.</summary>
            public float[] DefaultHoldsMs;
            /// <summary>Optional sprite index where the art turns to face a second direction (Irish Whip f2, D-039);
            /// recorded as MoveAnimationTable TurnTime. -1 = none.</summary>
            public int TurnIndex = -1;
            /// <summary>Built-in default only; the built clip's contact time comes from the WrestlerFrameTiming asset.</summary>
            public float ContactTime => KeyFrames[ContactIndex] / SourceFps;
        }

        /// <summary>One wrestler's art for this slice. Null back-facing art = placeholder built from the front art.</summary>
        public sealed class WrestlerArtSpec
        {
            public string Name;
            public bool ArtFacesLeft = true;
            public Sprite StandFront;
            public Sprite StandBack;                 // null -> placeholder (StandFront)
            public Sprite[] WalkFront;
            public int[] WalkKeyFrames;              // key time of each sprite, in SourceFps frames
            public int WalkLoopFrames;               // loop length in SourceFps frames
            public Sprite[] WalkBack;                // null -> placeholder (WalkFront timing)
            public int[] WalkBackKeyFrames;
            public int WalkBackLoopFrames;
            public Sprite[] Run;                     // null -> placeholder (WalkFront at double speed)
            public int[] RunKeyFrames;
            public int RunLoopFrames;
            public MoveClipSpec Strike1;             // right punch; null -> placeholder (StandFront pose)
            public MoveClipSpec Strike2;             // left punch; null -> placeholder
            public MoveClipSpec Strike3;             // combo finisher; null -> placeholder
            public MoveClipSpec Stomp;               // stomp on a downed opponent; null -> placeholder (StandFront pose)
            public Sprite StruckA;                   // hit reaction A (lean back); null (either) -> placeholder for both
            public Sprite StruckB;                   // hit reaction B (head snap), every non-knockdown hit but Strike 1
            public Sprite StruckC;                   // hit reaction C, combo hit 2 (Strike 2); null -> the StruckB clip
            public Sprite[] LockUp;                  // Lock Up hold (A, initiator); null -> placeholder
            public Sprite[] LockedUp;                // Locked Up hold (B, receiver); null -> placeholder
            public MoveClipSpec Bodyslam;            // Bodyslam (A, Grappling); contact = impact frame; null -> LockUp hold stand-in
            public MoveClipSpec Bodyslammed;         // Bodyslammed (B, Grappled); contact = impact frame; null -> LockedUp hold stand-in
            public MoveClipSpec IrishWhip;           // Irish Whip whipper (A, Grappling, D-039); contact = release (f4); turn = f2; null -> LockUp hold stand-in
            public MoveClipSpec Fall;                // Falling Down (D-041): struck_a, fall2, fall3, downed; turn = downed (landing); null -> downed-only PLACEHOLDER
            public Sprite Crouch;                    // Crouch (D-043), the get-up; null -> PLACEHOLDER (StandFront pose)
            public Sprite StompBounce;               // Stomp bounce (D-047): R7 of the bodyslam; null -> PLACEHOLDER (downed sprite)
            /// <summary>D-044: the downed sprite's pivot is the body centre (Peaux's v5 R6), not a bodyslam pair root, so
            /// after a fall it is moved toward the fall side until its feet end meets fall3's (WrestlerView
            /// fallLandingShift). False keeps the accepted D-041 landing (Barry): shift 0.</summary>
            public bool AlignFallLandingToFeet;
        }

        public const string RebuildMenuPath = "Rural Rumble/Rebuild Wrestler Animations";

        /// <summary>
        /// Designer rebuild: rewrites both wrestlers' clips, override controllers and MoveAnimationTable contact
        /// times from their WrestlerFrameTiming assets (Hold (ms) per sprite). Prefabs, sprites, import settings
        /// and the Animator graph are left as they are. Not available in Play Mode.
        /// </summary>
        [MenuItem(RebuildMenuPath)]
        public static void RebuildWrestlerAnimationsMenu() => Debug.Log(RebuildWrestlerAnimations());

        [MenuItem(RebuildMenuPath, true)]
        private static bool RebuildWrestlerAnimationsValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static string RebuildWrestlerAnimations()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before rebuilding wrestler animations.");
            AssetDatabase.SaveAssets(); // pending Inspector edits to the timing assets
            return BuildBarry(writePrefab: false) + "\n" + BuildPeaux(writePrefab: false);
        }

        [MenuItem("Rural Rumble/Tooling/Build Barry Animations + Prefab")]
        public static void BuildBarryMenu() => BuildBarry();

        /// <summary>Even walk timing (designer default, 2026-10-31): 8 sprites, 3 frames (125 ms) each, 1.000 s loop.</summary>
        private static readonly int[] EvenWalkKeyFrames = { 0, 3, 6, 9, 12, 15, 18, 21 };
        private const int EvenWalkLoopFrames = 24;

        /// <param name="writePrefab">False rebuilds clips and the override only, leaving the prefab untouched.</param>
        public static string BuildBarry(bool writePrefab = true)
        {
            const string art = "Assets/Art/Wrestlers/Barry/";
            var spec = new WrestlerArtSpec
            {
                Name = "Barry",
                ArtFacesLeft = true,
                StandFront = LoadSprites(art + "barry_stand_front.png").Single(),
                StandBack = LoadSprites(art + "barry_stand_back.png").Single(),
                // Source video frames 53,56,59,62,66,69,72,75 (ASSET_REGISTRY gen-barry-walk-front-8), retimed evenly.
                WalkFront = LoadSprites(art + "barry_walk_front.png"),
                WalkKeyFrames = EvenWalkKeyFrames,
                WalkLoopFrames = EvenWalkLoopFrames,
                // Source video frames 37,42,46,51,55,60,65,69 (ASSET_REGISTRY gen-barry-walk-back-v4-8), retimed evenly.
                WalkBack = LoadSprites(art + "barry_walk_back.png"),
                WalkBackKeyFrames = EvenWalkKeyFrames,
                WalkBackLoopFrames = EvenWalkLoopFrames,
                Run = LoadSprites(art + "barry_run.png"),
                // Video frames 32-42 every 2nd (ASSET_REGISTRY gen-barry-run-6). Loop 13 keeps the clip as first built
                // (last sprite held 3 frames, the others 2); 12 would make it even. Not retimed without a designer call.
                RunKeyFrames = new[] { 0, 2, 4, 6, 8, 10 },
                RunLoopFrames = 13,
                // Designer timing (game speed) 130/100/130 ms, contact = frame 3. Rounded per frame to 24 fps:
                // 3/2/3 samples = 125/83.3/125 ms, 333 ms total; contact at sample 5 (208.3 ms).
                Strike1 = new MoveClipSpec
                {
                    Sprites = LoadSprites(art + "barry_strike1.png"),
                    KeyFrames = new[] { 0, 3, 5 }, LengthFrames = 8, ContactIndex = 2,
                },
                Strike2 = new MoveClipSpec
                {
                    Sprites = LoadSprites(art + "barry_strike2.png"),
                    KeyFrames = new[] { 0, 3, 5 }, LengthFrames = 8, ContactIndex = 2,
                },
                // Designer timing 130/230 ms, contact = frame 2. Rounded: 3/6 samples = 125/250 ms, 375 ms total;
                // contact at sample 3 (125 ms).
                Strike3 = new MoveClipSpec
                {
                    Sprites = LoadSprites(art + "barry_strike3.png"),
                    KeyFrames = new[] { 0, 3 }, LengthFrames = 9, ContactIndex = 1,
                },
                // gen-barry-stomp-A-2: designer timing 200/400 ms, contact = frame 2 (boot planted). See StompKeyFrames.
                Stomp = new MoveClipSpec
                {
                    Sprites = LoadSprites(art + "barry_stomp.png"),
                    KeyFrames = StompKeyFrames, LengthFrames = StompLengthFrames, ContactIndex = StompContactIndex,
                },
                // gen-barry-struck2-B-1 (lean back) and gen-barry-struck3-B-1 (head snap), imported by ImportStruckSheets.
                StruckA = LoadSprites(art + "barry_struck_a.png").Single(),
                StruckB = LoadSprites(art + "barry_struck_b.png").Single(),
                // gen-barry-struck-c-E-1 (combo hit 2), imported by ImportStruckSheets.
                StruckC = LoadSprites(art + "barry_struck_c.png").Single(),
                // D-032: gen-barry-lockup-5 / gen-barry-lockedup-5 (pair sheets vs Peaux), imported by ImportLockUpSheets.
                LockUp = LoadSprites(art + "barry_lockup.png"),
                LockedUp = LoadSprites(art + "barry_lockedup.png"),
                // D-044: Barry slams Peaux is the v5 throw (barry_bodyslam5_1..5 + his stand, set below). Peaux slams
                // Barry is the same throw since 2026-11-17 (barry_bodyslammed5_1..7, R1-R7 then R6); the D-033 v3 sheet
                // barry_bodyslammed.png stays in the project, unused.
                Bodyslammed = BarryThrowSlammedSpec(),
                // R6 is centred on the body (like Peaux's), so the fall lands it feet-aligned (fallLandingShift).
                AlignFallLandingToFeet = true,
                // D-039: gen-barry-irishwhip-4 (Barry whips, 4 frames), imported by ImportIrishWhipSheets.
                IrishWhip = IrishWhipSpec(LoadSprites(art + "barry_irishwhip.png"), RuralRumble.Combat.IrishWhipChoreography.BarryStaged),
            };
            // D-044: S1-S5 (barry_bodyslam5_1..5, imported here from bodyslam5_cells.json if needed), then his stand.
            spec.Bodyslam = BarryThrowSlamSpec(spec.StandFront);
            // D-041: gen-barry-fall2-D-1 / gen-barry-fall3-B-1 (barry_fall2.png, barry_fall3.png).
            spec.Fall = FallSpec(art, "barry", spec);
            // D-043: barry_crouch.png (186x175, root at (93,175) = Bottom Center), drawn at the root.
            spec.Crouch = CrouchSprite(art + "barry_crouch.png");
            // D-047: barry_bodyslammed5_7.png (R7, the bounce), same import / pivot as his downed R6.
            spec.StompBounce = ThrowSprite(BarryThrowSlammedArt, 7);
            return Build(spec, writePrefab);
        }

        [MenuItem("Rural Rumble/Tooling/Build Peaux Animations + Prefab")]
        public static void BuildPeauxMenu() => BuildPeaux();

        /// <summary>
        /// Peaux (approved 2026-10-31: gen-peaux-stand-front, gen-peaux-walk-front-8, gen-peaux-stand-back-a,
        /// gen-peaux-walk-back-8, gen-peaux-run-6, gen-peaux-strike1-3, gen-peaux-strike2-3, gen-peaux-strike3-2).
        /// Stomp uses gen-peaux-stomp-2; the other combat-core slots are PLACEHOLDER stand-ins built from those sprites. The earlier
        /// Peaux_StandBack / WalkBack / Run / Strike1-3 _PLACEHOLDER clips are no longer referenced by the override
        /// (left for the designer to delete).
        /// </summary>
        /// <param name="writePrefab">False rebuilds clips and the override only, leaving the prefab untouched.</param>
        public static string BuildPeaux(bool writePrefab = true)
        {
            const string art = "Assets/Art/Wrestlers/Peaux/";
            var spec = new WrestlerArtSpec
            {
                Name = "Peaux",
                ArtFacesLeft = true,
                StandFront = LoadSprites(art + "peaux_stand_front.png").Single(),
                StandBack = LoadSprites(art + "peaux_stand_back.png").Single(),
                // Source video frames 25,28,31,34,37,40,43,46, loop 25..49 (ASSET_REGISTRY gen-peaux-walk-front-8):
                // a key every 3 source frames, 24-frame loop = the even walk timing.
                WalkFront = LoadSprites(art + "peaux_walk_front.png"),
                WalkKeyFrames = EvenWalkKeyFrames,
                WalkLoopFrames = EvenWalkLoopFrames,
                // Source video frames 25,28,32,35,39,42,46,49, loop 25..53 (ASSET_REGISTRY gen-peaux-walk-back-8:
                // offsets 0,3,7,10,14,17,21,24, 28-frame period), retimed evenly like Barry's back walk.
                WalkBack = LoadSprites(art + "peaux_walk_back.png"),
                WalkBackKeyFrames = EvenWalkKeyFrames,
                WalkBackLoopFrames = EvenWalkLoopFrames,
                Run = LoadSprites(art + "peaux_run.png"),
                // Video frames 60-70 every 2nd, 12-frame loop (ASSET_REGISTRY gen-peaux-run-6): 2 frames per sprite,
                // 0.5 s. Barry's run keeps a legacy 13-frame loop (see BuildBarry); Peaux uses the source period.
                RunKeyFrames = new[] { 0, 2, 4, 6, 8, 10 },
                RunLoopFrames = 12,
                // Same frame roles and timing as Barry's strikes (designer timing 130/100/130 ms and 130/230 ms,
                // rounded to 24 fps; D-024 contact 0.208 / 0.208 / 0.125 s). gen-peaux-strike1-3/strike2-3/strike3-2.
                Strike1 = new MoveClipSpec
                {
                    Sprites = LoadSprites(art + "peaux_strike1.png"),
                    KeyFrames = new[] { 0, 3, 5 }, LengthFrames = 8, ContactIndex = 2,
                },
                Strike2 = new MoveClipSpec
                {
                    Sprites = LoadSprites(art + "peaux_strike2.png"),
                    KeyFrames = new[] { 0, 3, 5 }, LengthFrames = 8, ContactIndex = 2,
                },
                Strike3 = new MoveClipSpec
                {
                    Sprites = LoadSprites(art + "peaux_strike3.png"),
                    KeyFrames = new[] { 0, 3 }, LengthFrames = 9, ContactIndex = 1,
                },
                // gen-peaux-stomp-2: same frame roles and timing as Barry's stomp.
                Stomp = new MoveClipSpec
                {
                    Sprites = LoadSprites(art + "peaux_stomp.png"),
                    KeyFrames = StompKeyFrames, LengthFrames = StompLengthFrames, ContactIndex = StompContactIndex,
                },
                // gen-peaux-struck-a-1 (lean back) and gen-peaux-struck-b-1 (head snap), imported by ImportStruckSheets.
                StruckA = LoadSprites(art + "peaux_struck_a.png").Single(),
                StruckB = LoadSprites(art + "peaux_struck_b.png").Single(),
                // gen-peaux-struck-c-A-2 (combo hit 2), imported by ImportStruckSheets.
                StruckC = LoadSprites(art + "peaux_struck_c.png").Single(),
                // D-032: gen-peaux-lockup-5 / gen-peaux-lockedup-5 (pair sheets vs Barry), imported by ImportLockUpSheets.
                LockUp = LoadSprites(art + "peaux_lockup.png"),
                LockedUp = LoadSprites(art + "peaux_lockedup.png"),
                // D-044: Barry slams Peaux is the v5 throw (peaux_bodyslammed5_1..7). Peaux slams Barry is the same
                // throw since 2026-11-17 (peaux_bodyslam5_1..5 + his stand, set below). The v3 sheets peaux_bodyslam.png
                // and peaux_bodyslammed.png stay in the project, unused.
                Bodyslammed = PeauxThrowSlammedSpec(),
                // D-044: R6 is centred on the body, unlike the D-033 receiver sprites, so the fall lands it feet-aligned.
                AlignFallLandingToFeet = true,
                // D-039: gen-peaux-irishwhip-4 (Peaux whips, 4 frames), imported by ImportIrishWhipSheets.
                IrishWhip = IrishWhipSpec(LoadSprites(art + "peaux_irishwhip.png"), RuralRumble.Combat.IrishWhipChoreography.PeauxStaged),
            };
            // Peaux slams Barry throw (2026-11-17): S1-S5 (peaux_bodyslam5_1..5), then his stand.
            spec.Bodyslam = PeauxThrowSlamSpec(spec.StandFront);
            // D-041: gen-peaux-fall2-D-1 / gen-peaux-fall3-C-1 (peaux_fall2.png, peaux_fall3.png).
            spec.Fall = FallSpec(art, "peaux", spec);
            // D-043: peaux_crouch.png (192x145, root at (96,145) = Bottom Center), drawn at the root.
            spec.Crouch = CrouchSprite(art + "peaux_crouch.png");
            // D-047: peaux_bodyslammed5_7.png (R7, the bounce), same import / pivot as his downed R6.
            spec.StompBounce = ThrowSprite(PeauxThrowSlammedArt, 7);
            return Build(spec, writePrefab);
        }

        /// <summary>Falling Down default holds (D-041, designer): struck_a 100, fall2 120, fall3 130 ms (0.35 s, the
        /// knockdown slide's default length), then the downed sprite. The downed hold only sets the clip length (the
        /// last sprite is held for the rest of the knockdown either way); 500 ms like the Knocked Down clip.</summary>
        public static readonly float[] FallDefaultHoldsMs = { 100f, 120f, 130f, 500f };
        /// <summary>Sprite index of the landing (downed sprite) in the fall clip: recorded as its MoveAnimationTable
        /// TurnTime, where WrestlerView switches from the fall frames' orientation to the downed sprite's.</summary>
        public const int FallLandIndex = 3;

        /// <summary>
        /// The Falling Down clip (D-041) for a wrestler whose fall art exists: {prefix}_struck_a, {prefix}_fall2,
        /// {prefix}_fall3 (single sprites, stored head on the RIGHT, attacker on the left), then the downed sprite (last
        /// Bodyslammed sprite, stored head on the LEFT). Returns null (downed-only PLACEHOLDER) while either fall PNG is
        /// missing, so a wrestler's art is picked up as soon as both files are in their art folder. A fall PNG that is
        /// not yet imported with the wrestler settings is imported here (whole image = one cell, pivot Bottom Center).
        /// </summary>
        private static MoveClipSpec FallSpec(string artFolder, string prefix, WrestlerArtSpec spec)
        {
            string fall2 = artFolder + prefix + "_fall2.png", fall3 = artFolder + prefix + "_fall3.png";
            if (AssetImporter.GetAtPath(fall2) == null || AssetImporter.GetAtPath(fall3) == null)
                return null;
            if (spec.StruckA == null || spec.Bodyslammed == null)
                return null;
            EnsureSingleSpriteImport(fall2);
            EnsureSingleSpriteImport(fall3);
            return new MoveClipSpec
            {
                Sprites = new[]
                {
                    spec.StruckA, LoadSprites(fall2).Single(), LoadSprites(fall3).Single(),
                    spec.Bodyslammed.Sprites[spec.Bodyslammed.Sprites.Length - 1],
                },
                KeyFrames = new[] { 0, 1, 2, 3 }, LengthFrames = 4, ContactIndex = 0, TurnIndex = FallLandIndex,
                DefaultHoldsMs = (float[])FallDefaultHoldsMs.Clone(),
            };
        }

        /// <summary>
        /// Imports a one-frame wrestler sprite PNG (whole image = one cell, ImportSheet settings: PPU 60, Point,
        /// uncompressed, pivot Bottom Center) unless it already has exactly those settings. Used for the fall frames.
        /// </summary>
        public static string EnsureSingleSpriteImport(string texturePath)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"No TextureImporter at {texturePath}.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToArray();
            bool ok = importer.textureType == TextureImporterType.Sprite &&
                      importer.spriteImportMode == SpriteImportMode.Multiple &&
                      Mathf.Approximately(importer.spritePixelsPerUnit, WrestlerPpu) &&
                      importer.filterMode == FilterMode.Point &&
                      importer.textureCompression == TextureImporterCompression.Uncompressed &&
                      !importer.mipmapEnabled &&
                      sprites.Length == 1 &&
                      Mathf.Approximately(sprites[0].rect.width, width) && Mathf.Approximately(sprites[0].rect.height, height) &&
                      Mathf.Approximately(sprites[0].pivot.x, width / 2f) && Mathf.Approximately(sprites[0].pivot.y, 0f);
            return ok ? $"{texturePath}: already imported." : ImportSheet(texturePath, width, height);
        }

        /// <summary>Imports every wrestler's fall frames that exist (D-041; Barry 242x172 / 332x164, one sprite each,
        /// pivot Bottom Center = the root on the ground). The builders also do this on their own.</summary>
        public static string ImportFallSheets()
        {
            var lines = new List<string>();
            foreach (string path in new[]
                     {
                         "Assets/Art/Wrestlers/Barry/barry_fall2.png", "Assets/Art/Wrestlers/Barry/barry_fall3.png",
                         "Assets/Art/Wrestlers/Peaux/peaux_fall2.png", "Assets/Art/Wrestlers/Peaux/peaux_fall3.png",
                     })
                lines.Add(AssetImporter.GetAtPath(path) == null ? $"{path}: not present." : EnsureSingleSpriteImport(path));
            return string.Join("\n", lines);
        }

        /// <summary>
        /// The Crouch sprite (D-043) for a wrestler, imported with the wrestler settings first if needed (whole image =
        /// one cell, PPU 60, Point, uncompressed, pivot Bottom Center = the root). Null (PLACEHOLDER clip) while the PNG
        /// is missing.
        /// </summary>
        private static Sprite CrouchSprite(string texturePath)
        {
            if (AssetImporter.GetAtPath(texturePath) == null)
                return null;
            EnsureSingleSpriteImport(texturePath);
            return LoadSprites(texturePath).Single();
        }

        /// <summary>Imports both wrestlers' crouch sprites (D-043; Barry 186x175, Peaux 192x145, one sprite each, pivot
        /// Bottom Center = the root on the ground). The builders also do this on their own.</summary>
        public static string ImportCrouchSheets()
        {
            var lines = new List<string>();
            foreach (string path in new[] { "Assets/Art/Wrestlers/Barry/barry_crouch.png", "Assets/Art/Wrestlers/Peaux/peaux_crouch.png" })
                lines.Add(AssetImporter.GetAtPath(path) == null ? $"{path}: not present." : EnsureSingleSpriteImport(path));
            return string.Join("\n", lines);
        }

        /// <summary>
        /// The whipper's Irish Whip clip (D-039): f1 push start, f2 turn, f3 final push, f4 follow-through, played once
        /// with f4 held. Built-in holds = the staged combat frame times (frame1 / frame2 / frame3 / followThrough: Barry
        /// 120 / 100 / 120 / 330 ms, Peaux 120 / 100 / 120 / 250 ms), because Combat keys the runner's root to those
        /// times. Contact (HIT) = f4, the release, so WrestlerView contact-scales the clip onto the whip's Startup
        /// (f1 + f2 + f3); turn = f2 (WrestlerView draws f2-f4 facing the whip direction).
        /// </summary>
        private static MoveClipSpec IrishWhipSpec(Sprite[] sprites, RuralRumble.Combat.IrishWhipChoreography staged) =>
            new MoveClipSpec
            {
                Sprites = sprites,
                KeyFrames = new[] { 0, 1, 2, 3 }, LengthFrames = 4, ContactIndex = 3, TurnIndex = 1,
                DefaultHoldsMs = new[]
                {
                    staged.frame1 * 1000f, staged.frame2 * 1000f, staged.frame3 * 1000f, staged.followThrough * 1000f,
                },
            };

        /// <summary>
        /// Imports the approved Irish Whip whipper sheets (D-039, ASSET_REGISTRY gen-barry-irishwhip-4 /
        /// gen-peaux-irishwhip-4): barry_irishwhip 4 x 288x196, peaux_irishwhip 4 x 258x178, wrestler settings, pivot
        /// Bottom Center. Each frame is packed so the rear boot centre sits where it does in the wrestler's stand_front
        /// (39 / 33 px right of the cell centre), so the plain bottom-centre pivot is the stand_front convention and
        /// the one the staged runner offsets were measured with (cell centre to cell centre). Run before the builders.
        /// </summary>
        public static string ImportIrishWhipSheets()
        {
            return string.Join("\n", new[]
            {
                ImportSheet("Assets/Art/Wrestlers/Barry/barry_irishwhip.png", 288, 196),
                ImportSheet("Assets/Art/Wrestlers/Peaux/peaux_irishwhip.png", 258, 178),
            });
        }

        /// <summary>Imports Peaux's approved sheets with the wrestler settings: stand front 102x215 and back 92x214
        /// (one sprite each), walk front 8 x 114x219, walk back 8 x 108x222, run 6 x 158x207, strikes 3 x 222x212,
        /// 3 x 212x223 and 2 x 150x214, stomp 2 x 174x214.</summary>
        public static string ImportPeauxSheets()
        {
            const string art = "Assets/Art/Wrestlers/Peaux/";
            return string.Join("\n", new[]
            {
                ImportSheet(art + "peaux_stand_front.png", 102, 215),
                ImportSheet(art + "peaux_walk_front.png", 114, 219),
                ImportSheet(art + "peaux_stand_back.png", 92, 214),
                ImportSheet(art + "peaux_walk_back.png", 108, 222),
                ImportSheet(art + "peaux_run.png", 158, 207),
                ImportSheet(art + "peaux_strike1.png", 222, 212),
                ImportSheet(art + "peaux_strike2.png", 212, 223),
                ImportSheet(art + "peaux_strike3.png", 150, 214),
                ImportSheet(art + "peaux_stomp.png", 174, 214),
            });
        }

        /// <summary>
        /// Imports Barry's strike sheets (approved 2026-10-31) and stomp sheet (gen-barry-stomp-A-2) with the wrestler
        /// settings and slices them by their own cell sizes (ASSET_REGISTRY / designer: 296x243, 236x242, 200x256;
        /// stomp 172x256).
        /// </summary>
        public static string ImportBarryStrikeSheets()
        {
            const string art = "Assets/Art/Wrestlers/Barry/";
            return string.Join("\n", new[]
            {
                ImportSheet(art + "barry_strike1.png", 296, 243),
                ImportSheet(art + "barry_strike2.png", 236, 242),
                ImportSheet(art + "barry_strike3.png", 200, 256),
                ImportSheet(art + "barry_stomp.png", 172, 256),
            });
        }

        /// <summary>Distance between the two cell bottom-centres (hips) the lock-up pair sheets are drawn for:
        /// 132 px = 2.2 units at 60 PPU (ASSET_REGISTRY gen-*-lockup-5 / gen-*-lockedup-5).</summary>
        public const int LockUpArtPairSpacingPx = 132;

        /// <summary>
        /// D-032: how far each lock-up sprite's pivot sits toward the opponent from bottom-centre, in native pixels:
        /// (art spacing - combat lockUpPairSpacing * PPU) / 2, rounded to a whole pixel. With the project tuning
        /// (1.2 units = 72 px) that is 30 px, so roots 1.2 units apart draw the hips 2.2 units apart. Reads the
        /// combat tuning asset only; presentation never changes the spacing.
        /// </summary>
        public static int LockUpPivotShiftPx()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<RuralRumble.Combat.CombatTuning>("Assets/Data/Tuning/CombatTuning.asset");
            if (tuning == null)
                throw new InvalidOperationException("Assets/Data/Tuning/CombatTuning.asset not found; cannot place lock-up pivots.");
            return Mathf.RoundToInt((LockUpArtPairSpacingPx - tuning.lockUpPairSpacing * WrestlerPpu) / 2f);
        }

        /// <summary>
        /// Imports the approved lock-up pair sheets (D-032; 3 frames each, own cell sizes from ASSET_REGISTRY):
        /// barry_lockup 228x251, barry_lockedup 208x230, peaux_lockup 264x242, peaux_lockedup 220x228. Same wrestler
        /// settings as every sheet, except the pivot is moved LockUpPivotShiftPx toward the opponent (-x, as the art
        /// faces left). SpriteRenderer.flipX mirrors around the pivot, so a flipped sprite's offset still points at
        /// the opponent. Run before the Barry / Peaux builders.
        /// </summary>
        public static string ImportLockUpSheets()
        {
            int shift = LockUpPivotShiftPx();
            return string.Join("\n", new[]
            {
                ImportSheet("Assets/Art/Wrestlers/Barry/barry_lockup.png", 228, 251, shift),
                ImportSheet("Assets/Art/Wrestlers/Barry/barry_lockedup.png", 208, 230, shift),
                ImportSheet("Assets/Art/Wrestlers/Peaux/peaux_lockup.png", 264, 242, shift),
                ImportSheet("Assets/Art/Wrestlers/Peaux/peaux_lockedup.png", 220, 228, shift),
            }) + $"\nLock-up pivot shift: {shift} px toward the opponent.";
        }

        /// <summary>
        /// Imports the approved bodyslam pair sheets (D-033, ASSET_REGISTRY gen-*-bodyslam(med)-3), each by its own
        /// cell size: barry_bodyslam 7 x 298x285, peaux_bodyslammed 7 x 496x401 (Barry slams Peaux), peaux_bodyslam
        /// 6 x 360x266, barry_bodyslammed 6 x 526x322 (Peaux slams Barry). The pair art is drawn with pivots 132 px
        /// apart, like the lock-up, so every sheet gets the same LockUpPivotShiftPx toward the opponent (the pair
        /// starts from the lock-up positions). The receivers' registry pivots sit above the cell bottom (Peaux 4 px,
        /// Barry 1 px), kept as drawn. All four are wider than 2048, so ImportSheet raises the max texture size to
        /// 4096 (never downscaled). Run before the Barry / Peaux builders.
        /// </summary>
        public static string ImportBodyslamSheets()
        {
            int shift = LockUpPivotShiftPx();
            return string.Join("\n", new[]
            {
                ImportSheet("Assets/Art/Wrestlers/Barry/barry_bodyslam.png", 298, 285, shift),
                ImportSheet("Assets/Art/Wrestlers/Peaux/peaux_bodyslammed.png", 496, 401, shift, 4),
                ImportSheet("Assets/Art/Wrestlers/Peaux/peaux_bodyslam.png", 360, 266, shift),
                ImportSheet("Assets/Art/Wrestlers/Barry/barry_bodyslammed.png", 526, 322, shift, 1),
            }) + $"\nBodyslam pivot shift: {shift} px toward the opponent.";
        }

        // ---------- D-044 bodyslam throw (Barry slams Peaux, v5) ----------

        /// <summary>Cell geometry, pivots and flip rules of the v5 throw frames (Art Wrangler, D-044).</summary>
        public const string BodyslamThrowCellsPath = "Assets/Art/Wrestlers/bodyslam5_cells.json";
        /// <summary>barry_bodyslam5_{1..5}.png = these cells.json entries (its promoted_files map).</summary>
        public static readonly string[] BarryThrowSlamCells = { "S1c", "S2c", "S3", "S4", "S5" };
        /// <summary>peaux_bodyslammed5_{1..7}.png = these cells.json entries.</summary>
        public static readonly string[] PeauxThrowSlammedCells = { "R1c", "R2c", "R3", "R4", "R5", "R6", "R7" };
        public const string BarryThrowSlamArt = "Assets/Art/Wrestlers/Barry/barry_bodyslam5_";
        public const string PeauxThrowSlammedArt = "Assets/Art/Wrestlers/Peaux/peaux_bodyslammed5_";

        /// <summary>Peaux slams Barry, same 12-frame throw structure (designer-approved 2026-11-17; Art Wrangler's
        /// bodyslam_pb_cells_final.json, copied here). Its pivots already include the D-044 follow-up (S3-S5 = the
        /// feet, R3 = the lift pivot moved with S3), so they are used as written.</summary>
        public const string PeauxThrowCellsPath = "Assets/Art/Wrestlers/bodyslam_pb_cells.json";
        public static readonly string[] PeauxThrowSlamCells = { "S1", "S2", "S3", "S4", "S5" };
        public static readonly string[] BarryThrowSlammedCells = { "R1", "R2", "R3", "R4", "R5", "R6", "R7" };
        public const string PeauxThrowSlamArt = "Assets/Art/Wrestlers/Peaux/peaux_bodyslam5_";
        public const string BarryThrowSlammedArt = "Assets/Art/Wrestlers/Barry/barry_bodyslammed5_";

        /// <summary>D-044 Starting point: S1 120, S2 120, S3 180, S4 100, S5 100 ms, then Barry's stand. The stand's hold
        /// only sets the clip length (Barry is Standing from the landing on); its start is the landing (0.62 s).</summary>
        public static readonly float[] ThrowSlamDefaultHoldsMs = { 120f, 120f, 180f, 100f, 100f, 100f };
        /// <summary>D-044 Starting point: R1-R5 = S1-S5's holds, R6 100 (impact), R7 150 (bounce), then R6 held. The last
        /// hold only sets the clip length: the end of the landing tail, after which the Knocked Down clip (same R6) shows.</summary>
        public static readonly float[] ThrowSlammedDefaultHoldsMs = { 120f, 120f, 180f, 100f, 100f, 100f, 150f, 100f };
        /// <summary>Sprite index of the turn: S3 (Barry turns to the throw direction) / R4 (the release: from here the
        /// receiver's frames are stored for a throw to screen-right). Recorded as MoveAnimationTable TurnTime.</summary>
        public const int ThrowSlamTurnIndex = 2;
        public const int ThrowSlammedTurnIndex = 3;
        /// <summary>Sprite index of the landing (contact): Barry's stand / R6, both at 0.62 s with the defaults.</summary>
        public const int ThrowSlamContactIndex = 5;
        public const int ThrowSlammedContactIndex = 5;

        /// <summary>One cells.json entry: cell size, pivot (px from the cell's left edge, ground = the bottom row) and
        /// the pair pivot shift it already contains (pivot_dx).</summary>
        public struct ThrowCell
        {
            public int Width, Height, PivotFromLeft, PivotDx;
        }

        /// <summary>Reads one entry of <see cref="BodyslamThrowCellsPath"/> (flat JSON objects; no Unity JSON dictionary
        /// support, so each field is read by name).</summary>
        public static ThrowCell ReadThrowCell(string key) => ReadThrowCell(BodyslamThrowCellsPath, key);

        /// <summary>Reads one entry of a throw cells.json (<see cref="BodyslamThrowCellsPath"/> or
        /// <see cref="PeauxThrowCellsPath"/>).</summary>
        public static ThrowCell ReadThrowCell(string cellsPath, string key)
        {
            string text = System.IO.File.ReadAllText(cellsPath);
            var block = System.Text.RegularExpressions.Regex.Match(text,
                "\"" + System.Text.RegularExpressions.Regex.Escape(key) + "\"\\s*:\\s*\\{(?<b>[^{}]*)\\}");
            if (!block.Success)
                throw new InvalidOperationException($"{cellsPath}: no entry \"{key}\".");
            string b = block.Groups["b"].Value;
            int Field(string name)
            {
                var m = System.Text.RegularExpressions.Regex.Match(b, "\"" + name + "\"\\s*:\\s*(-?\\d+)");
                if (!m.Success)
                    throw new InvalidOperationException($"{cellsPath} \"{key}\": no {name}.");
                return int.Parse(m.Groups[1].Value);
            }
            var cell = System.Text.RegularExpressions.Regex.Match(b, "\"cell\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)");
            if (!cell.Success)
                throw new InvalidOperationException($"{cellsPath} \"{key}\": no cell.");
            return new ThrowCell
            {
                Width = int.Parse(cell.Groups[1].Value), Height = int.Parse(cell.Groups[2].Value),
                PivotFromLeft = Field("pivot_px_from_left"), PivotDx = Field("pivot_dx"),
            };
        }

        /// <summary>
        /// The pivot (px from the left edge, stored orientation) each throw frame is imported with (D-044):
        /// <list type="bullet">
        /// <item>S1, S2, R1, R2, R4-R7: as in cells.json. S1/S2/R1/R2 keep the lock-up 30 px pair shift (D-032/D-033), so
        /// the lift lines up with the lock-up; R4-R7 are bottom centre = the receiver's body centre on the ground.</item>
        /// <item>S3-S5: the feet centre (cells.json pivot minus its pivot_dx): "no foot jump" (designer). Barry's feet go
        /// back to his root at S3, where he turns anyway, so S4, S5 and the stand after them do not jump.</item>
        /// <item>R3: moved by the same amount as S3 (both are stored facing left and flip together), so Peaux stays on
        /// Barry's hands overhead.</item>
        /// </list>
        /// </summary>
        public static int ThrowPivotFromLeft(string key)
        {
            ThrowCell c = ReadThrowCell(key);
            switch (key)
            {
                case "S3": case "S4": case "S5":
                    return c.PivotFromLeft - c.PivotDx;
                case "R3":
                    return c.PivotFromLeft - ReadThrowCell("S3").PivotDx;
                default:
                    return c.PivotFromLeft;
            }
        }

        /// <summary>
        /// Imports the 24 throw PNGs (12 D-044 Barry slams Peaux + 12 Peaux slams Barry; one cell each, whole image) with the wrestler settings (PPU 60, Point,
        /// uncompressed, no mipmaps, Full Rect) and the pivots of <see cref="ThrowPivotFromLeft"/>, ground on the bottom
        /// row. Skips a file that already has exactly those settings. The builders call it on their own.
        /// </summary>
        public static string ImportBodyslamThrowSprites()
        {
            var lines = new List<string>();
            for (int i = 0; i < BarryThrowSlamCells.Length; i++)
                lines.Add(EnsureThrowSpriteImport(BarryThrowSlamArt + (i + 1) + ".png", BarryThrowSlamCells[i]));
            for (int i = 0; i < PeauxThrowSlammedCells.Length; i++)
                lines.Add(EnsureThrowSpriteImport(PeauxThrowSlammedArt + (i + 1) + ".png", PeauxThrowSlammedCells[i]));
            // Peaux slams Barry (2026-11-17): pivots exactly as its cells.json (see PeauxThrowCellsPath).
            for (int i = 0; i < PeauxThrowSlamCells.Length; i++)
                lines.Add(EnsureThrowSpriteImport(PeauxThrowSlamArt + (i + 1) + ".png", PeauxThrowSlamCells[i], PeauxThrowCellsPath));
            for (int i = 0; i < BarryThrowSlammedCells.Length; i++)
                lines.Add(EnsureThrowSpriteImport(BarryThrowSlammedArt + (i + 1) + ".png", BarryThrowSlammedCells[i], PeauxThrowCellsPath));
            return string.Join("\n", lines);
        }

        /// <param name="cellsPath">Null = the v5 Barry-slams-Peaux file with <see cref="ThrowPivotFromLeft"/>'s pivot
        /// moves; otherwise that file's pivot_px_from_left as written.</param>
        private static string EnsureThrowSpriteImport(string texturePath, string cellKey, string cellsPath = null)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"No TextureImporter at {texturePath}.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            ThrowCell cell = ReadThrowCell(cellsPath ?? BodyslamThrowCellsPath, cellKey);
            if (cell.Width != width || cell.Height != height)
                throw new InvalidOperationException(
                    $"{texturePath} is {width}x{height}, but {cellsPath ?? BodyslamThrowCellsPath} \"{cellKey}\" says {cell.Width}x{cell.Height}.");
            int pivotPx = cellsPath == null ? ThrowPivotFromLeft(cellKey) : cell.PivotFromLeft;
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToArray();
            bool ok = importer.textureType == TextureImporterType.Sprite &&
                      importer.spriteImportMode == SpriteImportMode.Multiple &&
                      Mathf.Approximately(importer.spritePixelsPerUnit, WrestlerPpu) &&
                      importer.filterMode == FilterMode.Point &&
                      importer.textureCompression == TextureImporterCompression.Uncompressed &&
                      !importer.mipmapEnabled &&
                      sprites.Length == 1 &&
                      Mathf.Approximately(sprites[0].rect.width, width) && Mathf.Approximately(sprites[0].rect.height, height) &&
                      Mathf.Abs(sprites[0].pivot.x - pivotPx) < 0.01f && Mathf.Abs(sprites[0].pivot.y) < 0.01f;
            if (ok)
                return $"{texturePath}: already imported (pivot {pivotPx} px).";
            // ImportSheet moves the pivot pivotTowardOpponentPx toward -x from the cell centre.
            return ImportSheet(texturePath, width, height, width / 2 - pivotPx) + $" ({cellKey}, pivot {pivotPx} px from the left)";
        }

        private static Sprite ThrowSprite(string artPrefix, int number) =>
            LoadSprites(artPrefix + number + ".png").Single();

        /// <summary>
        /// Barry_Bodyslam (D-044): S1-S5 then Barry's stand sprite, played once (stand held). Contact = the stand (start
        /// = the landing, 0.62 s), so the clip is contact-scaled onto the bodyslam Startup and the landing falls on the
        /// start of a real frame; turn = S3 (WrestlerView draws S3 on facing the throw direction).
        /// </summary>
        private static MoveClipSpec BarryThrowSlamSpec(Sprite stand) => ThrowSlamSpec(BarryThrowSlamArt, stand);

        /// <summary>Peaux_Bodyslam (Peaux slams Barry, 2026-11-17): S1-S5 then Peaux's stand, built exactly like
        /// <see cref="BarryThrowSlamSpec"/>.</summary>
        private static MoveClipSpec PeauxThrowSlamSpec(Sprite stand) => ThrowSlamSpec(PeauxThrowSlamArt, stand);

        private static MoveClipSpec ThrowSlamSpec(string artPrefix, Sprite stand)
        {
            ImportBodyslamThrowSprites();
            var sprites = new List<Sprite>();
            for (int i = 1; i <= 5; i++)
                sprites.Add(ThrowSprite(artPrefix, i));
            sprites.Add(stand);
            return new MoveClipSpec
            {
                Sprites = sprites.ToArray(),
                KeyFrames = Enumerable.Range(0, sprites.Count).ToArray(), LengthFrames = sprites.Count,
                ContactIndex = ThrowSlamContactIndex, TurnIndex = ThrowSlamTurnIndex,
                DefaultHoldsMs = (float[])ThrowSlamDefaultHoldsMs.Clone(),
            };
        }

        /// <summary>
        /// Peaux_Bodyslammed (D-044): R1-R7 then R6 held. Contact = the first R6 (impact, 0.62 s); turn = R4 (the release:
        /// R1-R3 follow the Locked Up facing, R4-R7 are stored for a throw to screen-right). The last sprite (R6) is
        /// Peaux's downed sprite for every knockdown.
        /// </summary>
        private static MoveClipSpec PeauxThrowSlammedSpec() => ThrowSlammedSpec(PeauxThrowSlammedArt);

        /// <summary>Barry_Bodyslammed (Peaux slams Barry, 2026-11-17): R1-R7 then R6 held, built exactly like
        /// <see cref="PeauxThrowSlammedSpec"/>. Its R6 is Barry's downed sprite for every knockdown.</summary>
        private static MoveClipSpec BarryThrowSlammedSpec() => ThrowSlammedSpec(BarryThrowSlammedArt);

        private static MoveClipSpec ThrowSlammedSpec(string artPrefix)
        {
            ImportBodyslamThrowSprites();
            var sprites = new List<Sprite>();
            for (int i = 1; i <= 7; i++)
                sprites.Add(ThrowSprite(artPrefix, i));
            sprites.Add(sprites[5]); // R6 again, held
            return new MoveClipSpec
            {
                Sprites = sprites.ToArray(),
                KeyFrames = Enumerable.Range(0, sprites.Count).ToArray(), LengthFrames = sprites.Count,
                ContactIndex = ThrowSlammedContactIndex, TurnIndex = ThrowSlammedTurnIndex,
                DefaultHoldsMs = (float[])ThrowSlammedDefaultHoldsMs.Clone(),
            };
        }

        /// <summary>Imports the approved single-frame Struck sprites (gen-barry-struck2-B-1, gen-barry-struck3-B-1,
        /// gen-barry-struck-c-E-1, gen-peaux-struck-a-1, gen-peaux-struck-b-1, gen-peaux-struck-c-A-2) with the wrestler
        /// settings, one sprite each, pivot Bottom Center: Barry A 186x197, B 132x230, C 116x239; Peaux A 172x187,
        /// B 124x213, C 102x212. Run before the Barry / Peaux builders.</summary>
        public static string ImportStruckSheets()
        {
            return string.Join("\n", new[]
            {
                ImportSheet("Assets/Art/Wrestlers/Barry/barry_struck_a.png", 186, 197),
                ImportSheet("Assets/Art/Wrestlers/Barry/barry_struck_b.png", 132, 230),
                ImportSheet("Assets/Art/Wrestlers/Barry/barry_struck_c.png", 116, 239),
                ImportSheet("Assets/Art/Wrestlers/Peaux/peaux_struck_a.png", 172, 187),
                ImportSheet("Assets/Art/Wrestlers/Peaux/peaux_struck_b.png", 124, 213),
                ImportSheet("Assets/Art/Wrestlers/Peaux/peaux_struck_c.png", 102, 212),
            });
        }

        // ---------- sprite import ----------

        /// <summary>
        /// Wrestler sheet import (ARCHITECTURE 3.1): Sprite (Multiple), PPU 60, Point, uncompressed, no mipmaps,
        /// Clamp, Full Rect, then a single-row-or-grid slice at the sheet's own cell size, read left-to-right,
        /// top-to-bottom, named {file}_{i}, pivot Bottom Center. Never rescales. Existing sprite IDs are kept by name.
        /// pivotTowardOpponentPx (lock-up pair sheets only, D-032) moves the pivot that many pixels toward -x
        /// (toward the opponent for left-facing art), still on the bottom row: a Custom pivot. pivotUpPx (bodyslam
        /// receiver sheets only, D-033) raises the pivot that many pixels above the cell bottom, where the art's
        /// ground line is drawn. Max texture size is the smallest power of two that holds the whole sheet, at least
        /// 2048 (the default), so a wide sheet is never downscaled on import.
        /// </summary>
        public static string ImportSheet(string texturePath, int cellWidth, int cellHeight, int pivotTowardOpponentPx = 0,
            int pivotUpPx = 0)
        {
            var pivot = new Vector2(0.5f - pivotTowardOpponentPx / (float)cellWidth, pivotUpPx / (float)cellHeight);
            SpriteAlignment alignment = pivotTowardOpponentPx == 0 && pivotUpPx == 0 ? SpriteAlignment.BottomCenter : SpriteAlignment.Custom;
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"No TextureImporter at {texturePath}.");

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width % cellWidth != 0 || height % cellHeight != 0)
                throw new InvalidOperationException(
                    $"{texturePath} is {width}x{height}, not a whole number of {cellWidth}x{cellHeight} cells.");
            int maxSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
            if (maxSize > 16384)
                throw new InvalidOperationException($"{texturePath} is {width}x{height}, larger than Unity's 16384 texture limit.");

            importer.maxTextureSize = maxSize;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = WrestlerPpu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)alignment;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var edit = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
            if (edit == null)
                throw new InvalidOperationException($"{texturePath}: importer has no edit capability; aborted.");
            var caps = edit.GetEditCapability();
            if (!caps.HasCapability(UnityEditor.U2D.Sprites.EEditCapability.CreateAndDeleteSprite) ||
                !caps.HasCapability(UnityEditor.U2D.Sprites.EEditCapability.EditPivot))
                throw new InvalidOperationException($"{texturePath}: importer cannot create sprites or edit pivots; aborted.");

            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(texturePath);
            int columns = width / cellWidth, rows = height / cellHeight;
            var rects = new List<UnityEditor.SpriteRect>();
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    string name = $"{baseName}_{rects.Count}";
                    var rect = existing.TryGetValue(name, out var old) ? old : new UnityEditor.SpriteRect { spriteID = GUID.Generate() };
                    rect.name = name;
                    // Texture rects are bottom-up: the top row of cells is at y = height - cellHeight.
                    rect.rect = new Rect(col * cellWidth, height - (row + 1) * cellHeight, cellWidth, cellHeight);
                    rect.alignment = alignment;
                    rect.pivot = pivot;
                    rect.border = Vector4.zero;
                    rects.Add(rect);
                }
            }
            provider.SetSpriteRects(rects.ToArray());
            var nameIds = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
            if (nameIds != null)
                nameIds.SetNameFileIdPairs(rects.Select(r => new UnityEditor.SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return $"{texturePath}: {width}x{height}, {rects.Count} sprites of {cellWidth}x{cellHeight}, pivot ({pivot.x:0.####}, {pivot.y:0.####}), max size {maxSize}.";
        }

        /// <summary>Builds (or updates) the shared controller, the wrestler's clips and override, and its prefab. Returns a summary.</summary>
        public static string Build(WrestlerArtSpec spec, bool writePrefab = true)
        {
            Validate(spec);
            AnimatorController controller = GetOrCreateSharedController();

            string folder = "Assets/Animation/" + spec.Name;
            EnsureFolder(folder);
            currentTiming = LoadOrCreateTiming(spec.Name);
            try
            {
                return BuildClips(spec, writePrefab, controller, folder);
            }
            finally
            {
                currentTiming = null;
            }
        }

        private static string BuildClips(WrestlerArtSpec spec, bool writePrefab, AnimatorController controller, string folder)
        {

            bool standBackPlaceholder = spec.StandBack == null;
            bool walkBackPlaceholder = spec.WalkBack == null;

            AnimationClip standFront = WriteSpriteClip($"{folder}/{spec.Name}_StandFront.anim",
                new[] { spec.StandFront }, new[] { 0 }, 1, loop: true);
            AnimationClip standBack = WriteSpriteClip(
                $"{folder}/{spec.Name}_StandBack{(standBackPlaceholder ? "_PLACEHOLDER" : "")}.anim",
                new[] { standBackPlaceholder ? spec.StandFront : spec.StandBack }, new[] { 0 }, 1, loop: true);
            AnimationClip walkFront = WriteSpriteClip($"{folder}/{spec.Name}_WalkFront.anim",
                spec.WalkFront, spec.WalkKeyFrames, spec.WalkLoopFrames, loop: true);
            AnimationClip walkBack = walkBackPlaceholder
                ? WriteSpriteClip($"{folder}/{spec.Name}_WalkBack_PLACEHOLDER.anim",
                    spec.WalkFront, spec.WalkKeyFrames, spec.WalkLoopFrames, loop: true)
                : WriteSpriteClip($"{folder}/{spec.Name}_WalkBack.anim",
                    spec.WalkBack, spec.WalkBackKeyFrames, spec.WalkBackLoopFrames, loop: true);
            AnimationClip run = spec.Run == null
                ? WriteRunStandIn(spec, folder)
                : WriteSpriteClip($"{folder}/{spec.Name}_Run.anim", spec.Run, spec.RunKeyFrames, spec.RunLoopFrames, loop: true);

            var bySlot = new Dictionary<string, AnimationClip>
            {
                { SlotStandFront, standFront },
                { SlotStandBack, standBack },
                { SlotWalkFront, walkFront },
                { SlotWalkBack, walkBack },
                { SlotRun, run },
            };

            MoveAnimationTable table = GetOrCreateMoveTable();
            WriteMoveClip(spec.Strike1, $"{folder}/{spec.Name}_Strike1.anim", MoveId.Strike1, SlotStrike1, bySlot, table);
            WriteMoveClip(spec.Strike2, $"{folder}/{spec.Name}_Strike2.anim", MoveId.Strike2, SlotStrike2, bySlot, table);
            WriteMoveClip(spec.Strike3, $"{folder}/{spec.Name}_Strike3.anim", MoveId.Strike3, SlotStrike3, bySlot, table);
            WriteMoveClip(spec.Stomp, $"{folder}/{spec.Name}_Stomp.anim", MoveId.Stomp, SlotStomp, bySlot, table);
            string standIns = WriteMissingArtStandIns(spec, folder, bySlot, table) + WriteStandIns(spec, folder, bySlot, table)
                              + " " + WriteLockUpStandIns(spec, folder, bySlot, table);
            // D-033: bodyslam sides, played once with the last frame held; contact time = impact frame, so WrestlerView
            // lands the impact at the Bodyslam MoveTiming's Startup. No art: the lock-up hold clip stands in (no entry).
            WriteMoveClip(spec.Bodyslam, $"{folder}/{spec.Name}_{WrestlerView.BodyslamStateName}.anim", MoveId.Bodyslam,
                SlotBodyslam, bySlot, table);
            WriteMoveClip(spec.Bodyslammed, $"{folder}/{spec.Name}_{WrestlerView.BodyslammedStateName}.anim", MoveId.Bodyslam,
                SlotBodyslammed, bySlot, table);
            if (spec.Bodyslam == null)
            {
                bySlot[SlotBodyslam] = bySlot[SlotLockUp];
                standIns += " Bodyslam(=LockUp)";
            }
            if (spec.Bodyslammed == null)
            {
                bySlot[SlotBodyslammed] = bySlot[SlotLockedUp];
                standIns += " Bodyslammed(=LockedUp)";
            }
            // D-039: the whipper's Irish Whip, played once with f4 held; contact = release (f4), turn = f2. No art: the
            // lock-up hold clip stands in (no table entry, no turn).
            WriteMoveClip(spec.IrishWhip, $"{folder}/{spec.Name}_{WrestlerView.IrishWhipStateName}.anim", MoveId.IrishWhip,
                SlotIrishWhip, bySlot, table);
            if (spec.IrishWhip == null)
            {
                bySlot[SlotIrishWhip] = bySlot[SlotLockUp];
                standIns += " IrishWhip(=LockUp)";
            }
            if (!WriteFallClip(spec, folder, bySlot, table))
                standIns += " Fall(=downed)";
            // D-043: the crouch, one sprite held (not looped). WrestlerView shows it for the whole get-up at the root (no
            // offset; the front boot lines up with the stand). No art: the stand sprite stands in.
            bySlot[SlotCrouch] = spec.Crouch != null
                ? WriteSpriteClip($"{folder}/{spec.Name}_{WrestlerView.CrouchStateName}.anim", new[] { spec.Crouch }, new[] { 0 }, 6, loop: false)
                : WriteSpriteClip($"{folder}/{spec.Name}_{WrestlerView.CrouchStateName}_PLACEHOLDER.anim", new[] { spec.StandFront }, new[] { 0 }, 6, loop: false);
            if (spec.Crouch == null)
                standIns += " Crouch";
            if (!WriteStompBounceClip(spec, folder, bySlot))
                standIns += " StompBounce(=downed)";
            EditorUtility.SetDirty(table);

            AnimatorOverrideController overrides = WriteOverride($"{folder}/{spec.Name}.overrideController", controller, bySlot);

            string prefabPath = writePrefab ? WritePrefab(spec, overrides, table) : "(not rebuilt)";
            // Knockdowns not caused by a bodyslam show the downed sprite mirrored in place (WrestlerView). Kept in sync
            // on every rebuild, also when the prefab is otherwise left alone (only downedMirrorShift and the D-044
            // fallLandingShift are written).
            WriteDownedMirrorShift(spec);
            AssetDatabase.SaveAssets();

            return $"Built {spec.Name}: controller {ControllerPath}, override {AssetDatabase.GetAssetPath(overrides)}, prefab {prefabPath}. " +
                   $"Placeholders: {(standBackPlaceholder ? "StandBack " : "")}{(walkBackPlaceholder ? "WalkBack " : "")}{standIns}".TrimEnd();
        }

        /// <summary>
        /// Falling Down (D-041). With fall art: {Name}_Fall.anim = struck_a, fall2, fall3, downed (held), played once at
        /// native speed with the designer's FrameTiming holds (defaults <see cref="FallDefaultHoldsMs"/>). Its
        /// MoveAnimationTable entry (Move None, no contact) records the start of the downed sprite as TurnTime: WrestlerView
        /// draws the fall frames head toward FallSide (stored head-right) before it and the downed sprite (stored head-left)
        /// mirrored with downedMirrorShift from it, so head and feet never swap sides. Without fall art:
        /// {Name}_Fall_PLACEHOLDER.anim holds only the downed sprite (no table entry = landed from the start), which is the
        /// D-038 picture oriented by FallSide. Returns false for the placeholder.
        /// </summary>
        private static bool WriteFallClip(WrestlerArtSpec spec, string folder, Dictionary<string, AnimationClip> bySlot,
            MoveAnimationTable table)
        {
            if (spec.Fall != null)
            {
                MoveClipSpec f = spec.Fall;
                TimedClip timed = WriteTimedClip($"{folder}/{spec.Name}_{WrestlerView.FallStateName}.anim", f.Sprites, f.KeyFrames,
                    f.LengthFrames, false, null, ClipPlayback.Native, -1, f.DefaultHoldsMs, f.KeyFrames[f.TurnIndex]);
                bySlot[SlotFall] = timed.Clip;
                table.SetEntry(MoveId.None, timed.Clip, 0f, timed.TurnTime);
                return true;
            }
            Sprite downed = spec.Bodyslammed.Sprites[spec.Bodyslammed.Sprites.Length - 1];
            bySlot[SlotFall] = WriteSpriteClip($"{folder}/{spec.Name}_{WrestlerView.FallStateName}_PLACEHOLDER.anim",
                new[] { downed }, new[] { 0 }, 12, loop: false);
            return false;
        }

        /// <summary>
        /// Stomp bounce (D-047): {Name}_StompBounce.anim = R7 (the bodyslam bounce, its 17 px lift baked into the art) held
        /// for its FrameTiming Hold (default <see cref="StompBounceDefaultHoldMs"/>), played once at native speed, rest pose
        /// (no child offset, rotation or tint: WrestlerView applies the downed pose's flip / mirror shift, so R7 sits where it
        /// does in the bodyslam tail relative to R6). No table entry. At the clip's end WrestlerView returns to the lying
        /// pose. Without R7 the PLACEHOLDER holds the downed sprite (no visible bounce). Returns false for the placeholder.
        /// </summary>
        private static bool WriteStompBounceClip(WrestlerArtSpec spec, string folder, Dictionary<string, AnimationClip> bySlot)
        {
            if (spec.StompBounce != null)
            {
                bySlot[SlotStompBounce] = WriteTimedClip($"{folder}/{spec.Name}_{WrestlerView.StompBounceStateName}.anim",
                    new[] { spec.StompBounce }, new[] { 0 }, 1, false, null, ClipPlayback.Native, -1,
                    new[] { StompBounceDefaultHoldMs }).Clip;
                return true;
            }
            Sprite downed = spec.Bodyslammed.Sprites[spec.Bodyslammed.Sprites.Length - 1];
            bySlot[SlotStompBounce] = WriteTimedClip($"{folder}/{spec.Name}_{WrestlerView.StompBounceStateName}_PLACEHOLDER.anim",
                new[] { downed }, new[] { 0 }, 1, false, null, ClipPlayback.Native, -1, new[] { StompBounceDefaultHoldMs }).Clip;
            return false;
        }

        // ---------- combat-core stand-ins (PLACEHOLDER) ----------

        /// <summary>Stomp stand-in contact frame (boot comes down), in SourceFps frames: 5 = 0.208 s at 1x.</summary>
        public const int StompStandInContactFrame = 5;

        /// <summary>
        /// Approved stomp timing (gen-barry-stomp-A-2, gen-peaux-stomp-2): frame 0 wind-up (knee chamber) 200 ms,
        /// frame 1 contact (boot planted) held 400 ms. Rounded per frame to 24 fps like the strikes: 5 / 10 samples =
        /// 208.3 / 416.7 ms, 625 ms total; contact at sample 5 (0.208 s, same as the stand-in). WrestlerView scales it
        /// to the tuned Stomp Startup (0.2 s -> 1.04x), so the clip ends at Startup + Active + Recovery (0.6 s).
        /// </summary>
        public static readonly int[] StompKeyFrames = { 0, 5 };
        public const int StompLengthFrames = 15;
        public const int StompContactIndex = 1;

        /// <summary>
        /// Temporary clips for states with no art yet, built only from the wrestler's approved stand/walk sprites
        /// with child-sprite offset, rotation and tint (ARCHITECTURE 6). Named {Name}_{State}_PLACEHOLDER.anim.
        /// All keys are stepped at SourceFps. Offsets are whole native pixels, authored for left-facing art.
        /// Knocked Down holds the downed sprite (last Bodyslammed sprite) instead of a posed stand sprite; Knocked Out
        /// maps to the Knocked Down clip (D-023). Returns the list of stand-in names for the summary.
        /// </summary>
        private static string WriteStandIns(WrestlerArtSpec spec, string folder, Dictionary<string, AnimationClip> bySlot,
            MoveAnimationTable table)
        {
            string P(string state) => $"{folder}/{spec.Name}_{state}_PLACEHOLDER.anim";
            Sprite[] stand = { spec.StandFront };
            Sprite[] standBack = { spec.StandBack != null ? spec.StandBack : spec.StandFront };
            int[] one = { 0 };

            // Struck: approved hit reactions StruckA (lean back, built but unused since D-040), StruckB (head snap) and
            // StruckC (combo hit 2). WrestlerView shows StruckC for a Strike 2 hit and StruckB for every other
            // non-knockdown hit (Slot_Struck / Slot_StruckB / Slot_StruckC). Each holds its sprite for 10 frames (0.417 s; Struck lasts
            // 0.4 s by default, D-024) and is restarted on every hit. Same 6 px knockback as the old stand-in, easing back
            // over 0.25 s, but no rotation: the sprites already lean, and rotating them about the feet
            // would lift a boot off the ground. Without both sprites, the PLACEHOLDER (stand sprite, leaning back
            // 6 deg) fills every slot. StruckC without its sprite reuses the StruckB clip.
            // No red tint: the hit flash is code-driven (Juice WrestlerFlashOverlay, D-046), so color stays white.
            AnimationClip struck, struckB, struckC;
            if (spec.StruckA != null && spec.StruckB != null)
            {
                PoseKey[] hit =
                {
                    new PoseKey(0, 6, 0, 0),
                    new PoseKey(2, 4, 0, 0),
                    new PoseKey(4, 2, 0, 0),
                    new PoseKey(6, 0, 0, 0),
                };
                struck = WriteSpriteClip($"{folder}/{spec.Name}_StruckA.anim", new[] { spec.StruckA }, one, 10, loop: false, hit);
                struckB = WriteSpriteClip($"{folder}/{spec.Name}_StruckB.anim", new[] { spec.StruckB }, one, 10, loop: false, hit);
                struckC = spec.StruckC != null
                    ? WriteSpriteClip($"{folder}/{spec.Name}_StruckC.anim", new[] { spec.StruckC }, one, 10, loop: false, hit)
                    : struckB;
            }
            else
            {
                struck = struckB = struckC = WriteSpriteClip(P("Struck"), stand, one, 10, loop: false, new[]
                {
                    new PoseKey(0, 6, 0, -6),
                    new PoseKey(2, 4, 0, -4),
                    new PoseKey(4, 2, 0, -2),
                    new PoseKey(6, 0, 0, 0),
                });
            }

            // Knocked Down (designer, 2026-11-12): every knockdown shows the wrestler's downed sprite = the last sprite
            // of their own Bodyslammed clip (the same Sprite asset, so the same pivot), held with the rest pose (no
            // offset, rotation or tint). A bodyslam therefore ends in Knocked Down with no visible change (head toward
            // the slammer). Every other knockdown (strike, run collision, obstacle, KO from standing) plays the Fall
            // state instead (D-041, WriteFallClip), which ends on this same sprite oriented by FallSide; this clip is
            // only shown when there is no fall side. 12 frames (0.5 s) long, not looped; the length only matters for
            // WrestlerView's KO-while-down jump to the end (D-023). Still named _PLACEHOLDER so references stay stable.
            if (spec.Bodyslammed == null || spec.Bodyslammed.Sprites == null || spec.Bodyslammed.Sprites.Length == 0)
                throw new ArgumentException($"{spec.Name}: Knocked Down uses the last Bodyslammed sprite; no Bodyslammed art.");
            Sprite[] downed = { spec.Bodyslammed.Sprites[spec.Bodyslammed.Sprites.Length - 1] };
            AnimationClip knockedDown = WriteSpriteClip(P("KnockedDown"), downed, one, 12, loop: false);

            // Recovering (D-023: clutching ribs, walking slowly). Stand: hunched forward 6 deg with a 2 px breathing bob
            // (1 s loop). Walk: the walk cycle at half speed (6 frames per sprite, 2 s loop; Recovering walk speed is
            // 0.5x by default) with the same hunch. Back-facing variants: same timing, no lean (it would not read
            // from behind).
            AnimationClip recStand = WriteSpriteClip(P("RecoveringStand"), stand, one, 24, loop: true, new[]
            {
                new PoseKey(0, 0, 0, 6), new PoseKey(12, 0, -2, 6),
            });
            AnimationClip recStandBack = WriteSpriteClip(P("RecoveringStandBack"), standBack, one, 24, loop: true, new[]
            {
                new PoseKey(0, 0, 0, 0), new PoseKey(12, 0, -2, 0),
            });
            int[] halfSpeed = Enumerable.Range(0, spec.WalkFront.Length).Select(i => i * 6).ToArray();
            AnimationClip recWalk = WriteSpriteClip(P("RecoveringWalk"), spec.WalkFront, halfSpeed,
                spec.WalkFront.Length * 6, loop: true, new[] { new PoseKey(0, 0, 0, 6) });
            Sprite[] walkBack = spec.WalkBack ?? spec.WalkFront;
            int[] halfSpeedBack = Enumerable.Range(0, walkBack.Length).Select(i => i * 6).ToArray();
            AnimationClip recWalkBack = WriteSpriteClip(P("RecoveringWalkBack"), walkBack, halfSpeedBack,
                walkBack.Length * 6, loop: true, null);

            // Stomp, only for a wrestler with no stomp art (spec.Stomp == null): rises 10 px leaning back (wind-up),
            // slams down 2 px leaning forward onto the boot at the contact frame (5 = 0.208 s), settles by frame 9.
            // 0.5 s long; WrestlerView scales it to the tuned Startup.
            if (spec.Stomp == null)
            {
                TimedClip stompTimed = WriteTimedClip(P("Stomp"), stand, one, 12, false, new[]
                {
                    new PoseKey(0, 0, 0, 0),
                    new PoseKey(1, 0, 2, -4),
                    new PoseKey(2, 0, 6, -8),
                    new PoseKey(3, 0, 10, -10),
                    new PoseKey(StompStandInContactFrame, 0, -2, 6),
                    new PoseKey(7, 0, 0, 3),
                    new PoseKey(9, 0, 0, 0),
                }, ClipPlayback.ContactScaled, StompStandInContactFrame);
                table.SetEntry(MoveId.Stomp, stompTimed.Clip, stompTimed.ContactTime);
                bySlot[SlotStomp] = stompTimed.Clip;
            }

            bySlot[SlotStruck] = struck;
            bySlot[SlotStruckB] = struckB;
            bySlot[SlotStruckC] = struckC;
            bySlot[SlotKnockedDown] = knockedDown;
            bySlot[SlotKnockedOut] = knockedDown; // D-023: Knocked Out reuses Knocked Down
            bySlot[SlotRecoveringStandFront] = recStand;
            bySlot[SlotRecoveringStandBack] = recStandBack;
            bySlot[SlotRecoveringWalkFront] = recWalk;
            bySlot[SlotRecoveringWalkBack] = recWalkBack;
            return (struck == struckB ? "Struck(+StruckB+StruckC) " : struckC == struckB ? "StruckC(=StruckB) " : "") +
                   "KnockedDown(+KnockedOut) RecoveringStand RecoveringStandBack RecoveringWalk RecoveringWalkBack" +
                   (spec.Stomp == null ? " Stomp" : "");
        }

        // ---------- lock-up slice stand-ins (PLACEHOLDER) ----------

        /// <summary>Lock Up / Locked Up PLACEHOLDER hold loop length (SourceFps frames): a 0.5 s push-pull.</summary>
        public const int LockUpHoldLoopFrames = 12;

        /// <summary>
        /// Approved Lock Up / Locked Up clips (D-032, ASSET_REGISTRY gen-*-lockup-5 / -lockedup-5): designer timing
        /// f1 330 ms, f2 530 ms, f3 held to the end of the lock-up, played once (not looped). Rounded per frame to
        /// 24 fps like the strikes: 8 / 13 samples = 333 / 542 ms, f3 from sample 21 (875 ms) and held by the
        /// non-looping clip. The holds have no MoveAnimationTable entry, so WrestlerView plays them at native
        /// speed (MoveSpeed 1).
        /// </summary>
        public static readonly int[] LockUpHoldKeyFrames = { 0, 8, 21 };
        public const int LockUpHoldLengthFrames = 22;

        /// <summary>
        /// Approved bodyslam timing (D-033, ASSET_REGISTRY gen-*-bodyslam(med)-3), played once with the last frame
        /// held, rounded per frame to 24 fps like the lock-up and strikes.
        /// 7-frame pair (Barry slams Peaux: barry_bodyslam + peaux_bodyslammed): 300/140/430/130/230/700/500 ms ->
        /// 7/3/10/3/6/17/12 samples = 292/125/417/125/250/708/500 ms, 2.417 s; impact = frame 6 (receiver flat on
        /// the ground) at sample 29 = 1.208 s.
        /// 6-frame pair (Peaux slams Barry: peaux_bodyslam + barry_bodyslammed): 300/430/130/230/700/500 ms ->
        /// 7/10/3/6/17/12 samples, 2.292 s; impact = frame 5 at sample 26 = 1.083 s.
        /// </summary>
        public static readonly int[] BodyslamKeyFrames7 = { 0, 7, 10, 20, 23, 29, 46 };
        public const int BodyslamLengthFrames7 = 58;
        public const int BodyslamImpactIndex7 = 5;
        public static readonly int[] BodyslamKeyFrames6 = { 0, 7, 17, 20, 26, 43 };
        public const int BodyslamLengthFrames6 = 55;
        public const int BodyslamImpactIndex6 = 4;
        /// <summary>Whiff and break stand-in length (SourceFps frames, 0.417 s native). WrestlerView fits them to the
        /// move's total time (whiff 0.4 s, break 0.2 + 0.2 s by default), so no contact frame is recorded.</summary>
        public const int LockUpOneShotFrames = 10;

        /// <summary>
        /// Lock-up slice stand-ins (ARCHITECTURE 6, D-031), built only from the wrestler's approved stand and strike
        /// sprites with child-sprite offset and lean (no new art). Named {Name}_{State}_PLACEHOLDER.anim.
        /// Authored for left-facing art: -x = toward the opponent, +rotation = leaning forward. The pair stands
        /// lockUpPairSpacing apart (Combat, 1.2 units = 72 px by default), which already overlaps the two bodies, so
        /// the hold keeps A at rest and B leaning back 6-8 px; the break shoves both further apart before settling.
        /// Lock Up / Locked Up below are used only when the wrestler has no lock-up art (spec.LockUp / LockedUp null);
        /// Barry and Peaux have approved art (D-032), written as {Name}_LockUp.anim / {Name}_LockedUp.anim.
        ///   Lock Up (A, hold, loops): Strike 1 contact frame (arm reaching forward), leaning in, pushing on beat 2.
        ///   Locked Up (B, hold, loops): Strike 2 first frame (fists up, braced), leaning back, rocked back on beat 2.
        ///   Break (A): reach frame for 2 frames, then the stand sprite stepping back 8 px and settling.
        ///   Break (B): braced frame for 2 frames, then the stand sprite staggering back 14 px and settling.
        ///   Whiff (A alone): Strike 1 wind-up, lunge into the reach frame overreaching, a clutch at air, back to stand.
        /// A wrestler without strike art falls back to the stand sprite. Returns the stand-in names for the summary.
        /// </summary>
        private static string WriteLockUpStandIns(WrestlerArtSpec spec, string folder, Dictionary<string, AnimationClip> bySlot,
            MoveAnimationTable table)
        {
            string P(string state) => $"{folder}/{spec.Name}_{state}_PLACEHOLDER.anim";
            Sprite stand = spec.StandFront;
            Sprite reach = spec.Strike1 != null ? spec.Strike1.Sprites[spec.Strike1.ContactIndex] : stand;
            Sprite windUp = spec.Strike1 != null ? spec.Strike1.Sprites[0] : stand;
            Sprite braced = spec.Strike2 != null ? spec.Strike2.Sprites[0] : stand;

            var names = new List<string>();
            // Approved art (D-032): played once with the last frame held (non-looping clip), rest pose (no offset or
            // lean; the pair spacing lives in the sprite pivots). The old _PLACEHOLDER hold files stay, unreferenced.
            AnimationClip hold, held;
            if (spec.LockUp != null)
                hold = WriteSpriteClip($"{folder}/{spec.Name}_{WrestlerView.LockUpStateName}.anim", spec.LockUp,
                    LockUpHoldKeyFrames, LockUpHoldLengthFrames, loop: false);
            else
            {
                hold = WriteSpriteClip(P(WrestlerView.LockUpStateName), new[] { reach }, new[] { 0 },
                    LockUpHoldLoopFrames, loop: true, new[]
                    {
                        new PoseKey(0, 0, 0, 4),
                        new PoseKey(6, -2, 0, 6),
                    });
                names.Add(WrestlerView.LockUpStateName);
            }
            if (spec.LockedUp != null)
                held = WriteSpriteClip($"{folder}/{spec.Name}_{WrestlerView.LockedUpStateName}.anim", spec.LockedUp,
                    LockUpHoldKeyFrames, LockUpHoldLengthFrames, loop: false);
            else
            {
                held = WriteSpriteClip(P(WrestlerView.LockedUpStateName), new[] { braced }, new[] { 0 },
                    LockUpHoldLoopFrames, loop: true, new[]
                    {
                        new PoseKey(0, 6, 0, -6),
                        new PoseKey(6, 8, -2, -8),
                    });
                names.Add(WrestlerView.LockedUpStateName);
            }

            AnimationClip breakA = WriteSpriteClip(P(WrestlerView.BreakLockUpStateName), new[] { reach, stand }, new[] { 0, 2 },
                LockUpOneShotFrames, loop: false, new[]
                {
                    new PoseKey(0, 0, 0, 4),
                    new PoseKey(2, 4, 0, -4),
                    new PoseKey(4, 8, 0, -6),
                    new PoseKey(6, 6, 0, -3),
                    new PoseKey(8, 2, 0, 0),
                    new PoseKey(9, 0, 0, 0),
                }, ClipPlayback.FittedToMove);
            AnimationClip breakB = WriteSpriteClip(P(WrestlerView.BreakLockUpReceiverStateName), new[] { braced, stand }, new[] { 0, 2 },
                LockUpOneShotFrames, loop: false, new[]
                {
                    new PoseKey(0, 6, 0, -6),
                    new PoseKey(2, 10, 0, -8),
                    new PoseKey(4, 14, 0, -10),
                    new PoseKey(6, 10, 0, -5),
                    new PoseKey(8, 4, 0, -2),
                    new PoseKey(9, 0, 0, 0),
                }, ClipPlayback.FittedToMove);

            AnimationClip whiff = WriteSpriteClip(P(WrestlerView.LockUpWhiffStateName), new[] { windUp, reach, stand }, new[] { 0, 2, 8 },
                LockUpOneShotFrames, loop: false, new[]
                {
                    new PoseKey(0, 2, 0, -3),
                    new PoseKey(2, -6, 0, 6),
                    new PoseKey(4, -8, 0, 8),
                    new PoseKey(6, -6, -1, 6),
                    new PoseKey(8, -2, 0, 2),
                    new PoseKey(9, 0, 0, 0),
                }, ClipPlayback.FittedToMove);

            // Contact time 0 = no contact frame: WrestlerView fits the clip to the move's total time.
            table.SetEntry(MoveId.LockUp, whiff, 0f);
            table.SetEntry(MoveId.BreakLockUp, breakA, 0f);
            table.SetEntry(MoveId.BreakLockUp, breakB, 0f);

            bySlot[SlotLockUp] = hold;
            bySlot[SlotLockedUp] = held;
            bySlot[SlotBreakLockUp] = breakA;
            bySlot[SlotBreakLockUpReceiver] = breakB;
            bySlot[SlotLockUpWhiff] = whiff;
            names.AddRange(new[] { WrestlerView.BreakLockUpStateName, WrestlerView.BreakLockUpReceiverStateName,
                WrestlerView.LockUpWhiffStateName });
            return string.Join(" ", names);
        }

        /// <summary>
        /// PLACEHOLDER run for a wrestler with no run art: the walk-front sprites at double walk speed (2 frames each)
        /// leaning forward 8 deg. Named {Name}_Run_PLACEHOLDER.anim.
        /// </summary>
        private static AnimationClip WriteRunStandIn(WrestlerArtSpec spec, string folder)
        {
            int[] keys = Enumerable.Range(0, spec.WalkFront.Length).Select(i => i * 2).ToArray();
            return WriteSpriteClip($"{folder}/{spec.Name}_Run_PLACEHOLDER.anim", spec.WalkFront, keys,
                spec.WalkFront.Length * 2, loop: true, new[] { new PoseKey(0, 0, 0, 8) });
        }

        /// <summary>Strike stand-in contact frames (SourceFps), matching Barry's approved strikes: 5 (0.208 s) and 3 (0.125 s).</summary>
        public const int Strike12StandInContactFrame = 5;
        public const int Strike3StandInContactFrame = 3;

        /// <summary>
        /// PLACEHOLDER strikes for a wrestler with no strike art (only for null Strike specs): the stand-front sprite
        /// rocks back (wind-up), then lunges forward at the contact frame and settles. Same lengths and contact frames
        /// as Barry's strikes, so the tuned Startups (D-024) need no extra speed scaling. -x = forward (left-facing art).
        /// Returns the stand-in names (run included) for the summary; empty when the wrestler has all the art.
        /// </summary>
        private static string WriteMissingArtStandIns(WrestlerArtSpec spec, string folder,
            Dictionary<string, AnimationClip> bySlot, MoveAnimationTable table)
        {
            Sprite[] stand = { spec.StandFront };
            int[] one = { 0 };
            var names = new List<string>();
            if (spec.Run == null)
                names.Add("Run");

            void StandIn(MoveClipSpec art, MoveId move, string slot, int length, int contact, PoseKey[] poses)
            {
                if (art != null)
                    return;
                TimedClip timed = WriteTimedClip($"{folder}/{spec.Name}_{move}_PLACEHOLDER.anim", stand, one, length,
                    false, poses, ClipPlayback.ContactScaled, contact);
                bySlot[slot] = timed.Clip;
                table.SetEntry(move, timed.Clip, timed.ContactTime);
                names.Add(move.ToString());
            }

            // Strike 1 (right punch): wind-up at 3, lunge 8 px / lean 8 deg at contact (5), settle at 7. 8 frames (333 ms).
            StandIn(spec.Strike1, MoveId.Strike1, SlotStrike1, 8, Strike12StandInContactFrame, new[]
            {
                new PoseKey(0, 0, 0, 0),
                new PoseKey(3, 3, 0, -5),
                new PoseKey(Strike12StandInContactFrame, -8, 0, 8),
                new PoseKey(7, -3, 0, 3),
            });
            // Strike 2 (left punch): same timing, a lower, shorter lunge so it reads as a different hand.
            StandIn(spec.Strike2, MoveId.Strike2, SlotStrike2, 8, Strike12StandInContactFrame, new[]
            {
                new PoseKey(0, 0, 0, 0),
                new PoseKey(3, 2, -2, -3),
                new PoseKey(Strike12StandInContactFrame, -6, -2, 6),
                new PoseKey(7, -2, 0, 2),
            });
            // Strike 3 (finisher): wind-up 0-2, big lunge 12 px / 12 deg at contact (3), settle at 7. 9 frames (375 ms).
            StandIn(spec.Strike3, MoveId.Strike3, SlotStrike3, 9, Strike3StandInContactFrame, new[]
            {
                new PoseKey(0, 4, 2, -8),
                new PoseKey(Strike3StandInContactFrame, -12, 0, 12),
                new PoseKey(7, -4, 0, 4),
            });
            return names.Count == 0 ? "" : string.Join(" ", names) + " ";
        }

        // ---------- shared controller ----------

        /// <summary>
        /// Shared graph. Parameters: State (int = (int)CombatState), Back (float, 0 front / 1 back).
        /// States: Standing (1D blend tree on Back), Walking (1D blend tree on Back), Running (clip).
        /// Any State -> each state when State equals its CombatState value; instant, no exit time, no self-transition.
        /// Existing controller is left untouched so references survive; delete it by hand to rebuild.
        /// </summary>
        public static AnimatorController GetOrCreateSharedController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                EnsureStrikeStates(existing);
                return existing;
            }

            EnsureFolder(SharedFolder);
            AnimationClip slotStandFront = WriteSlotClip(SlotStandFront);
            AnimationClip slotStandBack = WriteSlotClip(SlotStandBack);
            AnimationClip slotWalkFront = WriteSlotClip(SlotWalkFront);
            AnimationClip slotWalkBack = WriteSlotClip(SlotWalkBack);
            AnimationClip slotRun = WriteSlotClip(SlotRun);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("State", AnimatorControllerParameterType.Int);
            controller.AddParameter("Back", AnimatorControllerParameterType.Float);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState standing = AddFrontBackState(controller, sm, CombatState.Standing, slotStandFront, slotStandBack, new Vector3(300, 0));
            AnimatorState walking = AddFrontBackState(controller, sm, CombatState.Walking, slotWalkFront, slotWalkBack, new Vector3(300, 80));
            AnimatorState running = sm.AddState(CombatState.Running.ToString(), new Vector3(300, 160));
            running.motion = slotRun;
            running.writeDefaultValues = false;

            sm.defaultState = standing;
            AddAnyStateTransition(sm, standing, CombatState.Standing);
            AddAnyStateTransition(sm, walking, CombatState.Walking);
            AddAnyStateTransition(sm, running, CombatState.Running);

            EnsureStrikeStates(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// Adds (once) the Striking states: parameters Move (int, (int)MoveId) and MoveSpeed (float, default 1);
        /// states Strike1-3 playing Slot_Strike1-3 at MoveSpeed; Any State -> StrikeN when State == Striking and
        /// Move == StrikeN (instant, no exit time, no self-transition). Leaves the existing states untouched.
        /// </summary>
        public static void EnsureStrikeStates(AnimatorController controller)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != MoveParam))
            {
                controller.AddParameter(MoveParam, AnimatorControllerParameterType.Int);
                changed = true;
            }
            if (controller.parameters.All(p => p.name != MoveSpeedParam))
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = MoveSpeedParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f,
                });
                changed = true;
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            changed |= EnsureMoveState(sm, MoveId.Strike1, SlotStrike1, new Vector3(560, 0));
            changed |= EnsureMoveState(sm, MoveId.Strike2, SlotStrike2, new Vector3(560, 80));
            changed |= EnsureMoveState(sm, MoveId.Strike3, SlotStrike3, new Vector3(560, 160));
            changed |= EnsureCombatCoreStates(controller);
            changed |= EnsureLockUpStates(controller);
            changed |= EnsureFallState(controller);
            changed |= EnsureCrouchState(controller);
            changed |= EnsureSlamTail(controller);
            changed |= EnsureStompBounceState(controller);

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }
        }

        private static bool EnsureMoveState(AnimatorStateMachine sm, MoveId move, string slotName, Vector3 position)
        {
            string stateName = move.ToString();
            if (sm.states.Any(s => s.state.name == stateName))
                return false;

            AnimatorState state = sm.AddState(stateName, position);
            state.motion = WriteSlotClip(slotName, loop: false);
            state.writeDefaultValues = false;
            state.speed = 1f;
            state.speedParameter = MoveSpeedParam;
            state.speedParameterActive = true;

            AnimatorStateTransition t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.Equals, (int)CombatState.Striking, "State");
            t.AddCondition(AnimatorConditionMode.Equals, (int)move, MoveParam);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0f;
            t.offset = 0f;
            t.canTransitionToSelf = false;
            t.interruptionSource = TransitionInterruptionSource.None;
            return true;
        }

        /// <summary>
        /// Adds (once) the combat-core slice states (ARCHITECTURE 4.2 slice rules, D-023, D-025):
        /// parameter Moving (bool); Stomp (move state, State == Striking and Move == Stomp, at MoveSpeed);
        /// Struck, KnockedDown, KnockedOut (one clip each, Any State when State equals the value);
        /// RecoveringStand / RecoveringWalk (front/back blend trees on Back; Any State when State == Recovering
        /// and Moving is false / true). All instant, no exit time, no self-transition. Existing states untouched.
        /// </summary>
        public static bool EnsureCombatCoreStates(AnimatorController controller)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != MovingParam))
            {
                controller.AddParameter(MovingParam, AnimatorControllerParameterType.Bool);
                changed = true;
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            changed |= EnsureMoveState(sm, MoveId.Stomp, SlotStomp, new Vector3(560, 240));
            changed |= EnsureClipState(sm, CombatState.Struck, SlotStruck, new Vector3(300, 240));
            changed |= EnsureStruckBState(controller, sm);
            changed |= EnsureStruckCState(controller, sm);
            changed |= EnsureClipState(sm, CombatState.KnockedDown, SlotKnockedDown, new Vector3(300, 320));
            changed |= EnsureClipState(sm, CombatState.KnockedOut, SlotKnockedOut, new Vector3(300, 400));

            if (sm.states.All(s => s.state.name != RecoveringStandState))
            {
                AnimatorState stand = AddFrontBackState(controller, sm, RecoveringStandState,
                    WriteSlotClip(SlotRecoveringStandFront), WriteSlotClip(SlotRecoveringStandBack), new Vector3(300, 480));
                AnimatorStateTransition t = AddAnyStateTransition(sm, stand, CombatState.Recovering);
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, MovingParam);
                changed = true;
            }
            if (sm.states.All(s => s.state.name != RecoveringWalkState))
            {
                AnimatorState walk = AddFrontBackState(controller, sm, RecoveringWalkState,
                    WriteSlotClip(SlotRecoveringWalkFront), WriteSlotClip(SlotRecoveringWalkBack), new Vector3(300, 560));
                AnimatorStateTransition t = AddAnyStateTransition(sm, walk, CombatState.Recovering);
                t.AddCondition(AnimatorConditionMode.If, 0f, MovingParam);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Adds (once) the lock-up slice states (ARCHITECTURE 6, D-031): parameter Paired (bool); all states play their
        /// slot clip at MoveSpeed (1 for the holds, fitted for whiff and break), Any State, instant, no exit time, no
        /// self-transition:
        ///   LockUp              State == LockUp   and Move == LockUp      and Paired   (A holding, loops)
        ///   LockUpWhiff         State == LockUp   and Move == LockUp      and !Paired  (A alone, D-031)
        ///   LockedUp            State == LockedUp and Move == LockUp                   (B held, loops)
        ///   BreakLockUp         State == LockUp   and Move == BreakLockUp              (A)
        ///   BreakLockUpReceiver State == LockedUp and Move == BreakLockUp              (B)
        ///   Bodyslam / Bodyslammed  State == Grappling / Grappled and Move == Bodyslam (D-033)
        ///   IrishWhip           State == Grappling and Move == IrishWhip               (whipper, D-039)
        /// Existing states untouched.
        /// </summary>
        public static bool EnsureLockUpStates(AnimatorController controller)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != PairedParam))
            {
                controller.AddParameter(PairedParam, AnimatorControllerParameterType.Bool);
                changed = true;
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            changed |= EnsureTimedState(sm, WrestlerView.LockUpStateName, SlotLockUp, true, CombatState.LockUp, MoveId.LockUp, true,
                new Vector3(820, 0));
            changed |= EnsureTimedState(sm, WrestlerView.LockUpWhiffStateName, SlotLockUpWhiff, false, CombatState.LockUp, MoveId.LockUp, false,
                new Vector3(820, 80));
            changed |= EnsureTimedState(sm, WrestlerView.LockedUpStateName, SlotLockedUp, true, CombatState.LockedUp, MoveId.LockUp, null,
                new Vector3(820, 160));
            changed |= EnsureTimedState(sm, WrestlerView.BreakLockUpStateName, SlotBreakLockUp, false, CombatState.LockUp, MoveId.BreakLockUp, null,
                new Vector3(820, 240));
            changed |= EnsureTimedState(sm, WrestlerView.BreakLockUpReceiverStateName, SlotBreakLockUpReceiver, false, CombatState.LockedUp,
                MoveId.BreakLockUp, null, new Vector3(820, 320));
            // D-033 bodyslam: Bodyslam (A) State == Grappling and Move == Bodyslam; Bodyslammed (B) State == Grappled
            // and Move == Bodyslam. Not looping (last frame held), at MoveSpeed.
            changed |= EnsureTimedState(sm, WrestlerView.BodyslamStateName, SlotBodyslam, false, CombatState.Grappling,
                MoveId.Bodyslam, null, new Vector3(820, 400));
            changed |= EnsureTimedState(sm, WrestlerView.BodyslammedStateName, SlotBodyslammed, false, CombatState.Grappled,
                MoveId.Bodyslam, null, new Vector3(820, 480));
            // D-039 Irish Whip: IrishWhip (A, the whipper) State == Grappling and Move == IrishWhip. Not looping (f4
            // held), at MoveSpeed. The runner (B) is State Running and plays the Run state (Move is ignored there).
            changed |= EnsureTimedState(sm, WrestlerView.IrishWhipStateName, SlotIrishWhip, false, CombatState.Grappling,
                MoveId.IrishWhip, null, new Vector3(820, 560));
            return changed;
        }

        /// <summary>
        /// Falling Down (D-041): parameter Fall (bool, set by WrestlerView); state "Fall" playing Slot_Fall once at 1x;
        /// Any State -> Fall when (State == KnockedDown or KnockedOut) and Fall. The existing Any State -> KnockedDown /
        /// KnockedOut transitions get "Fall false" (bodyslam knockdowns keep the downed-sprite clip). Instant, no exit
        /// time, no self-transition, so a knockout while already falling / lying keeps the Fall state where it is.
        /// Adds only what is missing.
        /// </summary>
        public static bool EnsureFallState(AnimatorController controller)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != FallParam))
            {
                controller.AddParameter(FallParam, AnimatorControllerParameterType.Bool);
                changed = true;
            }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                string dest = t.destinationState != null ? t.destinationState.name : null;
                if (dest != CombatState.KnockedDown.ToString() && dest != CombatState.KnockedOut.ToString())
                    continue;
                if (t.conditions.Any(c => c.parameter == FallParam))
                    continue;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, FallParam);
                changed = true;
            }
            if (sm.states.All(s => s.state.name != WrestlerView.FallStateName))
            {
                AnimatorState state = sm.AddState(WrestlerView.FallStateName, new Vector3(40, 360));
                state.motion = WriteSlotClip(SlotFall, loop: false);
                state.writeDefaultValues = false;
                foreach (CombatState downed in new[] { CombatState.KnockedDown, CombatState.KnockedOut })
                {
                    AnimatorStateTransition t = AddAnyStateTransition(sm, state, downed);
                    t.AddCondition(AnimatorConditionMode.If, 0f, FallParam);
                }
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Crouch (D-043): parameter Crouch (bool, set by WrestlerView); state "Crouch" playing Slot_Crouch once at 1x;
        /// Any State -> Crouch when State == KnockedDown and Crouch (the get-up at the end of a knockdown). The existing
        /// Any State -> KnockedDown / KnockedOut / Fall transitions get "Crouch false", so the crouch wins over the lying
        /// pose while it is set. Instant, no exit time, no self-transition. The state is reusable: a later use (getting
        /// back in the ring, picking up an opponent) adds its own Any State transition into it. Adds only what is missing.
        /// </summary>
        public static bool EnsureCrouchState(AnimatorController controller)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != CrouchParam))
            {
                controller.AddParameter(CrouchParam, AnimatorControllerParameterType.Bool);
                changed = true;
            }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                string dest = t.destinationState != null ? t.destinationState.name : null;
                if (dest != CombatState.KnockedDown.ToString() && dest != CombatState.KnockedOut.ToString() &&
                    dest != WrestlerView.FallStateName)
                    continue;
                if (t.conditions.Any(c => c.parameter == CrouchParam))
                    continue;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, CrouchParam);
                changed = true;
            }
            if (sm.states.All(s => s.state.name != WrestlerView.CrouchStateName))
            {
                AnimatorState state = sm.AddState(WrestlerView.CrouchStateName, new Vector3(40, 440));
                state.motion = WriteSlotClip(SlotCrouch, loop: false);
                state.writeDefaultValues = false;
                AnimatorStateTransition t = AddAnyStateTransition(sm, state, CombatState.KnockedDown);
                t.AddCondition(AnimatorConditionMode.If, 0f, CrouchParam);
                changed = true;
            }
            return changed;
        }

        /// <summary>Animator bool parameter (D-044): the thrown receiver's Bodyslammed clip is still playing its landing tail
        /// (R6 -> R7 -> R6) after Combat made him Knocked Down / Knocked Out. Set by WrestlerView.</summary>
        public const string SlamTailParam = "SlamTail";

        /// <summary>
        /// D-044 landing tail: parameter SlamTail (bool, set by WrestlerView); the Any State -> KnockedDown / KnockedOut /
        /// Fall transitions get "SlamTail false", so the Bodyslammed state keeps playing after the receiver turns Knocked
        /// Down on the landing tick, until WrestlerView clears SlamTail at the clip's end (the Knocked Down clip then shows
        /// the same R6). The Crouch transition is left alone (the get-up always wins). Adds only what is missing.
        /// </summary>
        public static bool EnsureSlamTail(AnimatorController controller)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != SlamTailParam))
            {
                controller.AddParameter(SlamTailParam, AnimatorControllerParameterType.Bool);
                changed = true;
            }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                string dest = t.destinationState != null ? t.destinationState.name : null;
                if (dest != CombatState.KnockedDown.ToString() && dest != CombatState.KnockedOut.ToString() &&
                    dest != WrestlerView.FallStateName)
                    continue;
                if (t.conditions.Any(c => c.parameter == SlamTailParam))
                    continue;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, SlamTailParam);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Stomp bounce (D-047): parameter StompBounce (bool, set by WrestlerView); state "StompBounce" playing
        /// Slot_StompBounce once at 1x (Animator speed still follows the hitstop / slow-mo hook). It has no Any State
        /// transition: WrestlerView plays it from its start on HitLanded(Stomp) for a downed wrestler, and plays the lying
        /// pose (Fall / KnockedOut / KnockedDown at its end) when the clip ends. The Any State -&gt; KnockedDown / KnockedOut /
        /// Fall transitions get "StompBounce false" so they don't cut it short. The Crouch transition is left alone (the
        /// D-043 get-up always wins). Adds only what is missing.
        /// </summary>
        public static bool EnsureStompBounceState(AnimatorController controller)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != StompBounceParam))
            {
                controller.AddParameter(StompBounceParam, AnimatorControllerParameterType.Bool);
                changed = true;
            }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                string dest = t.destinationState != null ? t.destinationState.name : null;
                if (dest != CombatState.KnockedDown.ToString() && dest != CombatState.KnockedOut.ToString() &&
                    dest != WrestlerView.FallStateName)
                    continue;
                if (t.conditions.Any(c => c.parameter == StompBounceParam))
                    continue;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, StompBounceParam);
                changed = true;
            }
            if (sm.states.All(s => s.state.name != WrestlerView.StompBounceStateName))
            {
                AnimatorState state = sm.AddState(WrestlerView.StompBounceStateName, new Vector3(40, 520));
                state.motion = WriteSlotClip(SlotStompBounce, loop: false);
                state.writeDefaultValues = false;
                changed = true;
            }
            return changed;
        }

        private static bool EnsureTimedState(AnimatorStateMachine sm, string stateName, string slotName, bool loop,
            CombatState combatState, MoveId move, bool? paired, Vector3 position)
        {
            if (sm.states.Any(s => s.state.name == stateName))
                return false;

            AnimatorState state = sm.AddState(stateName, position);
            state.motion = WriteSlotClip(slotName, loop);
            state.writeDefaultValues = false;
            state.speed = 1f;
            state.speedParameter = MoveSpeedParam;
            state.speedParameterActive = true;

            AnimatorStateTransition t = AddAnyStateTransition(sm, state, combatState);
            t.AddCondition(AnimatorConditionMode.Equals, (int)move, MoveParam);
            if (paired.HasValue)
                t.AddCondition(paired.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, PairedParam);
            return true;
        }

        /// <summary>
        /// Second hit reaction (presentation only; Combat has one Struck state): parameter StruckB (bool, set by
        /// WrestlerView, toggled on each hit); state StruckB playing Slot_StruckB, Any State when State == Struck and
        /// StruckB; the existing Any State -> Struck transition gets the matching "StruckB false" condition. Instant,
        /// no exit time, no self-transition. Adds only what is missing.
        /// </summary>
        private static bool EnsureStruckBState(AnimatorController controller, AnimatorStateMachine sm)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != StruckBParam))
            {
                controller.AddParameter(StruckBParam, AnimatorControllerParameterType.Bool);
                changed = true;
            }
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                if (t.destinationState == null || t.destinationState.name != WrestlerView.StruckStateName)
                    continue;
                if (t.conditions.Any(c => c.parameter == StruckBParam))
                    continue;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, StruckBParam);
                changed = true;
            }
            if (sm.states.All(s => s.state.name != WrestlerView.StruckBStateName))
            {
                AnimatorState state = sm.AddState(WrestlerView.StruckBStateName, new Vector3(300, 160));
                state.motion = WriteSlotClip(SlotStruckB, loop: false);
                state.writeDefaultValues = false;
                AnimatorStateTransition t = AddAnyStateTransition(sm, state, CombatState.Struck);
                t.AddCondition(AnimatorConditionMode.If, 0f, StruckBParam);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Third hit reaction (presentation only): parameter StruckC (bool, set by WrestlerView: true for a Strike 1
        /// hit); state StruckC playing Slot_StruckC, Any State when State == Struck and StruckC; the existing Any State
        /// -> Struck and -> StruckB transitions get the matching "StruckC false" condition. Instant, no exit time, no
        /// self-transition. Adds only what is missing.
        /// </summary>
        private static bool EnsureStruckCState(AnimatorController controller, AnimatorStateMachine sm)
        {
            bool changed = false;
            if (controller.parameters.All(p => p.name != StruckCParam))
            {
                controller.AddParameter(StruckCParam, AnimatorControllerParameterType.Bool);
                changed = true;
            }
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                if (t.destinationState == null || (t.destinationState.name != WrestlerView.StruckStateName &&
                                                   t.destinationState.name != WrestlerView.StruckBStateName))
                    continue;
                if (t.conditions.Any(c => c.parameter == StruckCParam))
                    continue;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, StruckCParam);
                changed = true;
            }
            if (sm.states.All(s => s.state.name != WrestlerView.StruckCStateName))
            {
                AnimatorState state = sm.AddState(WrestlerView.StruckCStateName, new Vector3(40, 240));
                state.motion = WriteSlotClip(SlotStruckC, loop: false);
                state.writeDefaultValues = false;
                AnimatorStateTransition t = AddAnyStateTransition(sm, state, CombatState.Struck);
                t.AddCondition(AnimatorConditionMode.If, 0f, StruckCParam);
                changed = true;
            }
            return changed;
        }

        private static bool EnsureClipState(AnimatorStateMachine sm, CombatState combatState, string slotName, Vector3 position)
        {
            string stateName = combatState.ToString();
            if (sm.states.Any(s => s.state.name == stateName))
                return false;
            AnimatorState state = sm.AddState(stateName, position);
            state.motion = WriteSlotClip(slotName, loop: false);
            state.writeDefaultValues = false;
            AddAnyStateTransition(sm, state, combatState);
            return true;
        }

        private static AnimatorState AddFrontBackState(AnimatorController controller, AnimatorStateMachine sm, CombatState combatState,
            AnimationClip front, AnimationClip back, Vector3 position) =>
            AddFrontBackState(controller, sm, combatState.ToString(), front, back, position);

        private static AnimatorState AddFrontBackState(AnimatorController controller, AnimatorStateMachine sm, string stateName,
            AnimationClip front, AnimationClip back, Vector3 position)
        {
            AnimatorState state = sm.AddState(stateName, position);
            state.writeDefaultValues = false;
            var tree = new BlendTree
            {
                name = stateName + "_FrontBack",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Back",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(front, 0f);
            tree.AddChild(back, 1f);
            state.motion = tree;
            return state;
        }

        private static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState target, CombatState combatState)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(target);
            t.AddCondition(AnimatorConditionMode.Equals, (int)combatState, "State");
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0f;
            t.offset = 0f;
            t.canTransitionToSelf = false;
            t.interruptionSource = TransitionInterruptionSource.None;
            return t;
        }

        private static AnimationClip WriteSlotClip(string slotName, bool loop = true)
        {
            string path = $"{SharedFolder}/{slotName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
                return clip;
            clip = new AnimationClip { name = slotName, frameRate = SourceFps };
            SetLoop(clip, loop);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---------- move clips + contact table ----------

        private static MoveAnimationTable GetOrCreateMoveTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<MoveAnimationTable>(MoveTablePath);
            if (table != null)
                return table;
            table = ScriptableObject.CreateInstance<MoveAnimationTable>();
            AssetDatabase.CreateAsset(table, MoveTablePath);
            return table;
        }

        /// <summary>Writes a non-looping move clip, maps it to its slot, and records its contact time. Null spec = skipped.</summary>
        private static void WriteMoveClip(MoveClipSpec move, string path, MoveId moveId, string slotName,
            Dictionary<string, AnimationClip> bySlot, MoveAnimationTable table)
        {
            if (move == null)
                return;
            TimedClip timed = WriteTimedClip(path, move.Sprites, move.KeyFrames, move.LengthFrames, false, null,
                ClipPlayback.ContactScaled, move.KeyFrames[move.ContactIndex], move.DefaultHoldsMs,
                move.TurnIndex >= 0 ? move.KeyFrames[move.TurnIndex] : -1);
            bySlot[slotName] = timed.Clip;
            table.SetEntry(moveId, timed.Clip, timed.ContactTime, timed.TurnTime);
        }

        // ---------- per-wrestler clips ----------

        private static readonly EditorCurveBinding SpriteBinding =
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        // Sprite-swap clips (WriteSpriteClip / WriteTimedClip, below) are on the Animator's own GameObject. keyFrames,
        // loopFrames and pose frames are the built-in timing in SourceFps frames; the wrestler's WrestlerFrameTiming
        // asset (designer Hold (ms) per step, defaults = these frames) decides the actual key times.

        /// <summary>
        /// One stepped pose key for a stand-in clip: child-sprite offset (native pixels), z rotation (degrees,
        /// about the feet pivot) and tint, at a SourceFps frame. Authored for left-facing art: +x = backwards,
        /// negative rotation = leaning back. WrestlerView mirrors x and rotation when the sprite is flipped.
        /// Never moves the wrestler root (ARCHITECTURE 6: visual offsets are child-sprite animation).
        /// </summary>
        public struct PoseKey
        {
            public int Frame;
            public float XPx, YPx, RotZ;
            public Color Tint;

            public PoseKey(int frame, float xPx, float yPx, float rotZ, Color? tint = null)
            {
                Frame = frame; XPx = xPx; YPx = yPx; RotZ = rotZ; Tint = tint ?? Color.white;
            }
        }

        private static readonly PoseKey[] RestPose = { new PoseKey(0, 0f, 0f, 0f) };

        private static EditorCurveBinding TransformBinding(string property) =>
            EditorCurveBinding.FloatCurve("", typeof(Transform), property);

        private static EditorCurveBinding ColorBinding(string channel) =>
            EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color." + channel);

        /// <summary>
        /// Writes stepped child-sprite pose curves (local position, local euler z, SpriteRenderer color) to every
        /// clip. Clips without a pose get the rest pose (0 offset, 0 rotation, white), so with Write Defaults off a
        /// stand-in's offset, rotation or tint never sticks into the next state.
        /// </summary>
        private static void WritePoseCurves(AnimationClip clip, PoseKey[] poses, Func<int, float> timeOfFrame)
        {
            bool rest = poses == null || poses.Length == 0;
            poses = rest ? RestPose : poses;
            float KeyTime(PoseKey k) => rest ? 0f : timeOfFrame(k.Frame);
            void Curve(EditorCurveBinding binding, Func<PoseKey, float> value)
            {
                var curve = new AnimationCurve(poses.Select(k => new Keyframe(KeyTime(k), value(k))).ToArray());
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            Curve(TransformBinding("m_LocalPosition.x"), k => k.XPx / WrestlerPpu);
            Curve(TransformBinding("m_LocalPosition.y"), k => k.YPx / WrestlerPpu);
            Curve(TransformBinding("m_LocalPosition.z"), k => 0f);
            Curve(TransformBinding("localEulerAnglesRaw.x"), k => 0f);
            Curve(TransformBinding("localEulerAnglesRaw.y"), k => 0f);
            Curve(TransformBinding("localEulerAnglesRaw.z"), k => k.RotZ);
            Curve(ColorBinding("r"), k => k.Tint.r);
            Curve(ColorBinding("g"), k => k.Tint.g);
            Curve(ColorBinding("b"), k => k.Tint.b);
            Curve(ColorBinding("a"), k => k.Tint.a);
        }

        /// <summary>A written clip and its contact time (seconds at native speed; 0 when it has no contact frame).</summary>
        private struct TimedClip
        {
            public AnimationClip Clip;
            public float ContactTime;
            /// <summary>Seconds (native) to the turn sprite (Irish Whip f2); 0 when the clip has none.</summary>
            public float TurnTime;
        }

        private static AnimationClip WriteSpriteClip(string path, Sprite[] sprites, int[] keyFrames, int loopFrames, bool loop,
            PoseKey[] poses = null, ClipPlayback playback = ClipPlayback.Native) =>
            WriteTimedClip(path, sprites, keyFrames, loopFrames, loop, poses, playback, -1).Clip;

        // ---------- designer frame timing (WrestlerFrameTiming) ----------

        /// <summary>Timing asset of the wrestler being built (set by Build); null = built-in timing only.</summary>
        private static WrestlerFrameTiming currentTiming;

        public static string FrameTimingPath(string wrestlerName) =>
            $"Assets/Animation/{wrestlerName}/{wrestlerName}_FrameTiming.asset";

        /// <summary>The wrestler's frame-timing asset, created empty if missing (the build fills it with defaults).</summary>
        private static WrestlerFrameTiming LoadOrCreateTiming(string wrestlerName)
        {
            string path = FrameTimingPath(wrestlerName);
            var timing = AssetDatabase.LoadAssetAtPath<WrestlerFrameTiming>(path);
            if (timing != null)
                return timing;
            timing = ScriptableObject.CreateInstance<WrestlerFrameTiming>();
            timing.WrestlerName = wrestlerName;
            AssetDatabase.CreateAsset(timing, path);
            return timing;
        }

        /// <summary>
        /// Seconds for a time in ms. When the time is (within 1e-3 ms) a whole number k of SourceFps frames it
        /// returns exactly k / SourceFps, the value the frame-based builder always wrote, so default timing
        /// produces byte-identical clips.
        /// </summary>
        private static float MsToSeconds(double ms)
        {
            double k = Math.Round(ms * SourceFps / 1000.0);
            if (Math.Abs(ms - k * 1000.0 / SourceFps) < 1e-3)
                return (int)k / SourceFps;
            return (float)(ms / 1000.0);
        }

        /// <summary>Shortest Hold (ms) the builder accepts.</summary>
        public const float MinHoldMs = 1f;

        /// <summary>
        /// Splits a clip into steps (every sprite key and every stand-in pose key, in time order; the last step
        /// lasts until the clip length), looks up / creates its entry in the current timing asset, and returns
        /// the cumulative start time of each step plus the total, in ms. The built-in frame timing is the
        /// default: hold = frames to the next step x 1000 / SourceFps. Designer holds are kept while the step
        /// count matches; an entry whose holds all still equal the stored defaults follows new built-in defaults.
        /// </summary>
        private static double[] ResolveStepTimes(string clipName, Sprite[] sprites, int[] keyFrames, int loopFrames, bool loop,
            PoseKey[] poses, ClipPlayback playback, int contactFrame, float[] defaultHoldsMs, out int[] stepFrames)
        {
            var frames = new SortedSet<int>(keyFrames);
            if (poses != null)
                foreach (PoseKey p in poses)
                {
                    if (p.Frame < 0 || p.Frame >= loopFrames)
                        throw new ArgumentException($"{clipName}: pose key at frame {p.Frame} is outside the clip (0..{loopFrames - 1}).");
                    frames.Add(p.Frame);
                }
            stepFrames = frames.ToArray();
            int n = stepFrames.Length;
            var keySet = new HashSet<int>(keyFrames);
            var stepSprites = new Sprite[n];
            var defaults = new float[n];
            int contactStep = -1;
            for (int s = 0; s < n; s++)
            {
                int f = stepFrames[s];
                int last = 0;
                for (int i = 0; i < keyFrames.Length; i++)
                    if (keyFrames[i] <= f) last = i;
                stepSprites[s] = sprites[last];
                int next = s + 1 < n ? stepFrames[s + 1] : loopFrames;
                defaults[s] = (next - f) * 1000f / SourceFps;
                if (f == contactFrame) contactStep = s;
            }
            if (defaultHoldsMs != null)
            {
                // Built-in holds given in ms (not whole 24 fps frames): the steps are the sprites, one hold each.
                if (defaultHoldsMs.Length != n)
                    throw new ArgumentException($"{clipName}: {defaultHoldsMs.Length} default holds for {n} steps.");
                for (int s = 0; s < n; s++)
                    defaults[s] = Mathf.Max(MinHoldMs, defaultHoldsMs[s]);
            }
            if (contactFrame >= 0 && contactStep < 0)
                throw new ArgumentException($"{clipName}: contact frame {contactFrame} is not a step of the clip.");

            float[] holds = defaults;
            if (currentTiming != null)
            {
                WrestlerFrameTiming.ClipTiming entry = currentTiming.Find(clipName);
                if (entry == null)
                {
                    entry = new WrestlerFrameTiming.ClipTiming { ClipName = clipName };
                    currentTiming.Clips.Add(entry);
                }
                bool sameShape = entry.Steps.Count == n;
                // Designer-approved defaults ('Set current as default') survive rebuilds while the clip keeps its step count.
                bool customDefaults = sameShape && entry.CustomDefaults;
                bool untouched = sameShape && entry.Steps.All(st => Mathf.Abs(st.HoldMs - st.DefaultHoldMs) < 1e-3f);
                if (!sameShape && entry.Steps.Count > 0)
                    Debug.LogWarning($"WrestlerAnimationBuilder: {currentTiming.name} {clipName} had {entry.Steps.Count} steps, " +
                                     $"the clip now has {n}; its Hold (ms) values were reset to the built-in defaults" +
                                     (entry.CustomDefaults ? " and its custom defaults were cleared." : "."));
                if (!sameShape)
                {
                    entry.Steps.Clear();
                    for (int s = 0; s < n; s++)
                        entry.Steps.Add(new WrestlerFrameTiming.Step());
                    entry.CustomDefaults = false;
                }
                for (int s = 0; s < n; s++)
                {
                    WrestlerFrameTiming.Step st = entry.Steps[s];
                    st.Sprite = stepSprites[s];
                    st.PoseOnly = !keySet.Contains(stepFrames[s]);
                    st.BuiltInHoldMs = defaults[s];
                    if (customDefaults)
                        continue; // keep the designer's Hold (ms) and Default values as they are
                    if (!sameShape || untouched)
                        st.HoldMs = defaults[s];
                    st.DefaultHoldMs = defaults[s];
                }
                entry.Loops = loop;
                entry.Playback = playback;
                entry.ContactStep = contactStep;
                EditorUtility.SetDirty(currentTiming);
                holds = entry.Steps.Select(st => Mathf.Max(MinHoldMs, st.HoldMs)).ToArray();
            }

            var start = new double[n + 1];
            for (int s = 0; s < n; s++)
                start[s + 1] = start[s] + holds[s];
            return start;
        }

        private static TimedClip WriteTimedClip(string path, Sprite[] sprites, int[] keyFrames, int loopFrames, bool loop,
            PoseKey[] poses, ClipPlayback playback, int contactFrame, float[] defaultHoldsMs = null, int turnFrame = -1)
        {
            string clipName = System.IO.Path.GetFileNameWithoutExtension(path);
            double[] startMs = ResolveStepTimes(clipName, sprites, keyFrames, loopFrames, loop, poses, playback, contactFrame,
                defaultHoldsMs, out int[] stepFrames);
            double totalMs = startMs[startMs.Length - 1];
            double MsOfFrame(int frame)
            {
                int s = Array.IndexOf(stepFrames, frame);
                if (s < 0) throw new ArgumentException($"{clipName}: frame {frame} is not a step.");
                return startMs[s];
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew)
                clip = new AnimationClip();

            clip.ClearCurves();
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, null);
            clip.frameRate = SourceFps;

            // Unity makes a sprite clip one sample (1 / SourceFps) longer than its last key, so the hold key that
            // repeats the last sprite goes one sample before the total: the clip is then exactly total long. A last
            // step shorter than one sample therefore shows for one sample (the Inspector warns about it).
            var keys = new List<ObjectReferenceKeyframe>();
            for (int i = 0; i < sprites.Length; i++)
                keys.Add(new ObjectReferenceKeyframe { time = MsToSeconds(MsOfFrame(keyFrames[i])), value = sprites[i] });
            double holdKeyMs = totalMs - 1000.0 / SourceFps;
            if (holdKeyMs > MsOfFrame(keyFrames[keyFrames.Length - 1]) + 1e-3)
                keys.Add(new ObjectReferenceKeyframe { time = MsToSeconds(holdKeyMs), value = sprites[sprites.Length - 1] });

            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys.ToArray());
            WritePoseCurves(clip, poses, frame => MsToSeconds(MsOfFrame(frame)));
            SetLoop(clip, loop);

            if (isNew)
                AssetDatabase.CreateAsset(clip, path);
            else
                EditorUtility.SetDirty(clip);
            return new TimedClip
            {
                Clip = clip,
                ContactTime = contactFrame >= 0 ? MsToSeconds(MsOfFrame(contactFrame)) : 0f,
                TurnTime = turnFrame >= 0 ? MsToSeconds(MsOfFrame(turnFrame)) : 0f,
            };
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static AnimatorOverrideController WriteOverride(string path, AnimatorController controller, Dictionary<string, AnimationClip> bySlot)
        {
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            bool isNew = aoc == null;
            if (isNew)
                aoc = new AnimatorOverrideController(controller);
            else
                aoc.runtimeAnimatorController = controller;

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
            aoc.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                AnimationClip slot = pairs[i].Key;
                bySlot.TryGetValue(slot.name, out AnimationClip replacement);
                if (replacement == null)
                    Debug.LogWarning($"WrestlerAnimationBuilder: no clip for slot {slot.name} in {path}.");
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(slot, replacement);
            }
            aoc.ApplyOverrides(pairs);

            if (isNew)
                AssetDatabase.CreateAsset(aoc, path);
            else
                EditorUtility.SetDirty(aoc);
            return aoc;
        }

        // ---------- prefab ----------

        /// <summary>
        /// Root (WrestlerView) with child "Sprite" (SpriteRenderer + Animator) at local (0,0,0), so the sprite's
        /// bottom-center pivot (feet) sits exactly on the root, which Combat's runner places at the feet point.
        /// No combat components. An existing prefab is edited in place (LoadPrefabContents) so its file IDs, and the
        /// WrestlerDefinition references to its root, survive; only the presentation fields are rewritten.
        /// </summary>
        /// <summary>
        /// Opaque x-extent of a sprite relative to its pivot, in world units as drawn (left-facing art): the first
        /// and one-past-last pixel columns with any alpha. Reads the source PNG (the texture is not readable).
        /// </summary>
        public static Vector2 OpaqueExtentX(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(path)))
                    throw new InvalidOperationException($"Could not read {path}.");
                Rect r = sprite.rect;
                Color32[] px = tex.GetPixels32();
                int min = int.MaxValue, max = -1;
                for (int y = (int)r.y; y < (int)(r.y + r.height); y++)
                    for (int x = (int)r.x; x < (int)(r.x + r.width); x++)
                        if (px[y * tex.width + x].a > 0)
                        {
                            int col = x - (int)r.x;
                            if (col < min) min = col;
                            if (col > max) max = col;
                        }
                if (max < 0)
                    throw new InvalidOperationException($"{sprite.name} is fully transparent.");
                return new Vector2((min - sprite.pivot.x) / sprite.pixelsPerUnit, (max + 1 - sprite.pivot.x) / sprite.pixelsPerUnit);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// Sets the prefab's WrestlerView.downedMirrorShift to (left + right) of the downed sprite's opaque x-extent:
        /// the x shift that puts the mirrored downed sprite back on the same ground as the unmirrored one. Writes the
        /// prefab only when the value changes. No-op if the prefab does not exist yet.
        /// </summary>
        /// <summary>
        /// D-044: extra x shift (world units, toward the fall side) of the landed downed sprite after a fall, so a downed
        /// sprite centred on the body (Peaux's R6) lands with its feet end where fall3's feet end is (feet toward what hit
        /// him, the D-041 look), instead of jumping back over the root. For a fall to +x, fall3 (stored head right) is
        /// drawn unflipped and its feet end is its left opaque edge; the mirrored, shifted downed sprite covers its stored
        /// span [left, right] with the feet on the left. 0 when the spec keeps the D-041 landing or has no fall art.
        /// </summary>
        private static float FallLandingShift(WrestlerArtSpec spec, Vector2 downedExtent)
        {
            if (!spec.AlignFallLandingToFeet || spec.Fall == null || spec.Fall.Sprites.Length <= FallLandIndex)
                return 0f;
            Vector2 fall3 = OpaqueExtentX(spec.Fall.Sprites[FallLandIndex - 1]);
            return fall3.x - downedExtent.x;
        }

        private static void WriteDownedMirrorShift(WrestlerArtSpec spec)
        {
            string path = $"{PrefabFolder}/{spec.Name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || spec.Bodyslammed == null)
                return;
            Vector2 extent = OpaqueExtentX(spec.Bodyslammed.Sprites[spec.Bodyslammed.Sprites.Length - 1]);
            float shift = extent.x + extent.y;
            float landing = FallLandingShift(spec, extent);
            var current = prefab.GetComponent<WrestlerView>();
            if (current != null && Mathf.Abs(current.DownedMirrorShift - shift) < 1e-4f &&
                Mathf.Abs(current.FallLandingShift - landing) < 1e-4f)
                return;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = contents.GetComponent<WrestlerView>();
                if (view == null)
                    return;
                var so = new SerializedObject(view);
                so.FindProperty("downedMirrorShift").floatValue = shift;
                so.FindProperty("fallLandingShift").floatValue = landing;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, path, out bool ok);
                if (!ok)
                    Debug.LogError($"WrestlerAnimationBuilder: could not save {path}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static string WritePrefab(WrestlerArtSpec spec, AnimatorOverrideController overrides, MoveAnimationTable table)
        {
            EnsureFolder(PrefabFolder);
            string path = $"{PrefabFolder}/{spec.Name}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = contents.GetComponent<WrestlerView>();
                    var animator = contents.GetComponentInChildren<Animator>();
                    var sr = contents.GetComponentInChildren<SpriteRenderer>();
                    if (view == null || animator == null || sr == null)
                        throw new InvalidOperationException($"{path} is missing WrestlerView, Animator or SpriteRenderer; not rewritten.");
                    animator.runtimeAnimatorController = overrides;
                    var so = new SerializedObject(view);
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.FindProperty("spriteRenderer").objectReferenceValue = sr;
                    so.FindProperty("artFacesLeft").boolValue = spec.ArtFacesLeft;
                    so.FindProperty("moveTable").objectReferenceValue = table;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, path, out bool ok);
                    if (!ok)
                        throw new InvalidOperationException($"Saving prefab {path} failed.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                return path;
            }

            var root = new GameObject(spec.Name);
            try
            {
                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(root.transform, false);
                spriteGo.transform.localPosition = Vector3.zero;

                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = spec.StandFront;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.sortingLayerID = 0; // Default, same as the Arena props
                sr.sortingOrder = 0;

                var animator = spriteGo.AddComponent<Animator>();
                animator.runtimeAnimatorController = overrides;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.applyRootMotion = false;

                var view = root.AddComponent<WrestlerView>();
                var so = new SerializedObject(view);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.FindProperty("spriteRenderer").objectReferenceValue = sr;
                so.FindProperty("artFacesLeft").boolValue = spec.ArtFacesLeft;
                so.FindProperty("moveTable").objectReferenceValue = table;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success)
                    throw new InvalidOperationException($"Saving prefab {path} failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            return path;
        }

        // ---------- helpers ----------

        /// <summary>Sprites of a texture, ordered by the numeric suffix of their names (sheet_0, sheet_1, ...).</summary>
        public static Sprite[] LoadSprites(string texturePath)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>()
                .OrderBy(s => SuffixNumber(s.name)).ThenBy(s => s.name, StringComparer.Ordinal).ToArray();
            if (sprites.Length == 0)
                throw new InvalidOperationException($"No sprites at {texturePath}.");
            return sprites;
        }

        private static int SuffixNumber(string name)
        {
            int underscore = name.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int n) ? n : -1;
        }

        private static void Validate(WrestlerArtSpec spec)
        {
            if (string.IsNullOrEmpty(spec.Name)) throw new ArgumentException("Name required.");
            if (spec.StandFront == null) throw new ArgumentException("StandFront required.");
            CheckSheet("WalkFront", spec.WalkFront, spec.WalkKeyFrames, spec.WalkLoopFrames);
            if (spec.Run != null)
                CheckSheet("Run", spec.Run, spec.RunKeyFrames, spec.RunLoopFrames);
            if (spec.WalkBack != null)
                CheckSheet("WalkBack", spec.WalkBack, spec.WalkBackKeyFrames, spec.WalkBackLoopFrames);
            CheckMove("Strike1", spec.Strike1);
            CheckMove("Strike2", spec.Strike2);
            CheckMove("Strike3", spec.Strike3);
            CheckMove("Stomp", spec.Stomp);
            if (spec.LockUp != null)
                CheckSheet("LockUp", spec.LockUp, LockUpHoldKeyFrames, LockUpHoldLengthFrames);
            if (spec.LockedUp != null)
                CheckSheet("LockedUp", spec.LockedUp, LockUpHoldKeyFrames, LockUpHoldLengthFrames);
            CheckMove("Bodyslam", spec.Bodyslam);
            CheckMove("Bodyslammed", spec.Bodyslammed);
            CheckMove("IrishWhip", spec.IrishWhip);
            if (spec.IrishWhip != null && (spec.IrishWhip.TurnIndex < 1 || spec.IrishWhip.TurnIndex >= spec.IrishWhip.Sprites.Length))
                throw new ArgumentException("IrishWhip: turn index out of range.");
            CheckMove("Fall", spec.Fall);
            if (spec.Fall != null && (spec.Fall.TurnIndex < 1 || spec.Fall.TurnIndex >= spec.Fall.Sprites.Length))
                throw new ArgumentException("Fall: landing index out of range.");
        }

        private static void CheckMove(string label, MoveClipSpec move)
        {
            if (move == null)
                return;
            CheckSheet(label, move.Sprites, move.KeyFrames, move.LengthFrames);
            if (move.ContactIndex < 0 || move.ContactIndex >= move.Sprites.Length)
                throw new ArgumentException($"{label}: contact index out of range.");
        }

        private static void CheckSheet(string label, Sprite[] sprites, int[] keys, int loopFrames)
        {
            if (sprites == null || sprites.Length == 0) throw new ArgumentException($"{label}: no sprites.");
            if (keys == null || keys.Length != sprites.Length) throw new ArgumentException($"{label}: need one key frame per sprite.");
            if (loopFrames <= keys[keys.Length - 1]) throw new ArgumentException($"{label}: loop length must be after the last key.");
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
