using System.Collections.Generic;
using NUnit.Framework;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>D-049 foot-contact channel plumbing (ARCHITECTURE 4.8): a raised contact reaches every subscriber
    /// unchanged, once; unsubscribing stops delivery; raising with no subscriber is safe.</summary>
    public class FootContactEventsTests
    {
        private readonly List<FootContact> received = new List<FootContact>();

        private void Record(FootContact c) => received.Add(c);

        [SetUp]
        public void SetUp()
        {
            received.Clear();
            FootContactEvents.Contact += Record;
        }

        [TearDown]
        public void TearDown() => FootContactEvents.Contact -= Record;

        [Test]
        public void Raise_DeliversTheContactUnchanged_Once()
        {
            var contact = new FootContact(WrestlerId.Cpu, Foot.Right, Gait.Run, new Vector2(18.5f, -14.25f));
            FootContactEvents.Raise(contact);

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(WrestlerId.Cpu, received[0].Wrestler);
            Assert.AreEqual(Foot.Right, received[0].Foot);
            Assert.AreEqual(Gait.Run, received[0].Gait);
            Assert.AreEqual(new Vector2(18.5f, -14.25f), received[0].Position);
        }

        [Test]
        public void Raise_KeepsOrderAndIdentityAcrossWrestlersAndGaits()
        {
            FootContactEvents.Raise(new FootContact(WrestlerId.Player, Foot.Left, Gait.Walk, new Vector2(1f, -13f)));
            FootContactEvents.Raise(new FootContact(WrestlerId.Cpu, Foot.Right, Gait.Run, new Vector2(2f, -14f)));

            Assert.AreEqual(2, received.Count);
            Assert.AreEqual(WrestlerId.Player, received[0].Wrestler);
            Assert.AreEqual(Gait.Walk, received[0].Gait);
            Assert.AreEqual(Foot.Left, received[0].Foot);
            Assert.AreEqual(WrestlerId.Cpu, received[1].Wrestler);
            Assert.AreEqual(Gait.Run, received[1].Gait);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            FootContactEvents.Contact -= Record;
            FootContactEvents.Raise(new FootContact(WrestlerId.Player, Foot.Left, Gait.Walk, Vector2.zero));
            Assert.AreEqual(0, received.Count);
        }

        [Test]
        public void EverySubscriberGetsTheContact()
        {
            int second = 0;
            void Count(FootContact _) => second++;
            FootContactEvents.Contact += Count;
            try
            {
                FootContactEvents.Raise(new FootContact(WrestlerId.Player, Foot.Left, Gait.Walk, Vector2.zero));
            }
            finally
            {
                FootContactEvents.Contact -= Count;
            }
            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void Raise_WithNoSubscriber_DoesNotThrow()
        {
            FootContactEvents.Contact -= Record;
            Assert.DoesNotThrow(() =>
                FootContactEvents.Raise(new FootContact(WrestlerId.Cpu, Foot.Right, Gait.Run, Vector2.one)));
        }

        [Test]
        public void SurfaceUnderAContact_IsALookupAtItsPosition()
        {
            // The Juice path in one line: surface = map.SurfaceAt(contact.Position).
            var map = new SurfaceMap(new[] { SurfaceType.Grass, SurfaceType.Puddle }, 2, 1, Vector2.zero, 1f);
            FootContactEvents.Raise(new FootContact(WrestlerId.Player, Foot.Left, Gait.Walk, new Vector2(1.5f, -0.5f)));
            Assert.AreEqual(SurfaceType.Puddle, map.SurfaceAt(received[0].Position));
        }
    }
}
