using System.Collections;
using System.Reflection;
using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// MatchCamera (ARCHITECTURE 3.1 / D-017) and WrestlerView (ARCHITECTURE 4.2) in isolation, on objects
    /// the test creates, so the results don't depend on the Game view size.
    /// </summary>
    public class PresentationTests
    {
        private const float ArenaWidth = 1600f / 60f;
        private GameObject rig;
        private GameObject a;
        private GameObject b;
        private GameObject barry;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { rig, a, b, barry })
                if (go != null) Object.Destroy(go);
        }

        private MatchCamera MakeCamera(out Camera cam)
        {
            rig = new GameObject("QA MatchCamera");
            var child = new GameObject("QA Camera");
            child.transform.SetParent(rig.transform, false);
            cam = child.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = 16f / 9f;
            return rig.AddComponent<MatchCamera>();
        }

        private static float EdgeMargin(MatchCamera mc) =>
            (float)typeof(MatchCamera).GetField("edgeMargin", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mc);

        private static void AssertInView(Camera cam, Transform t, string name)
        {
            float halfW = cam.orthographicSize * cam.aspect;
            Assert.That(Mathf.Abs(t.position.x - cam.transform.position.x), Is.LessThanOrEqualTo(halfW + 1e-3f), $"{name} is off screen");
        }

        // D-017 / ARCHITECTURE 3.1: default 4.5 at 16:9 (60% of the arena), zoom out up to 7.5 (whole arena)
        // when the wrestlers separate, ease back to exactly the default when they close in.
        [UnityTest]
        public IEnumerator MatchCamera_DefaultSize_ZoomsOutAndBackIn()
        {
            MatchCamera mc = MakeCamera(out Camera cam);
            a = new GameObject("QA Target A"); b = new GameObject("QA Target B");
            a.transform.position = new Vector3(12f, -11.6667f); b.transform.position = new Vector3(14f, -11.6667f);
            mc.SetTargets(a.transform, b.transform);

            Assert.AreEqual(4.5f, mc.DefaultOrthographicSize, 1e-3f);
            Assert.AreEqual(7.5f, mc.MaxOrthographicSize, 1e-3f);
            Assert.AreEqual(4.5f, cam.orthographicSize, 1e-3f, "starts at the default zoom");
            Assert.AreEqual(0.6f * ArenaWidth, 2f * cam.orthographicSize * cam.aspect, 1e-2f, "default view = 60% of arena width");
            yield return null;
            float px = cam.transform.position.x * 60f;
            Assert.AreEqual(Mathf.Round(px), px, 1e-2f, "pixel-snapped to 1/60 unit at default zoom");

            // Apart: 20 units. Required half-width = 10 + edgeMargin.
            a.transform.position = new Vector3(3.5f, -11.6667f); b.transform.position = new Vector3(23.5f, -11.6667f);
            yield return new WaitForSecondsRealtime(2.5f);
            float expected = (10f + EdgeMargin(mc)) / cam.aspect;
            Assert.AreEqual(Mathf.Clamp(expected, 4.5f, 7.5f), cam.orthographicSize, 0.05f, "zoomed out to keep both in view");
            AssertInView(cam, a.transform, "A"); AssertInView(cam, b.transform, "B");

            // Full width: capped at the whole arena.
            a.transform.position = new Vector3(0.5f, -11.6667f); b.transform.position = new Vector3(26.1667f, -11.6667f);
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.AreEqual(7.5f, cam.orthographicSize, 0.02f, "max zoom-out = whole arena");
            Assert.LessOrEqual(cam.orthographicSize, 7.5f + 1e-4f);
            Assert.AreEqual(ArenaWidth * 0.5f, cam.transform.position.x, 0.05f, "whole arena centered");

            // Back together: returns exactly to the default.
            a.transform.position = new Vector3(12f, -11.6667f); b.transform.position = new Vector3(14f, -11.6667f);
            yield return new WaitForSecondsRealtime(3f);
            Assert.AreEqual(4.5f, cam.orthographicSize, 1e-4f, "eased back to the default zoom");
        }

        [UnityTest]
        public IEnumerator MatchCamera_NeverShowsOutsideTheArena()
        {
            MatchCamera mc = MakeCamera(out Camera cam);
            a = new GameObject("QA Target A"); b = new GameObject("QA Target B");
            a.transform.position = new Vector3(0.5f, -13.6f); b.transform.position = new Vector3(1.5f, -13.6f);
            mc.SetTargets(a.transform, b.transform);
            yield return new WaitForSecondsRealtime(1f);
            float halfW = cam.orthographicSize * cam.aspect;
            Assert.GreaterOrEqual(cam.transform.position.x - halfW, -1e-3f, "view past the arena's left edge");
            Assert.GreaterOrEqual(cam.transform.position.y - cam.orthographicSize, -15f - 1e-3f, "view below the arena art");
            Assert.LessOrEqual(cam.transform.position.y + cam.orthographicSize, 1e-3f, "view above the arena art");
        }

        // ARCHITECTURE 4.2: presentation derives the flip from HorizontalFacing and drives the Animator State
        // from IWrestlerView.State; it never writes the root transform.
        [UnityTest]
        public IEnumerator WrestlerView_FlipsForRightFacing_AndSetsAnimatorState()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Wrestlers/Barry.prefab");
            Assert.IsNotNull(prefab, "Barry.prefab not found");
            barry = Object.Instantiate(prefab, new Vector3(5f, -12f, 0f), Quaternion.identity);
            var view = barry.GetComponent<WrestlerView>();
            Assert.IsNotNull(view); Assert.IsNotNull(view.Animator); Assert.IsNotNull(view.SpriteRenderer);
            var fake = new FakeWrestlerView { State = CombatState.Walking, Facing = new Vector2(1f, 0f), HorizontalFacing = 1 };
            view.Bind(fake);
            yield return null;
            Assert.IsTrue(view.SpriteRenderer.flipX, "Barry art faces left: HorizontalFacing +1 flips");
            Assert.AreEqual((int)CombatState.Walking, view.Animator.GetInteger(WrestlerView.StateParam));
            Assert.AreEqual(0f, view.Animator.GetFloat(WrestlerView.BackParam));

            fake.HorizontalFacing = -1; fake.Facing = new Vector2(-1f, 1f).normalized; fake.State = CombatState.Running;
            yield return null;
            Assert.IsFalse(view.SpriteRenderer.flipX, "HorizontalFacing -1 is not flipped");
            Assert.AreEqual((int)CombatState.Running, view.Animator.GetInteger(WrestlerView.StateParam));
            Assert.AreEqual(1f, view.Animator.GetFloat(WrestlerView.BackParam), "up-left is back-facing");

            fake.Facing = new Vector2(0f, 1f); fake.State = CombatState.Standing; // straight up keeps HF -1
            yield return null;
            Assert.IsFalse(view.SpriteRenderer.flipX);
            Assert.AreEqual((int)CombatState.Standing, view.Animator.GetInteger(WrestlerView.StateParam));
            Assert.AreEqual(1f, view.Animator.GetFloat(WrestlerView.BackParam));

            fake.Position = new Vector2(20f, -10f);
            yield return null;
            Assert.AreEqual(new Vector3(5f, -12f, 0f), barry.transform.position, "WrestlerView must not move the root");
#else
            Assert.Ignore("Needs the Editor AssetDatabase.");
            yield break;
#endif
        }

        // STYLE_GUIDE: back-facing for Up/Left, Up, Up/Right only.
        [Test]
        public void WrestlerView_BackFacingRule_AllEightDirections()
        {
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                Assert.AreEqual(y > 0, WrestlerView.IsBackFacing(new Vector2(x, y).normalized), $"({x},{y})");
            }
            Assert.IsTrue(WrestlerView.ShouldFlip(1, artFacesLeft: true));
            Assert.IsFalse(WrestlerView.ShouldFlip(-1, artFacesLeft: true));
            Assert.IsFalse(WrestlerView.ShouldFlip(1, artFacesLeft: false));
            Assert.IsTrue(WrestlerView.ShouldFlip(-1, artFacesLeft: false));
        }

        private sealed class FakeWrestlerView : IWrestlerView
        {
            public WrestlerId Id { get; set; } = WrestlerId.Player;
            public CombatState State { get; set; }
            public MoveId CurrentMove => MoveId.None;
            public MovePhase Phase => MovePhase.None;
            public float Health => 100f;
            public float MaxHealth => 100f;
            public Vector2 Position { get; set; }
            public Vector2 Facing { get; set; }
            public int HorizontalFacing { get; set; }
        }
    }
}
