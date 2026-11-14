using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// Irish Whip presentation (ARCHITECTURE 6 "Irish Whip presentation contract", D-039), driven by a real
    /// CombatMatch ticked every frame with Time.deltaTime before WrestlerView.Update, as CombatMatchRunner does
    /// (execution order -50), with the roots written from the combat positions. Uses the project CombatTuning asset
    /// (read only) and the real Barry / Peaux definitions and prefabs, so each whipper's own choreography and clip play.
    /// Checks every rendered frame of the whip: the whipper plays its IrishWhip clip at 1x (holds = combat frame
    /// times), f1 is drawn facing the lock-up facing and f2-f4 facing the whip direction (so the flip changes only for
    /// a whip through), the whipper's sprite never runs ahead of combat's frame (the runner's keyed spot), the runner
    /// draws in front of the whipper until the whip ends, and the runner plays its Run clip.
    /// </summary>
    public class IrishWhipPresentationTests
    {
        private const string TuningPath = "Assets/Data/Tuning/CombatTuning.asset";
        private static readonly Vector2 PlayerStart = new Vector2(10f, -12f);
        private static readonly Vector2 CpuStart = new Vector2(11.5f, -12f);

        private readonly List<Object> spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // A fixed 60 fps step: the Editor may throttle real frames, and combat clamps dt to maxTickStep while the
            // Animator would not, so real time would let the clip drift from combat by more than the game ever does.
            Time.captureDeltaTime = 1f / 60f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            foreach (Object o in spawned)
                if (o != null) Object.Destroy(o);
            spawned.Clear();
        }

        private struct FrameSample
        {
            public float WhipTime;
            public int CombatFrame;
            public int SpriteFrame;
            public bool WhipperFlip;
            public int WhipperOrder, RunnerOrder;
            public string WhipperState;
            public bool RunnerInRun;
        }

        [UnityTest]
        public IEnumerator BarryWhipsPeaux_Through() => RunWhip("Barry", "Peaux", 0f);

        [UnityTest]
        public IEnumerator BarryWhipsPeaux_Away() => RunWhip("Barry", "Peaux", 1f);

        [UnityTest]
        public IEnumerator PeauxWhipsBarry_Through() => RunWhip("Peaux", "Barry", 0f);

        [UnityTest]
        public IEnumerator PeauxWhipsBarry_Away() => RunWhip("Peaux", "Barry", 1f);

        /// <param name="stickX">Held left/right on the whip press: 0 = none (whip through), +1 = right (away, the
        /// player faces right).</param>
        private IEnumerator RunWhip(string whipperName, string runnerName, float stickX)
        {
#if UNITY_EDITOR
            var tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
            Assert.IsNotNull(tuning, TuningPath);
            var whipperDef = UnityEditor.AssetDatabase.LoadAssetAtPath<WrestlerDefinition>($"Assets/Data/Wrestlers/{whipperName}.asset");
            var runnerDef = UnityEditor.AssetDatabase.LoadAssetAtPath<WrestlerDefinition>($"Assets/Data/Wrestlers/{runnerName}.asset");
            Assert.IsNotNull(whipperDef);
            Assert.IsNotNull(runnerDef);
            IrishWhipChoreography c = tuning.IrishWhipFor(whipperDef);

            var match = new CombatMatch(tuning, null, PlayerStart, CpuStart, () => 0f, whipperDef, runnerDef);
            GameObject whipperGo = Spawn(whipperDef, PlayerStart);
            GameObject runnerGo = Spawn(runnerDef, CpuStart);
            var whipper = whipperGo.GetComponent<WrestlerView>();
            var runner = runnerGo.GetComponent<WrestlerView>();
            whipper.Bind(match.Player);
            whipper.BindEvents(match.Events, WrestlerId.Player);
            runner.Bind(match.Cpu);
            runner.BindEvents(match.Events, WrestlerId.Cpu);
            PairedMoveInfo? whipInfo = null;
            match.Events.PairedMoveStarted += i => { if (i.Move == MoveId.IrishWhip) whipInfo = i; };

            var driverGo = new GameObject("WhipTestDriver");
            spawned.Add(driverGo);
            var driver = driverGo.AddComponent<TickDriver>();
            driver.Init(match, whipperGo.transform, runnerGo.transform);
            int baseOrder = whipper.SpriteRenderer.sortingOrder;

            // Lock up, hold it a moment, then whip (holding stickX).
            driver.Queue(new WrestlerIntent(Vector2.zero, new ButtonState(true, true), ButtonState.None, ButtonState.None));
            for (int i = 0; i < 20; i++) driver.Queue(WrestlerIntent.None);
            driver.Queue(new WrestlerIntent(new Vector2(stickX, 0f), ButtonState.None, new ButtonState(true, true), ButtonState.None));
            for (int guard = 0; !whipInfo.HasValue; guard++)
            {
                Assert.Less(guard, 600, "setup: the whip never started");
                yield return null;
            }
            PairedMoveInfo info = whipInfo.Value;
            Assert.AreEqual(stickX == 0f, info.WhipThrough, "setup: whip direction");

            var samples = new List<FrameSample>();
            while (match.Player.State == CombatState.Grappling && match.Player.CurrentMove == MoveId.IrishWhip)
            {
                yield return new WaitForEndOfFrame(); // after LateUpdate: what this frame renders
                if (match.Player.State != CombatState.Grappling) break;
                float t = match.LockUps.WhipElapsed;
                AnimatorStateInfo st = whipper.Animator.GetCurrentAnimatorStateInfo(0);
                samples.Add(new FrameSample
                {
                    WhipTime = t,
                    CombatFrame = t < c.frame1 ? 0 : t < c.frame1 + c.frame2 ? 1 : t < c.Release ? 2 : 3,
                    SpriteFrame = SpriteIndex(whipper.SpriteRenderer.sprite),
                    WhipperFlip = whipper.SpriteRenderer.flipX,
                    WhipperOrder = whipper.SpriteRenderer.sortingOrder,
                    RunnerOrder = runner.SpriteRenderer.sortingOrder,
                    WhipperState = st.IsName(WrestlerView.IrishWhipStateName) ? "IrishWhip" : "other",
                    RunnerInRun = runner.Animator.GetCurrentAnimatorStateInfo(0).IsName("Running"),
                });
            }
            yield return null;
            yield return new WaitForEndOfFrame();

            string trace = Trace(samples);
            Debug.Log($"Irish Whip trace, {whipperName} whips {runnerName} {(info.WhipThrough ? "through" : "away")}:\n{trace}");
            Assert.Greater(samples.Count, 10, "setup: the whip lasted several frames\n" + trace);
            Assert.AreEqual(1f, whipper.MoveSpeed, 0.01f, "holds equal the combat frame times: 1x");

            bool flipF = WrestlerView.ShouldFlip(info.Facing, true);
            bool flipW = WrestlerView.ShouldFlip(info.WhipDirection, true);
            var seen = new HashSet<int>();
            for (int i = 0; i < samples.Count; i++)
            {
                FrameSample s = samples[i];
                string at = $" (frame {i}, whip t {s.WhipTime * 1000f:0} ms)\n{trace}";
                Assert.AreEqual("IrishWhip", s.WhipperState, "whipper plays the IrishWhip state" + at);
                Assert.IsTrue(s.RunnerInRun, "runner plays its Run clip" + at);
                Assert.Greater(s.RunnerOrder, s.WhipperOrder, "runner drawn in front of the whipper" + at);
                Assert.AreEqual(s.SpriteFrame == 0 ? flipF : flipW, s.WhipperFlip,
                    "f1 faces the lock-up facing, f2-f4 the whip direction" + at);
                Assert.LessOrEqual(s.SpriteFrame, s.CombatFrame, "the whipper's frame never runs ahead of combat's" + at);
                Assert.GreaterOrEqual(s.SpriteFrame, s.CombatFrame - 1, "and lags by at most one rendered frame" + at);
                seen.Add(s.SpriteFrame);
            }
            CollectionAssert.IsSupersetOf(seen, new[] { 0, 1, 2, 3 }, "every whip frame was shown\n" + trace);
            Assert.AreEqual(info.WhipThrough, flipF != flipW, "the whipper turns only for a whip through");

            Assert.AreEqual(baseOrder, runner.SpriteRenderer.sortingOrder, "runner back to its own order after the whip");
            Assert.AreEqual(baseOrder, whipper.SpriteRenderer.sortingOrder, "whipper back to its own order after the whip");
            Assert.IsFalse(runner.InFrontOfWhipper);
            Assert.AreEqual(0, whipper.WhipDirection);
#else
            Assert.Ignore("Needs the Editor AssetDatabase.");
            yield break;
#endif
        }

        private GameObject Spawn(WrestlerDefinition def, Vector2 at)
        {
            Assert.IsNotNull(def.Prefab, def.name + " has no prefab");
            GameObject go = Object.Instantiate(def.Prefab, at, Quaternion.identity);
            spawned.Add(go);
            return go;
        }

        private static int SpriteIndex(Sprite s)
        {
            if (s == null) return -1;
            int u = s.name.LastIndexOf('_');
            return s.name.Contains("irishwhip") && int.TryParse(s.name.Substring(u + 1), out int i) ? i : -1;
        }

        private static string Trace(List<FrameSample> samples)
        {
            var sb = new StringBuilder();
            foreach (FrameSample s in samples)
                sb.Append($"t {s.WhipTime * 1000f:0.0} ms: combat f{s.CombatFrame + 1}, sprite f{s.SpriteFrame + 1}, " +
                          $"flip {s.WhipperFlip}, order whipper {s.WhipperOrder} runner {s.RunnerOrder}\n");
            return sb.ToString();
        }

        /// <summary>Ticks the match before WrestlerView.Update, like CombatMatchRunner (order -50), and writes the roots.</summary>
        [DefaultExecutionOrder(-50)]
        private sealed class TickDriver : MonoBehaviour
        {
            private CombatMatch match;
            private Transform playerRoot, cpuRoot;
            private readonly Queue<WrestlerIntent> playerIntents = new Queue<WrestlerIntent>();

            public void Init(CombatMatch m, Transform player, Transform cpu)
            {
                match = m;
                playerRoot = player;
                cpuRoot = cpu;
            }

            public void Queue(WrestlerIntent intent) => playerIntents.Enqueue(intent);

            private void Update()
            {
                if (match == null) return;
                WrestlerIntent p = playerIntents.Count > 0 ? playerIntents.Dequeue() : WrestlerIntent.None;
                match.Tick(Mathf.Min(Time.deltaTime, match.Tuning.maxTickStep), p, WrestlerIntent.None);
                Write(playerRoot, match.Player.Position);
                Write(cpuRoot, match.Cpu.Position);
            }

            private static void Write(Transform root, Vector2 feet)
            {
                if (root != null) root.position = new Vector3(feet.x, feet.y, root.position.z);
            }
        }
    }
}
