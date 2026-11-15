using System;
using System.Collections.Generic;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Presentation
{
    /// <summary>
    /// Contact time of each move clip at native speed (ARCHITECTURE 4.2, Attack timing). On MoveStarted,
    /// presentation looks up the clip that is about to play and sets playback speed so the contact frame
    /// lands at the move's tuned Startup. Data only: never triggers hits or state changes.
    ///
    /// Entries are keyed by clip, so one table serves every wrestler (each wrestler's override controller
    /// supplies its own clip for a move). Asset: Assets/Animation/MoveAnimationTable.asset, written by
    /// WrestlerAnimationBuilder (Animation &amp; Tooling).
    ///
    /// Moves with no contact frame (the lock-up whiff and both sides of BreakLockUp) are recorded with
    /// ContactTime 0, meaning the whole clip is stretched to the move's Startup + Active + Recovery. The looping
    /// Lock Up / Locked Up holds have no entry (native speed). The lock-up slice clips are PLACEHOLDER stand-ins
    /// built from the approved stand and strike sprites (WrestlerAnimationBuilder.WriteLockUpStandIns).
    /// Both bodyslam sides (Move Bodyslam, D-033) are recorded with their impact frame as ContactTime, so the pair
    /// lands the impact at the Bodyslam Startup.
    /// The whipper's Irish Whip clips (Move IrishWhip, D-039) record the release (start of f4) as ContactTime and the
    /// start of f2 as TurnTime: WrestlerView draws the whipper facing the lock-up facing before TurnTime and the whip
    /// direction from TurnTime on (clip time at native speed, so it follows the sprite change at any playback speed).
    /// The Falling Down clips (D-041) have an entry with Move None, no contact time and the landing (start of the downed
    /// sprite) as TurnTime: WrestlerView switches from the fall frames' orientation to the mirrored downed sprite there.
    /// </summary>
    [CreateAssetMenu(menuName = "Rural Rumble/Move Animation Table", fileName = "MoveAnimationTable")]
    public sealed class MoveAnimationTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Move this clip plays for.")]
            public MoveId Move;
            [Tooltip("The wrestler's own clip (the override for the move's shared slot).")]
            public AnimationClip Clip;
            [Tooltip("Seconds from clip start to the first frame of the contact pose, at native (1x) speed. " +
                     "0 = no contact frame: the whole clip is fitted to the move's total time (lock-up whiff, break).")]
            public float ContactTime;
            [Tooltip("Seconds from clip start (native speed) at which the art turns to face a second direction; 0 = none. " +
                     "Irish Whip only (D-039): the start of f2, where the whipper turns to the whip direction.")]
            public float TurnTime;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Contact time (native speed) recorded for this clip.</summary>
        public bool TryGetContactTime(AnimationClip clip, out float contactTime)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Clip == clip && clip != null)
                {
                    contactTime = entries[i].ContactTime;
                    return true;
                }
            }
            contactTime = 0f;
            return false;
        }

        /// <summary>Turn time (native speed, seconds) recorded for this clip; false if none (no entry or 0).</summary>
        public bool TryGetTurnTime(AnimationClip clip, out float turnTime)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Clip == clip && clip != null && entries[i].TurnTime > 0f)
                {
                    turnTime = entries[i].TurnTime;
                    return true;
                }
            }
            turnTime = 0f;
            return false;
        }

        /// <summary>Adds or replaces the entry for this clip. Editor tooling only.</summary>
        public void SetEntry(MoveId move, AnimationClip clip, float contactTime, float turnTime = 0f)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            var entry = new Entry { Move = move, Clip = clip, ContactTime = contactTime, TurnTime = turnTime };
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Clip == clip)
                {
                    entries[i] = entry;
                    return;
                }
            }
            entries.Add(entry);
        }
    }
}
