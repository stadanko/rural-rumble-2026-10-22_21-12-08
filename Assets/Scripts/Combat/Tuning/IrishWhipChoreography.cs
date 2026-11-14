using System;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Where the whipped wrestler's root is during each whipper frame (D-039), in pixels (60 px = 1 unit) along
    /// the whip direction from the whipper's root: negative = on the side the runner comes from, positive = past
    /// the whipper in the whip direction. The runner holds each frame's offset for that whole frame (f1-f3); f4
    /// is where the runner starts the follow-through, after which it runs on at runSpeed.
    /// </summary>
    [Serializable]
    public struct WhipRunnerOffsets
    {
        [Tooltip("Runner offset during whipper frame 1 (push start), px along the whip direction.")]
        public float f1;
        [Tooltip("Runner offset during whipper frame 2 (turn), px along the whip direction.")]
        public float f2;
        [Tooltip("Runner offset during whipper frame 3 (final push), px along the whip direction.")]
        public float f3;
        [Tooltip("Runner offset when whipper frame 4 (follow-through) starts, px along the whip direction. From there the runner runs on at runSpeed.")]
        public float f4;

        public WhipRunnerOffsets(float f1, float f2, float f3, float f4)
        {
            this.f1 = f1;
            this.f2 = f2;
            this.f3 = f3;
            this.f4 = f4;
        }
    }

    /// <summary>
    /// One whipper's Irish Whip (D-039): the four whipper frame times and the runner's keyed path for a whip
    /// <b>through</b> (the runner keeps the direction it faced in the lock-up and passes the whipper) and
    /// <b>away</b> (the runner turns and runs the way the whipper faced). Combat moves the runner's root along
    /// these keys; presentation times the whipper's four frames to the same frame times (ARCHITECTURE 6).
    /// </summary>
    [Serializable]
    public struct IrishWhipChoreography
    {
        [Tooltip("The wrestler doing the whip (Assets/Data/Wrestlers/). Ignored on the shared fallback (CombatTuning.irishWhip).")]
        public WrestlerDefinition whipper;
        [Tooltip("Seconds whipper frame 1 (push start, still facing the opponent) is shown.")]
        [Min(0f)] public float frame1;
        [Tooltip("Seconds whipper frame 2 (turn to the whip direction) is shown.")]
        [Min(0f)] public float frame2;
        [Tooltip("Seconds whipper frame 3 (final push) is shown. Its end is the release (the whip's contact point): the runner starts running from the f4 offset.")]
        [Min(0f)] public float frame3;
        [Tooltip("Seconds whipper frame 4 (follow-through) is held while the runner runs clear (about 1.5 runner body widths past the hand). The whipper is rooted until it ends, then free.")]
        [Min(0f)] public float followThrough;
        [Tooltip("Runner offsets for a whip through (past the whipper): the runner keeps the direction it faced in the lock-up.")]
        public WhipRunnerOffsets throughPx;
        [Tooltip("Runner offsets for a whip away: the runner turns and runs the way the whipper faced in the lock-up.")]
        public WhipRunnerOffsets awayPx;

        public IrishWhipChoreography(WrestlerDefinition whipper, float frame1, float frame2, float frame3, float followThrough,
            WhipRunnerOffsets throughPx, WhipRunnerOffsets awayPx)
        {
            this.whipper = whipper;
            this.frame1 = frame1;
            this.frame2 = frame2;
            this.frame3 = frame3;
            this.followThrough = followThrough;
            this.throughPx = throughPx;
            this.awayPx = awayPx;
        }

        /// <summary>Seconds from the whip press to the release (start of f4): f1 + f2 + f3.</summary>
        public float Release => frame1 + frame2 + frame3;

        /// <summary>
        /// As a <see cref="MoveTiming"/>: Startup = f1 + f2 + f3 (press to release, the contact point = start of
        /// f4), Active = 0, Recovery = f4 held. Total = how long the whipper is rooted.
        /// </summary>
        public MoveTiming Timing => new MoveTiming(Release, 0f, followThrough);

        /// <summary>Barry whipping Peaux, as staged in the approved art (D-039).</summary>
        public static IrishWhipChoreography BarryStaged => new IrishWhipChoreography(null, 0.12f, 0.10f, 0.12f, 0.33f,
            new WhipRunnerOffsets(-80f, 55f, 98f, 222f), new WhipRunnerOffsets(58f, 64f, 98f, 222f));

        /// <summary>Peaux whipping Barry, as staged in the approved art (D-039).</summary>
        public static IrishWhipChoreography PeauxStaged => new IrishWhipChoreography(null, 0.12f, 0.10f, 0.12f, 0.25f,
            new WhipRunnerOffsets(-55f, 50f, 80f, 167f), new WhipRunnerOffsets(70f, 74f, 82f, 167f));

        /// <summary>
        /// The runner's root offset in world units along the whip direction, <paramref name="t"/> seconds after
        /// the whip press: the f1, f2, f3 offsets are held for their whole frame (stepped, no easing), then from
        /// the release the runner moves from the f4 offset at <paramref name="runSpeed"/>.
        /// </summary>
        public float RunnerOffset(bool through, float t, float runSpeed)
        {
            WhipRunnerOffsets o = through ? throughPx : awayPx;
            float px;
            if (t < frame1) px = o.f1;
            else if (t < frame1 + frame2) px = o.f2;
            else if (t < Release) px = o.f3;
            else return o.f4 / MatchCamera.PixelsPerUnit + (t - Release) * runSpeed;
            return px / MatchCamera.PixelsPerUnit;
        }
    }
}
