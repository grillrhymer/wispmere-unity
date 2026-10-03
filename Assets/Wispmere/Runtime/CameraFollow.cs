using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Wispmere
{
    /// <summary>
    /// Elevated action-RPG follow camera. The camera keeps a stable world
    /// bearing so camera-relative movement remains predictable; the arrival
    /// reveal smoothly pulls back and rises above the square.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        private const int ObstructionYawSteps = 4;
        private const int ObstructionElevationSteps = 2;
        private const float ObstructionAngleStep = 18f;
        private const float ObstructionElevationStep = 12f;

        [Header("Wiring")]
        public Transform target;

        [Header("Framing")]
        public Vector3 playOffset = new Vector3(8f, 16f, 20f);
        public Vector3 revealOffset = new Vector3(10f, 23f, 28f);
        public float followSpeed = 4.5f;
        public float focusHeight = 0.9f;
        public float collisionRadius = 0.35f;
        public float collisionPadding = 0.2f;
        public float minimumDistance = 4f;
        public bool usePolishedControls;
        [Range(35f, 50f)] public float pitch = 38f;
        public float yaw = 65f;
        public float zoomDistance = 27.6f;
        public float minimumZoom = 20f;
        public float maximumZoom = 36f;
        public float zoomSensitivity = 1.8f;
        public float orbitSensitivity = 0.16f;
        public float positionSmoothTime = 0.28f;
        public float lookAheadDistance = 0.3f;

        private bool _revealing;
        private Vector3 _positionVelocity;
        private bool _initialized;
        private float _targetZoom;
        private float _zoomVelocity;
        private readonly RaycastHit[] _obstructionHits = new RaycastHit[32];
        private Vector3 _lastObstructionDirection;
        private bool _hasLastObstructionDirection;

        public float TargetZoomDistance { get { return _targetZoom; } }

        private void Awake()
        {
            _targetZoom = Mathf.Clamp(zoomDistance, minimumZoom, maximumZoom);
        }

        public void SetReveal(bool on)
        {
            _revealing = on;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            Vector3 focus = GetFocus();
            Vector3 offset = usePolishedControls ? GetPolishedOffset() : playOffset;
            transform.position = ResolveObstruction(focus, target.position + offset);
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            _positionVelocity = Vector3.zero;
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                Debug.LogError("[Wispmere] CameraFollow needs its target assigned.", this);
                enabled = false;
                return;
            }

            if (!usePolishedControls)
            {
                Vector3 legacyFocus = GetFocus();
                Vector3 legacyOffset = _revealing ? revealOffset : playOffset;
                Vector3 legacyGoal = ResolveObstruction(legacyFocus, target.position + legacyOffset);
                if (!_initialized)
                {
                    transform.position = legacyGoal;
                    _initialized = true;
                }
                else
                {
                    transform.position = Vector3.SmoothDamp(transform.position, legacyGoal,
                        ref _positionVelocity, 1f / Mathf.Max(0.01f, followSpeed));
                }

                Quaternion legacyRotation = Quaternion.LookRotation(legacyFocus - transform.position, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, legacyRotation,
                    1f - Mathf.Exp(-followSpeed * Time.deltaTime));
                return;
            }

            Vector3 focus = GetFocus();
            float zoom = Mathf.SmoothDamp(zoomDistance, _targetZoom, ref _zoomVelocity,
                positionSmoothTime);
            Vector3 offset = GetPolishedOffset(zoom);
            Vector3 goal = ResolveObstruction(focus, target.position + offset);
            if (!_initialized)
            {
                transform.position = goal;
                _initialized = true;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, goal, ref _positionVelocity,
                    Mathf.Max(0.01f, positionSmoothTime));
            }

            Quaternion lookRotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation,
                1f - Mathf.Exp(-Mathf.Max(0.01f, followSpeed) * Time.deltaTime));
            zoomDistance = zoom;
        }

        private void Update()
        {
            if (!usePolishedControls) return;
            Mouse mouse = Mouse.current;
            if (mouse == null || PointerOverUI()) return;

            if (mouse.middleButton.isPressed)
                AdjustOrbit(mouse.delta.ReadValue());
            float wheel = mouse.scroll.ReadValue().y;
            AdjustZoom(wheel);
        }

        public void AdjustOrbit(Vector2 delta)
        {
            yaw += delta.x * orbitSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, 35f, 50f);
        }

        public void AdjustZoom(float scrollDelta)
        {
            if (Mathf.Abs(scrollDelta) <= 0.01f) return;
            _targetZoom = Mathf.Clamp(_targetZoom - scrollDelta * zoomSensitivity * 0.01f,
                minimumZoom, maximumZoom);
        }

        private static bool PointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private Vector3 GetPolishedOffset(float zoom = -1f)
        {
            if (zoom < 0f) zoom = Mathf.Clamp(zoomDistance, minimumZoom, maximumZoom);
            float currentPitch = Mathf.Clamp(pitch + (_revealing ? 4f : 0f), 35f, 50f);
            float horizontalDistance = Mathf.Cos(currentPitch * Mathf.Deg2Rad) * zoom;
            float verticalDistance = Mathf.Sin(currentPitch * Mathf.Deg2Rad) * zoom;
            Vector3 offset = new Vector3(
                Mathf.Sin(yaw * Mathf.Deg2Rad) * horizontalDistance,
                verticalDistance,
                Mathf.Cos(yaw * Mathf.Deg2Rad) * horizontalDistance);
            if (_revealing)
                offset *= revealOffset.magnitude / Mathf.Max(0.01f, playOffset.magnitude);
            return offset;
        }

        private Vector3 GetFocus()
        {
            Vector3 focus = target.position + Vector3.up * focusHeight;
            if (usePolishedControls)
            {
                Vector3 heading = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
                focus += heading * lookAheadDistance;
            }
            return focus;
        }

        private Vector3 ResolveObstruction(Vector3 focus, Vector3 desiredPosition)
        {
            Vector3 offset = desiredPosition - focus;
            float distance = offset.magnitude;
            if (distance <= 0f) return desiredPosition;

            Vector3 direction = offset / distance;
            float nearest = GetClearDistance(focus, direction, distance);
            if (nearest >= distance)
            {
                _hasLastObstructionDirection = false;
                return desiredPosition;
            }

            float bestDistance = nearest;
            Vector3 bestDirection = direction;
            float bestScore = ScoreObstructionBearing(direction, nearest, direction);
            for (int step = 1; step <= ObstructionYawSteps; step++)
            {
                float yawOffset = step * ObstructionAngleStep;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 horizontalDirection =
                        Quaternion.AngleAxis(yawOffset * side, Vector3.up) * direction;
                    for (int elevationStep = 0; elevationStep <= ObstructionElevationSteps; elevationStep++)
                    {
                        Vector3 candidateRight = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
                        Vector3 candidateDirection = elevationStep == 0
                            ? horizontalDirection
                            : Quaternion.AngleAxis(-elevationStep * ObstructionElevationStep,
                                candidateRight) * horizontalDirection;
                        float candidateDistance = GetClearDistance(focus, candidateDirection, distance);
                        float candidateScore = ScoreObstructionBearing(candidateDirection,
                            candidateDistance, direction);
                        if (candidateScore > bestScore + 0.01f)
                        {
                            bestDistance = candidateDistance;
                            bestDirection = candidateDirection;
                            bestScore = candidateScore;
                        }
                    }
                }
            }

            _lastObstructionDirection = bestDirection;
            _hasLastObstructionDirection = true;
            return focus + bestDirection * Mathf.Max(0.25f, bestDistance);
        }

        private float ScoreObstructionBearing(Vector3 candidateDirection, float clearDistance,
            Vector3 desiredDirection)
        {
            float score = clearDistance
                - Vector3.Angle(desiredDirection, candidateDirection) * 0.018f;
            if (_hasLastObstructionDirection)
                score -= Vector3.Angle(_lastObstructionDirection, candidateDirection) * 0.012f;
            return score;
        }

        private float GetClearDistance(Vector3 origin, Vector3 direction, float distance)
        {
            float nearest = distance;
            int hitCount = Physics.SphereCastNonAlloc(origin, collisionRadius, direction,
                _obstructionHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hitCount == _obstructionHits.Length)
            {
                RaycastHit[] allHits = Physics.SphereCastAll(origin, collisionRadius, direction,
                    distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in allHits)
                    nearest = GetClearDistanceForHit(hit, nearest);
                return nearest;
            }

            for (int i = 0; i < hitCount; i++)
                nearest = GetClearDistanceForHit(_obstructionHits[i], nearest);
            return nearest;
        }

        private float GetClearDistanceForHit(RaycastHit hit, float nearest)
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == target || hitTransform.IsChildOf(target)) return nearest;
            return Mathf.Min(nearest, hit.distance - collisionPadding);
        }
    }
}
