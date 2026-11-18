using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-046 Red flash: "Red flashes baked into some animation clips are replaced by this, so every hit looks the same."
    /// ARCHITECTURE 4.7: every {Name}_Struck* clip keys SpriteRenderer colour flat white like the other clips.
    /// </summary>
    public class StruckClipTintTests
    {
        [Test]
        public void StruckClips_KeySpriteRendererColourFlatWhite()
        {
            string[] paths = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Animation" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetFileNameWithoutExtension(p).Contains("_Struck"))
                .ToArray();
            Assert.GreaterOrEqual(paths.Length, 6, "Barry and Peaux StruckA/B/C clips expected under Assets/Animation");

            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (b.type != typeof(SpriteRenderer) || !b.propertyName.StartsWith("m_Color"))
                        continue;
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, b);
                    foreach (Keyframe k in curve.keys)
                        Assert.AreEqual(1f, k.value, 1e-4f,
                            $"{path}: {b.path}/{b.propertyName} keyed {k.value} at {k.time:0.###} s (D-046: no baked tint)");
                }
            }
        }
    }
}
