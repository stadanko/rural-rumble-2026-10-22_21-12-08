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
