using System;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleVector2Tests
    {
        [Test]
        public void Constructor_RejectsInvalidCoordinates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleVector2(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleVector2(0f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleVector2(float.PositiveInfinity, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleVector2(0f, float.NegativeInfinity));
        }

        [Test]
        public void Operations_PreserveExistingVectorBehavior()
        {
            var left = new BattleVector2(3f, 4f);
            var right = new BattleVector2(1f, 2f);

            Assert.AreEqual(new BattleVector2(4f, 6f), left + right);
            Assert.AreEqual(new BattleVector2(2f, 2f), left - right);
            Assert.AreEqual(new BattleVector2(6f, 8f), left * BattleScalar.FromFloat(2f));
            Assert.AreEqual(25f, left.SqrMagnitude);
            Assert.AreEqual(5f, left.Magnitude);
            Assert.AreEqual(5f, BattleVector2.Distance(BattleVector2.Zero, left));
            Assert.AreEqual(25f, BattleVector2.SqrDistance(BattleVector2.Zero, left));
        }

        [Test]
        public void RawCoordinates_RoundTripThroughScalarBackend()
        {
            var value = new BattleVector2(3f, 4f);
            BattleVector2 restored = BattleVector2.FromRaw(value.XRaw, value.YRaw);

            Assert.AreEqual(value, restored);
            Assert.AreEqual(value.XRaw, restored.XRaw);
            Assert.AreEqual(value.YRaw, restored.YRaw);
            Assert.AreEqual(25f, restored.SqrMagnitude, 0.000001f);
            Assert.AreEqual(5f, restored.Magnitude, 0.000001f);
        }
    }
}
