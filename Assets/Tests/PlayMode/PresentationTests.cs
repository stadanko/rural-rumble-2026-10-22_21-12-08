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
        // D-048 arena art: 2160x1080 native px = 36x18 world units; walkable feet band x 2..34, y -17.3333..-12.25.
        private const float ArenaWidth = 2160f / 60f;
        private const float ArenaHeight = 1080f / 60f;
        private const float BandBack = -12.25f, BandFront = -17.3333f;
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

        private static void AssertInsideArt(Camera cam, string when)
        {
            float halfW = cam.orthographicSize * cam.aspect, halfH = cam.orthographicSize;
            Vector3 c = cam.transform.position;
            Assert.GreaterOrEqual(c.x - halfW, -1e-3f, $"{when}: view past the art's left edge");
            Assert.LessOrEqual(c.x + halfW, ArenaWidth + 1e-3f, $"{when}: view past the art's right edge");
            Assert.LessOrEqual(c.y + halfH, 1e-3f, $"{when}: view above the art");
            Assert.GreaterOrEqual(c.y - halfH, -ArenaHeight - 1e-3f, $"{when}: view below the art");
        }

        // D-017 behaviour with D-048 numbers: default 4.5 at 16:9 (960x540 native), zoom out up to 1.45x the default
        // (6.525) when the wrestlers separate, ease back to exactly the default when they close in.
        [UnityTest]
        public IEnumerator MatchCamera_DefaultSize_ZoomsOutAndBackIn()
        {
            MatchCamera mc = MakeCamera(out Camera cam);
            a = new GameObject("QA Target A"); b = new GameObject("QA Target B");
            a.transform.position = new Vector3(17f, -15f); b.transform.position = new Vector3(19f, -15f);
            mc.SetTargets(a.transform, b.transform);

            Assert.AreEqual(4.5f, mc.DefaultOrthographicSize, 1e-3f);
            Assert.AreEqual(4.5f * 1.45f, mc.MaxOrthographicSize, 1e-3f);
            Assert.AreEqual(4.5f, cam.orthographicSize, 1e-3f, "starts at the default zoom");
            Assert.AreEqual(960f / 60f, 2f * cam.orthographicSize * cam.aspect, 1e-2f, "default view = 960 native px wide");
            Assert.Less(mc.MaxOrthographicSize * cam.aspect * 2f, ArenaWidth * 0.75f, "max zoom-out well short of the whole arena");
            yield return null;
            float px = cam.transform.position.x * 60f;
            Assert.AreEqual(Mathf.Round(px), px, 1e-2f, "pixel-snapped to 1/60 unit at default zoom");
            Assert.AreEqual(18f, cam.transform.position.x, 0.02f, "centered on the midpoint");

            // Apart: 16 units. Required half-width = 8 + edgeMargin.
            a.transform.position = new Vector3(10f, -15f); b.transform.position = new Vector3(26f, -15f);
            yield return new WaitForSecondsRealtime(2.5f);
            float expected = (8f + EdgeMargin(mc)) / cam.aspect;
            Assert.AreEqual(Mathf.Clamp(expected, 4.5f, mc.MaxOrthographicSize), cam.orthographicSize, 0.05f, "zoomed out to keep both in view");
            AssertInView(cam, a.transform, "A"); AssertInView(cam, b.transform, "B");

            // Full band width: capped at the max zoom-out; the first target (the player) stays on screen.
            a.transform.position = new Vector3(2f, -15f); b.transform.position = new Vector3(34f, -15f);
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.AreEqual(mc.MaxOrthographicSize, cam.orthographicSize, 0.02f, "capped at the max zoom-out");
            AssertInView(cam, a.transform, "A (player) past the cap");
            AssertInsideArt(cam, "max zoom");

            // Back together: returns exactly to the default.
            a.transform.position = new Vector3(17f, -15f); b.transform.position = new Vector3(19f, -15f);
            yield return new WaitForSecondsRealtime(3f);
            Assert.AreEqual(4.5f, cam.orthographicSize, 1e-4f, "eased back to the default zoom");
        }

        // D-048: the camera scrolls vertically to keep the wrestlers' bodies in view across the deeper band.
        [UnityTest]
        public IEnumerator MatchCamera_ScrollsVertically_KeepsBodiesInView()
        {
            MatchCamera mc = MakeCamera(out Camera cam);
            a = new GameObject("QA Target A"); b = new GameObject("QA Target B");
            a.transform.position = new Vector3(17f, BandBack); b.transform.position = new Vector3(19f, BandBack);
            mc.SetTargets(a.transform, b.transform);
            yield return new WaitForSecondsRealtime(1.5f);
            float backY = cam.transform.position.y;
            Assert.GreaterOrEqual(backY + cam.orthographicSize, BandBack + 4.0f, "head of a wrestler at the back edge in view");
            Assert.LessOrEqual(backY - cam.orthographicSize, BandBack, "feet at the back edge in view");

            a.transform.position = new Vector3(17f, BandFront); b.transform.position = new Vector3(19f, BandFront);
            yield return new WaitForSecondsRealtime(1.5f);
            float frontY = cam.transform.position.y;
            Assert.Less(frontY, backY - 1f, "camera scrolled down toward the front of the yard");
            Assert.LessOrEqual(frontY - cam.orthographicSize, BandFront, "feet at the front edge in view");
            Assert.GreaterOrEqual(frontY + cam.orthographicSize, BandFront + 4.0f, "head at the front edge in view");
            Assert.AreEqual(4.5f, cam.orthographicSize, 1e-3f, "same depth: no zoom");

            // One at the back, one at the front: zooms out just enough to keep both bodies.
            a.transform.position = new Vector3(17f, BandBack); b.transform.position = new Vector3(19f, BandFront);
            yield return new WaitForSecondsRealtime(2f);
            Assert.GreaterOrEqual(cam.transform.position.y + cam.orthographicSize, BandBack + 4.0f - 0.02f, "back wrestler's head in view");
            Assert.LessOrEqual(cam.transform.position.y - cam.orthographicSize, BandFront + 0.02f, "front wrestler's feet in view");
            AssertInsideArt(cam, "back/front split");
        }

        // D-048: at every band corner, at the default zoom and at the max zoom-out, the view never shows past the
        // backdrop's edges (checked every frame while the rig eases).
        [UnityTest]
        public IEnumerator MatchCamera_NeverShowsPastTheBackdrop()
        {
            MatchCamera mc = MakeCamera(out Camera cam);
            a = new GameObject("QA Target A"); b = new GameObject("QA Target B");
            var poses = new (Vector2 a, Vector2 b, bool max, string name)[]
            {
                (new Vector2(2f, BandBack), new Vector2(3f, BandBack), false, "default, back-left corner"),
                (new Vector2(33f, BandBack), new Vector2(34f, BandBack), false, "default, back-right corner"),
                (new Vector2(2f, BandFront), new Vector2(3f, BandFront), false, "default, front-left corner"),
                (new Vector2(33f, BandFront), new Vector2(34f, BandFront), false, "default, front-right corner"),
                (new Vector2(2f, BandBack), new Vector2(34f, BandFront), true, "max zoom, back-left to front-right"),
                (new Vector2(34f, BandBack), new Vector2(2f, BandFront), true, "max zoom, back-right to front-left"),
                (new Vector2(2f, BandFront), new Vector2(34f, BandFront), true, "max zoom, front edge"),
                (new Vector2(2f, BandBack), new Vector2(34f, BandBack), true, "max zoom, back edge"),
            };
            a.transform.position = poses[0].a; b.transform.position = poses[0].b;
            mc.SetTargets(a.transform, b.transform);
            foreach (var p in poses)
            {
                a.transform.position = p.a; b.transform.position = p.b;
                float end = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    AssertInsideArt(cam, p.name);
                }
                Assert.AreEqual(p.max ? mc.MaxOrthographicSize : 4.5f, cam.orthographicSize, 0.05f, $"{p.name}: zoom");
            }
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
            public int FallSide { get; set; }
            public bool IsGettingUp { get; set; }
            public float GetUpTimeRemaining { get; set; }
            public float GetUpDuration { get; set; } = 0.25f;
        }
    }
}
