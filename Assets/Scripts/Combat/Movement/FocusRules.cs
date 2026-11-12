using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Focus (D-037, COMBAT_SPEC "Focus"): which way a free wrestler faces, and which way a run starts.
    /// Pure functions; CombatMatch / CombatWrestler apply the result.
    /// </summary>
    public static class FocusRules
    {
        /// <summary>
        /// The Focus back-stance height in world units, read from tuning now (Play Mode edits apply at once).
        /// Tuning is in pixels; 60 px = 1 unit (D-013).
        /// </summary>
        public static float BackStanceHeightUnits(CombatTuning tuning) =>
            tuning.focusBackStanceHeightPx / MatchCamera.PixelsPerUnit;

        /// <summary>
        /// The quantized facing a free wrestler at <paramref name="self"/> takes toward an opponent at
        /// <paramref name="opponent"/>: x is the sign of the horizontal offset (exactly equal x keeps
        /// <paramref name="currentHorizontal"/>); y is +1 (back stance) when the opponent is more than
        /// <paramref name="backStanceHeightUnits"/> higher on screen (larger world y), else 0 (front stance).
        /// </summary>
        public static Vector2Int FacingToward(Vector2 self, Vector2 opponent, int currentHorizontal, float backStanceHeightUnits)
        {
            int x = HorizontalToward(opponent.x - self.x, currentHorizontal);
            int y = opponent.y - self.y > backStanceHeightUnits ? 1 : 0;
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Left/right direction of a run started now: the held left/right (<paramref name="moveX"/> beyond
        /// <paramref name="deadzone"/>), otherwise toward the opponent (<paramref name="dx"/> = opponent.x - self.x;
        /// exactly 0 keeps <paramref name="currentHorizontal"/>).
        /// </summary>
        public static int RunDirection(float moveX, float deadzone, float dx, int currentHorizontal)
        {
            if (Mathf.Abs(moveX) > deadzone)
                return moveX > 0f ? 1 : -1;
            return HorizontalToward(dx, currentHorizontal);
        }

        private static int HorizontalToward(float dx, int currentHorizontal)
        {
            if (dx > 0f) return 1;
            if (dx < 0f) return -1;
            return currentHorizontal >= 0 ? 1 : -1;
        }
    }
}
