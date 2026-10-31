using System.Collections.Generic;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// Test input source (ARCHITECTURE 4.1: "Tests use a ScriptedInput : IWrestlerInput that returns
    /// queued intents"). Queued intents are returned first, one per Read; when the queue is empty it
    /// returns the currently held stick with no buttons pressed.
    /// </summary>
    public sealed class ScriptedInput : IWrestlerInput
    {
        private readonly Queue<WrestlerIntent> queued = new Queue<WrestlerIntent>();

        /// <summary>Stick held on every tick that has no queued intent.</summary>
        public Vector2 HeldMove { get; set; }

        public int ReadCount { get; private set; }

        public void Enqueue(WrestlerIntent intent) => queued.Enqueue(intent);

        /// <summary>Queues one tick where Run/Whip (B / Space) goes down, with the held stick.</summary>
        public void PressRunOnce() =>
            Enqueue(new WrestlerIntent(HeldMove, ButtonState.None, new ButtonState(true, true), ButtonState.None));

        /// <summary>Queues one tick where Strike (X / J) goes down, with the held stick.</summary>
        public void PressStrikeOnce() =>
            Enqueue(new WrestlerIntent(HeldMove, ButtonState.None, ButtonState.None, new ButtonState(true, true)));

        /// <summary>Queues one tick where Lock Up/Grapple (A / K) goes down, with the held stick.</summary>
        public void PressGrappleOnce() =>
            Enqueue(new WrestlerIntent(HeldMove, new ButtonState(true, true), ButtonState.None, ButtonState.None));

        /// <summary>Queues one tick with nothing pressed and the held stick.</summary>
        public void Wait() =>
            Enqueue(new WrestlerIntent(HeldMove, ButtonState.None, ButtonState.None, ButtonState.None));

        /// <summary>Queues <paramref name="ticks"/> intents: Strike pressed on the listed tick indices, idle otherwise.</summary>
        public void StrikeOnTicks(int ticks, params int[] pressTicks)
        {
            var set = new HashSet<int>(pressTicks);
            for (int i = 0; i < ticks; i++)
            {
                if (set.Contains(i)) PressStrikeOnce();
                else Wait();
            }
        }

        public int QueuedCount => queued.Count;

        public void Release() => HeldMove = Vector2.zero;

        public WrestlerIntent Read()
        {
            ReadCount++;
            return queued.Count > 0
                ? queued.Dequeue()
                : new WrestlerIntent(HeldMove, ButtonState.None, ButtonState.None, ButtonState.None);
        }
    }
}
