using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Tiny point-filtered textures made at runtime for the D-046 effects, so no art file is shipped or catalogued:
    /// a solid square (blood drops and stains), a 9×9 four-point impact star and an 8×8 ragged dust puff. White, so the particle
    /// colour tints them.
    /// </summary>
    public static class PixelTextures
    {
        private static readonly string[] StarRows =
        {
            "....#....",
            "....#....",
            "..#.#.#..",
            "...###...",
            "#########",
            "...###...",
            "..#.#.#..",
            "....#....",
            "....#....",
        };

        private static readonly string[] PuffRows =
        {
            "..#.##..",
            ".######.",
            "#######.",
            "########",
            ".#######",
            "########",
            ".######.",
            "..##.#..",
        };

        public static Texture2D Square(string name) => FromRows(name, new[] { "#" });
        public static Texture2D Star(string name) => FromRows(name, StarRows);
        public static Texture2D Puff(string name) => FromRows(name, PuffRows);

        /// <summary>'#' = opaque white, anything else = transparent. Row 0 is the top.</summary>
        public static Texture2D FromRows(string name, string[] rows)
        {
            int h = rows.Length;
            int w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                string row = rows[h - 1 - y];
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = x < row.Length && row[x] == '#' ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
