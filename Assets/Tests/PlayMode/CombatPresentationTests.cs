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

        [TearDown]
        public void TearDown()
        {
            if (playerGo != null) Object.Destroy(playerGo);
            if (cpuGo != null) Object.Destroy(cpuGo);
            if (tuning != null) Object.Destroy(tuning);
        }

        private void Build(Vector2 playerStart, Vector2 cpuStart, System.Action<CombatTuning> configure = null)
        {
#if UNITY_EDITOR
            tuning = ScriptableObject.CreateInstance<CombatTuning>();
            configure?.Invoke(tuning);
            match = new CombatMatch(tuning, null, playerStart, cpuStart);
            playerInput = new QueuedInput();
            cpuInput = new QueuedInput();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(BarryPrefab);
            Assert.IsNotNull(prefab, BarryPrefab + " not found");
            playerGo = Object.Instantiate(prefab, playerStart, Quaternion.identity);
            cpuGo = Object.Instantiate(prefab, cpuStart, Quaternion.identity);
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
        [UnityTest]
        public IEnumerator Struck_ClipRestartsOnEveryHit()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t => t.struckDuration = 2f);
            yield return null;
            playerInput.PressStrikeAt(0, 5); // Strike 1, buffered Strike 2
            TickUntil(() => match.Cpu.State == CombatState.Struck);
            yield return Settle();
            yield return new WaitForSeconds(0.3f);
            AnimatorStateInfo before = StateOf(cpuView);
            Assert.IsTrue(before.IsName("Struck"), "Animator should be in Struck");
            Assert.Greater(before.normalizedTime, 0.4f, "setup: the Struck clip has advanced");

            int struckEntries = 0;
            match.Events.StateChanged += (id, from, to) => { if (id == WrestlerId.Cpu && to == CombatState.Struck) struckEntries++; };
            var hits = new List<HitInfo>();
            match.Events.HitLanded += hits.Add;
            TickUntil(() => hits.Count > 0);
            Assert.AreEqual(MoveId.Strike2, hits[0].Move);
            Assert.AreEqual(CombatState.Struck, match.Cpu.State);
            Assert.AreEqual(0, struckEntries, "setup: a Struck restart raises no StateChanged");

            yield return Settle();
            AnimatorStateInfo after = StateOf(cpuView);
            Assert.IsTrue(after.IsName("Struck"));
            Assert.Less(after.normalizedTime, before.normalizedTime - 0.25f, "the Struck clip should restart on the second hit");
        }

        // D-023 / ARCHITECTURE 4.2: Knocked Out reuses the knockdown fall. Knocked out while already lying down (a
        // stomp, D-025) holds the lying pose instead of replaying the fall.
        [UnityTest]
        public IEnumerator KnockoutWhileDown_HoldsLyingPose()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f; t.stompDamage = 50f;
            });
            yield return null;
            playerInput.PressStrikeAt(0);
            TickUntil(() => match.Cpu.State == CombatState.KnockedDown);
            TickUntil(() => match.Player.State == CombatState.Standing);
            yield return Settle();
            yield return new WaitForSeconds(0.8f); // knockdown fall finishes
            Assert.IsTrue(StateOf(cpuView).IsName("KnockedDown"));
            float lyingRot = SpriteRotZ(cpuView);
            Assert.Greater(Mathf.Abs(lyingRot), 45f, "setup: the knocked-down pose is lying (rotated)");

            playerInput.PressStrikeAt(0); // stomp
            TickUntil(() => match.IsOver);
            Assert.AreEqual(CombatState.KnockedOut, match.Cpu.State);
            yield return Settle();
            AnimatorStateInfo ko = StateOf(cpuView);
            Assert.IsTrue(ko.IsName("KnockedOut"), "Animator should be in KnockedOut");
            Assert.GreaterOrEqual(ko.normalizedTime, 0.99f, "KO while down should jump to the end of the fall");
            Assert.AreEqual(lyingRot, SpriteRotZ(cpuView), 1f, "the lying pose is held (no replayed fall)");
        }

        // Contrast: knocked out from standing plays the fall from its start.
        [UnityTest]
        public IEnumerator KnockoutFromStanding_PlaysTheFall()
        {
            Build(new Vector2(10f, -12f), new Vector2(12f, -12f), t => t.maxHealth = 3f);
            yield return null;
            playerInput.PressStrikeAt(0);
            TickUntil(() => match.IsOver);
            Assert.AreEqual(CombatState.KnockedOut, match.Cpu.State);
            yield return Settle();
            AnimatorStateInfo ko = StateOf(cpuView);
            Assert.IsTrue(ko.IsName("KnockedOut"), "Animator should be in KnockedOut");
            Assert.Less(ko.normalizedTime, 0.9f, "a KO from standing plays the fall");
        }

        // WrestlerView: clips are authored for left-facing art; a right-facing (flipped) wrestler mirrors the clip's
        // x offset and z rotation so the fall still goes away from where it faces. A run collision (D-029) knocks
        // both down on the same tick, so the two lie in the same clip pose, facing opposite ways.
        [UnityTest]
        public IEnumerator FlippedWrestler_MirrorsClipOffsetAndRotation()
        {
            Build(new Vector2(5f, -12f), new Vector2(12f, -12f), t => { t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f; });
            yield return null;
            playerInput.PressRunAt(0);
            TickUntil(() => match.Player.State == CombatState.KnockedDown && match.Cpu.State == CombatState.KnockedDown);
            yield return Settle();
            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(1, match.Player.HorizontalFacing, "setup: player faces right");
            Assert.AreEqual(-1, match.Cpu.HorizontalFacing, "setup: CPU faces left");
            Assert.IsTrue(playerView.SpriteRenderer.flipX, "right-facing Barry is flipped");
            Assert.IsFalse(cpuView.SpriteRenderer.flipX);

            Vector3 left = cpuView.SpriteRenderer.transform.localPosition;
            Vector3 right = playerView.SpriteRenderer.transform.localPosition;
            float leftRot = SpriteRotZ(cpuView), rightRot = SpriteRotZ(playerView);
            Assert.Greater(Mathf.Abs(left.x), 0.1f, "setup: the knockdown clip offsets the sprite in x");
            Assert.Greater(Mathf.Abs(leftRot), 10f, "setup: the knockdown clip rotates the sprite");
            Assert.AreEqual(-left.x, right.x, 1e-3f, "x offset mirrored");
            Assert.AreEqual(left.y, right.y, 1e-3f, "y offset unchanged");
            Assert.AreEqual(-leftRot, rightRot, 0.5f, "z rotation mirrored");
            Assert.AreEqual(new Vector3(5f, -12f, 0f), playerGo.transform.position, "the root never moves");
        }

        /// <summary>Queued test input (EditMode's ScriptedInput lives in another assembly).</summary>
        private sealed class QueuedInput : IWrestlerInput
        {
            private readonly Queue<WrestlerIntent> queue = new Queue<WrestlerIntent>();

            public void PressStrikeAt(params int[] ticks) => Queue(ticks, strike: true);
            public void PressRunAt(params int[] ticks) => Queue(ticks, strike: false);

            private void Queue(int[] ticks, bool strike)
            {
                int last = 0;
                foreach (int t in ticks) last = Mathf.Max(last, t);
                var set = new HashSet<int>(ticks);
                for (int i = 0; i <= last; i++)
                {
                    ButtonState press = set.Contains(i) ? new ButtonState(true, true) : ButtonState.None;
                    queue.Enqueue(new WrestlerIntent(Vector2.zero, ButtonState.None, strike ? ButtonState.None : press, strike ? press : ButtonState.None));
                }
            }

            public WrestlerIntent Read() => queue.Count > 0 ? queue.Dequeue() : WrestlerIntent.None;
        }
    }
}
