using UnityEngine;

namespace Combat.Unity.Display
{
    [DisallowMultipleComponent]
    public sealed class CombatViewTransformSmoother : MonoBehaviour
    {
        private const float DurationEpsilon = 0.0001f;

        private UnityCombatViewSmoothingSettings _settings = UnityCombatViewSmoothingSettings.Default;
        private Vector3 _positionStart;
        private Vector3 _positionTarget;
        private float _positionElapsed;
        private bool _isPositionSmoothing;
        private Quaternion _rotationStart;
        private Quaternion _rotationTarget = Quaternion.identity;
        private float _rotationElapsed;
        private bool _isRotationSmoothing;

        public void Configure(UnityCombatViewSmoothingSettings settings)
        {
            _settings = settings;
            SnapTo(transform.position, transform.localRotation);
        }

        public void SnapTo(Vector3 position, Quaternion localRotation)
        {
            transform.position = position;
            transform.localRotation = localRotation;
            _positionStart = position;
            _positionTarget = position;
            _positionElapsed = 0f;
            _isPositionSmoothing = false;
            _rotationStart = localRotation;
            _rotationTarget = localRotation;
            _rotationElapsed = 0f;
            _isRotationSmoothing = false;
        }

        public void SnapPosition(Vector3 position)
        {
            transform.position = position;
            _positionStart = position;
            _positionTarget = position;
            _positionElapsed = 0f;
            _isPositionSmoothing = false;
        }

        public void SnapLocalRotation(Quaternion localRotation)
        {
            transform.localRotation = localRotation;
            _rotationStart = localRotation;
            _rotationTarget = localRotation;
            _rotationElapsed = 0f;
            _isRotationSmoothing = false;
        }

        public void SetTargetPosition(Vector3 position)
        {
            if (_settings.PositionDurationSeconds <= DurationEpsilon)
            {
                SnapPosition(position);
                return;
            }

            _positionStart = transform.position;
            _positionTarget = position;
            _positionElapsed = 0f;
            _isPositionSmoothing = true;
        }

        public void SetTargetLocalRotation(Quaternion localRotation)
        {
            if (_settings.RotationDurationSeconds <= DurationEpsilon)
            {
                SnapLocalRotation(localRotation);
                return;
            }

            _rotationStart = transform.localRotation;
            _rotationTarget = localRotation;
            _rotationElapsed = 0f;
            _isRotationSmoothing = true;
        }

        public void ResetForPool()
        {
            _settings = UnityCombatViewSmoothingSettings.Immediate;
            SnapTo(transform.position, transform.localRotation);
        }

        internal void TickForTests(float deltaSeconds)
        {
            Tick(deltaSeconds);
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        private void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            TickPosition(deltaSeconds);
            TickRotation(deltaSeconds);
        }

        private void TickPosition(float deltaSeconds)
        {
            if (!_isPositionSmoothing)
            {
                return;
            }

            _positionElapsed += deltaSeconds;
            float t = Mathf.Clamp01(_positionElapsed / _settings.PositionDurationSeconds);
            transform.position = Vector3.Lerp(_positionStart, _positionTarget, t);
            if (t >= 1f)
            {
                _isPositionSmoothing = false;
                _positionStart = _positionTarget;
            }
        }

        private void TickRotation(float deltaSeconds)
        {
            if (!_isRotationSmoothing)
            {
                return;
            }

            _rotationElapsed += deltaSeconds;
            float t = Mathf.Clamp01(_rotationElapsed / _settings.RotationDurationSeconds);
            transform.localRotation = Quaternion.Slerp(_rotationStart, _rotationTarget, t);
            if (t >= 1f)
            {
                _isRotationSmoothing = false;
                _rotationStart = _rotationTarget;
            }
        }
    }
}
