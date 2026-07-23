using Combat.Unity.Authoring;
using Combat.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests.Unity.Editor
{
    public sealed class BattleSpatialMapColliderImporterTests
    {
        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void CircleCollider_ImportsWorldCenterRadiusAndFilter()
        {
            _gameObject = new GameObject("Spatial Circle");
            _gameObject.transform.position = new Vector3(2f, 3f, 0f);
            _gameObject.transform.localScale = new Vector3(2f, 2f, 1f);
            CircleCollider2D collider = _gameObject.AddComponent<CircleCollider2D>();
            collider.offset = new Vector2(0.5f, -0.5f);
            collider.radius = 0.75f;

            bool success = BattleSpatialMapColliderImporter.TryCreateEntry(
                collider,
                7,
                2u,
                4u,
                out BattleSpatialEntry entry,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(entry.StableId, Is.EqualTo(7));
            Assert.That(entry.Shape, Is.EqualTo(BattleSpatialShape.Circle));
            Assert.That(entry.Center, Is.EqualTo(new Vector2(3f, 2f)));
            Assert.That(entry.Radius, Is.EqualTo(1.5f));
            Assert.That(entry.CategoryBits, Is.EqualTo(2u));
            Assert.That(entry.MaskBits, Is.EqualTo(4u));
        }

        [Test]
        public void CircleCollider_RejectsNonUniformWorldScale()
        {
            _gameObject = new GameObject("Invalid Spatial Circle");
            _gameObject.transform.localScale = new Vector3(2f, 1f, 1f);
            CircleCollider2D collider = _gameObject.AddComponent<CircleCollider2D>();

            bool success = BattleSpatialMapColliderImporter.TryCreateEntry(
                collider,
                1,
                1u,
                1u,
                out _,
                out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("uniform"));
        }

        [Test]
        public void BoxCollider_RejectsWorldRotationThatIsNotAxisAligned()
        {
            _gameObject = new GameObject("Invalid Spatial Box");
            _gameObject.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            BoxCollider2D collider = _gameObject.AddComponent<BoxCollider2D>();

            bool success = BattleSpatialMapColliderImporter.TryCreateEntry(
                collider,
                1,
                1u,
                1u,
                out _,
                out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("axis-aligned"));
        }

        [Test]
        public void BoxCollider_RejectsTiltOutsideWorldXyPlane()
        {
            _gameObject = new GameObject("Tilted Spatial Box");
            _gameObject.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            BoxCollider2D collider = _gameObject.AddComponent<BoxCollider2D>();

            bool success = BattleSpatialMapColliderImporter.TryCreateEntry(
                collider,
                1,
                1u,
                1u,
                out _,
                out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("axis-aligned"));
        }
    }
}
