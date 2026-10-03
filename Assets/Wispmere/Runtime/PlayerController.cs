using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Wispmere
{
    /// <summary>
    /// Third-person walker driven by the WispmereInput asset
    /// (WASD + arrows + gamepad stick; E / buttonSouth = interact).
    /// Movement only — arrival autopilot and dialogue locking live in
    /// GameManager. On-screen Stick (Input System) can drive the same
    /// Move action for touch with no code changes.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterCustomizer))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Wiring")]
        public InputActionAsset inputAsset;

        [Header("Tuning (matches web: 230 px/s, 86 px radius)")]
        public float moveSpeed = 4.6f;
        public float turnSpeed = 12f;
        public float interactRadius = 1.7f;

        public bool InputLocked { get; set; }
        public Vector3 MoveDir { get; private set; }

        private CharacterController _body;
        private CharacterCustomizer _look;
        private InputAction _move;
        private InputAction _interact;
        private InputAction _pause;
        private object _clickTarget;
        private Vector3 _clickDestination;
        private bool _hasClickDestination;
        private float _feedbackUntil;
        private string _mouseFeedback;
        private readonly RaycastHit[] _mouseHits = new RaycastHit[32];

        public string HoveredTargetLabel { get; private set; }
        public bool HasPendingClickMovement { get { return _hasClickDestination || _clickTarget != null; } }
        public string ClickTargetLabel
        {
            get
            {
                if (_clickTarget is ResourceNode) return ((ResourceNode)_clickTarget).label;
                if (_clickTarget is Interactable) return ((Interactable)_clickTarget).label;
                return "";
            }
        }
        public string MouseFeedback
        {
            get { return Time.unscaledTime < _feedbackUntil ? _mouseFeedback : ""; }
        }

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _look = GetComponent<CharacterCustomizer>();
            var map = inputAsset.FindActionMap("Player");
            _move = map.FindAction("Move");
            _interact = map.FindAction("Interact");
            _pause = map.FindAction("Pause");
            _interact.performed += _ => GameManager.Instance.TryInteract();
            _pause.performed += _ => GameManager.Instance.TogglePause();
        }

        private void OnEnable()
        {
            _move.Enable(); _interact.Enable(); _pause.Enable();
        }

        private void OnDisable()
        {
            _move.Disable(); _interact.Disable(); _pause.Disable();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Camera camera = Camera.main;
                if (camera != null)
                    TryHandleWorldClick(camera.ScreenPointToRay(mouse.position.ReadValue()),
                        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            }
            UpdateHover(mouse);

            Vector2 input = InputLocked ? Vector2.zero : _move.ReadValue<Vector2>();
            Transform view = Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = view == null ? Vector3.back : Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = view == null ? Vector3.right : Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
            Vector3 dir = right * input.x + forward * input.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            if (InputLocked)
            {
                _clickTarget = null;
                _hasClickDestination = false;
                dir = Vector3.zero;
            }
            else if (dir.sqrMagnitude > 0.01f)
            {
                _clickTarget = null;
                _hasClickDestination = false;
            }
            else
            {
                dir = GetClickMoveDirection();
            }
            MoveDir = dir;

            _body.SimpleMove(dir * moveSpeed);

            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion face = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, turnSpeed * Time.deltaTime);
            }
        }

        public bool TryHandleWorldClick(Ray ray, bool pointerOverUI)
        {
            if (pointerOverUI || InputLocked) return false;

            int hitCount = GetSortedMouseHits(ray);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _mouseHits[i];
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;

                ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    if (!node.IsRipe)
                    {
                        SetMouseFeedback(node.label + " is regrowing.");
                        return true;
                    }
                    _clickTarget = node;
                    _hasClickDestination = false;
                    SetMouseFeedback("Moving to " + node.label + ".");
                    return true;
                }

                Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
                if (interactable != null)
                {
                    _clickTarget = interactable;
                    _hasClickDestination = false;
                    SetMouseFeedback("Moving to " + interactable.label + ".");
                    return true;
                }

                if (hit.collider.gameObject.name == "Ground")
                {
                    _clickTarget = null;
                    _clickDestination = hit.point;
                    _hasClickDestination = true;
                    SetMouseFeedback("Moving.");
                    return true;
                }

                SetMouseFeedback("Nothing to interact with here.");
                return false;
            }

            SetMouseFeedback("No walkable ground there.");
            return false;
        }

        private Vector3 GetClickMoveDirection()
        {
            if (_clickTarget is ResourceNode)
            {
                var node = (ResourceNode)_clickTarget;
                if (!node || !node.IsRipe)
                {
                    _clickTarget = null;
                    return Vector3.zero;
                }

                Vector3 toNode = node.transform.position - transform.position;
                toNode.y = 0f;
                if (toNode.magnitude <= interactRadius)
                {
                    _clickTarget = null;
                    node.Interact();
                    return Vector3.zero;
                }
                return toNode.normalized;
            }

            if (_clickTarget is Interactable)
            {
                var interactable = (Interactable)_clickTarget;
                if (!interactable)
                {
                    _clickTarget = null;
                    return Vector3.zero;
                }

                Vector3 toInteractable = interactable.transform.position - transform.position;
                toInteractable.y = 0f;
                if (toInteractable.magnitude <= interactable.radius)
                {
                    _clickTarget = null;
                    interactable.Interact();
                    return Vector3.zero;
                }
                return toInteractable.normalized;
            }

            if (_hasClickDestination)
            {
                Vector3 toDestination = _clickDestination - transform.position;
                toDestination.y = 0f;
                if (toDestination.magnitude <= 0.35f)
                {
                    _hasClickDestination = false;
                    return Vector3.zero;
                }
                return toDestination.normalized;
            }

            return Vector3.zero;
        }

        private void UpdateHover(Mouse mouse)
        {
            HoveredTargetLabel = "";
            if (mouse == null || InputLocked
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                return;

            Camera camera = Camera.main;
            if (camera == null) return;
            int hitCount = GetSortedMouseHits(camera.ScreenPointToRay(mouse.position.ReadValue()));

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _mouseHits[i];
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;
                ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    HoveredTargetLabel = node.IsRipe ? "Gather " + node.label : node.label + " (regrowing)";
                    return;
                }
                Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
                if (interactable != null)
                {
                    HoveredTargetLabel = interactable.label;
                    return;
                }
                return;
            }
        }

        private int GetSortedMouseHits(Ray ray)
        {
            int count = Physics.RaycastNonAlloc(ray, _mouseHits, 200f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = _mouseHits[i];
                int j = i - 1;
                while (j >= 0 && _mouseHits[j].distance > hit.distance)
                {
                    _mouseHits[j + 1] = _mouseHits[j];
                    j--;
                }
                _mouseHits[j + 1] = hit;
            }
            return count;
        }

        private void SetMouseFeedback(string message)
        {
            _mouseFeedback = message;
            _feedbackUntil = Time.unscaledTime + 2f;
        }

        /// <summary>Nearest interactable or ripe node in range, like the web build.</summary>
        public object FindTarget()
        {
            object best = null;
            float bestD = float.MaxValue;

            foreach (var it in Interactable.All)
            {
                float d = Vector3.Distance(transform.position, it.transform.position);
                if (d < it.radius && d < bestD)
                {
                    best = it; bestD = d;
                }
            }
            foreach (var node in ResourceNode.All)
            {
                if (!node.IsRipe) continue;
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < interactRadius && d < bestD)
                {
                    best = node; bestD = d;
                }
            }
            return best;
        }

        public void ApplyAppearance(Appearance a)
        {
            _look.Apply(a);
        }

        /// <summary>Arrival autopilot: walk straight toward the square.</summary>
        public bool AutoWalkToward(Vector3 goal, float speedScale = 0.75f)
        {
            Vector3 to = goal - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.4f) return true;
            _body.SimpleMove(to.normalized * (moveSpeed * speedScale));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), turnSpeed * Time.deltaTime);
            return false;
        }
    }
}
