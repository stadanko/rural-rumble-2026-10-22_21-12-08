using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuralRumble.EditorTooling
{
    /// <summary>How a clip's timing reaches the screen at runtime (WrestlerView + MoveAnimationTable).</summary>
    public enum ClipPlayback
    {
        /// <summary>Plays at 1x: the Hold (ms) values are exactly what the player sees.</summary>
        Native,
        /// <summary>Speed-scaled so the contact sprite lands at the move's Startup (CombatTuning, D-024/D-033).</summary>
        ContactScaled,
        /// <summary>Stretched so the whole clip fills the move's total time (CombatTuning): lock-up whiff and break.</summary>
        FittedToMove,
    }

    /// <summary>
    /// Per-wrestler, designer-editable on-screen time of every sprite (or pose step) in every clip that
    /// WrestlerAnimationBuilder builds (GDD "Inspector-Tunable Values": animation speed at each step).
    /// Editor-only data: WrestlerAnimationBuilder reads it when it writes the .anim clips and the contact times in
    /// MoveAnimationTable. Not live in Play Mode: edit, then run Rural Rumble/Rebuild Wrestler Animations (or the
    /// Rebuild button on this asset's Inspector). Created by the builder with today's timing if missing.
    /// Asset: Assets/Animation/{Wrestler}/{Wrestler}_FrameTiming.asset.
    /// </summary>
    public sealed class WrestlerFrameTiming : ScriptableObject
    {
        [Serializable]
        public sealed class Step
        {
            [Tooltip("Sprite shown during this step (set by the builder; change the art through the Art Wrangler).")]
            public Sprite Sprite;
            [Tooltip("True when only the stand-in pose (offset, lean, tint) changes here, not the sprite.")]
            public bool PoseOnly;
            [Tooltip("How long this step stays on screen, in milliseconds, before the next one (for a looping clip, " +
                     "the last step holds until the loop restarts).")]
            [Min(1f)] public float HoldMs;
            [Tooltip("This step's default: what 'Reset to defaults' restores. Starts as the built-in timing; " +
                     "'Set current as default' replaces it with the designer's approved value.")]
            public float DefaultHoldMs;
            [Tooltip("The builder's original built-in timing for this step (set on every rebuild; read-only). " +
                     "'Restore built-in defaults' copies it back into Default.")]
            public float BuiltInHoldMs;
        }

        [Serializable]
        public sealed class ClipTiming
        {
            [Tooltip("The .anim clip this timing builds (in this wrestler's Assets/Animation folder).")]
            public string ClipName;
            [Tooltip("Looping clips repeat; one-shot clips hold their last sprite.")]
            public bool Loops;
            [Tooltip("How the clip is played in game.")]
            public ClipPlayback Playback;
            [Tooltip("Index of the contact step (the hit lands on this sprite); -1 = none.")]
            public int ContactStep = -1;
            [Tooltip("True after 'Set current as default': the Default values are the designer's, and rebuilds keep " +
                     "them instead of refreshing them from the builder's built-in timing.")]
            public bool CustomDefaults;
            public List<Step> Steps = new List<Step>();

            /// <summary>Total clip length at native speed, in ms.</summary>
            public float TotalMs
            {
                get
                {
                    double t = 0;
                    foreach (Step s in Steps) t += s.HoldMs;
                    return (float)t;
                }
            }

            /// <summary>Time from clip start to the given step, at native speed, in ms.</summary>
            public float StartMs(int step)
            {
                double t = 0;
                for (int i = 0; i < step && i < Steps.Count; i++) t += Steps[i].HoldMs;
                return (float)t;
            }
        }

        [Tooltip("Wrestler these clips belong to.")]
        public string WrestlerName;
        public List<ClipTiming> Clips = new List<ClipTiming>();

        public ClipTiming Find(string clipName)
        {
            foreach (ClipTiming c in Clips)
                if (c.ClipName == clipName) return c;
            return null;
        }
    }
}
