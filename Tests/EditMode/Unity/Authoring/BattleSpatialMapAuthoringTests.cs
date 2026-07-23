using System.Reflection;
using Combat.Core.Battle;
using Combat.Unity.Authoring;
using Combat.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests.Unity.Authoring
{
    public sealed class BattleSpatialMapAuthoringTests
    {
        private BattleSpatialMapAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BattleSpatialMapAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void Converter_SortsEntriesAndPreservesFilterData()
        {
            SetEntries(
                new BattleSpatialEntry(
                    2,
                    BattleSpatialShape.Aabb,
                    new Vector2(2f, 0f),
                    0f,
                    new Vector2(2f, 4f),
                    4u,
                    8u),
                new BattleSpatialEntry(
                    1,
                    BattleSpatialShape.Circle,
                    Vector2.zero,
                    0.5f,
                    Vector2.zero,
                    1u,
                    2u));

            BattleSpatialMapDefinition definition =
                BattleAuthoringConverter.BuildSpatialMapDefinition(_asset);

            Assert.That(definition.Entries[0].StableId, Is.EqualTo(1));
            Assert.That(definition.Entries[0].CategoryBits, Is.EqualTo(1u));
            Assert.That(definition.Entries[1].StableId, Is.EqualTo(2));
            Assert.That(definition.Entries[1].Size, Is.EqualTo(new BattleVector2(2f, 4f)));
            Assert.That(definition.Entries[1].MaskBits, Is.EqualTo(8u));
        }

        [Test]
        public void Validator_ReportsDuplicateIdAndInvalidCircleRadius()
        {
            SetEntries(
                new BattleSpatialEntry(
                    1,
                    BattleSpatialShape.Circle,
                    Vector2.zero,
                    0f,
                    Vector2.zero,
                    1u,
                    1u),
                new BattleSpatialEntry(
                    1,
                    BattleSpatialShape.Aabb,
                    Vector2.one,
                    0f,
                    Vector2.one,
                    1u,
                    1u));

            BattleAuthoringValidationReport report =
                BattleAuthoringValidator.ValidateSpatialMap(_asset);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues, Has.Some.Property("PropertyPath").EqualTo("entries[0].radius"));
            Assert.That(report.Issues, Has.Some.Property("PropertyPath").EqualTo("entries[1].stableId"));
        }

        [Test]
        public void NullAsset_ConvertsToEmptyDefinition()
        {
            BattleSpatialMapDefinition definition =
                BattleAuthoringConverter.BuildSpatialMapDefinition(null);

            Assert.That(definition.Entries, Is.Empty);
        }

        private void SetEntries(params BattleSpatialEntry[] entries)
        {
            FieldInfo field = typeof(BattleSpatialMapAsset).GetField(
                "_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_asset, entries);
        }
    }
}
