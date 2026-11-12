using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.Juice;
using UnityEditor;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    public class MenuAudioSettingsTests
    {
        [TestCase(UiSound.Navigate, "navigate", 0.1f)]
        [TestCase(UiSound.Confirm, "confirm", 0.2f)]
        [TestCase(UiSound.Back, "back", 0.3f)]
        [TestCase(UiSound.Start, "start", 0.4f)]
        public void SlotFor_ReturnsMatchingSlot(UiSound sound, string field, float volume)
        {
            var settings = ScriptableObject.CreateInstance<MenuAudioSettings>();
            try
            {
                var so = new SerializedObject(settings);
                so.FindProperty("navigate.volume").floatValue = 0.1f;
                so.FindProperty("confirm.volume").floatValue = 0.2f;
                so.FindProperty("back.volume").floatValue = 0.3f;
                so.FindProperty("start.volume").floatValue = 0.4f;
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreEqual(volume, settings.SlotFor(sound).volume, 1e-6f, field);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SlotFor_UnknownValue_IsNull()
        {
            var settings = ScriptableObject.CreateInstance<MenuAudioSettings>();
            try
            {
                Assert.IsNull(settings.SlotFor((UiSound)99));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void EmptySlot_IsNotPlayable()
        {
            Assert.IsFalse(new AudioClipSlot(0.35f).IsPlayable);
        }
    }
}
