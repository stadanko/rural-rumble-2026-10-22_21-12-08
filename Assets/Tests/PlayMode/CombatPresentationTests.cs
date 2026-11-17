using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// WrestlerView driven by a real CombatMatch (ARCHITECTURE 4.2 "Combat -> presentation"): CombatEvents'
    /// raise methods are internal, so the test builds a match, ticks it itself with queued intents, and lets two
    /// Barry prefab instances (bound with Bind + BindEvents, as MatchBootstrap does) animate on real frames.
    ///
    /// Frame order: the coroutine resumes after every Update, so a tick done here is applied by WrestlerView.Update
    /// on the next frame, and the Animator evaluates after that Update. <see cref="Settle"/> waits those two frames.
    /// Combat time only advances when the test ticks; real time only moves the Animator.
    /// </summary>
    public class CombatPresentationTests
    {
        private const float Dt = 0.01f;
        private const string BarryPrefab = "Assets/Prefabs/Wrestlers/Barry.prefab";

        private CombatTuning tuning;
        private CombatMatch match;
        private QueuedInput playerInput;
        private QueuedInput cpuInput;
        private GameObject playerGo;
        private GameObject cpuGo;
        private WrestlerView playerView;
        private WrestlerView cpuView;

        [SetUp]
        public void SetUp()
        {
            // A fixed 60 fps step so the Animator's clip time per frame is the same whether or not the Editor throttles
            // real frames (the fall's 100-130 ms holds are only a few frames each).
            Time.captureDeltaTime = 1f / 60f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            if (playerGo != null) Object.Destroy(playerGo);
            if (cpuGo != null) Object.Destroy(cpuGo);
            if (tuning != null) Object.Destroy(tuning);
        }

        private void Build(Vector2 playerStart, Vector2 cpuStart, System.Action<CombatTuning> configure = null,
            string cpuPrefabPath = BarryPrefab, ArenaGeometry arena = null)
        {
#if UNITY_EDITOR
            tuning = ScriptableObject.CreateInstance<CombatTuning>();
            configure?.Invoke(tuning);
            match = new CombatMatch(tuning, arena, playerStart, cpuStart);
            playerInput = new QueuedInput();
            cpuInput = new QueuedInput();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(BarryPrefab);
            Assert.IsNotNull(prefab, BarryPrefab + " not found");
            var cpuPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(cpuPrefabPath);
            Assert.IsNotNull(cpuPrefab, cpuPrefabPath + " not found");
            playerGo = Object.Instantiate(prefab, playerStart, Quaternion.identity);
            cpuGo = Object.Instantiate(cpuPrefab, cpuStart, Quaternion.identity);
            playerView = playerGo.GetComponent<WrestlerView>();
            cpuView = cpuGo.GetComponent<WrestlerView>();
            playerView.Bind(match.Player);
            playerView.BindEvents(match.Events, WrestlerId.Player);
            cpuView.Bind(match.Cpu);
            cpuView.BindEvents(match.Events, WrestlerId.Cpu);
#else
            Assert.Ignore("Needs the Editor AssetDatabase.");
#endif
        }

        private void Tick(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
                match.Tick(Dt, playerInput.Read(), cpuInput.Read());
        }

        private void TickUntil(System.Func<bool> until, float maxSeconds = 5f)
        {
            for (float t = 0f; t < maxSeconds; t += Dt)
            {
                Tick();
                if (until()) return;
            }
            Assert.Fail($"combat condition not reached within {maxSeconds} s");
        }

        /// <summary>One frame for WrestlerView.Update to apply, one more so the Animator has evaluated.</summary>
        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        private static AnimatorStateInfo StateOf(WrestlerView v) =>
            v.Animator.IsInTransition(0) ? v.Animator.GetNextAnimatorStateInfo(0) : v.Animator.GetCurrentAnimatorStateInfo(0);

        private static float ContactTime(WrestlerView v, string clipPath)
        {
#if UNITY_EDITOR
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.IsNotNull(clip, clipPath + " not found");
            Assert.IsTrue(v.MoveTable.TryGetContactTime(clip, out float contact), $"MoveAnimationTable has no entry for {clipPath}");
            return contact;
#else
            return 1f;
#endif
        }

        private static float ExpectedSpeed(float contact, float startup) =>
            Mathf.Clamp(contact / startup, WrestlerView.MinMoveSpeed, WrestlerView.MaxMoveSpeed);

        private static float SpriteRotZ(WrestlerView v)
        {
            float z = v.SpriteRenderer.transform.localEulerAngles.z % 360f;
            return z > 180f ? z - 360f : z;
        }

        // ARCHITECTURE 4.2 Attack timing: on MoveStarted, playback speed = clip contact time / tuned Startup, read
        // at that moment (Play Mode tuning edits apply). A buffered combo strike starts on the same tick the previous
        // one ends, so the state never leaves Striking; the view must still restart the new strike's clip.
        [UnityTest]
        public IEnumerator Strike_MoveSpeedFromTable_AndBufferedComboRestartsTheClip()
        {
            Build(new Vector2(10f, -12f), new Vector2(20f, -12f)); // CPU out of reach: whiffs only
            yield return null;
            float contact1 = ContactTime(playerView, "Assets/Animation/Barry/Barry_Strike1.anim");
            float contact2 = ContactTime(playerView, "Assets/Animation/Barry/Barry_Strike2.anim");
            var playerStates = new List<CombatState>();
            match.Events.StateChanged += (id, from, to) => { if (id == WrestlerId.Player) playerStates.Add(to); };

            playerInput.PressStrikeAt(0, 5); // second press buffered during Strike 1
            Tick();
            Assert.AreEqual(MoveId.Strike1, match.Player.CurrentMove);
            yield return Settle();
            float speed1 = ExpectedSpeed(contact1, tuning.strike1Timing.Startup);
            Assert.AreEqual(speed1, playerView.MoveSpeed, 1e-3f, "Strike 1 speed = contact / Startup");
            Assert.AreEqual(speed1, playerView.Animator.GetFloat(WrestlerView.MoveSpeedParam), 1e-3f);
            Assert.IsTrue(StateOf(playerView).IsName("Strike1"), "Animator should be in Strike1");

            // Play Mode tuning edit: Strike 2's Startup changes before it starts (D-005).
            tuning.strike2Timing = new MoveTiming(0.3f, tuning.strike2Timing.Active, tuning.strike2Timing.Recovery);
            TickUntil(() => match.Player.Phase == MovePhase.Recovery);
            yield return new WaitForSeconds(0.3f); // let the Strike 1 clip run on
            Assert.Greater(StateOf(playerView).normalizedTime, 0.3f, "setup: Strike 1 clip has advanced");

            TickUntil(() => match.Player.CurrentMove == MoveId.Strike2);
            CollectionAssert.AreEqual(new[] { CombatState.Striking }, playerStates, "setup: buffered Strike 2 never left Striking");
            yield return Settle();
            float speed2 = ExpectedSpeed(contact2, 0.3f);
            Assert.AreEqual(speed2, playerView.MoveSpeed, 1e-3f, "Strike 2 speed uses the edited Startup");
            Assert.AreEqual(speed2, playerView.Animator.GetFloat(WrestlerView.MoveSpeedParam), 1e-3f);
            AnimatorStateInfo s2 = StateOf(playerView);
            Assert.IsTrue(s2.IsName("Strike2"), "Animator should be in Strike2");
            Assert.Less(s2.normalizedTime, 0.5f, "Strike 2 should play from its start");

            TickUntil(() => match.Player.State == CombatState.Standing);
            yield return Settle();
            Assert.AreEqual(1f, playerView.MoveSpeed, 1e-5f, "speed resets to 1 when the move ends");
        }

        // D-024: each hit restarts Struck without a StateChanged; presentation restarts the Struck clip on HitLanded.
        // The reaction follows the hit that landed (presentation only): combo hit 1 (Strike 1) shows "StruckB", and a
        // Strike 2 landing while StruckB shows switches to "StruckC" (the head turn) from its start (D-042).
        [UnityTest]
        public IEnumerator Struck_ClipRestartsOnEveryHit_StruckBOnStrike1ThenStruckC()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t => t.struckDuration = 2f);
            yield return null;
            var hits = new List<HitInfo>();
            match.Events.HitLanded += hits.Add;
            playerInput.PressStrikeAt(0, 5); // Strike 1, buffered Strike 2
            TickUntil(() => match.Cpu.State == CombatState.Struck);
            Assert.AreEqual(1, hits.Count, "setup: one hit so far");
            Assert.AreEqual(MoveId.Strike1, hits[0].Move, "setup: the first hit is Strike 1");
            yield return Settle();
            yield return new WaitForSeconds(0.3f);
            AnimatorStateInfo before = StateOf(cpuView);
            Assert.IsTrue(before.IsName(WrestlerView.StruckBStateName), "a Strike 1 hit shows StruckB");
            Assert.IsFalse(cpuView.Animator.GetBool(WrestlerView.StruckCParam), "StruckC parameter not set for Strike 1");
            Assert.Greater(before.normalizedTime, 0.4f, "setup: the Struck clip has advanced");
            Assert.AreEqual("barry_struck_b_0", cpuView.SpriteRenderer.sprite.name, "a Strike 1 hit shows the struck B sprite");

            int struckEntries = 0;
            match.Events.StateChanged += (id, from, to) => { if (id == WrestlerId.Cpu && to == CombatState.Struck) struckEntries++; };
            TickUntil(() => hits.Count > 1);
            Assert.AreEqual(MoveId.Strike2, hits[1].Move);
            Assert.AreEqual(CombatState.Struck, match.Cpu.State);
            Assert.AreEqual(0, struckEntries, "setup: a Struck restart raises no StateChanged");

            yield return Settle();
            AnimatorStateInfo after = StateOf(cpuView);
            Assert.IsTrue(after.IsName(WrestlerView.StruckCStateName), "the Strike 2 hit switches to StruckC");
            Assert.IsTrue(cpuView.Animator.GetBool(WrestlerView.StruckCParam), "StruckC parameter set for Strike 2");
            Assert.Less(after.normalizedTime, before.normalizedTime - 0.25f, "the reaction should play from the start on the second hit");
            Assert.AreEqual("barry_struck_c_0", cpuView.SpriteRenderer.sprite.name, "a Strike 2 hit shows the struck C sprite");
        }

        // A Struck that is not a combo hit shows "StruckB": a run into the truck above the knockdown threshold puts the
        // runner in Struck with an ObstacleHit and no HitLanded (D-030).
        [UnityTest]
        public IEnumerator ObstacleCrashStruck_ShowsStruckB()
        {
            // ARCHITECTURE 3.1 movement-slice band and truck footprint (the same values as the EditMode SliceArena).
            var truck = new ArenaRect(-0.6667f, -10.8333f, 7.7f, -9.6667f);
            var arena = new ArenaGeometry(new ArenaRect(0f, -13.6667f, 26.6667f, -9.6667f), new[] { truck });
            Build(new Vector2(12f, -10.2f), new Vector2(1f, -13f), arena: arena);
            yield return null;
            var hits = new List<HitInfo>();
            match.Events.HitLanded += hits.Add;
            int obstacleHits = 0;
            match.Events.ObstacleHit += (id, contact) => { if (id == WrestlerId.Player) obstacleHits++; };
            playerInput.PressRunAt(0); // runs left, toward the CPU, into the truck
            TickUntil(() => match.Player.State == CombatState.Struck);
            Assert.AreEqual(1, obstacleHits, "setup: the player crashed into the truck");
            Assert.IsEmpty(hits, "setup: an obstacle is not a HitLanded");

            yield return Settle();
            Assert.IsTrue(StateOf(playerView).IsName(WrestlerView.StruckBStateName), "an obstacle crash shows StruckB");
            Assert.IsFalse(playerView.Animator.GetBool(WrestlerView.StruckCParam), "StruckC parameter not set");
            Assert.AreEqual("barry_struck_b_0", playerView.SpriteRenderer.sprite.name, "an obstacle crash shows the struck B sprite");
        }

        // D-041: knocked out while already lying down (a stomp, D-025; Combat reports FallSide 0 then) keeps the Fall
        // state where it is: the downed sprite stays exactly as it lay after the fall (same sprite, flip and offset), with
        // the fall side the wrestler went down with.
        [UnityTest]
        public IEnumerator KnockoutWhileDown_HoldsLyingPose()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f; t.stompDamage = 50f;
                t.knockdownSlideDistance = 0f; // D-041 slide off: the player stays in stomp range without stepping in
            });
            yield return null;
            playerInput.PressStrikeAt(0);
            TickUntil(() => match.Cpu.State == CombatState.KnockedDown);
            int side = match.Cpu.FallSide;
            Assert.AreEqual(1, side, "setup: hit from the left, falls toward +x");
            TickUntil(() => match.Player.State == CombatState.Standing);
            yield return Settle();
            yield return new WaitForSeconds(0.8f); // the fall lands (0.35 s)
            Assert.IsTrue(StateOf(cpuView).IsName(WrestlerView.FallStateName));
            Assert.AreEqual(BarryDownedSprite(), cpuView.SpriteRenderer.sprite, "setup: the fall has landed on the downed sprite");
            Assert.IsTrue(cpuView.DownedMirrored, "setup: the landed fall shows the downed sprite mirrored");
            Assert.AreEqual(side, HeadSide(cpuView), "setup: head on the fall side");
            bool lyingFlip = cpuView.SpriteRenderer.flipX;
            Vector3 lyingPos = cpuView.SpriteRenderer.transform.localPosition;

            playerInput.PressStrikeAt(0); // stomp
            TickUntil(() => match.IsOver);
            Assert.AreEqual(CombatState.KnockedOut, match.Cpu.State);
            Assert.AreEqual(0, match.Cpu.FallSide, "setup: a stomp KO while down reports no fall side");
            for (int f = 0; f < 30; f++)
            {
                yield return new WaitForEndOfFrame();
                Assert.IsTrue(StateOf(cpuView).IsName(WrestlerView.FallStateName), "the lying pose is kept (no new fall, no switch)");
                Assert.AreEqual(BarryDownedSprite(), cpuView.SpriteRenderer.sprite, "the downed sprite is held");
                Assert.AreEqual(lyingFlip, cpuView.SpriteRenderer.flipX, "the downed sprite keeps its flip");
                Assert.AreEqual(lyingPos.x, cpuView.SpriteRenderer.transform.localPosition.x, 1e-4f, "the downed sprite keeps its offset");
                Assert.AreEqual(side, cpuView.DownedFallSide, "the latched fall side is kept");
            }
        }

        // D-041: knocked out from standing plays the whole fall from its start (struck_a, fall2, fall3, downed).
        [UnityTest]
        public IEnumerator KnockoutFromStanding_PlaysTheFall()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t => t.maxHealth = 3f);
            yield return null;
            playerInput.PressStrikeAt(0);
            TickUntil(() => match.IsOver);
            Assert.AreEqual(CombatState.KnockedOut, match.Cpu.State);
            Assert.AreEqual(1, match.Cpu.FallSide, "setup: a KO from standing falls away from the attacker");
            yield return SampleFall(cpuView, 1f);
            AssertBarryFall(1);
        }

        // D-041 Falling Down, both sides: a strike knockdown plays struck_a -> fall2 -> fall3 -> downed. On every frame the
        // head is on the FallSide side (fall frames stored head-right drawn unflipped for +1 / flipped for -1; the downed
        // sprite, stored head-left, mirrored with downedMirrorShift), so head and feet never swap sides.
        [UnityTest]
        public IEnumerator StrikeKnockdown_HitFromLeft_FallsTowardPlusX() => StrikeKnockdownFall(cpuOnRight: true);

        [UnityTest]
        public IEnumerator StrikeKnockdown_HitFromRight_FallsTowardMinusX() => StrikeKnockdownFall(cpuOnRight: false);

        private IEnumerator StrikeKnockdownFall(bool cpuOnRight)
        {
            Vector2 left = new Vector2(10f, -12f), right = new Vector2(12f, -12f);
            Build(cpuOnRight ? left : right, cpuOnRight ? right : left, t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f;
            });
            yield return null;
            playerInput.PressStrikeAt(0);
            TickUntil(() => match.Cpu.State == CombatState.KnockedDown);
            int side = match.Cpu.FallSide;
            Assert.AreEqual(cpuOnRight ? 1 : -1, side, "setup: falls away from the attacker");
            yield return SampleFall(cpuView, 1f);
            AssertBarryFall(side);
        }

        // D-041: a bodyslam knockdown (FallSide 0) never plays the fall; it ends on the downed sprite exactly as before.
        [UnityTest]
        public IEnumerator BodyslamKnockdown_ShowsNoFall()
        {
            Build(new Vector2(10f, -12f), new Vector2(11.5f, -12f), t => { t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f; });
            yield return null;
            playerInput.PressGrappleAt(0, 1); // lock up, then grapple: bodyslam
            TickUntil(() => match.Cpu.State == CombatState.Grappled);
            // Render the slam (the view sees Grappled), then let combat reach the knockdown.
            for (int f = 0; f < 30 && match.Cpu.State == CombatState.Grappled; f++)
            {
                yield return null;
                Tick(2);
            }
            TickUntil(() => match.Cpu.State == CombatState.KnockedDown);
            Assert.AreEqual(0, match.Cpu.FallSide, "setup: a bodyslam knockdown has no fall side");
            for (int f = 0; f < 45; f++)
            {
                yield return new WaitForEndOfFrame();
                Assert.IsFalse(StateOf(cpuView).IsName(WrestlerView.FallStateName), $"frame {f}: no fall after a bodyslam");
            }
            Assert.IsTrue(StateOf(cpuView).IsName("KnockedDown"));
            Assert.AreEqual(BarryDownedSprite(), cpuView.SpriteRenderer.sprite, "the downed sprite");
            Assert.IsFalse(cpuView.DownedMirrored, "drawn exactly as the bodyslam's last frame");
            Assert.AreEqual(0, cpuView.DownedFallSide);
            Assert.AreEqual(0f, cpuView.SpriteRenderer.transform.localPosition.x, 1e-4f, "no mirror shift");
        }

        // D-041 Peaux: his fall art (gen-peaux-fall2-D-1 / gen-peaux-fall3-C-1) plays the same sequence as Barry's:
        // struck_a -> fall2 -> fall3 -> his downed sprite, head on the fall side every frame, landing about 0.35 s in.
        [UnityTest]
        public IEnumerator PeauxStrikeKnockdown_PlaysHisFall()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f;
            }, PeauxPrefab);
            yield return null;
            playerInput.PressStrikeAt(0);
            TickUntil(() => match.Cpu.State == CombatState.KnockedDown);
            Assert.AreEqual(1, match.Cpu.FallSide, "setup");
            yield return SampleFall(cpuView, 1f);
            // D-044: Peaux's downed sprite is now R6 (peaux_bodyslammed5_6, pivot = body centre).
            Sprite r6 = LastSprite("Assets/Art/Wrestlers/Peaux/peaux_bodyslammed5_6.png");
            AssertFall(1, "peaux", r6);
            // D-044 / T2.6: fall3 -> R6 lands without a jump: R6's feet end (the end toward the attacker, -x here) sits
            // where fall3's did (the builder's fallLandingShift).
            Assert.Greater(cpuView.FallLandingShift, 0.5f, "setup: Peaux's prefab has a fall landing shift");
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(r6, cpuView.SpriteRenderer.sprite, "setup: landed");
            Assert.IsTrue(cpuView.SpriteRenderer.flipX, "setup: R6 (stored head left) flipped, head toward +x");
            float root = cpuGo.transform.position.x;
            // Flipped, R6's stored right edge (his feet) is drawn on the left; fall3 (stored head right) is unflipped.
            float downedFeet = root + cpuView.SpriteRenderer.transform.localPosition.x - OpaqueExtentX(r6).y;
            float fall3Feet = root + OpaqueExtentX(OnlySprite("Assets/Art/Wrestlers/Peaux/peaux_fall3.png")).x;
            Assert.AreEqual(fall3Feet, downedFeet, 1.01f / 60f, "fall3 and R6 feet ends line up (within 1 px)");
        }

        /// <summary>Opaque x-extent of a sprite relative to its pivot, world units, stored orientation (read from the PNG).</summary>
        private static Vector2 OpaqueExtentX(Sprite sprite)
        {
#if UNITY_EDITOR
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(System.IO.File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(sprite))));
                Rect r = sprite.rect;
                Color32[] px = tex.GetPixels32();
                int min = int.MaxValue, max = -1;
                for (int y = (int)r.y; y < (int)(r.y + r.height); y++)
                    for (int x = (int)r.x; x < (int)(r.x + r.width); x++)
                        if (px[y * tex.width + x].a > 0)
                        {
                            min = Mathf.Min(min, x - (int)r.x);
                            max = Mathf.Max(max, x - (int)r.x);
                        }
                return new Vector2((min - sprite.pivot.x) / sprite.pixelsPerUnit, (max + 1 - sprite.pivot.x) / sprite.pixelsPerUnit);
            }
            finally
            {
                Object.Destroy(tex);
            }
#else
            return Vector2.zero;
#endif
        }

        private const string PeauxPrefab = "Assets/Prefabs/Wrestlers/Peaux.prefab";

        private struct FallSample
        {
            public int Frame;
            public Sprite Sprite;
            public bool FlipX;
            public float OffsetX;
        }

        private readonly List<FallSample> fallSamples = new List<FallSample>();

        /// <summary>Samples what each frame renders (after LateUpdate) while the Animator is in the Fall state.</summary>
        private IEnumerator SampleFall(WrestlerView v, float seconds)
        {
            fallSamples.Clear();
            int frames = Mathf.RoundToInt(seconds * 60f);
            for (int f = 0; f < frames; f++)
            {
                yield return new WaitForEndOfFrame();
                if (!v.Animator.GetCurrentAnimatorStateInfo(0).IsName(WrestlerView.FallStateName))
                {
                    Assert.AreEqual(0, fallSamples.Count, $"frame {f}: left the Fall state");
                    Assert.Less(f, 3, "the Fall state should start within two frames");
                    continue;
                }
                fallSamples.Add(new FallSample
                {
                    Frame = f, Sprite = v.SpriteRenderer.sprite, FlipX = v.SpriteRenderer.flipX,
                    OffsetX = v.SpriteRenderer.transform.localPosition.x,
                });
            }
        }

        /// <summary>
        /// The sampled Barry fall: sprites in order struck_a, fall2, fall3, downed (each shown, none repeated after the
        /// next), landing about 0.35 s (21 frames) in; head on <paramref name="side"/> on every frame; fall frames at the
        /// root (no offset), the downed sprite mirrored and shifted by downedMirrorShift toward the fall side.
        /// </summary>
        private void AssertBarryFall(int side) => AssertFall(side, "barry", BarryDownedSprite());

        /// <summary>The sampled fall of the wrestler whose art files start with <paramref name="prefix"/> (see AssertBarryFall).</summary>
        private void AssertFall(int side, string prefix, Sprite downed)
        {
            string trace = string.Join(" ", fallSamples.ConvertAll(s => $"{s.Frame}:{s.Sprite.name}{(s.FlipX ? "F" : "")}"));
            Assert.Greater(fallSamples.Count, 30, "setup: the Fall state played\n" + trace);
            var order = new List<string>();
            int landFrame = -1;
            float shift = cpuView.DownedMirrorShift;
            foreach (FallSample s in fallSamples)
            {
                if (order.Count == 0 || order[order.Count - 1] != s.Sprite.name)
                    order.Add(s.Sprite.name);
                bool isDowned = s.Sprite == downed;
                if (isDowned && landFrame < 0) landFrame = s.Frame - fallSamples[0].Frame;
                Assert.AreEqual(side, HeadSide(s.Sprite, s.FlipX, downed), $"frame {s.Frame}: head on the fall side\n{trace}");
                Assert.AreEqual(isDowned ? side * (shift + cpuView.FallLandingShift) : 0f, s.OffsetX, 1e-3f,
                    $"frame {s.Frame}: sprite x offset (downedMirrorShift + D-044 fallLandingShift)\n{trace}");
            }
            CollectionAssert.AreEqual(new[] { prefix + "_struck_a_0", prefix + "_fall2_0", prefix + "_fall3_0", downed.name }, order, trace);
            Assert.That(landFrame, Is.InRange(19, 23), "lands about 0.35 s in (default holds 100 / 120 / 130 ms)\n" + trace);
        }

        /// <summary>World side (+1 / -1) of the head: fall frames are stored head-right, the downed sprite head-left.</summary>
        private static int HeadSide(Sprite sprite, bool flipX, Sprite downed)
        {
            int storedHeadRight = sprite == downed ? -1 : 1;
            return flipX ? -storedHeadRight : storedHeadRight;
        }

        private static int HeadSide(WrestlerView v) => HeadSide(v.SpriteRenderer.sprite, v.SpriteRenderer.flipX, BarryDownedSprite());

        // Designer 2026-11-12 / D-041: a knockdown that does not follow a bodyslam (here a run collision, D-029, which
        // knocks both down on the same tick, each falling away from the other) ends its fall on the downed sprite
        // mirrored in place: shifted by downedMirrorShift so the body covers the same ground behind the root with head
        // and feet swapped (feet toward the opponent). The two fall opposite ways, so they lie as exact mirror images.
        [UnityTest]
        public IEnumerator RunCollisionKnockdown_ShowsDownedSpriteMirroredInPlace()
        {
            Build(new Vector2(5f, -12f), new Vector2(12f, -12f), t => { t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f; });
            yield return null;
            playerInput.PressRunAt(0);
            TickUntil(() => match.Player.State == CombatState.KnockedDown && match.Cpu.State == CombatState.KnockedDown);
            Assert.AreEqual(-1, match.Player.FallSide, "setup: the runner falls back toward -x");
            Assert.AreEqual(1, match.Cpu.FallSide, "setup: the CPU falls toward +x");
            yield return Settle();
            yield return new WaitForSeconds(0.6f); // the falls land (0.35 s)
            Assert.IsTrue(StateOf(playerView).IsName(WrestlerView.FallStateName) && StateOf(cpuView).IsName(WrestlerView.FallStateName));

            Assert.AreEqual(1, match.Player.HorizontalFacing, "setup: player faces right");
            Assert.AreEqual(-1, match.Cpu.HorizontalFacing, "setup: CPU faces left");
            Assert.AreEqual(BarryDownedSprite(), playerView.SpriteRenderer.sprite, "player shows the downed sprite");
            Assert.AreEqual(BarryDownedSprite(), cpuView.SpriteRenderer.sprite, "CPU shows the downed sprite");
            Assert.IsTrue(playerView.DownedMirrored && cpuView.DownedMirrored, "a run-collision knockdown is mirrored");
            Assert.IsFalse(playerView.SpriteRenderer.flipX, "right-facing, mirrored: not flipped");
            Assert.IsTrue(cpuView.SpriteRenderer.flipX, "left-facing, mirrored: flipped");

            // Barry's R6 is centred on the body: downedMirrorShift ~0, and the D-044 fallLandingShift puts its feet end
            // where fall3's was (toward the fall side).
            float shift = cpuView.DownedMirrorShift + cpuView.FallLandingShift;
            Assert.Greater(shift, 0.5f, "setup: the prefab has a downed mirror / fall landing shift");
            Assert.AreEqual(shift, cpuView.SpriteRenderer.transform.localPosition.x, 1e-3f, "left-facing: shifted +shift");
            Assert.AreEqual(-shift, playerView.SpriteRenderer.transform.localPosition.x, 1e-3f, "right-facing: shifted -shift");
            Assert.AreEqual(0f, SpriteRotZ(cpuView), 0.01f, "no rotation");

            Bounds cb = cpuView.SpriteRenderer.bounds, pb = playerView.SpriteRenderer.bounds;
            float cRoot = cpuGo.transform.position.x, pRoot = playerGo.transform.position.x;
            Assert.AreEqual(-(cb.min.x - cRoot), pb.max.x - pRoot, 1e-3f, "mirror images about their roots");
            Assert.AreEqual(-(cb.max.x - cRoot), pb.min.x - pRoot, 1e-3f, "mirror images about their roots");
            Assert.AreEqual(new Vector3(5f, -12f, 0f), playerGo.transform.position, "the root never moves");
        }

        // ------------------------------------------------------------------ D-043 get-up (crouch at the root)

        private const string BarryCrouch = "Assets/Art/Wrestlers/Barry/barry_crouch.png";
        private const string PeauxCrouch = "Assets/Art/Wrestlers/Peaux/peaux_crouch.png";
        private const string BarryStand = "Assets/Art/Wrestlers/Barry/barry_stand_front.png";
        private const string PeauxStand = "Assets/Art/Wrestlers/Peaux/peaux_stand_front.png";

        // D-043: every knockdown ends with the get-up. The crouch shows for the whole get-up, drawn by the combat facing at
        // the root (child offset 0, no slide: designer, after playtesting); then the wrestler stands as before, on the same
        // anchor. Both wrestlers, both fall sides / facings.
        [UnityTest]
        public IEnumerator GetUp_Barry_HitFromLeft_CrouchAtRootThenStands() => GetUpCrouch(BarryPrefab, BarryCrouch, BarryStand, true);

        [UnityTest]
        public IEnumerator GetUp_Barry_HitFromRight_CrouchAtRootThenStands() => GetUpCrouch(BarryPrefab, BarryCrouch, BarryStand, false);

        [UnityTest]
        public IEnumerator GetUp_Peaux_HitFromLeft_CrouchAtRootThenStands() => GetUpCrouch(PeauxPrefab, PeauxCrouch, PeauxStand, true);

        [UnityTest]
        public IEnumerator GetUp_Peaux_HitFromRight_CrouchAtRootThenStands() => GetUpCrouch(PeauxPrefab, PeauxCrouch, PeauxStand, false);

        private IEnumerator GetUpCrouch(string cpuPrefab, string crouchPath, string standPath, bool cpuOnRight)
        {
            Vector2 left = new Vector2(10f, -12f), right = new Vector2(12f, -12f);
            Build(cpuOnRight ? left : right, cpuOnRight ? right : left, t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; t.getUpTime = 0.25f;
            }, cpuPrefab);
            yield return null;
            Sprite crouch = OnlySprite(crouchPath), stand = OnlySprite(standPath);
            Assert.IsNotNull(crouch, "setup: crouch sprite");

            playerInput.PressStrikeAt(0);
            TickUntil(() => match.Cpu.State == CombatState.KnockedDown);
            int side = match.Cpu.FallSide;
            Assert.AreEqual(cpuOnRight ? 1 : -1, side, "setup: falls away from the attacker");
            yield return Settle();
            yield return new WaitForSeconds(0.6f); // the fall lands (0.35 s)
            Assert.IsTrue(StateOf(cpuView).IsName(WrestlerView.FallStateName), "setup: lying after the fall");
            Assert.IsFalse(cpuView.ShowingGetUp);
            Assert.AreNotEqual(0f, cpuView.SpriteRenderer.transform.localPosition.x, "setup: the lying sprite is shifted");

            TickUntil(() => match.Cpu.IsGettingUp);
            bool facingFlip = WrestlerView.ShouldFlip(match.Cpu.HorizontalFacing, true);
            int frames = 0;
            for (int i = 0; i < 40 && match.Cpu.IsGettingUp; i++)
            {
                yield return Settle();
                Assert.IsTrue(StateOf(cpuView).IsName(WrestlerView.CrouchStateName), $"get-up tick {i}: the Crouch state");
                Assert.AreEqual(crouch, cpuView.SpriteRenderer.sprite, $"get-up tick {i}: the crouch sprite");
                Assert.IsTrue(cpuView.ShowingGetUp);
                Assert.AreEqual(facingFlip, cpuView.SpriteRenderer.flipX, "the crouch is drawn by the combat facing");
                Assert.AreEqual(0f, cpuView.SpriteRenderer.transform.localPosition.x, 1e-4f,
                    $"get-up tick {i}: the crouch is drawn at the root (no slide)");
                Assert.AreEqual(0f, SpriteRotZ(cpuView), 0.01f, "no rotation");
                frames++;
                Tick(1);
            }
            Assert.Greater(frames, 15, "setup: the whole 0.25 s get-up was sampled");

            TickUntil(() => match.Cpu.State != CombatState.KnockedDown, 1f);
            // A one-strike knockdown needs health below the Recovering threshold, so he gets up into Recovering (its
            // stand pose is the stand sprite, hunched).
            Assert.AreEqual(CombatState.Recovering, match.Cpu.State, "setup: gets up into Recovering (health below 30)");
            yield return Settle();
            Assert.IsTrue(StateOf(cpuView).IsName("RecoveringStand"), "standing (Recovering) after the get-up");
            Assert.AreEqual(stand, cpuView.SpriteRenderer.sprite, "the stand sprite");
            Assert.IsFalse(cpuView.ShowingGetUp);
            Assert.AreEqual(0f, cpuView.SpriteRenderer.transform.localPosition.x, 1e-4f, "the stand is on the same anchor");
            Assert.AreEqual(facingFlip, cpuView.SpriteRenderer.flipX, "stands facing the same way as the crouch");
        }

        // D-043: the bodyslam receiver (no fall side; lying as the slam left him) gets up the same way: the crouch at the
        // root, then Standing (the bodyslam leaves him above 30 health).
        [UnityTest]
        public IEnumerator GetUp_AfterBodyslam_CrouchAtRootThenStands()
        {
            Build(new Vector2(10f, -12f), new Vector2(11.5f, -12f), t =>
            {
                t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; t.getUpTime = 0.25f;
            });
            yield return null;
            playerInput.PressGrappleAt(0, 1); // lock up, then grapple: bodyslam
            TickUntil(() => match.Cpu.State == CombatState.Grappled);
            for (int f = 0; f < 30 && match.Cpu.State == CombatState.Grappled; f++)
            {
                yield return null;
                Tick(2);
            }
            TickUntil(() => match.Cpu.State == CombatState.KnockedDown);
            yield return Settle();
            Assert.AreEqual(BarryDownedSprite(), cpuView.SpriteRenderer.sprite, "setup: lying after the slam");
            Assert.IsFalse(cpuView.DownedMirrored, "setup: as the slam left him");

            TickUntil(() => match.Cpu.IsGettingUp);
            for (int i = 0; i < 40 && match.Cpu.IsGettingUp; i++)
            {
                yield return Settle();
                Assert.IsTrue(StateOf(cpuView).IsName(WrestlerView.CrouchStateName), $"get-up tick {i}: the Crouch state");
                Assert.AreEqual(OnlySprite(BarryCrouch), cpuView.SpriteRenderer.sprite);
                Assert.AreEqual(0f, cpuView.SpriteRenderer.transform.localPosition.x, 1e-4f, "at the root (no slide)");
                Tick(1);
            }

            TickUntil(() => match.Cpu.State != CombatState.KnockedDown, 1f);
            Assert.AreEqual(CombatState.Standing, match.Cpu.State, "setup: the bodyslam leaves him above 30 health");
            yield return Settle();
            Assert.IsTrue(StateOf(cpuView).IsName("Standing"));
            Assert.AreEqual(OnlySprite(BarryStand), cpuView.SpriteRenderer.sprite, "the stand sprite");
            Assert.AreEqual(0f, cpuView.SpriteRenderer.transform.localPosition.x, 1e-4f, "on the root");
        }

        /// <summary>The single sprite of a one-sprite texture (any name).</summary>
        private static Sprite OnlySprite(string texturePath)
        {
#if UNITY_EDITOR
            foreach (Object o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(texturePath))
                if (o is Sprite s) return s;
#endif
            return null;
        }

        // D-043: a knockout has no get-up: the knocked-out wrestler never shows the crouch.
        [UnityTest]
        public IEnumerator Knockout_ShowsNoCrouch()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t => t.maxHealth = 3f);
            yield return null;
            playerInput.PressStrikeAt(0);
            TickUntil(() => match.IsOver);
            Assert.AreEqual(CombatState.KnockedOut, match.Cpu.State);
            for (int f = 0; f < 90; f++)
            {
                yield return new WaitForEndOfFrame();
                Assert.IsFalse(StateOf(cpuView).IsName(WrestlerView.CrouchStateName), $"frame {f}: no crouch on a knockout");
                Assert.IsFalse(cpuView.ShowingGetUp);
            }
            Assert.AreEqual(BarryDownedSprite(), cpuView.SpriteRenderer.sprite, "lying");
        }

        // D-043: a Stomp knockout during the get-up goes straight back to the lying pose the wrestler got up from (same
        // sprite, flip and offset as before the get-up), with no new fall and no crouch.
        [UnityTest]
        public IEnumerator StompKnockoutDuringGetUp_ReturnsToLyingPose()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; t.getUpTime = 2f;
                t.stompDamage = 50f;
                t.knockdownSlideDistance = 0f; // the player stays in stomp range
            });
            yield return null;
            playerInput.PressStrikeAt(0);
            TickUntil(() => match.Cpu.State == CombatState.KnockedDown);
            yield return Settle();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(BarryDownedSprite(), cpuView.SpriteRenderer.sprite, "setup: lying");
            bool lyingFlip = cpuView.SpriteRenderer.flipX;
            float lyingX = cpuView.SpriteRenderer.transform.localPosition.x;

            TickUntil(() => match.Cpu.IsGettingUp && match.Player.State == CombatState.Standing);
            yield return Settle();
            Assert.IsTrue(StateOf(cpuView).IsName(WrestlerView.CrouchStateName), "setup: getting up");

            playerInput.PressStrikeAt(0); // stomp
            TickUntil(() => match.IsOver);
            Assert.AreEqual(CombatState.KnockedOut, match.Cpu.State, "setup: the stomp knocks out during the get-up");
            for (int f = 0; f < 30; f++)
            {
                yield return new WaitForEndOfFrame();
                if (f < 2) continue; // the view applies the KO on the next Update
                Assert.IsTrue(StateOf(cpuView).IsName(WrestlerView.FallStateName), $"frame {f}: back in the lying pose");
                Assert.AreEqual(BarryDownedSprite(), cpuView.SpriteRenderer.sprite, $"frame {f}: the downed sprite, no new fall");
                Assert.AreEqual(lyingFlip, cpuView.SpriteRenderer.flipX, "same flip as before the get-up");
                Assert.AreEqual(lyingX, cpuView.SpriteRenderer.transform.localPosition.x, 1e-4f, "same offset, no slide");
                Assert.IsFalse(cpuView.ShowingGetUp);
            }
        }

        /// <summary>Barry's downed sprite: the last sprite of Barry_Bodyslammed, his throw R6 (Peaux slams Barry, 2026-11-17).</summary>
        private static Sprite BarryDownedSprite() => LastSprite("Assets/Art/Wrestlers/Barry/barry_bodyslammed5_6.png");

        /// <summary>The highest-numbered sprite of a sheet.</summary>
        private static Sprite LastSprite(string texturePath)
        {
#if UNITY_EDITOR
            Sprite last = null;
            int lastIndex = -1;
            foreach (Object o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(texturePath))
                if (o is Sprite s && int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int i) && i > lastIndex)
                {
                    lastIndex = i;
                    last = s;
                }
            return last;
#else
            return null;
#endif
        }

        /// <summary>Queued test input (EditMode's ScriptedInput lives in another assembly).</summary>
        private sealed class QueuedInput : IWrestlerInput
        {
            private readonly Queue<WrestlerIntent> queue = new Queue<WrestlerIntent>();

            public void PressStrikeAt(params int[] ticks) => Queue(ticks, 2);
            public void PressRunAt(params int[] ticks) => Queue(ticks, 1);
            public void PressGrappleAt(params int[] ticks) => Queue(ticks, 0);

            /// <param name="button">0 = Lock Up / Grapple, 1 = Run / Whip, 2 = Strike.</param>
            private void Queue(int[] ticks, int button)
            {
                int last = 0;
                foreach (int t in ticks) last = Mathf.Max(last, t);
                var set = new HashSet<int>(ticks);
                for (int i = 0; i <= last; i++)
                {
                    ButtonState press = set.Contains(i) ? new ButtonState(true, true) : ButtonState.None;
                    queue.Enqueue(new WrestlerIntent(Vector2.zero, button == 0 ? press : ButtonState.None,
                        button == 1 ? press : ButtonState.None, button == 2 ? press : ButtonState.None));
                }
            }

            public WrestlerIntent Read() => queue.Count > 0 ? queue.Dequeue() : WrestlerIntent.None;
        }
    }
}
