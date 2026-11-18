using System.Collections.Generic;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Juice;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>D-046 theatrics decisions (Juice pure classes): impact classification, feel defaults, shake, flash, particles, stains.</summary>
    public sealed class TheatricsRulesTests
    {
        private static readonly Vector2 PlayerPos = new Vector2(10f, -11.5f);
        private static readonly Vector2 CpuPos = new Vector2(11.5f, -11.5f);

        private static List<Impact> Resolve(TickEvents e)
        {
            var list = new List<Impact>();
            ImpactClassifier.Resolve(e, PlayerPos, CpuPos, list);
            return list;
        }

        // ------------------------------------------------------------ classification

        [TestCase(MoveId.Strike1, ImpactKind.LightStrike)]
        [TestCase(MoveId.Strike2, ImpactKind.LightStrike)]
        [TestCase(MoveId.Strike3, ImpactKind.HeavyStrike)]
        [TestCase(MoveId.Stomp, ImpactKind.Stomp)]
        [TestCase(MoveId.Bodyslam, ImpactKind.Slam)]
        public void KindFor_MapsMoves(MoveId move, ImpactKind expected) =>
            Assert.AreEqual(expected, ImpactClassifier.KindFor(move));

        [Test]
        public void Hit_GivesDefenderOneImpact_FromAttacker()
        {
            var e = new TickEvents();
            e.Hits.Add(new HitInfo(WrestlerId.Player, WrestlerId.Cpu, MoveId.Strike1, 5f, new Vector2(11f, -11.5f)));
            e.Damaged[1] = true;
            List<Impact> r = Resolve(e);
            Assert.AreEqual(1, r.Count, "the damage of a hit is the hit's impact, not a second one");
            Assert.AreEqual(WrestlerId.Cpu, r[0].Receiver);
            Assert.AreEqual(ImpactKind.LightStrike, r[0].Kind);
            Assert.AreEqual(PlayerPos.x, r[0].SourceX);
            Assert.IsFalse(r[0].Knockout);
        }

        [Test]
        public void Strike3WithKnockdown_IsOneHeavyImpact()
        {
            var e = new TickEvents();
            e.Hits.Add(new HitInfo(WrestlerId.Cpu, WrestlerId.Player, MoveId.Strike3, 10f, PlayerPos));
            e.KnockedDown[0] = true;
            List<Impact> r = Resolve(e);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(ImpactKind.HeavyStrike, r[0].Kind);
            Assert.AreEqual(CpuPos.x, r[0].SourceX);
        }

        [Test]
        public void ObstacleHit_IsCrash_AwayFromContact()
        {
            var e = new TickEvents();
            var contact = new Vector2(6.3f, -12f);
            e.Obstacles.Add(new KeyValuePair<WrestlerId, Vector2>(WrestlerId.Player, contact));
            e.Damaged[0] = true;
            e.KnockedDown[0] = true;
            List<Impact> r = Resolve(e);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(ImpactKind.Crash, r[0].Kind);
            Assert.AreEqual(contact.x, r[0].SourceX);
        }

        [Test]
        public void BothDownWithARunner_IsRunCollision_ForBoth()
        {
            var e = new TickEvents();
            e.KnockedDown[0] = e.KnockedDown[1] = true;
            e.DownFromRunning[0] = true;
            List<Impact> r = Resolve(e);
            Assert.AreEqual(2, r.Count);
            Assert.IsTrue(r.TrueForAll(i => i.Kind == ImpactKind.RunCollision));
            Impact p = r.Find(i => i.Receiver == WrestlerId.Player);
            Assert.AreEqual(CpuPos.x, p.SourceX, "each one sprays away from the other");
        }

        [Test]
        public void KnockdownAlone_IsKnockdown_SoItStillFlashes()
        {
            var e = new TickEvents();
            e.KnockedDown[1] = true;
            List<Impact> r = Resolve(e);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(ImpactKind.Knockdown, r[0].Kind);
            Assert.AreEqual(WrestlerId.Cpu, r[0].Receiver);
        }

        [Test]
        public void DamageAlone_IsDamage()
        {
            var e = new TickEvents();
            e.Damaged[0] = true;
            List<Impact> r = Resolve(e);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(ImpactKind.Damage, r[0].Kind);
        }

        [Test]
        public void Empty_GivesNothing()
        {
            var e = new TickEvents();
            Assert.IsTrue(e.IsEmpty);
            Assert.AreEqual(0, Resolve(e).Count);
        }

        [Test]
        public void Knockout_RidesOnTheBlowThatCausedIt()
        {
            var e = new TickEvents();
            e.Hits.Add(new HitInfo(WrestlerId.Player, WrestlerId.Cpu, MoveId.Strike2, 5f, CpuPos));
            e.KnockedOut[1] = true;
            e.HasEnded = true;
            e.Ended = new MatchResult(WrestlerId.Player, WrestlerId.Cpu, 30f);
            List<Impact> r = Resolve(e);
            Assert.AreEqual(1, r.Count);
            Assert.IsTrue(r[0].Knockout);
            Assert.AreEqual(ImpactKind.LightStrike, r[0].Kind);
        }

        [Test]
        public void Draw_KnocksOutBoth()
        {
            var e = new TickEvents();
            e.KnockedOut[0] = e.KnockedOut[1] = true;
            e.DownFromRunning[1] = true;
            e.HasEnded = true;
            e.Ended = MatchResult.Draw(12f);
            List<Impact> r = Resolve(e);
            Assert.AreEqual(2, r.Count);
            Assert.IsTrue(r.TrueForAll(i => i.Knockout && i.Kind == ImpactKind.RunCollision));
        }

        [Test]
        public void Clear_EmptiesTheTick()
        {
            var e = new TickEvents();
            e.Hits.Add(new HitInfo(WrestlerId.Player, WrestlerId.Cpu, MoveId.Strike1, 5f, CpuPos));
            e.KnockedOut[0] = e.Damaged[1] = e.HasEnded = true;
            e.Clear();
            Assert.IsTrue(e.IsEmpty);
        }

        [TestCase(5f, 3f, 1, 1)]
        [TestCase(3f, 5f, 1, -1)]
        [TestCase(4f, 4f, -1, -1)]
        [TestCase(4f, 4f, 0, 1)]
        public void AwayFrom(float receiverX, float sourceX, int fallback, int expected) =>
            Assert.AreEqual(expected, ImpactClassifier.AwayFrom(receiverX, sourceX, fallback));

        // ------------------------------------------------------------ feel defaults (D-046 Starting points)

        [Test]
        public void FeelDefaults_MatchD046Hitstops_AndScaleWithImpact()
        {
            var t = new ImpactFeelTable();
            Assert.AreEqual(0.05f, t.lightStrike.hitstopSeconds, 1e-4f);
            Assert.AreEqual(0.09f, t.heavyStrike.hitstopSeconds, 1e-4f);
            Assert.AreEqual(0.09f, t.stomp.hitstopSeconds, 1e-4f);
            Assert.AreEqual(0.10f, t.crash.hitstopSeconds, 1e-4f);
            Assert.AreEqual(0.10f, t.runCollision.hitstopSeconds, 1e-4f);
            Assert.AreEqual(0.12f, t.slam.hitstopSeconds, 1e-4f);

            Assert.Less(t.lightStrike.shakeAmplitude, t.heavyStrike.shakeAmplitude);
            Assert.Less(t.heavyStrike.shakeAmplitude, t.slam.shakeAmplitude);
            Assert.Less(t.slam.shakeAmplitude, t.knockout.shakeAmplitude);
            Assert.Less(t.lightStrike.bloodDrops, t.heavyStrike.bloodDrops);
            Assert.Less(t.heavyStrike.bloodDrops, t.slam.bloodDrops);
            Assert.Less(t.slam.bloodDrops, t.knockout.bloodDrops);
            Assert.Greater(t.slam.dustPuffs, 0);
            Assert.Greater(t.crash.dustPuffs, 0);
        }

        [Test]
        public void KnockoutHitstop_FitsBeforeVictory()
        {
            // The hook runs hitstop and slow motion at the same time; Victory loads 1.5 s real time after the KO.
            var t = new ImpactFeelTable();
            Assert.LessOrEqual(t.knockout.hitstopSeconds, 1.4f);
            Assert.Less(t.knockout.hitstopSeconds, 1.0f, "the KO hitstop is shorter than the default 1.0 s slow motion");
        }

        [Test]
        public void FeelTable_GetReturnsEachKind()
        {
            var t = new ImpactFeelTable();
            Assert.AreSame(t.slam, t.Get(ImpactKind.Slam));
            Assert.AreSame(t.crash, t.Get(ImpactKind.Crash));
            Assert.AreSame(t.damage, t.Get(ImpactKind.Damage));
            Assert.AreSame(t.knockdown, t.Get(ImpactKind.Knockdown));
        }

        // ------------------------------------------------------------ shake

        [Test]
        public void Shake_DecaysToZero()
        {
            var s = new ScreenShake();
            s.Kick(0.2f, 0.5f);
            Assert.AreEqual(0.2f, s.Strength, 1e-5f);
            s.Advance(0.25f);
            Assert.AreEqual(0.05f, s.Strength, 1e-5f);
            s.Advance(0.3f);
            Assert.AreEqual(0f, s.Strength);
            Assert.IsFalse(s.IsShaking);
        }

        [Test]
        public void Shake_WeakKickDoesNotCutAStrongOne()
        {
            var s = new ScreenShake();
            s.Kick(0.3f, 1f);
            s.Kick(0.05f, 0.1f);
            Assert.AreEqual(0.3f, s.Strength, 1e-5f);
            s.Advance(0.5f);
            s.Kick(0.1f, 0.2f); // 0.075 left, so the stronger new kick replaces it
            Assert.AreEqual(0.1f, s.Strength, 1e-5f);
        }

        [Test]
        public void Shake_SnapsToArtPixels()
        {
            Vector2 v = ScreenShake.SnapToPixels(new Vector2(0.026f, -0.04f), 60f);
            Assert.AreEqual(2f / 60f, v.x, 1e-5f);
            Assert.AreEqual(-2f / 60f, v.y, 1e-5f);
            Assert.LessOrEqual(ScreenShake.Offset(0.1f, 0.3f, 1f).magnitude, 0.1f + 1e-5f);
        }

        // ------------------------------------------------------------ flash

        [Test]
        public void Flash_HoldsThenFades()
        {
            var f = new FlashEnvelope();
            Assert.AreEqual(0f, f.Alpha);
            f.Trigger(0.8f, 0.1f, 0.2f);
            Assert.AreEqual(0.8f, f.Alpha, 1e-5f);
            f.Advance(0.1f);
            Assert.AreEqual(0.8f, f.Alpha, 1e-5f);
            f.Advance(0.1f);
            Assert.AreEqual(0.4f, f.Alpha, 1e-4f);
            f.Advance(0.2f);
            Assert.AreEqual(0f, f.Alpha);
        }

        [Test]
        public void Flash_RetriggerKeepsTheLongerHold()
        {
            var f = new FlashEnvelope();
            f.Trigger(0.8f, 0.5f, 0.5f);   // knockout flash
            f.Advance(0.1f);
            f.Trigger(0.8f, 0.05f, 0.1f);  // a light hit on the same tick or right after
            f.Advance(0.3f);
            Assert.AreEqual(0.8f, f.Alpha, 1e-5f, "still inside the knockout's hold");
        }

        // ------------------------------------------------------------ particles and blood

        [Test]
        public void Drop_ArcsAndLandsOnItsGroundLine()
        {
            var list = new FxParticleList(8);
            list.Add(new FxParticle { Position = new Vector2(0f, 2f), Velocity = new Vector2(3f, 2f), Gravity = 20f, Lands = true, GroundY = -0.3f, Lifetime = 5f });
            var landed = new List<FxParticle>();
            float maxY = 2f;
            for (int i = 0; i < 300 && landed.Count == 0; i++)
            {
                list.Step(1f / 60f, landed);
                if (list.Count > 0) maxY = Mathf.Max(maxY, list[0].Position.y);
            }
            Assert.AreEqual(1, landed.Count);
            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(-0.3f, landed[0].Position.y, 1e-5f, "lands exactly on its ground line, never falls past it");
            Assert.Greater(maxY, 2f, "rose before falling (an arc)");
            Assert.Greater(landed[0].Position.x, 0f);
        }

        [Test]
        public void Particle_ExpiresAndFades()
        {
            var list = new FxParticleList(4);
            list.Add(new FxParticle { Lifetime = 1f, StartSize = 1f, EndSize = 3f, FadeFrom = 0.5f });
            list.Step(0.75f, null);
            Assert.AreEqual(2.5f, list[0].Size, 1e-4f);
            Assert.AreEqual(0.5f, list[0].Alpha01, 1e-4f);
            list.Step(0.3f, null);
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void ParticleList_ReplacesOldestWhenFull()
        {
            var list = new FxParticleList(2);
            for (int i = 0; i < 3; i++)
                list.Add(new FxParticle { Position = new Vector2(i, 0f), Lifetime = 1f });
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(1f, list[0].Position.x);
        }

        [Test]
        public void ZeroDt_FreezesParticles()
        {
            var list = new FxParticleList(2);
            list.Add(new FxParticle { Position = Vector2.one, Velocity = Vector2.right, Gravity = 10f, Lifetime = 1f });
            list.Step(0f, null);
            Assert.AreEqual(Vector2.one, list[0].Position, "hitstop (combat scale 0) holds the blood in the air");
        }

        [Test]
        public void SprayVelocity_GoesAwayFromSource_UnlessBackSplash()
        {
            Vector2 away = BloodSpray.Velocity(-1, 5f, 0f, 60f, 0.1f, 1f, 0.5f, 0.9f);
            Assert.Less(away.x, 0f);
            Assert.Greater(away.y, 0f);
            Assert.AreEqual(5f, away.magnitude, 1e-4f);
            Vector2 back = BloodSpray.Velocity(-1, 5f, 0f, 60f, 0.1f, 1f, 0.5f, 0.05f);
            Assert.Greater(back.x, 0f);
        }

        [Test]
        public void GroundY_StaysInTheWalkableBand_AndBelowTheStart()
        {
            var band = new ArenaRect(1.5f, -13.6667f, 25.1667f, -9.6667f);
            Assert.AreEqual(-13.6667f, BloodSpray.GroundY(-13.5f, -0.5f, -10f, 0.2f, band), 1e-4f);
            Assert.AreEqual(-11.2f, BloodSpray.GroundY(-11f, 0.3f, -11f, 0.2f, band), 1e-4f, "lying body: at least minDrop below the start");
            Assert.AreEqual(-11.1f, BloodSpray.GroundY(-11f, -0.1f, -9f, 0.2f, band), 1e-4f);
        }

        [Test]
        public void Stain_IsClampedInside_AndSkippedUnderObstacles()
        {
            var band = new ArenaRect(1.5f, -13.6667f, 25.1667f, -9.6667f);
            var truck = new ArenaRect(-2f, -13f, 6.3667f, -11.8333f);
            Assert.IsTrue(BloodSpray.TryPlaceStain(new Vector2(30f, -20f), band, new[] { truck }, out Vector2 s));
            Assert.AreEqual(new Vector2(25.1667f, -13.6667f), s);
            Assert.IsFalse(BloodSpray.TryPlaceStain(new Vector2(4f, -12.5f), band, new[] { truck }, out _));
        }

        [Test]
        public void BehindObstacle_OnlyFartherBackThanItsFrontEdge_AndNearItsSides()
        {
            var truck = new ArenaRect(-2f, -13f, 6.3667f, -11.8333f);
            var obstacles = new[] { truck };
            Assert.IsTrue(BloodSpray.IsBehindObstacle(new Vector2(2f, -11.67f), obstacles, 1.5f), "behind the truck");
            Assert.IsTrue(BloodSpray.IsBehindObstacle(new Vector2(7.5f, -11.67f), obstacles, 1.5f), "lying body reaches past its side");
            Assert.IsFalse(BloodSpray.IsBehindObstacle(new Vector2(2f, -13.3f), obstacles, 1.5f), "in the lane in front of it");
            Assert.IsFalse(BloodSpray.IsBehindObstacle(new Vector2(12f, -11.67f), obstacles, 1.5f), "open yard");
            Assert.IsFalse(BloodSpray.IsBehindObstacle(Vector2.zero, null, 1.5f));
        }

        // ------------------------------------------------------------ stains

        [Test]
        public void Stains_PastTheCap_OldestFadeAndGo()
        {
            var book = new StainBook(3, 1f);
            for (int i = 0; i < 5; i++)
                book.Add(new Stain { Position = new Vector2(i, 0f) }, 10f);
            Assert.AreEqual(5, book.Count);
            Assert.AreEqual(3, book.KeptCount);
            Assert.AreEqual(1f, book.Alpha01(4, 10.5f));
            Assert.AreEqual(0.5f, book.Alpha01(0, 10.5f), 1e-5f, "the oldest is fading");
            book.Update(11.1f);
            Assert.AreEqual(3, book.Count);
            Assert.AreEqual(2f, book[0].Position.x, "the two oldest are gone");
        }

        [Test]
        public void Stains_HardLimitIsTwiceTheCap()
        {
            var book = new StainBook(4, 100f);
            for (int i = 0; i < 20; i++)
                book.Add(new Stain { Position = new Vector2(i, 0f) }, 0f);
            Assert.AreEqual(8, book.Count);
            Assert.AreEqual(4, book.KeptCount);
            Assert.AreEqual(19f, book[7].Position.x);
        }

        [Test]
        public void PixelTextures_StarIsSymmetric()
        {
            Texture2D star = PixelTextures.Star("t");
            try
            {
                Assert.AreEqual(9, star.width);
                Assert.AreEqual(FilterMode.Point, star.filterMode);
                Color32[] px = star.GetPixels32();
                Assert.AreEqual(255, px[4 * 9 + 4].a, "centre is lit");
                Assert.AreEqual(0, px[0].a, "corner is clear");
                Assert.AreEqual(px[4 * 9 + 0].a, px[4 * 9 + 8].a);
            }
            finally
            {
                Object.DestroyImmediate(star);
            }
        }
    }
}
