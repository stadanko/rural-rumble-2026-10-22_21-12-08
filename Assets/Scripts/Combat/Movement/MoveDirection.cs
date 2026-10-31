using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>Quantizes raw stick input to the eight walking directions (D-001).</summary>
    public static class MoveDirection
    {
        /// <summary>
        /// Returns a direction whose components are each -1, 0 or 1 (not normalized), or zero when the
        /// stick is inside the deadzone. Each 45-degree sector maps to its nearest direction.
        /// </summary>
        public static Vector2Int Quantize(Vector2 raw, float deadzone)
        {
            if (raw.sqrMagnitude <= deadzone * deadzone || raw.sqrMagnitude < 1e-8f)
                return Vector2Int.zero;

            float angle = Mathf.Atan2(raw.y, raw.x);
            int sector = Mathf.RoundToInt(angle / (Mathf.PI * 0.25f)); // -4..4
            float snapped = sector * Mathf.PI * 0.25f;
            return new Vector2Int(Mathf.RoundToInt(Mathf.Cos(snapped)), Mathf.RoundToInt(Mathf.Sin(snapped)));
        }

        /// <summary>Unit-length facing vector for a quantized direction.</summary>
        public static Vector2 ToFacing(Vector2Int dir) => ((Vector2)dir).normalized;
    }
}
