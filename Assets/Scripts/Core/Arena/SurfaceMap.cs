using System;
using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>Ground surface under a foot (D-049). Dirt is the default for anything unmarked or off the map.</summary>
    public enum SurfaceType
    {
        Dirt = 0,
        Grass = 1,
        Puddle = 2
    }

    /// <summary>
    /// Plain C# surface lookup over a grid of texels aligned to the arena art (D-049, ARCHITECTURE 4.8).
    /// Presentation data only: no combat rule reads it. Built from the colour-coded mask texture by
    /// <see cref="ArenaSurfaceMap"/>, or from cells directly in tests.
    /// </summary>
    public sealed class SurfaceMap
    {
        private readonly SurfaceType[] cells; // row-major, row 0 = top of the art

        /// <summary>Texels across.</summary>
        public int Width { get; }
        /// <summary>Texels down.</summary>
        public int Height { get; }
        /// <summary>World position of the map's top-left corner (the arena art's top-left is (0, 0), 3.1).</summary>
        public Vector2 WorldTopLeft { get; }
        /// <summary>World units per texel (4 native px / 60 PPU for the D-048 map).</summary>
        public float WorldUnitsPerTexel { get; }

        /// <param name="cellsTopDown">Width x Height cells, row-major, row 0 = the top row of the art.</param>
        public SurfaceMap(SurfaceType[] cellsTopDown, int width, int height, Vector2 worldTopLeft, float worldUnitsPerTexel)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Map size must be positive.");
            if (cellsTopDown == null || cellsTopDown.Length != width * height)
                throw new ArgumentException("Need exactly width x height cells.", nameof(cellsTopDown));
            if (!(worldUnitsPerTexel > 0f))
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerTexel), "Texel size must be positive.");
            cells = (SurfaceType[])cellsTopDown.Clone();
            Width = width;
            Height = height;
            WorldTopLeft = worldTopLeft;
            WorldUnitsPerTexel = worldUnitsPerTexel;
        }

        /// <summary>
        /// Builds a map from texture pixels in Unity's order (<c>Texture2D.GetPixels32</c>: row 0 = bottom).
        /// Colour code: blue-dominant = Puddle, green-dominant = Grass, anything else = Dirt (see <see cref="Classify"/>).
        /// </summary>
        public static SurfaceMap FromPixels(Color32[] pixelsBottomUp, int width, int height, Vector2 worldTopLeft, float worldUnitsPerTexel)
        {
            if (pixelsBottomUp == null || pixelsBottomUp.Length != width * height)
                throw new ArgumentException("Need exactly width x height pixels.", nameof(pixelsBottomUp));
            var top = new SurfaceType[width * height];
            for (int y = 0; y < height; y++)
            {
                int srcRow = (height - 1 - y) * width;
                int dstRow = y * width;
                for (int x = 0; x < width; x++)
                    top[dstRow + x] = Classify(pixelsBottomUp[srcRow + x]);
            }
            return new SurfaceMap(top, width, height, worldTopLeft, worldUnitsPerTexel);
        }

        /// <summary>Mask colour code: Puddle = blue (0, 0, 255), Grass = green (0, 255, 0), Dirt = anything else (black).
        /// A channel counts when it is at least 128 and strictly above the other two, so compression noise can't flip a texel.</summary>
        public static SurfaceType Classify(Color32 c)
        {
            if (c.b >= 128 && c.b > c.r && c.b > c.g)
                return SurfaceType.Puddle;
            if (c.g >= 128 && c.g > c.r && c.g > c.b)
                return SurfaceType.Grass;
            return SurfaceType.Dirt;
        }

        /// <summary>Surface at a world point (nearest texel). Off the map returns Dirt.</summary>
        public SurfaceType SurfaceAt(Vector2 world)
        {
            float fx = (world.x - WorldTopLeft.x) / WorldUnitsPerTexel;
            float fy = (WorldTopLeft.y - world.y) / WorldUnitsPerTexel;
            if (float.IsNaN(fx) || float.IsNaN(fy) || fx < 0f || fy < 0f || fx >= Width || fy >= Height)
                return SurfaceType.Dirt;
            int x = Mathf.Min((int)fx, Width - 1);
            int y = Mathf.Min((int)fy, Height - 1);
            return cells[y * Width + x];
        }

        /// <summary>Surface of one texel (column x, row y from the top). Out of range returns Dirt.</summary>
        public SurfaceType CellAt(int x, int y) =>
            x < 0 || y < 0 || x >= Width || y >= Height ? SurfaceType.Dirt : cells[y * Width + x];
    }
}
