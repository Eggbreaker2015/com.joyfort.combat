using System;
using UnityEngine;
using UnityEngine.Pool;

namespace Combat.Unity.Pooling
{
    internal sealed class GameObjectPool
    {
        private readonly string _name;
        private readonly Transform _inactiveRoot;
        private readonly Action<GameObject> _resetAction;
        private readonly ObjectPool<GameObject> _pool;

        public GameObjectPool(
            string name,
            Transform inactiveRoot,
            Func<GameObject> createFunc,
            Action<GameObject> resetAction,
            int defaultCapacity = 16,
            int maxSize = 256)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Pool name is required.", nameof(name));
            }

            if (defaultCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultCapacity));
            }

            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            }

            if (defaultCapacity > maxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultCapacity), "Default capacity cannot exceed max size.");
            }

            _name = name;
            _inactiveRoot = inactiveRoot != null ? inactiveRoot : throw new ArgumentNullException(nameof(inactiveRoot));
            _resetAction = resetAction ?? throw new ArgumentNullException(nameof(resetAction));

            if (createFunc == null)
            {
                throw new ArgumentNullException(nameof(createFunc));
            }

            _pool = new ObjectPool<GameObject>(
                createFunc,
                OnGet,
                OnRelease,
                DestroyObject,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public GameObject Get(Transform parent)
        {
            return Get(parent, NoOpConfigure);
        }

        public GameObject Get(Transform parent, Action<GameObject> configureAction)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (configureAction == null)
            {
                throw new ArgumentNullException(nameof(configureAction));
            }

            GameObject instance = _pool.Get();
            if (instance.activeSelf)
            {
                instance.SetActive(false);
            }

            instance.transform.SetParent(parent, worldPositionStays: false);
            configureAction(instance);
            instance.SetActive(true);
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (_inactiveRoot == null)
            {
                CleanupBeforeDestroy(instance);
                DestroyObject(instance);
                return;
            }

            _pool.Release(instance);
        }

        public void Clear()
        {
            _pool.Clear();
        }

        private void OnGet(GameObject instance)
        {
            if (instance == null)
            {
                throw new InvalidOperationException($"Pool {_name} produced a null GameObject.");
            }
        }

        private void OnRelease(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(_inactiveRoot, worldPositionStays: false);
            _resetAction(instance);
        }

        private void CleanupBeforeDestroy(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(null, worldPositionStays: false);
            _resetAction(instance);
        }

        private static void DestroyObject(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void NoOpConfigure(GameObject instance)
        {
        }
    }
}
