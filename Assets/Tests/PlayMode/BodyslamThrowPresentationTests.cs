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
    /// Bodyslam throw presentation (D-044, ARCHITECTURE 6 "Bodyslam throw: integration contract", ACCEPTANCE_TESTS
    /// T2.1-T2.7), driven like <see cref="IrishWhipPresentationTests"/>: a real CombatMatch ticked every frame with
    /// Time.deltaTime before WrestlerView.Update (execution order -50), roots written from the combat positions, the real
    /// Barry / Peaux definitions and prefabs, and a copy of the project CombatTuning (knockdowns shortened so the get-up
    /// is reached quickly; the throw entries and bodyslam timing are the project's unless a test overrides one in the copy). Every rendered frame is sampled after
    /// LateUpdate.
    /// </summary>
    public class BodyslamThrowPresentationTests
    {
        private const string TuningPath = "Assets/Data/Tuning/CombatTuning.asset";
        private static readonly Vector2 Left = new Vector2(10f, -12f);
        private static readonly Vector2 Right = new Vector2(11.5f, -12f);

        private readonly List<Object> spawned = new List<Object>();

        [SetUp]
        public void SetUp() => Time.captureDeltaTime = 1f / 60f;

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            foreach (Object o in spawned)
                if (o != null) Object.Destroy(o);
            spawned.Clear();
        }

        private struct Sample
        {
            public int Frame;
            public CombatState BarryState, PeauxState;
            public int SlamIndex, SlammedIndex;      // S1..S5 = 0..4, Barry's stand = 5; R1..R7 = 0..6; -1 other
            public bool BarryFlip, PeauxFlip;
            public float BarryLocalX, PeauxLocalX, PeauxSpriteX, PeauxRootX;
            public int BarryOrder, PeauxOrder;
            public bool PeauxInBodyslammed, PeauxInKnockedDown, PeauxInCrouch, BarryInBodyslam;
            public string PeauxSprite;
        }

        // Barry on the left faces right (+1) in the lock-up, so Peaux is thrown to the left (T = -1), and vice versa.
        [UnityTest]
        public IEnumerator BarrySlamsPeaux_ThrowLeft() => RunThrow(barryOnLeft: true);

        [UnityTest]
        public IEnumerator BarrySlamsPeaux_ThrowRight() => RunThrow(barryOnLeft: false);

        // Peaux slams Barry (art approved 2026-11-17, throw on in the project asset since D-045): the same checks with the
        // roles swapped, using the project's entry as shipped.
        [UnityTest]
        public IEnumerator PeauxSlamsBarry_ThrowLeft() => RunThrow(barryOnLeft: true, "Peaux", "Barry");

        [UnityTest]
        public IEnumerator PeauxSlamsBarry_ThrowRight() => RunThrow(barryOnLeft: false, "Peaux", "Barry");

        // The same with Peaux's entry forced to the art's measured values in the tuning copy (159 / 342 px, Starting
        // point), so these keep checking the art even if the project asset is retuned.
        [UnityTest]
        public IEnumerator PeauxSlamsBarry_ThrowLeft_EnabledInCopy() => RunThrow(barryOnLeft: true, "Peaux", "Barry", true);

        [UnityTest]
        public IEnumerator PeauxSlamsBarry_ThrowRight_EnabledInCopy() => RunThrow(barryOnLeft: false, "Peaux", "Barry", true);

        /// <summary>T2.1-T2.5 for one throw direction. "barry" / "peaux" below are the slammer / receiver (Barry slams
        /// Peaux unless the names say otherwise).</summary>
        private IEnumerator RunThrow(bool barryOnLeft, string slammerName = "Barry", string receiverName = "Peaux",
            bool enableThrowInCopy = false)
        {
#if UNITY_EDITOR
            string sl = slammerName.ToLowerInvariant(), rc = receiverName.ToLowerInvariant();
            Setup(out CombatMatch match, out WrestlerView barry, out WrestlerView peaux, out TickDriver driver,
                  out WrestlerDefinition barryDef, barryOnLeft ? Left : Right, barryOnLeft ? Right : Left, slammerName, receiverName,
                  enableThrowInCopy ? (System.Action<CombatTuning, WrestlerDefinition>)EnableThrow : null);
            PairedMoveInfo? slam = null;
            match.Events.PairedMoveStarted += i => { if (i.Move == MoveId.Bodyslam) slam = i; };
            Sprite stand = OnlySprite($"Assets/Art/Wrestlers/{slammerName}/{sl}_stand_front.png");
            Sprite r6 = OnlySprite($"Assets/Art/Wrestlers/{receiverName}/{rc}_bodyslammed5_6.png");

            driver.Queue(new WrestlerIntent(Vector2.zero, new ButtonState(true, true), ButtonState.None, ButtonState.None));
            for (int i = 0; i < 20; i++) driver.Queue(WrestlerIntent.None);
            driver.Queue(new WrestlerIntent(Vector2.zero, new ButtonState(true, true), ButtonState.None, ButtonState.None));
            for (int guard = 0; !slam.HasValue; guard++)
            {
                Assert.Less(guard, 600, "setup: the bodyslam never started");
                yield return null;
            }
            PairedMoveInfo info = slam.Value;
            int T = info.ThrowDirection;
            int F = info.Facing;
            Assert.AreEqual(barryOnLeft ? -1 : 1, T, "setup: thrown past Barry");
            Assert.AreEqual(-T, F, "setup: T = -Facing");
            float lockUpX = peaux.transform.position.x;
            Assert.AreEqual(1f, barry.MoveSpeed, 0.01f, "T2.1: Barry's clip plays at 1x at the defaults");
            Assert.AreEqual(1f, peaux.MoveSpeed, 0.01f, "T2.1: Peaux's clip plays at 1x at the defaults");

            var samples = new List<Sample>();
            // Sample every frame until the get-up starts (0.62 s slam + 0.6 s knockdown here).
            for (int f = 0; f < 300 && !match.Cpu.IsGettingUp; f++)
            {
                yield return new WaitForEndOfFrame();
                samples.Add(Take(f, match, barry, peaux, sl, rc));
            }
            string trace = Trace(samples);
            Debug.Log($"Bodyslam throw trace, T {T:+0;-0}:\n{trace}");

            bool flipF = WrestlerView.ShouldFlip(F, true), flipT = WrestlerView.ShouldFlip(T, true);
            bool lateFlip = T < 0; // R4-R7 / R6 stored for a throw to screen-right
            var seenS = new HashSet<int>();
            var seenR = new HashSet<int>();
            int landFrame = -1, tailEnd = -1;
            bool sawR7AfterLanding = false;
            for (int i = 0; i < samples.Count; i++)
            {
                Sample s = samples[i];
                string at = $" (frame {s.Frame})\n{trace}";
                bool combatReleased = Mathf.Abs(s.PeauxRootX - lockUpX) > 1e-4f;
                bool grappling = s.BarryState == CombatState.Grappling;
                if (grappling)
                {
                    // T2.1: the two clips stay in step frame for frame (R1-R5 holds = S1-S5).
                    Assert.AreEqual(s.SlamIndex, s.SlammedIndex, "T2.1: S and R frames in step" + at);
                    // T2.2: S1-S2 face the lock-up facing, S3 on the throw direction.
                    Assert.AreEqual(s.SlamIndex <= 1 ? flipF : flipT, s.BarryFlip, "T2.2: Barry's facing per frame" + at);
                    Assert.Greater(s.BarryOrder, s.PeauxOrder, "the slammer draws in front during the slam" + at);
                    seenS.Add(s.SlamIndex);
                }
                Assert.AreEqual(0f, s.BarryLocalX, 1e-4f, "Barry is always drawn at his root (no child offset)" + at);
                if (s.PeauxInBodyslammed)
                {
                    seenR.Add(s.SlammedIndex);
                    if (s.SlammedIndex <= 2)
                    {
                        // T2.3: R1-R3 at the lock-up spot, Locked Up facing (= T), even on a frame the root already jumped.
                        Assert.AreEqual(lockUpX, s.PeauxSpriteX, 1e-4f, "T2.3: R1-R3 drawn at the lock-up spot" + at);
                        Assert.AreEqual(flipT, s.PeauxFlip, "T2.3: R1-R3 follow the Locked Up facing" + at);
                    }
                    else
                    {
                        // T2.1/T2.3: R4+ never shows before Combat released him; drawn at his root, oriented for T.
                        Assert.IsTrue(combatReleased, "T2.1: R4 never shows before the release" + at);
                        Assert.AreEqual(0f, s.PeauxLocalX, 1e-4f, "T2.3: R4-R7 at the moving root, no child offset" + at);
                        Assert.AreEqual(lateFlip, s.PeauxFlip, "T2.3: R4-R7 oriented for the throw direction" + at);
                    }
                }
                bool down = s.PeauxState == CombatState.KnockedDown || s.PeauxState == CombatState.KnockedOut;
                if (down && landFrame < 0)
                {
                    landFrame = i;
                    // T2.1: R6 shows on the landing tick (or one frame late at most).
                    Assert.That(s.SlammedIndex, Is.InRange(4, 5), "T2.1: R6 starts at the landing" + at);
                    Assert.AreNotEqual(CombatState.Grappling, s.BarryState, "the slammer is free at the landing" + at);
                }
                if (landFrame >= 0 && down && tailEnd < 0)
                {
                    if (s.PeauxInBodyslammed)
                    {
                        // T2.4: the R6 -> R7 -> R6 tail keeps playing while he is already Knocked Down.
                        if (s.SlammedIndex == 6) sawR7AfterLanding = true;
                        Assert.AreEqual(lateFlip, s.PeauxFlip, "T2.4: the tail keeps the throw orientation" + at);
                    }
                    else if (!s.PeauxInCrouch)
                    {
                        tailEnd = i;
                        Assert.IsTrue(s.PeauxInKnockedDown, "T2.4: after the tail, the Knocked Down clip" + at);
                    }
                }
                if (tailEnd >= 0 && s.PeauxInKnockedDown)
                {
                    // T2.4: the downed pose is the same R6, same orientation, at the root: no jump.
                    Assert.AreEqual(r6.name, s.PeauxSprite, "T2.4: downed sprite = R6" + at);
                    Assert.AreEqual(lateFlip, s.PeauxFlip, "T2.4: head toward Barry, as the slam left him" + at);
                    Assert.AreEqual(0f, s.PeauxLocalX, 1e-4f, "T2.4: no offset" + at);
                }
                // T2.5: Barry's S3-S5 and the stand after them all have their feet centre on the root.
                if (s.SlamIndex >= 2 || s.BarryState != CombatState.Grappling)
                    Assert.AreEqual(0f, s.BarryLocalX, 1e-4f, "T2.5" + at);
            }
            CollectionAssert.IsSupersetOf(seenS, new[] { 0, 1, 2, 3, 4 }, "every S frame shown\n" + trace);
            CollectionAssert.IsSupersetOf(seenR, new[] { 0, 1, 2, 3, 4, 5, 6 }, "every R frame shown\n" + trace);
            Assert.Greater(landFrame, 0, "the slam landed\n" + trace);
            Assert.IsTrue(sawR7AfterLanding, "T2.4: R7 (bounce) shown after he turned Knocked Down\n" + trace);
            Assert.Greater(tailEnd, landFrame, "T2.4: the tail ended in the Knocked Down clip\n" + trace);
            Assert.AreEqual(T, match.Player.HorizontalFacing, "Barry faces the throw after the landing");
            Assert.AreEqual(stand, barry.SpriteRenderer.sprite, "Barry stands after the slam");
            Assert.AreEqual(flipT, barry.SpriteRenderer.flipX, "the stand faces the throw direction, like S3-S5");

            // T2.5: pivots. S3-S5 and the stand are bottom-centre (feet centre) sprites, so their feet are on the root.
            foreach (Sprite sp in new[]
                     {
                         OnlySprite($"Assets/Art/Wrestlers/{slammerName}/{sl}_bodyslam5_3.png"),
                         OnlySprite($"Assets/Art/Wrestlers/{slammerName}/{sl}_bodyslam5_4.png"),
                         OnlySprite($"Assets/Art/Wrestlers/{slammerName}/{sl}_bodyslam5_5.png"), stand,
                     })
                Assert.AreEqual(sp.rect.width / 2f, sp.pivot.x, 0.51f, $"T2.5: {sp.name} pivot at the feet centre");

            // T2.4 / D-043: the crouch takes over at the get-up, drawn by the combat facing at the root.
            Assert.IsTrue(match.Cpu.IsGettingUp, "setup: the get-up started");
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(peaux.Animator.GetCurrentAnimatorStateInfo(0).IsName(WrestlerView.CrouchStateName), "the crouch at the get-up");
            Assert.AreEqual(0f, peaux.SpriteRenderer.transform.localPosition.x, 1e-4f, "the crouch at the root");
            Assert.AreEqual(WrestlerView.ShouldFlip(match.Cpu.HorizontalFacing, true), peaux.SpriteRenderer.flipX,
                "the crouch is drawn by the combat facing");
#else
            Assert.Ignore("Needs the Editor AssetDatabase.");
            yield break;
#endif
        }

        // T2.7: a disabled throw entry (the no-throw fallback) in a tuning copy: Peaux slams Barry with Peaux's entry off.
        // Barry's root never moves, no slam tail is played, and his downed sprite is his R6. (The project asset has the
        // throw on since D-045; this keeps the fallback covered.)
        [UnityTest]
        public IEnumerator PeauxSlamsBarry_DisabledInCopy_NoThrow()
        {
#if UNITY_EDITOR
            Setup(out CombatMatch match, out WrestlerView peaux, out WrestlerView barry, out TickDriver driver,
                  out _, Left, Right, "Peaux", "Barry", DisableThrow);
            PairedMoveInfo? slam = null;
            match.Events.PairedMoveStarted += i => { if (i.Move == MoveId.Bodyslam) slam = i; };
            driver.Queue(new WrestlerIntent(Vector2.zero, new ButtonState(true, true), ButtonState.None, ButtonState.None));
            for (int i = 0; i < 20; i++) driver.Queue(WrestlerIntent.None);
            driver.Queue(new WrestlerIntent(Vector2.zero, new ButtonState(true, true), ButtonState.None, ButtonState.None));
            for (int guard = 0; !slam.HasValue; guard++)
            {
                Assert.Less(guard, 600, "setup: the bodyslam never started");
                yield return null;
            }
            Assert.AreEqual(0, slam.Value.ThrowDirection, "no throw with Peaux's entry disabled");
            float barryX = barry.transform.position.x;
            // Peaux slams Barry plays the throw clips (peaux_bodyslam5 S1-S5 + stand, barry_bodyslammed5 R1-R7 + R6) even
            // with the entry disabled; it then runs without a throw. Barry's downed sprite is his R6.
            Sprite barryDowned = OnlySprite("Assets/Art/Wrestlers/Barry/barry_bodyslammed5_6.png");
            bool sawSlam = false;
            for (int f = 0; f < 120 && match.Cpu.State != CombatState.KnockedDown; f++)
            {
                yield return new WaitForEndOfFrame();
                Assert.AreEqual(barryX, barry.transform.position.x, 1e-5f, "the receiver's root never moves");
                Assert.AreEqual(0, peaux.ThrowDirection);
                Assert.AreEqual(0, barry.ThrowDirection);
                Assert.IsFalse(barry.PlayingSlamTail);
                if (peaux.Animator.GetCurrentAnimatorStateInfo(0).IsName(WrestlerView.BodyslamStateName))
                {
                    sawSlam = true;
                    string slammer = peaux.SpriteRenderer.sprite.name;
                    Assert.IsTrue(slammer.StartsWith("peaux_bodyslam5_") || slammer == "peaux_stand_front_0", "Peaux's slam clip: " + slammer);
                    StringAssert.StartsWith("barry_bodyslammed5_", barry.SpriteRenderer.sprite.name, "Barry's receiver clip");
                }
            }
            Assert.IsTrue(sawSlam);
            Assert.AreEqual(CombatState.KnockedDown, match.Cpu.State, "setup: knocked down");
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(barry.Animator.GetCurrentAnimatorStateInfo(0).IsName("KnockedDown"));
            Assert.AreEqual(barryDowned, barry.SpriteRenderer.sprite, "Barry's downed sprite (R6)");
            Assert.AreEqual(0, barry.DownedThrowDirection);
#else
            Assert.Ignore("Needs the Editor AssetDatabase.");
            yield break;
#endif
        }

        // T2.6: Peaux's downed sprite is R6 in his Knocked Down / Knocked Out clips and at the end of his fall; since
        // 2026-11-17 Barry's is his own R6 (barry_bodyslammed5_6) the same way. (The falls themselves are checked in
        // CombatPresentationTests.)
        [Test]
        public void DownedSprites_PeauxR6_BarryR6()
        {
#if UNITY_EDITOR
            Sprite r6 = OnlySprite("Assets/Art/Wrestlers/Peaux/peaux_bodyslammed5_6.png");
            Sprite barryDowned = OnlySprite("Assets/Art/Wrestlers/Barry/barry_bodyslammed5_6.png");
            Assert.AreEqual(barryDowned, LastKey("Assets/Animation/Barry/Barry_Bodyslammed.anim"));
            Assert.AreEqual(r6, LastKey("Assets/Animation/Peaux/Peaux_KnockedDown_PLACEHOLDER.anim"));
            Assert.AreEqual(r6, LastKey("Assets/Animation/Peaux/Peaux_Fall.anim"));
            Assert.AreEqual(r6, LastKey("Assets/Animation/Peaux/Peaux_Bodyslammed.anim"));
            Assert.AreEqual(barryDowned, LastKey("Assets/Animation/Barry/Barry_KnockedDown_PLACEHOLDER.anim"));
            Assert.AreEqual(barryDowned, LastKey("Assets/Animation/Barry/Barry_Fall.anim"));
            var overrides = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Animation/Peaux/Peaux.overrideController");
            Assert.AreEqual("Peaux_KnockedDown_PLACEHOLDER", overrides["Slot_KnockedDown"].name);
            Assert.AreEqual("Peaux_KnockedDown_PLACEHOLDER", overrides["Slot_KnockedOut"].name);
#else
            Assert.Ignore("Needs the Editor AssetDatabase.");
#endif
        }

        // ------------------------------------------------------------------ helpers

        private void Setup(out CombatMatch match, out WrestlerView player, out WrestlerView cpu, out TickDriver driver,
            out WrestlerDefinition playerDef, Vector2 playerAt, Vector2 cpuAt, string playerName, string cpuName,
            System.Action<CombatTuning, WrestlerDefinition> tweakPlayer = null)
        {
            match = null; player = cpu = null; driver = null; playerDef = null;
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
            Assert.IsNotNull(asset, TuningPath);
            CombatTuning tuning = Object.Instantiate(asset); // a copy: the project asset is never changed
            spawned.Add(tuning);
            tuning.minKnockdownTime = 0.6f;
            tuning.maxKnockdownTime = 0.6f;
            playerDef = UnityEditor.AssetDatabase.LoadAssetAtPath<WrestlerDefinition>($"Assets/Data/Wrestlers/{playerName}.asset");
            var cpuDef = UnityEditor.AssetDatabase.LoadAssetAtPath<WrestlerDefinition>($"Assets/Data/Wrestlers/{cpuName}.asset");
            Assert.IsNotNull(playerDef);
            Assert.IsNotNull(cpuDef);
            tweakPlayer?.Invoke(tuning, playerDef);
            match = new CombatMatch(tuning, null, playerAt, cpuAt, () => 0.5f, playerDef, cpuDef);
            player = Spawn(playerDef, playerAt).GetComponent<WrestlerView>();
            cpu = Spawn(cpuDef, cpuAt).GetComponent<WrestlerView>();
            player.Bind(match.Player);
            player.BindEvents(match.Events, WrestlerId.Player);
            cpu.Bind(match.Cpu);
            cpu.BindEvents(match.Events, WrestlerId.Cpu);
            var go = new GameObject("ThrowTestDriver");
            spawned.Add(go);
            driver = go.AddComponent<TickDriver>();
            driver.Init(match, player.transform, cpu.transform);
#endif
        }

        private GameObject Spawn(WrestlerDefinition def, Vector2 at)
        {
            GameObject go = Object.Instantiate(def.Prefab, at, Quaternion.identity);
            spawned.Add(go);
            return go;
        }

        /// <summary>Turns off the slammer's throw entry in the test's tuning copy (never the project asset).</summary>
        private static void DisableThrow(CombatTuning tuning, WrestlerDefinition slammer)
        {
            for (int i = 0; i < tuning.bodyslamThrowBySlammer.Length; i++)
                if (tuning.bodyslamThrowBySlammer[i].slammer == slammer)
                {
                    tuning.bodyslamThrowBySlammer[i].throwEnabled = false;
                    return;
                }
            Assert.Fail($"setup: no bodyslamThrowBySlammer entry for {slammer.name}");
        }

        /// <summary>Turns on the slammer's throw entry in the test's tuning copy (never the project asset), with the
        /// Peaux-slams-Barry art's suggested receiver roots: R4 159 px, landing 342 px (1.59 x 215).</summary>
        private static void EnableThrow(CombatTuning tuning, WrestlerDefinition slammer)
        {
            for (int i = 0; i < tuning.bodyslamThrowBySlammer.Length; i++)
                if (tuning.bodyslamThrowBySlammer[i].slammer == slammer)
                {
                    tuning.bodyslamThrowBySlammer[i].throwEnabled = true;
                    tuning.bodyslamThrowBySlammer[i].releaseTime = 0.42f;
                    tuning.bodyslamThrowBySlammer[i].releaseOffsetPx = 159f;
                    tuning.bodyslamThrowBySlammer[i].landingDistancePx = 342f;
                    tuning.bodyslamThrowBySlammer[i].distanceVariance = 0.10f;
                    return;
                }
            Assert.Fail($"setup: no bodyslamThrowBySlammer entry for {slammer.name}");
        }

        private static Sample Take(int frame, CombatMatch match, WrestlerView barry, WrestlerView peaux,
            string sl = "barry", string rc = "peaux")
        {
            AnimatorStateInfo b = barry.Animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo p = peaux.Animator.GetCurrentAnimatorStateInfo(0);
            Sprite bs = barry.SpriteRenderer.sprite, ps = peaux.SpriteRenderer.sprite;
            return new Sample
            {
                Frame = frame,
                BarryState = match.Player.State,
                PeauxState = match.Cpu.State,
                SlamIndex = bs == null ? -1 : bs.name.StartsWith(sl + "_bodyslam5_") ? Number(bs.name, sl + "_bodyslam5_") - 1
                    : bs.name.StartsWith(sl + "_stand_front") ? 5 : -1,
                SlammedIndex = ps == null || !ps.name.StartsWith(rc + "_bodyslammed5_") ? -1 : Number(ps.name, rc + "_bodyslammed5_") - 1,
                BarryFlip = barry.SpriteRenderer.flipX,
                PeauxFlip = peaux.SpriteRenderer.flipX,
                BarryLocalX = barry.SpriteRenderer.transform.localPosition.x,
                PeauxLocalX = peaux.SpriteRenderer.transform.localPosition.x,
                PeauxSpriteX = peaux.SpriteRenderer.transform.position.x,
                PeauxRootX = peaux.transform.position.x,
                BarryOrder = barry.SpriteRenderer.sortingOrder,
                PeauxOrder = peaux.SpriteRenderer.sortingOrder,
                BarryInBodyslam = b.IsName(WrestlerView.BodyslamStateName),
                PeauxInBodyslammed = p.IsName(WrestlerView.BodyslammedStateName),
                PeauxInKnockedDown = p.IsName("KnockedDown") || p.IsName("KnockedOut"),
                PeauxInCrouch = p.IsName(WrestlerView.CrouchStateName),
                PeauxSprite = ps != null ? ps.name : "-",
            };
        }

        /// <summary>"barry_bodyslam5_3_0" with prefix "barry_bodyslam5_" -> 3.</summary>
        private static int Number(string name, string prefix)
        {
            string rest = name.Substring(prefix.Length);
            int u = rest.IndexOf('_');
            return int.Parse(u < 0 ? rest : rest.Substring(0, u));
        }

        private static string Trace(List<Sample> samples)
        {
            var sb = new StringBuilder();
            foreach (Sample s in samples)
                sb.Append($"{s.Frame}: Barry {s.BarryState} S{s.SlamIndex + 1}{(s.BarryFlip ? "F" : "")} | Peaux {s.PeauxState} " +
                          $"{s.PeauxSprite}{(s.PeauxFlip ? "F" : "")} root {s.PeauxRootX:0.000} drawn {s.PeauxSpriteX:0.000} " +
                          $"{(s.PeauxInBodyslammed ? "Bodyslammed" : s.PeauxInKnockedDown ? "Downed" : s.PeauxInCrouch ? "Crouch" : "other")}\n");
            return sb.ToString();
        }

        private static Sprite OnlySprite(string texturePath)
        {
#if UNITY_EDITOR
            foreach (Object o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(texturePath))
                if (o is Sprite s) return s;
#endif
            return null;
        }

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

        /// <summary>The sprite of a clip's last sprite key.</summary>
        private static Object LastKey(string clipPath)
        {
#if UNITY_EDITOR
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.IsNotNull(clip, clipPath);
            foreach (var binding in UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys.Length > 0) return keys[keys.Length - 1].value;
            }
#endif
            return null;
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
