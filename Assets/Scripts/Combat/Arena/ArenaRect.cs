using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>Axis-aligned rectangle on the arena plane (x = left/right, y = depth), world units.</summary>
    public readonly struct ArenaRect
    {
        public readonly float XMin;
        public readonly float YMin;
        public readonly float XMax;
        public readonly float YMax;

        public ArenaRect(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = Mathf.Min(xMin, xMax);
            XMax = Mathf.Max(xMin, xMax);
            YMin = Mathf.Min(yMin, yMax);
            YMax = Mathf.Max(yMin, yMax);
        }

        public static ArenaRect FromMinMax(Vector2 min, Vector2 max) => new ArenaRect(min.x, min.y, max.x, max.y);

        public float Width => XMax - XMin;
        public float Height => YMax - YMin;
        public Vector2 Center => new Vector2((XMin + XMax) * 0.5f, (YMin + YMax) * 0.5f);

        public bool Contains(Vector2 p) => p.x >= XMin && p.x <= XMax && p.y >= YMin && p.y <= YMax;

        public override string ToString() => $"ArenaRect(x {XMin:0.####}..{XMax:0.####}, y {YMin:0.####}..{YMax:0.####})";
    }
}
