using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// D-049 / ARCHITECTURE 4.8: the walk and run clips raise one <see cref="FootContactEvents"/> contact per foot-contact
    /// frame, from the Walking / Running Animator states only, never while the Animator is paused, never twice for one
    /// contact. Real wrestler prefabs and clips; the combat view is a fake, and frames run at a fixed 1/60 s
    /// (Time.captureDeltaTime) so counts are exact.
    /// </summary>
    public class FootContactPresentationTests
    {
        private const float Dt = 1f / 60f;
        private const float TimeTolerance = Dt + 2e-3f;

        /// <summary>Expected foot contacts per clip (D-049, read from the art): event time in seconds and foot.</summary>
        private static readonly Dictionary<string, (float time, Foot foot)[]> Expected = new Dictionary<string, (float, Foot)[]>
        {
            { "Barry_WalkFront", new[] { (0f, Foot.Left), (0.625f, Foot.Right) } },
            { "Barry_WalkBack", new[] { (0.125f, Foot.Left), (0.875f, Foot.Right) } },
            { "Barry_Run", new[] { (4f / 24f, Foot.Left), (10f / 24f, Foot.Right) } },
            { "Peaux_WalkFront", new[] { (0.125f, Foot.Left), (0.375f, Foot.Right) } },
            { "Peaux_WalkBack", new[] { (0f, Foot.Right), (0.5f, Foot.Left) } },
            { "Peaux_Run", new[] { (2f / 24f, Foot.Left), (8f / 24f, Foot.Right) } },
        };

        private GameObject wrestler;
        private WrestlerView view;
        private FakeView fake;
        private readonly List<(FootContact contact, float animTime)> contacts = new List<(FootContact, float)>();
        private float captureBefore;

        [SetUp]
        public void SetUp()
        {
            captureBefore = Time.captureDeltaTime;
            Time.captureDeltaTime = Dt;
            contacts.Clear();
            FootContactEvents.Contact += OnContact;
        }

        [TearDown]
        public void TearDown()
        {
            FootContactEvents.Contact -= OnContact;
            Time.captureDeltaTime = captureBefore;
            if (wrestler != null) Object.Destroy(wrestler);
        }

        private void OnContact(FootContact c)
        {
            contacts.Add((c, view != null && view.Animator != null ? StateTime() : -1f));
        }

        private void Spawn(string name, CombatState state, Vector2 facing, WrestlerId id = WrestlerId.Cpu)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Wrestlers/{name}.prefab");
            Assert.IsNotNull(prefab, $"{name}.prefab not found");
            wrestler = Object.Instantiate(prefab, new Vector3(12.5f, -15.25f, 0f), Quaternion.identity);
            view = wrestler.GetComponent<WrestlerView>();
            Assert.IsNotNull(view.Animator.GetComponent<WrestlerAnimationEventRelay>(), "the Animator's GameObject carries the event relay");
            fake = new FakeView { State = state, Facing = facing, HorizontalFacing = facing.x > 0f ? 1 : -1, Position = new Vector2(12.5f, -15.25f) };
            view.Bind(fake);
            view.BindEvents(new CombatEvents(), id);
#else
            Assert.Ignore("Needs the Editor AssetDatabase.");
#endif
        }

        private string ClipName(string slot) => view.ClipForSlot(slot).name;

        private static string ExpectedClip(string wrestlerName, string suffix) => $"{wrestlerName}_{suffix}";

        /// <summary>Plays until the state <paramref name="stateName"/> has run <paramref name="seconds"/> of clip time.</summary>
        private IEnumerator PlayUntil(string stateName, float seconds)
        {
            int hash = Animator.StringToHash(stateName);
            for (int i = 0; i < 2000; i++)
            {
                yield return null;
                AnimatorStateInfo s = view.Animator.GetCurrentAnimatorStateInfo(0);
                if (s.shortNameHash == hash && StateTime() >= seconds)
                    yield break;
            }
            Assert.Fail($"{stateName} never reached {seconds} s of clip time");
        }

        /// <summary>Clip seconds played in the current state (normalized time x the clip's own length; AnimatorStateInfo.length
        /// is divided by Animator.speed, so it is wrong across a speed change).</summary>
        private float StateTime()
        {
            AnimatorStateInfo s = view.Animator.GetCurrentAnimatorStateInfo(0);
            float length = 0f, weight = -1f;
            foreach (AnimatorClipInfo ci in view.Animator.GetCurrentAnimatorClipInfo(0))
                if (ci.weight > weight) { weight = ci.weight; length = ci.clip.length; }
            return s.normalizedTime * length;
        }

        /// <summary>The contacts a clip owes from its start to <paramref name="elapsed"/>: absolute time and foot, in order.</summary>
        private static List<(float time, Foot foot)> Owed((float time, Foot foot)[] perLoop, float length, float elapsed)
        {
            var owed = new List<(float, Foot)>();
            for (int loop = 0; loop * length <= elapsed; loop++)
                foreach (var e in perLoop)
                    if (loop * length + e.time <= elapsed)
                        owed.Add((loop * length + e.time, e.foot));
            return owed;
        }

        private void AssertContactsMatch(string clipName, Gait gait, float length, float elapsed, string what)
        {
            List<(float time, Foot foot)> owed = Owed(Expected[clipName], length, elapsed);
            // An event within one frame of the end may or may not have fired yet.
            int certain = owed.Count(o => o.time <= elapsed - TimeTolerance);
            Assert.That(contacts.Count, Is.InRange(certain, owed.Count), $"{what}: contacts raised ({clipName}, {elapsed:0.###} s of clip time)");
            for (int i = 0; i < contacts.Count; i++)
            {
                (FootContact c, float animTime) = contacts[i];
                Assert.AreEqual(gait, c.Gait, $"{what}: contact {i} gait");
                Assert.AreEqual(owed[i].foot, c.Foot, $"{what}: contact {i} foot");
                Assert.AreEqual(WrestlerId.Cpu, c.Wrestler, $"{what}: contact {i} wrestler id (from BindEvents)");
                Assert.AreEqual((Vector2)wrestler.transform.position, c.Position, $"{what}: contact {i} position = root feet point");
                Assert.AreEqual(owed[i].time, animTime, TimeTolerance, $"{what}: contact {i} lands on the contact frame's time");
            }
        }

        private void AssertClipEvents(string clipName, string slot)
        {
            AnimationClip clip = view.ClipForSlot(slot);
            Assert.AreEqual(clipName, clip.name, $"{slot} plays {clipName}");
            var expected = Expected[clipName];
            AnimationEvent[] evs = clip.events;
            Assert.AreEqual(expected.Length, evs.Length, $"{clipName}: foot-contact events in the clip");
            for (int i = 0; i < evs.Length; i++)
            {
                Assert.AreEqual(WrestlerAnimationEventRelay.FootContactFunction, evs[i].functionName, $"{clipName} event {i}");
                Assert.AreEqual(expected[i].time, evs[i].time, 1e-4f, $"{clipName} event {i} time");
                Assert.AreEqual((int)expected[i].foot, evs[i].intParameter, $"{clipName} event {i} foot");
            }
        }

        // Walking N seconds, front and back: the expected contacts at the contact frames' clip times, Gait = Walk.
        [UnityTest]
        public IEnumerator Walking_RaisesWalkContacts_AtContactFrames(
            [Values("Barry", "Peaux")] string name, [Values(false, true)] bool back)
        {
            Spawn(name, CombatState.Walking, back ? new Vector2(-1f, 1f).normalized : new Vector2(-1f, 0f));
            string clipName = ExpectedClip(name, back ? "WalkBack" : "WalkFront");
            Assert.AreEqual(clipName, ClipForState(back), "the walk clip playing");
            AssertClipEvents(clipName, back ? "Slot_WalkBack" : "Slot_WalkFront");
            yield return PlayUntil(WrestlerView.WalkingStateName, 2.3f);
            float elapsed = StateTime();
            float length = view.ClipForSlot(back ? "Slot_WalkBack" : "Slot_WalkFront").length;
            AssertContactsMatch(clipName, Gait.Walk, length, elapsed, $"{name} walking {(back ? "back" : "front")}");
            Assert.GreaterOrEqual(contacts.Count, 4, "two loops of steps");
            Assert.AreEqual(contacts.Count, view.FootContactsRaised);
        }

        private string ClipForState(bool back) => ClipName(back ? "Slot_WalkBack" : "Slot_WalkFront");

        // Running: Run contacts at the run clip's contact frames.
        [UnityTest]
        public IEnumerator Running_RaisesRunContacts([Values("Barry", "Peaux")] string name)
        {
            Spawn(name, CombatState.Running, new Vector2(-1f, 0f));
            string clipName = ExpectedClip(name, "Run");
            Assert.AreEqual(clipName, ClipName("Slot_Run"));
            AssertClipEvents(clipName, "Slot_Run");
            yield return PlayUntil(WrestlerView.RunningStateName, 1.7f);
            AssertContactsMatch(clipName, Gait.Run, view.ClipForSlot("Slot_Run").length, StateTime(), $"{name} running");
            Assert.GreaterOrEqual(contacts.Count, 5);
            Assert.IsTrue(contacts.All(c => c.contact.Gait == Gait.Run));
        }

        // Hitstop (Animator.speed 0, as CombatMatchRunner does) right after a contact: nothing while paused, no repeat of
        // that contact when playback resumes, and the count afterwards still matches the clip time played.
        [UnityTest]
        public IEnumerator Hitstop_NoContactsWhilePaused_NoDuplicateOnResume([Values("Barry", "Peaux")] string name)
        {
            Spawn(name, CombatState.Running, new Vector2(-1f, 0f));
            for (int i = 0; i < 600 && contacts.Count < 2; i++)
                yield return null;
            Assert.AreEqual(2, contacts.Count, "two run contacts before the hitstop");
            view.Animator.speed = 0f; // the frame of the second contact: pause at the event time
            for (int i = 0; i < 20; i++)
                yield return null;
            Assert.AreEqual(2, contacts.Count, "no contacts during hitstop");
            view.Animator.speed = 1f;
            yield return null;
            yield return null;
            Assert.AreEqual(2, contacts.Count, "the paused contact is not raised again when playback resumes");
            yield return PlayUntil(WrestlerView.RunningStateName, 1.7f);
            AssertContactsMatch(ExpectedClip(name, "Run"), Gait.Run, view.ClipForSlot("Slot_Run").length, StateTime(), $"{name} after hitstop");

            // Slow motion (Animator.speed 0.25): steps keep coming at the slowed rate, once each.
            int before = contacts.Count;
            float t0 = StateTime();
            view.Animator.speed = 0.25f;
            for (int i = 0; i < 180; i++) // 3 s real = 0.75 s of clip time
                yield return null;
            view.Animator.speed = 1f;
            float played = StateTime() - t0;
            var owed = Owed(Expected[ExpectedClip(name, "Run")], view.ClipForSlot("Slot_Run").length, StateTime())
                .Count(o => o.time > t0 + 1e-4f && o.time <= t0 + played);
            Assert.That(contacts.Count - before, Is.InRange(owed - 1, owed), "slow motion: one contact per contact frame");
        }

        // Standing, striking and the Recovering walk raise nothing.
        [UnityTest]
        public IEnumerator NoContacts_WhileStandingStrikingOrRecoveringWalk([Values("Barry", "Peaux")] string name)
        {
            Spawn(name, CombatState.Standing, new Vector2(-1f, 0f));
            for (int i = 0; i < 60; i++)
                yield return null;
            Assert.AreEqual(0, contacts.Count, "standing");

            fake.State = CombatState.Striking;
            fake.CurrentMove = MoveId.Strike1;
            for (int i = 0; i < 40; i++)
                yield return null;
            Assert.AreEqual(Animator.StringToHash("Strike1"), view.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash, "striking plays Strike1");
            Assert.AreEqual(0, contacts.Count, "striking");

            fake.State = CombatState.Recovering;
            fake.CurrentMove = MoveId.None;
            for (int i = 0; i < 150; i++)
            {
                fake.Position += new Vector2(0.01f, 0f); // moving: the Recovering walk pose
                yield return null;
            }
            Assert.AreEqual(Animator.StringToHash("RecoveringWalk"), view.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                "recovering while moving plays RecoveringWalk");
            Assert.AreEqual(0, contacts.Count, "recovering walk");

            // Back to Walking: steps resume (the filter is the state, not a latch).
            fake.State = CombatState.Walking;
            yield return PlayUntil(WrestlerView.WalkingStateName, 1.1f);
            Assert.GreaterOrEqual(contacts.Count, 2, "walking again raises contacts");
            Assert.IsTrue(contacts.All(c => c.contact.Gait == Gait.Walk));
        }

        [Test]
        public void GaitFromState_And_EventLoop()
        {
            Assert.IsTrue(WrestlerView.TryGetGait(10, 10, 20, out Gait g)); Assert.AreEqual(Gait.Walk, g);
            Assert.IsTrue(WrestlerView.TryGetGait(20, 10, 20, out g)); Assert.AreEqual(Gait.Run, g);
            Assert.IsFalse(WrestlerView.TryGetGait(30, 10, 20, out _), "any other state raises nothing");
            // Event at 0.875 of a 1 s loop, read after the frame stepped across the wrap: still loop 0.
            Assert.AreEqual(0, WrestlerView.EventLoop(1.01f, 0.875f, 1f));
            Assert.AreEqual(1, WrestlerView.EventLoop(1.88f, 0.875f, 1f));
            Assert.AreEqual(2, WrestlerView.EventLoop(2.0f, 0f, 1f));
        }

        private sealed class FakeView : IWrestlerView
        {
            public WrestlerId Id { get; set; } = WrestlerId.Cpu;
            public CombatState State { get; set; }
            public MoveId CurrentMove { get; set; }
            public MovePhase Phase => MovePhase.None;
            public float Health => 100f;
            public float MaxHealth => 100f;
            public Vector2 Position { get; set; }
            public Vector2 Facing { get; set; }
            public int HorizontalFacing { get; set; }
            public int FallSide => 0;
            public bool IsGettingUp => false;
            public float GetUpTimeRemaining => 0f;
            public float GetUpDuration => 0.25f;
        }
    }
}
