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
    /// and references survive. Standing, Walking, Running, the three Striking clips (Strike1-3), and PLACEHOLDER stand-ins
    /// for Stomp, Struck, Knocked Down / Knocked Out and Recovering (stand + walk) built from the stand/walk sprites.
    ///
    /// Menu: Rural Rumble/Tooling/Build Barry Animations + Prefab, Rural Rumble/Tooling/Build Peaux Animations + Prefab.
    /// A wrestler with no run or strike art gets PLACEHOLDER stand-ins for those too (WriteMissingArtStandIns).
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
        // Combat-core slice slots. Barry fills these with stand-in (PLACEHOLDER) clips until real art exists.
        public const string SlotStomp = "Slot_Stomp";
        public const string SlotStruck = "Slot_Struck";
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
            };
            return Build(spec, writePrefab);
        }

        [MenuItem("Rural Rumble/Tooling/Build Peaux Animations + Prefab")]
        public static void BuildPeauxMenu() => BuildPeaux();

        /// <summary>
        /// Peaux (approved 2026-10-31: gen-peaux-stand-front, gen-peaux-walk-front-8, gen-peaux-stand-back-a,
        /// gen-peaux-walk-back-8, gen-peaux-run-6, gen-peaux-strike1-3, gen-peaux-strike2-3, gen-peaux-strike3-2).
        /// Stomp and the other combat-core slots are PLACEHOLDER stand-ins built from those sprites. The earlier
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
            };
            return Build(spec, writePrefab);
        }

        /// <summary>Imports Peaux's approved sheets with the wrestler settings: stand front 102x215 and back 92x214
        /// (one sprite each), walk front 8 x 114x219, walk back 8 x 108x222, run 6 x 158x207, strikes 3 x 222x212,
        /// 3 x 212x223 and 2 x 150x214.</summary>
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
            });
        }

        /// <summary>
        /// Imports Barry's strike sheets (approved 2026-10-31) with the wrestler settings and slices them by their
        /// own cell sizes (ASSET_REGISTRY / designer: 296x243, 236x242, 200x256).
        /// </summary>
        public static string ImportBarryStrikeSheets()
        {
            const string art = "Assets/Art/Wrestlers/Barry/";
            return string.Join("\n", new[]
            {
                ImportSheet(art + "barry_strike1.png", 296, 243),
                ImportSheet(art + "barry_strike2.png", 236, 242),
                ImportSheet(art + "barry_strike3.png", 200, 256),
            });
        }

        // ---------- sprite import ----------

        /// <summary>
        /// Wrestler sheet import (ARCHITECTURE 3.1): Sprite (Multiple), PPU 60, Point, uncompressed, no mipmaps,
        /// Clamp, Full Rect, then a single-row-or-grid slice at the sheet's own cell size, read left-to-right,
        /// top-to-bottom, named {file}_{i}, pivot Bottom Center. Never rescales. Existing sprite IDs are kept by name.
        /// </summary>
        public static string ImportSheet(string texturePath, int cellWidth, int cellHeight)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"No TextureImporter at {texturePath}.");

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width % cellWidth != 0 || height % cellHeight != 0)
                throw new InvalidOperationException(
                    $"{texturePath} is {width}x{height}, not a whole number of {cellWidth}x{cellHeight} cells.");

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
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
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
                    rect.alignment = SpriteAlignment.BottomCenter;
                    rect.pivot = new Vector2(0.5f, 0f);
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
            return $"{texturePath}: {width}x{height}, {rects.Count} sprites of {cellWidth}x{cellHeight}.";
        }

        /// <summary>Builds (or updates) the shared controller, the wrestler's clips and override, and its prefab. Returns a summary.</summary>
        public static string Build(WrestlerArtSpec spec, bool writePrefab = true)
        {
            Validate(spec);
            AnimatorController controller = GetOrCreateSharedController();

            string folder = "Assets/Animation/" + spec.Name;
            EnsureFolder(folder);

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
            string standIns = WriteMissingArtStandIns(spec, folder, bySlot, table) + WriteStandIns(spec, folder, bySlot, table)
                              + " " + WriteLockUpStandIns(spec, folder, bySlot, table);
            EditorUtility.SetDirty(table);

            AnimatorOverrideController overrides = WriteOverride($"{folder}/{spec.Name}.overrideController", controller, bySlot);

            string prefabPath = writePrefab ? WritePrefab(spec, overrides, table) : "(not rebuilt)";
            AssetDatabase.SaveAssets();

            return $"Built {spec.Name}: controller {ControllerPath}, override {AssetDatabase.GetAssetPath(overrides)}, prefab {prefabPath}. " +
                   $"Placeholders: {(standBackPlaceholder ? "StandBack " : "")}{(walkBackPlaceholder ? "WalkBack " : "")}{standIns}".TrimEnd();
        }

        // ---------- combat-core stand-ins (PLACEHOLDER) ----------

        /// <summary>Stomp stand-in contact frame (boot comes down), in SourceFps frames: 5 = 0.208 s at 1x.</summary>
        public const int StompStandInContactFrame = 5;

        private static readonly Color HitTint = new Color(1f, 0.45f, 0.45f, 1f);
        private static readonly Color HitTintFade = new Color(1f, 0.7f, 0.7f, 1f);
        private static readonly Color HitTintFaint = new Color(1f, 0.88f, 0.88f, 1f);

        /// <summary>
        /// Temporary clips for states with no art yet, built only from the wrestler's approved stand/walk sprites
        /// with child-sprite offset, rotation and tint (ARCHITECTURE 6). Named {Name}_{State}_PLACEHOLDER.anim.
        /// All keys are stepped at SourceFps. Offsets are whole native pixels, authored for left-facing art.
        /// Knocked Out maps to the Knocked Down clip (D-023). Returns the list of stand-in names for the summary.
        /// </summary>
        private static string WriteStandIns(WrestlerArtSpec spec, string folder, Dictionary<string, AnimationClip> bySlot,
            MoveAnimationTable table)
        {
            string P(string state) => $"{folder}/{spec.Name}_{state}_PLACEHOLDER.anim";
            Sprite[] stand = { spec.StandFront };
            Sprite[] standBack = { spec.StandBack != null ? spec.StandBack : spec.StandFront };
            int[] one = { 0 };

            // Struck: red flash that fades, knocked back 6 px and leaning back 6 deg, recovering over 0.25 s. 0.417 s long
            // (Struck lasts 0.4 s by default, D-024); restarted on every hit.
            AnimationClip struck = WriteSpriteClip(P("Struck"), stand, one, 10, loop: false, new[]
            {
                new PoseKey(0, 6, 0, -6, HitTint),
                new PoseKey(2, 4, 0, -4, HitTintFade),
                new PoseKey(4, 2, 0, -2, HitTintFaint),
                new PoseKey(6, 0, 0, 0),
            });

            // Knocked Down: falls backwards over 4 frames (red flash on the first), bounces 9 px, then lies on the back.
            // Lying pose: rotated -90 about the feet (head toward +x = behind the wrestler), shifted -120 px so the
            // 240 px body is centered on the feet point (spans root x -2..+2 units), and raised 45 px so the
            // 102 px-thick body rests on the feet line (bottom edge 6 px below it). The sort point (sprite pivot)
            // is then 45 px (0.75 units) behind the feet, so a stomper within stomp range draws in front.
            // The x offsets are authored for a 240 px stand sprite and scale with the wrestler's stand height
            // (whole pixels; exactly the authored values for Barry's 240 px).
            float h = spec.StandFront.rect.height;
            float L(float xPx) => Mathf.Round(xPx * h / 240f);
            AnimationClip knockedDown = WriteSpriteClip(P("KnockedDown"), stand, one, 12, loop: false, new[]
            {
                new PoseKey(0, 4, 0, -12, HitTint),
                new PoseKey(1, L(-20), 6, -30, HitTintFade),
                new PoseKey(2, L(-60), 18, -55),
                new PoseKey(3, L(-100), 36, -80),
                new PoseKey(4, L(-120), 54, -90),
                new PoseKey(6, L(-120), 45, -90),
            });

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

            // Stomp: rises 10 px leaning back (wind-up), slams down 2 px leaning forward onto the boot at the contact
            // frame (5 = 0.208 s), settles by frame 9. 0.5 s long; WrestlerView scales it to the tuned Startup.
            AnimationClip stomp = WriteSpriteClip(P("Stomp"), stand, one, 12, loop: false, new[]
            {
                new PoseKey(0, 0, 0, 0),
                new PoseKey(1, 0, 2, -4),
                new PoseKey(2, 0, 6, -8),
                new PoseKey(3, 0, 10, -10),
                new PoseKey(StompStandInContactFrame, 0, -2, 6),
                new PoseKey(7, 0, 0, 3),
                new PoseKey(9, 0, 0, 0),
            });
            table.SetEntry(MoveId.Stomp, stomp, StompStandInContactFrame / SourceFps);

            bySlot[SlotStruck] = struck;
            bySlot[SlotKnockedDown] = knockedDown;
            bySlot[SlotKnockedOut] = knockedDown; // D-023: Knocked Out reuses Knocked Down
            bySlot[SlotRecoveringStandFront] = recStand;
            bySlot[SlotRecoveringStandBack] = recStandBack;
            bySlot[SlotRecoveringWalkFront] = recWalk;
            bySlot[SlotRecoveringWalkBack] = recWalkBack;
            bySlot[SlotStomp] = stomp;
            return "Struck KnockedDown(+KnockedOut) RecoveringStand RecoveringStandBack RecoveringWalk RecoveringWalkBack Stomp";
        }

        // ---------- lock-up slice stand-ins (PLACEHOLDER) ----------

        /// <summary>Lock Up / Locked Up hold loop length (SourceFps frames): a 0.5 s push-pull.</summary>
        public const int LockUpHoldLoopFrames = 12;
        /// <summary>Whiff and break stand-in length (SourceFps frames, 0.417 s native). WrestlerView fits them to the
        /// move's total time (whiff 0.4 s, break 0.2 + 0.2 s by default), so no contact frame is recorded.</summary>
        public const int LockUpOneShotFrames = 10;

        /// <summary>
        /// Lock-up slice stand-ins (ARCHITECTURE 6, D-031), built only from the wrestler's approved stand and strike
        /// sprites with child-sprite offset and lean (no new art). Named {Name}_{State}_PLACEHOLDER.anim.
        /// Authored for left-facing art: -x = toward the opponent, +rotation = leaning forward. The pair stands
        /// lockUpPairSpacing apart (Combat, 1.2 units = 72 px by default), which already overlaps the two bodies, so
        /// the hold keeps A at rest and B leaning back 6-8 px; the break shoves both further apart before settling.
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

            AnimationClip hold = WriteSpriteClip(P(WrestlerView.LockUpStateName), new[] { reach }, new[] { 0 },
                LockUpHoldLoopFrames, loop: true, new[]
                {
                    new PoseKey(0, 0, 0, 4),
                    new PoseKey(6, -2, 0, 6),
                });
            AnimationClip held = WriteSpriteClip(P(WrestlerView.LockedUpStateName), new[] { braced }, new[] { 0 },
                LockUpHoldLoopFrames, loop: true, new[]
                {
                    new PoseKey(0, 6, 0, -6),
                    new PoseKey(6, 8, -2, -8),
                });

            AnimationClip breakA = WriteSpriteClip(P(WrestlerView.BreakLockUpStateName), new[] { reach, stand }, new[] { 0, 2 },
                LockUpOneShotFrames, loop: false, new[]
                {
                    new PoseKey(0, 0, 0, 4),
                    new PoseKey(2, 4, 0, -4),
                    new PoseKey(4, 8, 0, -6),
                    new PoseKey(6, 6, 0, -3),
                    new PoseKey(8, 2, 0, 0),
                    new PoseKey(9, 0, 0, 0),
                });
            AnimationClip breakB = WriteSpriteClip(P(WrestlerView.BreakLockUpReceiverStateName), new[] { braced, stand }, new[] { 0, 2 },
                LockUpOneShotFrames, loop: false, new[]
                {
                    new PoseKey(0, 6, 0, -6),
                    new PoseKey(2, 10, 0, -8),
                    new PoseKey(4, 14, 0, -10),
                    new PoseKey(6, 10, 0, -5),
                    new PoseKey(8, 4, 0, -2),
                    new PoseKey(9, 0, 0, 0),
                });

            AnimationClip whiff = WriteSpriteClip(P(WrestlerView.LockUpWhiffStateName), new[] { windUp, reach, stand }, new[] { 0, 2, 8 },
                LockUpOneShotFrames, loop: false, new[]
                {
                    new PoseKey(0, 2, 0, -3),
                    new PoseKey(2, -6, 0, 6),
                    new PoseKey(4, -8, 0, 8),
                    new PoseKey(6, -6, -1, 6),
                    new PoseKey(8, -2, 0, 2),
                    new PoseKey(9, 0, 0, 0),
                });

            // Contact time 0 = no contact frame: WrestlerView fits the clip to the move's total time.
            table.SetEntry(MoveId.LockUp, whiff, 0f);
            table.SetEntry(MoveId.BreakLockUp, breakA, 0f);
            table.SetEntry(MoveId.BreakLockUp, breakB, 0f);

            bySlot[SlotLockUp] = hold;
            bySlot[SlotLockedUp] = held;
            bySlot[SlotBreakLockUp] = breakA;
            bySlot[SlotBreakLockUpReceiver] = breakB;
            bySlot[SlotLockUpWhiff] = whiff;
            return "LockUp LockedUp BreakLockUp BreakLockUpReceiver LockUpWhiff";
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
                AnimationClip clip = WriteSpriteClip($"{folder}/{spec.Name}_{move}_PLACEHOLDER.anim", stand, one, length,
                    loop: false, poses);
                bySlot[slot] = clip;
                table.SetEntry(move, clip, contact / SourceFps);
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
            AnimationClip clip = WriteSpriteClip(path, move.Sprites, move.KeyFrames, move.LengthFrames, loop: false);
            bySlot[slotName] = clip;
            table.SetEntry(moveId, clip, move.ContactTime);
        }

        // ---------- per-wrestler clips ----------

        private static readonly EditorCurveBinding SpriteBinding =
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        /// <summary>
        /// Sprite-swap clip on the Animator's own GameObject. keyFrames are in SourceFps frames. Unity makes a
        /// sprite clip one sample longer than its last key, so a hold key repeating the last sprite goes at
        /// loopFrames - 1: the clip is then exactly loopFrames long and the last sprite holds until the loop point.
        /// </summary>
        private static AnimationClip WriteSpriteClip(string path, Sprite[] sprites, int[] keyFrames, int loopFrames, bool loop) =>
            WriteSpriteClip(path, sprites, keyFrames, loopFrames, loop, null);

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
        private static void WritePoseCurves(AnimationClip clip, PoseKey[] poses)
        {
            poses = poses == null || poses.Length == 0 ? RestPose : poses;
            float KeyTime(PoseKey k) => k.Frame / SourceFps;
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

        private static AnimationClip WriteSpriteClip(string path, Sprite[] sprites, int[] keyFrames, int loopFrames, bool loop, PoseKey[] poses)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew)
                clip = new AnimationClip();

            clip.ClearCurves();
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, null);
            clip.frameRate = SourceFps;

            var keys = new List<ObjectReferenceKeyframe>();
            for (int i = 0; i < sprites.Length; i++)
                keys.Add(new ObjectReferenceKeyframe { time = keyFrames[i] / SourceFps, value = sprites[i] });
            if (loopFrames - 1 > keyFrames[keyFrames.Length - 1])
                keys.Add(new ObjectReferenceKeyframe { time = (loopFrames - 1) / SourceFps, value = sprites[sprites.Length - 1] });

            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys.ToArray());
            WritePoseCurves(clip, poses);
            SetLoop(clip, loop);

            if (isNew)
                AssetDatabase.CreateAsset(clip, path);
            else
                EditorUtility.SetDirty(clip);
            return clip;
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
