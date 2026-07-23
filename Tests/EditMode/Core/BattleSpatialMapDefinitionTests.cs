using System;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSpatialMapDefinitionTests
    {
        [Test]
        public void Constructor_SortsEntriesByStableId()
        {
            var definition = new BattleSpatialMapDefinition(new[]
            {
                Circle(2, new BattleVector2(2f, 0f)),
                Circle(1, BattleVector2.Zero)
            });

            Assert.That(definition.Entries[0].StableId, Is.EqualTo(1));
            Assert.That(definition.Entries[1].StableId, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_RejectsDuplicateStableIds()
        {
            Assert.Throws<ArgumentException>(() => new BattleSpatialMapDefinition(new[]
            {
                Circle(1, BattleVector2.Zero),
                Circle(1, BattleVector2.Right)
            }));
        }

        [Test]
        public void Entry_RejectsBoundsOutsideDeterministicDomain()
        {
            BattleVector2 edge = new BattleVector2(
                BattleSpatialMapDefinition.MaxCoordinateMagnitude,
                BattleScalar.Zero);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Circle(1, edge));
        }

        private static BattleSpatialEntryDefinition Circle(
            int stableId,
            BattleVector2 center)
        {
            return new BattleSpatialEntryDefinition(
                stableId,
                BattleSpatialShapeType.Circle,
                center,
                BattleScalar.One,
                BattleVector2.Zero,
                1u,
                uint.MaxValue);
        }
    }
}
