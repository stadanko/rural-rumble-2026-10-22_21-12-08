using System.Collections.Generic;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Juice;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    public class CombatSoundRulesTests
    {
        [TestCase(MoveId.Strike1, ImpactSound.Strike1)]
        [TestCase(MoveId.Strike2, ImpactSound.Strike2)]
        [TestCase(MoveId.Strike3, ImpactSound.Strike3)]
        [TestCase(MoveId.Stomp, ImpactSound.Stomp)]
        [TestCase(MoveId.Bodyslam, ImpactSound.BodyThud)]
        [TestCase(MoveId.LockUp, ImpactSound.None)]
        [TestCase(MoveId.IrishWhip, ImpactSound.None)]
        [TestCase(MoveId.None, ImpactSound.None)]
        public void ImpactFor_MapsMoveToClip(MoveId move, ImpactSound expected)
        {
            Assert.AreEqual(expected, CombatSoundRules.ImpactFor(move));
        }

        [TestCase(MoveId.Strike3)]
        [TestCase(MoveId.Stomp)]
        [TestCase(MoveId.Bodyslam)]
        public void ShouldGrunt_HeavyHits_AlwaysGrunt(MoveId move)
        {
            Assert.IsTrue(CombatSoundRules.ShouldGrunt(move, false, 0.99f, 0f));
        }

        [TestCase(MoveId.Strike1)]
        [TestCase(MoveId.Strike3)]
        [TestCase(MoveId.Bodyslam)]
        public void ShouldGrunt_KnockedOut_NeverGrunts(MoveId move)
        {
            Assert.IsFalse(CombatSoundRules.ShouldGrunt(move, true, 0f, 1f));
        }

        [TestCase(MoveId.Strike1)]
        [TestCase(MoveId.Strike2)]
        public void ShouldGrunt_LightHit_RollBelowChance_Grunts(MoveId move)
        {
            Assert.IsTrue(CombatSoundRules.ShouldGrunt(move, false, 0.349f, 0.35f));
        }

        [TestCase(MoveId.Strike1)]
        [TestCase(MoveId.Strike2)]
        public void ShouldGrunt_LightHit_RollAtChance_DoesNotGrunt(MoveId move)
        {
            Assert.IsFalse(CombatSoundRules.ShouldGrunt(move, false, 0.35f, 0.35f));
        }

        [Test]
        public void ShouldGrunt_LightHit_ChanceZero_NeverGrunts()
        {
            Assert.IsFalse(CombatSoundRules.ShouldGrunt(MoveId.Strike1, false, 0f, 0f));
        }

        [Test]
        public void ShouldGrunt_LightHit_ChanceOne_GruntsEvenOnRollOne()
        {
            // Random.value can return exactly 1.
            Assert.IsTrue(CombatSoundRules.ShouldGrunt(MoveId.Strike2, false, 1f, 1f));
        }

        [Test]
        public void ShouldGrunt_NonHitMove_DoesNotGrunt()
        {
            Assert.IsFalse(CombatSoundRules.ShouldGrunt(MoveId.LockUp, false, 0f, 1f));
        }

        [Test]
        public void Pitch_SpansPlusMinusVariation()
        {
            Assert.AreEqual(0.94f, CombatSoundRules.Pitch(0.06f, 0f), 1e-5f);
            Assert.AreEqual(1f, CombatSoundRules.Pitch(0.06f, 0.5f), 1e-5f);
            Assert.AreEqual(1.06f, CombatSoundRules.Pitch(0.06f, 1f), 1e-5f);
        }

        [Test]
        public void Pitch_NoVariation_IsOne()
        {
            Assert.AreEqual(1f, CombatSoundRules.Pitch(0f, 0.8f), 1e-6f);
        }

        // Footprints from ARCHITECTURE 3.1.
        private static readonly ArenaRect Truck = new ArenaRect(-0.6667f, -10.8333f, 7.7f, -9.6667f);
        private static readonly ArenaRect Pool = new ArenaRect(18.3333f, -10.6667f, 27.6667f, -9.6667f);

        [Test]
        public void NearestObstacle_ContactAtTruckEdge_IsTruck()
        {
            Assert.AreEqual(ObstacleSound.Truck, CombatSoundRules.NearestObstacle(new Vector2(8.2f, -10.5f), Truck, Pool));
        }

        [Test]
        public void NearestObstacle_ContactAtPoolEdge_IsPool()
        {
            Assert.AreEqual(ObstacleSound.Pool, CombatSoundRules.NearestObstacle(new Vector2(17.8f, -10.2f), Truck, Pool));
        }

        [Test]
        public void NearestObstacle_InsideRect_IsThatRect()
        {
            Assert.AreEqual(ObstacleSound.Pool, CombatSoundRules.NearestObstacle(new Vector2(20f, -10f), Truck, Pool));
        }

        [Test]
        public void NearestObstacle_MissingMarkers()
        {
            Vector2 p = new Vector2(20f, -10f);
            Assert.AreEqual(ObstacleSound.None, CombatSoundRules.NearestObstacle(p, null, null));
            Assert.AreEqual(ObstacleSound.Truck, CombatSoundRules.NearestObstacle(p, Truck, null));
            Assert.AreEqual(ObstacleSound.Pool, CombatSoundRules.NearestObstacle(p, null, Pool));
        }

        [Test]
        public void FindVoice_MatchesDefinition_OrNull()
        {
            var barry = ScriptableObject.CreateInstance<WrestlerDefinition>();
            var peaux = ScriptableObject.CreateInstance<WrestlerDefinition>();
            try
            {
                var barryVoice = new WrestlerVoice { wrestler = barry };
                var voices = new List<WrestlerVoice> { null, barryVoice };
                Assert.AreSame(barryVoice, CombatSoundRules.FindVoice(voices, barry));
                Assert.IsNull(CombatSoundRules.FindVoice(voices, peaux));
                Assert.IsNull(CombatSoundRules.FindVoice(voices, null));
                Assert.IsNull(CombatSoundRules.FindVoice(null, barry));
            }
            finally
            {
                Object.DestroyImmediate(barry);
                Object.DestroyImmediate(peaux);
            }
        }
    }
}
