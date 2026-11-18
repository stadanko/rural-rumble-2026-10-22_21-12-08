using System.Collections.Generic;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>A blood stain on the dirt (D-046).</summary>
    public struct Stain
    {
        public Vector2 Position;
        public float Width;
        public float Height;
        public Color32 Color;
        /// <summary>Time this stain started fading, or NaN while it is one of the kept stains.</summary>
        public float FadeStart;
    }

    /// <summary>
    /// Pure bookkeeping for ground stains that build up over the match (D-046): at most <c>cap</c> stains are kept at full
    /// strength; past the cap the oldest start fading and are removed when the fade ends. A hard limit of twice the cap
    /// drops the oldest fading stain at once, so a burst can never grow the list without bound.
    /// </summary>
    public sealed class StainBook
    {
        private readonly List<Stain> stains = new List<Stain>();

        public int Cap { get; set; }
        public float FadeSeconds { get; set; }

        public StainBook(int cap, float fadeSeconds)
        {
            Cap = cap;
            FadeSeconds = fadeSeconds;
        }

        public int Count => stains.Count;
        public Stain this[int i] => stains[i];

        public int KeptCount
        {
            get
            {
                int n = 0;
                foreach (Stain s in stains)
                    if (float.IsNaN(s.FadeStart)) n++;
                return n;
            }
        }

        public void Add(Stain s, float now)
        {
            s.FadeStart = float.NaN;
            stains.Add(s);
            int cap = Mathf.Max(0, Cap);
            int kept = KeptCount;
            for (int i = 0; i < stains.Count && kept > cap; i++)
            {
                if (!float.IsNaN(stains[i].FadeStart))
                    continue;
                Stain old = stains[i];
                old.FadeStart = now;
                stains[i] = old;
                kept--;
            }
            int hard = Mathf.Max(1, cap * 2);
            while (stains.Count > hard)
                stains.RemoveAt(0); // oldest first; past the cap these are the fading ones
        }

        /// <summary>Removes stains whose fade has finished.</summary>
        public void Update(float now)
        {
            for (int i = stains.Count - 1; i >= 0; i--)
            {
                if (Alpha01(i, now) <= 0f)
                    stains.RemoveAt(i);
            }
        }

        /// <summary>1 for a kept stain; a fading stain goes linearly from 1 to 0 over <see cref="FadeSeconds"/>.</summary>
        public float Alpha01(int i, float now)
        {
            float start = stains[i].FadeStart;
            if (float.IsNaN(start))
                return 1f;
            if (FadeSeconds <= 0f)
                return 0f;
            return Mathf.Clamp01(1f - (now - start) / FadeSeconds);
        }

        public void Clear() => stains.Clear();
    }
}
