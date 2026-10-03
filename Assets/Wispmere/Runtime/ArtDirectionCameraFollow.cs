using UnityEngine;

namespace Wispmere
{
    public class ArtDirectionCameraFollow : MonoBehaviour
    {
        public Transform target;
        public float distance = 24f;
        [Range(35f, 50f)] public float pitch = 43f;
        [Range(0f, 60f)] public float yawOffset = 28f;
        public float focusHeight = 1.35f;
        public float lookAheadDistance = 2f;
        public float positionSmoothTime = 0.22f;
        public float rotationSharpness = 10f;
        public float collisionRadius = 0.3f;
        public float collisionPadding = 0.2f;
        public float minimumDistance = 4f;

        private Vector3 _positionVelocity;
        private bool _initialized;
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                Debug.LogError("[Wispmere] Art direction camera requires a target.", this);
                enabled = false;
                return;
            }

            Vector3 pivot = GetPivot();
            Vector3 desiredPosition = GetCameraPosition(pivot);
            desiredPosition = ResolveObstruction(pivot, desiredPosition);

            if (!_initialized)
            {
                transform.position = desiredPosition;
                _initialized = true;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, desiredPosition, ref _positionVelocity, positionSmoothTime);
            }

            Quaternion desiredRotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                Debug.LogError("[Wispmere] Art direction camera requires a target.", this);
                return;
            }

            Vector3 pivot = GetPivot();
            transform.position = ResolveObstruction(pivot, GetCameraPosition(pivot));
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
            _positionVelocity = Vector3.zero;
            _initialized = true;
        }

        private Vector3 GetPivot()
        {
            return target.position + target.forward * lookAheadDistance + Vector3.up * focusHeight;
        }

        private Vector3 GetCameraPosition(Vector3 pivot)
        {
            Quaternion orbit = Quaternion.Euler(0f, target.eulerAngles.y + yawOffset, 0f);
            Vector3 behind = orbit * Vector3.back;
            float horizontalDistance = distance * Mathf.Cos(pitch * Mathf.Deg2Rad);
            float height = distance * Mathf.Sin(pitch * Mathf.Deg2Rad);
            return pivot + behind * horizontalDistance + Vector3.up * height;
        }

        private Vector3 ResolveObstruction(Vector3 pivot, Vector3 desiredPosition)
        {
            Vector3 offset = desiredPosition - pivot;
            float length = offset.magnitude;
            if (length <= 0f) return desiredPosition;

            Vector3 direction = offset / length;
            RaycastHit[] hits = Physics.SphereCastAll(
                pivot, collisionRadius, direction, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = length;
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == target || hitTransform.IsChildOf(target)) continue;
                nearest = Mathf.Min(nearest, hit.distance - collisionPadding);
            }

            if (nearest >= length) return desiredPosition;
            return pivot + direction * Mathf.Max(minimumDistance, nearest);
        }
    }
}
