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
    /// and references survive. Movement slice only: Standing, Walking, Running.
    ///
    /// Menu: Rural Rumble/Tooling/Build Barry Animations + Prefab.
    /// </summary>
    public static class WrestlerAnimationBuilder
    {
        public const string SharedFolder = "Assets/Animation/Shared";
        public const string ControllerPath = SharedFolder + "/Wrestler.controller";
        public const string PrefabFolder = "Assets/Prefabs/Wrestlers";

        /// <summary>Clips sampled from real-time video at 24 fps; keys sit on the source video frame offsets.</summary>
        public const float SourceFps = 24f;

        // Slot clips: empty clips the shared graph references; each wrestler's override controller replaces them.
        public const string SlotStandFront = "Slot_StandFront";
        public const string SlotStandBack = "Slot_StandBack";
        public const string SlotWalkFront = "Slot_WalkFront";
        public const string SlotWalkBack = "Slot_WalkBack";
        public const string SlotRun = "Slot_Run";

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
        }

        [MenuItem("Rural Rumble/Tooling/Build Barry Animations + Prefab")]
        public static void BuildBarryMenu() => BuildBarry();

        public static string BuildBarry()
        {
            const string art = "Assets/Art/Wrestlers/Barry/";
            var spec = new WrestlerArtSpec
            {
                Name = "Barry",
                ArtFacesLeft = true,
                StandFront = LoadSprites(art + "barry_stand_front.png").Single(),
                WalkFront = LoadSprites(art + "barry_walk_front.png"),
                // Video frames 53,56,59,62,66,69,72,75, loop 53-78 at 24 fps (ASSET_REGISTRY gen-barry-walk-front-8).
                WalkKeyFrames = new[] { 0, 3, 6, 9, 13, 16, 19, 22 },
                WalkLoopFrames = 25,
                Run = LoadSprites(art + "barry_run.png"),
                // Video frames 32-42 every 2nd, loop 32-44 at 24 fps (ASSET_REGISTRY gen-barry-run-6).
                RunKeyFrames = new[] { 0, 2, 4, 6, 8, 10 },
                RunLoopFrames = 12,
            };
            return Build(spec);
        }

        /// <summary>Builds (or updates) the shared controller, the wrestler's clips and override, and its prefab. Returns a summary.</summary>
        public static string Build(WrestlerArtSpec spec)
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

            AnimatorOverrideController overrides = WriteOverride($"{folder}/{spec.Name}.overrideController", controller,
                new Dictionary<string, AnimationClip>
                {
                    { SlotStandFront, standFront },
                    { SlotStandBack, standBack },
                    { SlotWalkFront, walkFront },
                    { SlotWalkBack, walkBack },
                    { SlotRun, run },
                });

            string prefabPath = WritePrefab(spec, overrides);
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
                return existing;

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

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
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

        private static AnimationClip WriteSlotClip(string slotName)
        {
            string path = $"{SharedFolder}/{slotName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
                return clip;
            clip = new AnimationClip { name = slotName, frameRate = SourceFps };
            SetLoop(clip, true);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---------- per-wrestler clips ----------

        private static readonly EditorCurveBinding SpriteBinding =
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        /// <summary>
        /// Sprite-swap clip on the Animator's own GameObject. keyFrames are in SourceFps frames; a final key
        /// repeats the last sprite at loopFrames so the last frame holds for its full duration.
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
            if (loopFrames > keyFrames[keyFrames.Length - 1])
                keys.Add(new ObjectReferenceKeyframe { time = loopFrames / SourceFps, value = sprites[sprites.Length - 1] });

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
