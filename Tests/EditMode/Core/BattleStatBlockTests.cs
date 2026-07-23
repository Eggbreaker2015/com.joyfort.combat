using System;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleStatBlockTests
    {
        [Test]
        public void BattleStatId_UsesStableSerializedValues()
        {
            Assert.AreEqual(0, (int)BattleStatId.MaxHealth);
            Assert.AreEqual(1, (int)BattleStatId.MoveSpeed);
        }

        [Test]
        public void Constructor_RejectsNullEntries()
        {
            Assert.Throws<ArgumentNullException>(() => new BattleStatBlock(null));
        }

        [Test]
        public void Constructor_RejectsDuplicateStats()
        {
            Assert.Throws<ArgumentException>(() => new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MaxHealth, 10f),
                new BattleStatEntry(BattleStatId.MaxHealth, 20f)
            }));
        }

        [Test]
        public void Entry_RejectsNonFiniteValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleStatEntry(BattleStatId.MaxHealth, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleStatEntry(BattleStatId.MaxHealth, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleStatEntry(BattleStatId.MaxHealth, float.NegativeInfinity));
        }

        [Test]
        public void RequireFloat_ReturnsConfiguredValue()
        {
            var block = new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MoveSpeed, 2.5f)
            });

            Assert.AreEqual(2.5f, block.RequireFloat(BattleStatId.MoveSpeed, "unit"));
        }

        [Test]
        public void RequireFloat_RejectsMissingStat()
        {
            var block = new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MoveSpeed, 2.5f)
            });

            Exception exception = Assert.Throws<ArgumentException>(() => block.RequireFloat(BattleStatId.MaxHealth, "Combatant 'melee'"));

            Assert.That(exception.Message, Does.Contain("Combatant 'melee'"));
            Assert.That(exception.Message, Does.Contain("MaxHealth"));
        }

        [Test]
        public void RequireInt_ReturnsWholeNumberValue()
        {
            var block = new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MaxHealth, 20f)
            });

            Assert.AreEqual(20, block.RequireInt(BattleStatId.MaxHealth, "unit"));
        }

        [Test]
        public void RequireInt_RejectsFractionalValue()
        {
            var block = new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MaxHealth, 20.5f)
            });

            Exception exception = Assert.Throws<ArgumentException>(() => block.RequireInt(BattleStatId.MaxHealth, "Combatant 'melee'"));

            Assert.That(exception.Message, Does.Contain("integer"));
            Assert.That(exception.Message, Does.Contain("MaxHealth"));
        }

        [Test]
        public void RequireInt_RejectsValuesOutsideIntRange()
        {
            var block = new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MaxHealth, 1e20f)
            });

            Exception exception = Assert.Throws<ArgumentException>(() => block.RequireInt(BattleStatId.MaxHealth, "Combatant 'melee'"));

            Assert.That(exception.Message, Does.Contain("Combatant 'melee'"));
            Assert.That(exception.Message, Does.Contain("MaxHealth"));
            Assert.That(exception.Message, Does.Contain("integer range"));
        }

        [Test]
        public void TryGetFloat_ReturnsFalseForMissingStat()
        {
            var block = new BattleStatBlock(new BattleStatEntry[0]);

            Assert.IsFalse(block.TryGetFloat(BattleStatId.MaxHealth, out float value));
            Assert.AreEqual(0f, value);
        }
    }
}
