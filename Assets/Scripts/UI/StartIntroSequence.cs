using System;
using UnityEngine;

namespace RuralRumble.UI
{
    /// <summary>What the Start screen controller must do after one <see cref="StartIntroSequence"/> call.</summary>
    public struct StartIntroStep
    {
        /// <summary>Raise <c>StartScreenEvents.RaiseLogoLanded(LandedSkipped)</c>.</summary>
        public bool RaiseLanded;
        public bool LandedSkipped;
        /// <summary>Raise <c>UiSound.LogoSlam</c> (only on a real impact, never on a skip).</summary>
        public bool PlaySlam;
        /// <summary>Show "Press Any Button to Start" and start blinking it.</summary>
        public bool ShowPrompt;
        /// <summary>Raise <c>UiSound.Start</c> and load the next scene.</summary>
        public bool StartGame;
    }

    /// <summary>
    /// One visit's Start screen intro state: logo drop, landed event (exactly once), prompt reveal, skip and start.
    /// Pure: the controller feeds unscaled time and presses, then acts on the returned steps.
    /// </summary>
    public sealed class StartIntroSequence
    {
        private readonly StartIntroTimeline timeline;
        private readonly float inputDelay;
        private float startAllowedAt;

        public StartIntroSequence(StartIntroTimeline timeline, float inputDelaySeconds)
        {
            this.timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            inputDelay = Mathf.Max(0f, inputDelaySeconds);
            startAllowedAt = inputDelay;
        }

        /// <summary>Unscaled seconds since the screen opened.</summary>
        public float Elapsed { get; private set; }
        public bool HasLanded { get; private set; }
        public bool PromptVisible { get; private set; }
        public bool Skipped { get; private set; }
        public bool Started { get; private set; }

        public StartIntroPhase Phase => Skipped || PromptVisible ? StartIntroPhase.Ready : timeline.PhaseAt(Elapsed);

        /// <summary>The logo's pose now; a skip snaps it to rest.</summary>
        public StartLogoPose Pose => Skipped ? StartLogoPose.Rest : timeline.PoseAt(Elapsed);

        /// <summary>Advances by dt unscaled seconds (negative counts as zero).</summary>
        public StartIntroStep Advance(float dt)
        {
            Elapsed += Mathf.Max(0f, dt);
            var step = new StartIntroStep();
            if (!HasLanded && Elapsed >= timeline.ImpactTime)
            {
                HasLanded = true;
                step.RaiseLanded = true;
                step.LandedSkipped = false;
                step.PlaySlam = true;
            }
            if (!PromptVisible && Elapsed >= timeline.PromptTime)
            {
                PromptVisible = true;
                step.ShowPrompt = true;
            }
            return step;
        }

        /// <summary>
        /// Any keyboard or gamepad button. Ignored inside the input delay after the screen opens (and after a skip, so
        /// two buttons in one frame can't skip and start). Before the prompt shows: skip to the landed state.
        /// After: start the game, once.
        /// </summary>
        public StartIntroStep Press()
        {
            var step = new StartIntroStep();
            if (Started || Elapsed < inputDelay) return step;

            if (!PromptVisible)
            {
                Skipped = true;
                PromptVisible = true;
                step.ShowPrompt = true;
                if (!HasLanded)
                {
                    HasLanded = true;
                    step.RaiseLanded = true;
                    step.LandedSkipped = true;
                }
                startAllowedAt = Elapsed + inputDelay;
                return step;
            }

            if (Elapsed < startAllowedAt) return step;
            Started = true;
            step.StartGame = true;
            return step;
        }
    }
}
