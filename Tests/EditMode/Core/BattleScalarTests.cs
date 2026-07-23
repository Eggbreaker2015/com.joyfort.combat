using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleScalarTests
    {
        [Test]
        public void FromFloat_StoresValueForCurrentCompatibilityLayer()
        {
            BattleScalar value = BattleScalar.FromFloat(1.25f);

            Assert.AreEqual(1.25f, value.ToFloat());
        }

        [Test]
        public void Operators_PreserveExistingFloatCompatibilityBehavior()
        {
            BattleScalar left = BattleScalar.FromFloat(3f);
            BattleScalar right = BattleScalar.FromFloat(2f);

            Assert.AreEqual(5f, (left + right).ToFloat());
            Assert.AreEqual(1f, (left - right).ToFloat());
            Assert.AreEqual(6f, (left * right).ToFloat());
            Assert.AreEqual(1.5f, (left / right).ToFloat());
        }

        [Test]
        public void RawValue_RoundTripsDeterministicPayload()
        {
            BattleScalar value = BattleScalar.FromFloat(1.25f);
            BattleScalar restored = BattleScalar.FromRaw(value.RawValue);

            Assert.AreEqual(value, restored);
            Assert.AreEqual(value.RawValue, restored.RawValue);
            Assert.AreEqual(1.25f, restored.ToFloat(), 0.000001f);
        }

        [Test]
        public void Sqrt_UsesScalarBackend()
        {
            BattleScalar value = BattleScalar.FromFloat(25f);

            Assert.AreEqual(5f, BattleScalar.Sqrt(value).ToFloat(), 0.000001f);
        }

        [Test]
        public void ToIntRoundHalfUpSaturating_RoundsWithoutFloatConversion()
        {
            Assert.AreEqual(1, BattleScalar.FromFloat(1.49f).ToIntRoundHalfUpSaturating());
            Assert.AreEqual(2, BattleScalar.FromFloat(1.5f).ToIntRoundHalfUpSaturating());
            Assert.AreEqual(0, BattleScalar.FromFloat(0.49f).ToIntRoundHalfUpSaturating());
            Assert.AreEqual(1, BattleScalar.FromFloat(0.5f).ToIntRoundHalfUpSaturating());
        }

        [Test]
        public void ToIntRoundHalfUpSaturating_ClampsOutsideDamageRange()
        {
            Assert.AreEqual(0, BattleScalar.FromFloat(-0.5f).ToIntRoundHalfUpSaturating());
            Assert.AreEqual(int.MaxValue, (BattleScalar.FromInt(int.MaxValue) + BattleScalar.One).ToIntRoundHalfUpSaturating());
        }

        [Test]
        public void FromDouble_RejectsInvalidValues()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BattleScalar.FromDouble(double.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BattleScalar.FromDouble(double.PositiveInfinity));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BattleScalar.FromDouble(double.NegativeInfinity));
        }

        [Test]
        public void InvalidFloatValues_AreRejectedAtCoreBoundary()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BattleScalar.FromFloat(float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BattleScalar.FromFloat(float.PositiveInfinity));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BattleScalar.FromFloat(float.NegativeInfinity));
        }
    }
}
