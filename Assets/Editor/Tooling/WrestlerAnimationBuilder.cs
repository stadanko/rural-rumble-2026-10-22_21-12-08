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
    /// and references survive. Standing, Walking, Running, and the three Striking clips (Strike1-3).
    ///
    /// Menu: Rural Rumble/Tooling/Build Barry Animations + Prefab.
    /// </summary>
    public static class WrestlerAnimationBuilder
    {
        public const string SharedFolder = "Assets/Animation/Shared";
        public const string ControllerPath = SharedFolder + "/Wrestler.controller";
        public const string PrefabFolder = "Assets/Prefabs/Wrestlers";
        public const string MoveTablePath = "Assets/Animation/MoveAnimationTable.asset";

        /// <summary>Animator int parameter: (int)MoveId of the current move. WrestlerView does not set it yet.</summary>
        public const string MoveParam = "Move";
        /// <summary>Animator float parameter (default 1): speed multiplier on move states only, so presentation can
        /// land the contact frame on the tuned Startup (ARCHITECTURE 4.2). WrestlerView does not set it yet.</summary>
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
            public Sprite[] Run;
            public int[] RunKeyFrames;
            public int RunLoopFrames;
            public MoveClipSpec Strike1;             // right punch
            public MoveClipSpec Strike2;             // left punch
            public MoveClipSpec Strike3;             // combo finisher
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
            AnimationClip run = WriteSpriteClip($"{folder}/{spec.Name}_Run.anim",
                spec.Run, spec.RunKeyFrames, spec.RunLoopFrames, loop: true);

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
            EditorUtility.SetDirty(table);

            AnimatorOverrideController overrides = WriteOverride($"{folder}/{spec.Name}.overrideController", controller, bySlot);

            string prefabPath = writePrefab ? WritePrefab(spec, overrides) : "(not rebuilt)";
            AssetDatabase.SaveAssets();

            return $"Built {spec.Name}: controller {ControllerPath}, override {AssetDatabase.GetAssetPath(overrides)}, prefab {prefabPath}. " +
                   $"Placeholders: {(standBackPlaceholder ? "StandBack " : "")}{(walkBackPlaceholder ? "WalkBack" : "")}".TrimEnd();
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

        private static AnimatorState AddFrontBackState(AnimatorController controller, AnimatorStateMachine sm, CombatState combatState,
            AnimationClip front, AnimationClip back, Vector3 position)
        {
            AnimatorState state = sm.AddState(combatState.ToString(), position);
            state.writeDefaultValues = false;
            var tree = new BlendTree
            {
                name = combatState + "_FrontBack",
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

        private static void AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState target, CombatState combatState)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(target);
            t.AddCondition(AnimatorConditionMode.Equals, (int)combatState, "State");
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0f;
            t.offset = 0f;
            t.canTransitionToSelf = false;
            t.interruptionSource = TransitionInterruptionSource.None;
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
        private static AnimationClip WriteSpriteClip(string path, Sprite[] sprites, int[] keyFrames, int loopFrames, bool loop)
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
        /// No combat components.
        /// </summary>
        private static string WritePrefab(WrestlerArtSpec spec, AnimatorOverrideController overrides)
        {
            EnsureFolder(PrefabFolder);
            string path = $"{PrefabFolder}/{spec.Name}.prefab";

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
