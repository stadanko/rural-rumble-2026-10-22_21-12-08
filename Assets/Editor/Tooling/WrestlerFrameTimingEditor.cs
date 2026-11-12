using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEditor;
using UnityEngine;

namespace RuralRumble.EditorTooling
{
    /// <summary>
    /// Inspector for WrestlerFrameTiming: one foldout per clip, a row per sprite (thumbnail, name, start time,
    /// editable Hold (ms), default), the clip total and contact time, and a Rebuild button that runs
    /// Rural Rumble/Rebuild Wrestler Animations.
    /// </summary>
    [CustomEditor(typeof(WrestlerFrameTiming))]
    public sealed class WrestlerFrameTimingEditor : Editor
    {
        private const float Thumb = 40f;
        private static readonly float OneSampleMs = 1000f / WrestlerAnimationBuilder.SourceFps;
        private const string TuningPath = "Assets/Data/Tuning/CombatTuning.asset";

        // The shared combat tuning (edited in place from this Inspector) and the two wrestlers' definitions.
        private SerializedObject tuningSo;
        private WrestlerDefinition selfDef;
        private WrestlerDefinition otherDef;

        private void OnEnable()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
            if (tuning == null)
                foreach (string guid in AssetDatabase.FindAssets("t:CombatTuning"))
                {
                    tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(AssetDatabase.GUIDToAssetPath(guid));
                    if (tuning != null) break;
                }
            tuningSo = tuning != null ? new SerializedObject(tuning) : null;

            string wrestler = ((WrestlerFrameTiming)target).WrestlerName;
            foreach (string guid in AssetDatabase.FindAssets("t:WrestlerDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<WrestlerDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                if (def.DisplayName == wrestler) selfDef = def;
                else if (otherDef == null) otherDef = def;
            }
        }

        public override void OnInspectorGUI()
        {
            var timing = (WrestlerFrameTiming)target;
            serializedObject.Update();
            tuningSo?.Update();

            EditorGUILayout.HelpBox(
                "How long each sprite of each animation stays on screen, in milliseconds.\n" +
                "1. Change Hold (ms) values.  2. Click Rebuild Wrestler Animations (also in the Rural Rumble menu).\n" +
                "Changes take effect only after a rebuild, and not while in Play Mode.\n" +
                "Moves with a contact sprite (marked HIT) are sped up or slowed down in game so the hit lands when " +
                "combat says (CombatTuning Startup): here you set the rhythm, combat sets when the hit lands.",
                MessageType.Info);
            DrawRebuildButton();

            SerializedProperty clips = serializedObject.FindProperty(nameof(WrestlerFrameTiming.Clips));
            DrawWholeAssetDefaultButtons(timing, clips);
            for (int c = 0; c < timing.Clips.Count; c++)
                DrawClip(timing, timing.Clips[c], clips.GetArrayElementAtIndex(c));

            serializedObject.ApplyModifiedProperties();
            tuningSo?.ApplyModifiedProperties();
            EditorGUILayout.Space();
            DrawRebuildButton();
        }

        private static void DrawRebuildButton()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(new GUIContent("Rebuild Wrestler Animations",
                        "Rebuilds Barry's and Peaux's animation clips and hit timings from their FrameTiming assets."),
                        GUILayout.Height(28)))
                    EditorApplication.delayCall += () =>
                    {
                        string summary = WrestlerAnimationBuilder.RebuildWrestlerAnimations();
                        Debug.Log(summary);
                    };
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Leave Play Mode to rebuild.", MessageType.None);
        }

        // ---------------------------------------------------------------- defaults

        /// <summary>Makes the clip's current Hold (ms) values its defaults ('Reset to defaults' returns to them,
        /// and rebuilds keep them). Doesn't change the animation itself, so no rebuild is needed.</summary>
        private static void SetCurrentAsDefault(SerializedProperty clipProp)
        {
            SerializedProperty steps = clipProp.FindPropertyRelative(nameof(WrestlerFrameTiming.ClipTiming.Steps));
            for (int s = 0; s < steps.arraySize; s++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(s);
                step.FindPropertyRelative(nameof(WrestlerFrameTiming.Step.DefaultHoldMs)).floatValue =
                    step.FindPropertyRelative(nameof(WrestlerFrameTiming.Step.HoldMs)).floatValue;
            }
            clipProp.FindPropertyRelative(nameof(WrestlerFrameTiming.ClipTiming.CustomDefaults)).boolValue = true;
        }

        /// <summary>Puts the builder's original built-in timing back into the Default column (Hold (ms) is untouched).</summary>
        private static void RestoreBuiltInDefaults(SerializedProperty clipProp)
        {
            SerializedProperty steps = clipProp.FindPropertyRelative(nameof(WrestlerFrameTiming.ClipTiming.Steps));
            for (int s = 0; s < steps.arraySize; s++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(s);
                float builtIn = step.FindPropertyRelative(nameof(WrestlerFrameTiming.Step.BuiltInHoldMs)).floatValue;
                if (builtIn > 0f)
                    step.FindPropertyRelative(nameof(WrestlerFrameTiming.Step.DefaultHoldMs)).floatValue = builtIn;
            }
            clipProp.FindPropertyRelative(nameof(WrestlerFrameTiming.ClipTiming.CustomDefaults)).boolValue = false;
        }

        private static bool HoldsMatchDefaults(WrestlerFrameTiming.ClipTiming clip)
        {
            foreach (WrestlerFrameTiming.Step st in clip.Steps)
                if (Mathf.Abs(st.HoldMs - st.DefaultHoldMs) >= 1e-3f) return false;
            return true;
        }

        private static void DrawWholeAssetDefaultButtons(WrestlerFrameTiming timing, SerializedProperty clips)
        {
            bool anyChanged = false, anyCustom = false;
            foreach (WrestlerFrameTiming.ClipTiming clip in timing.Clips)
            {
                anyChanged |= !HoldsMatchDefaults(clip);
                anyCustom |= clip.CustomDefaults;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!anyChanged))
                    if (GUILayout.Button(new GUIContent("Set ALL current values as defaults",
                            "Every clip's current Hold (ms) values become its defaults. The animation doesn't change.")) &&
                        EditorUtility.DisplayDialog("Set all as defaults",
                            $"Make the current Hold (ms) values of all {timing.Clips.Count} clips in {timing.name} their defaults?\n\n" +
                            "'Reset to defaults' will return to these values from now on. The animations themselves don't change.",
                            "Set as defaults", "Cancel"))
                        for (int c = 0; c < clips.arraySize; c++)
                            SetCurrentAsDefault(clips.GetArrayElementAtIndex(c));

                using (new EditorGUI.DisabledScope(!anyCustom))
                    if (GUILayout.Button(new GUIContent("Restore ALL built-in defaults",
                            "Put the original built-in timing back into every clip's Default column. Hold (ms) values are kept.")) &&
                        EditorUtility.DisplayDialog("Restore built-in defaults",
                            $"Replace the custom defaults of every clip in {timing.name} with the original built-in timing?\n\n" +
                            "Current Hold (ms) values are kept; only the Default column changes.",
                            "Restore", "Cancel"))
                        for (int c = 0; c < clips.arraySize; c++)
                            RestoreBuiltInDefaults(clips.GetArrayElementAtIndex(c));
            }
            EditorGUILayout.Space(4);
        }

        private static string PlaybackText(WrestlerFrameTiming.ClipTiming clip)
        {
            switch (clip.Playback)
            {
                case ClipPlayback.ContactScaled:
                    return "Speed-scaled in game: the whole clip is sped up or slowed down so the HIT sprite appears at the " +
                           "move's Startup in CombatTuning. Hold (ms) sets the rhythm and proportions; it does not change " +
                           "when the hit lands.";
                case ClipPlayback.FittedToMove:
                    return "Fitted in game: the whole clip is stretched to the move's total time in CombatTuning. " +
                           "Hold (ms) sets the proportions between sprites only.";
                default:
                    return "Plays at exactly these times in game.";
            }
        }

        private void DrawClip(WrestlerFrameTiming timing, WrestlerFrameTiming.ClipTiming clip, SerializedProperty clipProp)
        {
            string key = $"RR.FrameTiming.{timing.name}.{clip.ClipName}";
            float total = clip.TotalMs;
            string contact = clip.ContactStep >= 0 ? $", hit at {clip.StartMs(clip.ContactStep):0.#} ms" : "";
            string label = $"{clip.ClipName}   ({(clip.Loops ? "loops" : "plays once")}, {total:0.#} ms{contact})" +
                           (clip.CustomDefaults ? "   [custom defaults]" : "");
            bool open = SessionState.GetBool(key, false);
            bool now = EditorGUILayout.Foldout(open, label, true);
            if (now != open) SessionState.SetBool(key, now);
            if (!now) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(PlaybackText(clip), EditorStyles.wordWrappedMiniLabel);
                DrawCombatTiming(timing, clip);

                SerializedProperty steps = clipProp.FindPropertyRelative(nameof(WrestlerFrameTiming.ClipTiming.Steps));
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(Thumb + 20);
                    GUILayout.Label("Sprite", EditorStyles.miniBoldLabel, GUILayout.MinWidth(120));
                    GUILayout.Label(new GUIContent("Starts at", "Time from the start of the clip, in ms (read-only)."),
                        EditorStyles.miniBoldLabel, GUILayout.Width(64));
                    GUILayout.Label(new GUIContent("Hold (ms)", "How long this sprite stays on screen, in milliseconds."),
                        EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    GUILayout.Label(new GUIContent("Default", clip.CustomDefaults
                            ? "Your approved timing for this sprite, in ms (set with 'Set current as default')."
                            : "Built-in timing for this sprite, in ms."),
                        EditorStyles.miniBoldLabel, GUILayout.Width(56));
                    GUILayout.Space(36);
                }

                for (int s = 0; s < clip.Steps.Count; s++)
                {
                    WrestlerFrameTiming.Step step = clip.Steps[s];
                    SerializedProperty hold = steps.GetArrayElementAtIndex(s).FindPropertyRelative(nameof(WrestlerFrameTiming.Step.HoldMs));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16);
                        DrawSprite(GUILayoutUtility.GetRect(Thumb, Thumb, GUILayout.Width(Thumb), GUILayout.Height(Thumb)), step.Sprite);
                        string name = step.Sprite != null ? step.Sprite.name : "(none)";
                        if (step.PoseOnly) name += "  (pose change)";
                        GUILayout.Label($"{s + 1}. {name}", GUILayout.MinWidth(120));
                        GUILayout.Label($"{clip.StartMs(s):0.#}", GUILayout.Width(64));
                        EditorGUI.BeginChangeCheck();
                        float v = EditorGUILayout.DelayedFloatField(hold.floatValue, GUILayout.Width(80));
                        if (EditorGUI.EndChangeCheck())
                            hold.floatValue = Mathf.Max(WrestlerAnimationBuilder.MinHoldMs, v);
                        GUILayout.Label($"{step.DefaultHoldMs:0.#}", EditorStyles.miniLabel, GUILayout.Width(56));
                        GUILayout.Label(s == clip.ContactStep ? new GUIContent("HIT", "Contact sprite: the hit lands when this sprite appears.") : GUIContent.none,
                            EditorStyles.boldLabel, GUILayout.Width(36));
                    }
                }

                EditorGUILayout.LabelField("Total", $"{total:0.#} ms" +
                    (clip.ContactStep >= 0 ? $"   (hit at {clip.StartMs(clip.ContactStep):0.#} ms before speed scaling)" : ""));
                if (clip.Steps.Count > 0 && clip.Steps[clip.Steps.Count - 1].HoldMs < OneSampleMs - 1e-3f)
                    EditorGUILayout.HelpBox($"The last sprite always shows for at least {OneSampleMs:0.#} ms (one 24 fps sample); " +
                                            "shorter values make the clip that much longer.", MessageType.Warning);

                bool atDefaults = HoldsMatchDefaults(clip);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(atDefaults))
                    {
                        if (GUILayout.Button(new GUIContent("Set current as default",
                                "Make these Hold (ms) values this clip's defaults: 'Reset to defaults' returns to them and " +
                                "rebuilds keep them. The animation itself doesn't change."), GUILayout.Width(150)))
                            SetCurrentAsDefault(clipProp);
                        if (GUILayout.Button(new GUIContent("Reset to defaults", "Restore this clip's Hold (ms) values to its Default column."),
                                GUILayout.Width(130)))
                            for (int s = 0; s < clip.Steps.Count; s++)
                                steps.GetArrayElementAtIndex(s).FindPropertyRelative(nameof(WrestlerFrameTiming.Step.HoldMs)).floatValue =
                                    clip.Steps[s].DefaultHoldMs;
                    }
                    using (new EditorGUI.DisabledScope(!clip.CustomDefaults))
                        if (GUILayout.Button(new GUIContent("Restore built-in defaults",
                                "Put the original built-in timing back into the Default column. Hold (ms) values are kept."),
                                GUILayout.Width(160)))
                            RestoreBuiltInDefaults(clipProp);
                }
                EditorGUILayout.Space(4);
            }
        }

        // ---------------------------------------------------------------- combat timing (CombatTuning)

        /// <summary>Clip name without the wrestler prefix and the _PLACEHOLDER suffix, e.g. "Strike1", "Bodyslammed".</summary>
        private static string MoveKey(string clipName, string wrestler)
        {
            string key = clipName;
            if (!string.IsNullOrEmpty(wrestler) && key.StartsWith(wrestler + "_")) key = key.Substring(wrestler.Length + 1);
            if (key.EndsWith("_PLACEHOLDER")) key = key.Substring(0, key.Length - "_PLACEHOLDER".Length);
            return key;
        }

        /// <summary>
        /// Shows, inside the clip's foldout, the CombatTuning values that decide how the clip plays in game, editable
        /// in place (same asset as Assets/Data/Tuning/CombatTuning.asset, with Undo). Contact-scaled clips also get the
        /// resulting in-game playback speed and a button that sets the combat timing so the clip plays at 1x.
        /// </summary>
        private void DrawCombatTiming(WrestlerFrameTiming timing, WrestlerFrameTiming.ClipTiming clip)
        {
            string move = MoveKey(clip.ClipName, timing.WrestlerName);
            SerializedProperty moveTiming = null;   // a MoveTiming (Startup/Active/Recovery)
            SerializedProperty startup = null;      // the value the HIT sprite lands on (contact-scaled clips)
            SerializedProperty single = null;       // a single duration (whiff, struck, lock-up timer)
            string singleLabel = null, note = null;
            float fittedTotal = -1f;
            bool startupIsMoveTimings = false; // startup is moveTiming's own Startup (strikes, stomp, bodyslam fallback)

            switch (move)
            {
                case "Strike1": case "Strike2": case "Strike3": case "Stomp":
                    moveTiming = tuningSo?.FindProperty(char.ToLowerInvariant(move[0]) + move.Substring(1) + "Timing");
                    startup = moveTiming?.FindPropertyRelative(nameof(MoveTiming.Startup));
                    startupIsMoveTimings = true;
                    break;
                case "Bodyslam": case "Bodyslammed":
                {
                    WrestlerDefinition initiator = move == "Bodyslam" ? selfDef : otherDef;
                    startup = BodyslamStartupFor(initiator);
                    if (startup == null)
                    {
                        moveTiming = tuningSo?.FindProperty(nameof(CombatTuning.bodyslamTiming));
                        startup = moveTiming?.FindPropertyRelative(nameof(MoveTiming.Startup));
                        startupIsMoveTimings = true;
                        note = $"No per-wrestler impact time for {(initiator != null ? initiator.DisplayName : "this slammer")}; " +
                               "using the shared bodyslam Startup.";
                    }
                    else
                        note = $"Impact time when {(initiator != null ? initiator.DisplayName : "?")} slams. " +
                               (move == "Bodyslam"
                                   ? $"{(otherDef != null ? otherDef.DisplayName : "The opponent")}'s Bodyslammed clip uses the same value: keep both clips' timing in step."
                                   : $"Set by the slammer; {(initiator != null ? initiator.DisplayName : "the slammer")}'s Bodyslam clip uses the same value.");
                    break;
                }
                case "LockUpWhiff":
                    single = tuningSo?.FindProperty(nameof(CombatTuning.lockUpWhiffTime));
                    singleLabel = "Whiff lasts (s)";
                    if (single != null) fittedTotal = single.floatValue;
                    break;
                case "BreakLockUp": case "BreakLockUpReceiver":
                    moveTiming = tuningSo?.FindProperty(nameof(CombatTuning.breakLockUpTiming));
                    break;
                case "Struck":
                    single = tuningSo?.FindProperty(nameof(CombatTuning.struckDuration));
                    singleLabel = "Struck lasts (s)";
                    note = "How long the hit reaction lasts in combat; the clip itself plays at the times above.";
                    break;
                case "LockUp": case "LockedUp":
                    single = tuningSo?.FindProperty(nameof(CombatTuning.lockUpTimer));
                    singleLabel = "Lock-up lasts up to (s)";
                    note = "How long the lock-up holds before it breaks; the clip plays at the times above, last sprite held.";
                    break;
                default:
                    return; // walk, run, stand, knocked down, recovering: no combat timing to show
            }

            if (tuningSo == null)
            {
                EditorGUILayout.HelpBox("CombatTuning asset not found, so its timing can't be shown here.", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(new GUIContent("Combat timing  (CombatTuning: changes gameplay)",
                    "These are the shared combat values in Assets/Data/Tuning/CombatTuning.asset, edited in place. " +
                    "They change when hits land and how long moves last for both wrestlers and the CPU. No rebuild needed."),
                    EditorStyles.miniBoldLabel);
                if (moveTiming != null && startup != null && startupIsMoveTimings)
                {
                    EditorGUILayout.PropertyField(startup, new GUIContent("Hit lands at (Startup, s)",
                        "Seconds from the start of the move until the hit lands (the HIT sprite is shown then)."));
                    EditorGUILayout.PropertyField(moveTiming.FindPropertyRelative(nameof(MoveTiming.Active)),
                        new GUIContent("Hit window (Active, s)", "Seconds the hit can connect."));
                    EditorGUILayout.PropertyField(moveTiming.FindPropertyRelative(nameof(MoveTiming.Recovery)),
                        new GUIContent("Recovery (s)", "Seconds after the hit window before the wrestler can act again."));
                }
                else if (startup != null)
                {
                    EditorGUILayout.PropertyField(startup, new GUIContent("Impact at (s)",
                        "Seconds from the start of the bodyslam until the body hits the ground (damage lands then)."));
                    SerializedProperty shared = tuningSo.FindProperty(nameof(CombatTuning.bodyslamTiming));
                    if (shared != null)
                    {
                        EditorGUILayout.PropertyField(shared.FindPropertyRelative(nameof(MoveTiming.Active)),
                            new GUIContent("After impact: Active (s)", "Shared by both bodyslams."));
                        EditorGUILayout.PropertyField(shared.FindPropertyRelative(nameof(MoveTiming.Recovery)),
                            new GUIContent("After impact: Recovery (s)", "Shared by both bodyslams."));
                    }
                }
                else if (moveTiming != null)
                {
                    EditorGUILayout.PropertyField(moveTiming, new GUIContent("Move timing (s)"), true);
                    fittedTotal = moveTiming.FindPropertyRelative(nameof(MoveTiming.Startup)).floatValue +
                                  moveTiming.FindPropertyRelative(nameof(MoveTiming.Active)).floatValue +
                                  moveTiming.FindPropertyRelative(nameof(MoveTiming.Recovery)).floatValue;
                }
                if (single != null)
                    EditorGUILayout.PropertyField(single, new GUIContent(singleLabel));
                if (note != null)
                    EditorGUILayout.LabelField(note, EditorStyles.wordWrappedMiniLabel);

                // What the player actually sees.
                if (startup != null && clip.ContactStep >= 0)
                {
                    float contactS = clip.StartMs(clip.ContactStep) / 1000f;
                    float hitAt = startup.floatValue;
                    DrawSpeedReadout(contactS, hitAt, $"the HIT sprite is {contactS * 1000f:0.#} ms into the clip and lands at {hitAt * 1000f:0.#} ms in game");
                    using (new EditorGUI.DisabledScope(Mathf.Abs(contactS - hitAt) < 0.0005f))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("Match combat to this clip (play at 1x)",
                                "Sets the combat value above to the clip's HIT time, so the clip plays exactly as timed here. " +
                                "This changes when the hit lands in gameplay."), GUILayout.Width(240)) &&
                            EditorUtility.DisplayDialog("Change combat timing?",
                                $"Set the combat value to {contactS:0.###} s (now {hitAt:0.###} s)?\n\n" +
                                "The hit, and its damage, will land at that time in every match, for both wrestlers and the CPU.",
                                "Change it", "Cancel"))
                            startup.floatValue = contactS;
                    }
                }
                else if (fittedTotal > 0f && clip.Playback == ClipPlayback.FittedToMove)
                    DrawSpeedReadout(clip.TotalMs / 1000f, fittedTotal, $"the clip is {clip.TotalMs:0.#} ms and is fitted to the move's {fittedTotal * 1000f:0.#} ms");
            }
        }

        private static void DrawSpeedReadout(float clipSeconds, float gameSeconds, string detail)
        {
            if (gameSeconds <= 0f)
            {
                EditorGUILayout.HelpBox("The combat time is 0, so the clip can't be timed to it.", MessageType.Warning);
                return;
            }
            float speed = clipSeconds / gameSeconds;
            float shown = Mathf.Clamp(speed, RuralRumble.Presentation.WrestlerView.MinMoveSpeed, RuralRumble.Presentation.WrestlerView.MaxMoveSpeed);
            string text = $"In game this clip plays at {shown:0.##}x speed ({detail}).";
            MessageType type = Mathf.Abs(speed - 1f) < 0.01f ? MessageType.None : MessageType.Info;
            if (!Mathf.Approximately(speed, shown))
            {
                text += $" Limited from {speed:0.##}x.";
                type = MessageType.Warning;
            }
            EditorGUILayout.HelpBox(text, type);
        }

        /// <summary>The bodyslamStartupByInitiator entry for this slammer, or null if it has none.</summary>
        private SerializedProperty BodyslamStartupFor(WrestlerDefinition initiator)
        {
            SerializedProperty list = tuningSo?.FindProperty(nameof(CombatTuning.bodyslamStartupByInitiator));
            if (list == null || initiator == null) return null;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative(nameof(WrestlerStartup.wrestler)).objectReferenceValue == initiator)
                    return e.FindPropertyRelative(nameof(WrestlerStartup.startup));
            }
            return null;
        }

        private static void DrawSprite(Rect r, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || Event.current.type != EventType.Repaint)
                return;
            Rect tr = sprite.textureRect;
            float scale = Mathf.Min(r.width / tr.width, r.height / tr.height);
            var fit = new Rect(0, 0, tr.width * scale, tr.height * scale) { center = r.center };
            var uv = new Rect(tr.x / sprite.texture.width, tr.y / sprite.texture.height,
                tr.width / sprite.texture.width, tr.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(fit, sprite.texture, uv);
        }
    }
}
