using UnityEngine;
using UnityEngine.InputSystem;

namespace Wispmere
{
    /// <summary>
    /// Direct third-person movement using the WispmereInput asset.
    /// Arrival walking is handled separately by GameManager.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterCustomizer))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Wiring")]
        public InputActionAsset inputAsset;

        [Header("Tuning")]
        public float moveSpeed = 4.6f;
        public float turnSpeed = 12f;

        public bool InputLocked { get; set; }
        public Vector3 MoveDir { get; private set; }
        public InputAction CameraLookAction { get { return _cameraLook; } }

        private CharacterController _body;
        private CharacterCustomizer _look;
        private InputAction _move;
        private InputAction _cameraLook;
        private InputAction _interact;
        private InputAction _pause;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _look = GetComponent<CharacterCustomizer>();
            if (inputAsset == null)
            {
                Debug.LogError("[Wispmere] PlayerController needs the WispmereInput asset.", this);
                enabled = false;
                return;
            }

            InputActionMap map = inputAsset.FindActionMap("Player");
            if (map == null)
            {
                Debug.LogError("[Wispmere] WispmereInput is missing its Player action map.", this);
                enabled = false;
                return;
            }

            _move = map.FindAction("Move");
            _cameraLook = map.FindAction("Look");
            _interact = map.FindAction("Interact");
            _pause = map.FindAction("Pause");
            if (_move == null || _cameraLook == null || _interact == null || _pause == null)
            {
                Debug.LogError("[Wispmere] WispmereInput is missing a required player action.", this);
                enabled = false;
                return;
            }
            _interact.performed += OnInteract;
            _pause.performed += OnPause;
        }

        private void OnEnable()
        {
            if (_move == null) return;
            _move.Enable();
            _cameraLook.Enable();
            _interact.Enable();
            _pause.Enable();
        }

        private void OnDisable()
        {
            if (_move == null) return;
            _move.Disable();
            _cameraLook.Disable();
            _interact.Disable();
            _pause.Disable();
        }

        private void OnDestroy()
        {
            if (_interact != null) _interact.performed -= OnInteract;
            if (_pause != null) _pause.performed -= OnPause;
        }

        private void Update()
        {
            GameManager manager = GameManager.Instance;
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool inventoryPressed = gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
            inventoryPressed |= keyboard != null
                && (keyboard.iKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame);
            if (manager != null && inventoryPressed && manager.hud != null)
            {
                manager.hud.ToggleInventory();
            }

            bool cancelPressed = gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            if (manager != null && manager.IsPlacementActive)
            {
                if (cancelPressed)
                {
                    manager.HandleCancelInput();
                    MoveDir = Vector3.zero;
                    _body.SimpleMove(Vector3.zero);
                    return;
                }
                Vector2 placementMove = _move.ReadValue<Vector2>();
                bool confirm = (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
                    || (keyboard != null && keyboard.enterKey.wasPressedThisFrame);
                float rotate = 0f;
                if (gamepad != null)
                    rotate += (gamepad.rightShoulder.isPressed ? 1f : 0f)
                        - (gamepad.leftShoulder.isPressed ? 1f : 0f);
                if (keyboard != null)
                    rotate += (keyboard.eKey.isPressed ? 1f : 0f)
                        - (keyboard.qKey.isPressed ? 1f : 0f);
                manager.UpdatePlacement(placementMove, confirm, cancelPressed, rotate);
                MoveDir = Vector3.zero;
                _body.SimpleMove(Vector3.zero);
                return;
            }
            if (cancelPressed && manager != null)
            {
                bool wasInputLocked = InputLocked;
                manager.HandleCancelInput();
                if (wasInputLocked)
                {
                    MoveDir = Vector3.zero;
                    _body.SimpleMove(Vector3.zero);
                    return;
                }
            }

            Vector2 input = InputLocked ? Vector2.zero : _move.ReadValue<Vector2>();
            Transform view = Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = view == null
                ? Vector3.back
                : Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = view == null
                ? Vector3.right
                : Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
            Vector3 direction = right * input.x + forward * input.y;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            MoveDir = direction;
            _body.SimpleMove(direction * moveSpeed);

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion facing = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, facing,
                    turnSpeed * Time.deltaTime);
            }
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            if (GameManager.Instance != null) GameManager.Instance.TryInteract();
        }

        private void OnPause(InputAction.CallbackContext context)
        {
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
        }

        /// <summary>Nearest interactable or ripe resource node in proximity.</summary>
        public object FindTarget()
        {
            object best = null;
            float bestDistance = float.MaxValue;
            float bestFacing = float.MinValue;
            Vector3 playerPosition = transform.position;
            Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            foreach (Interactable interactable in Interactable.All)
            {
                if (interactable == null) continue;
                ConsiderTarget(interactable, interactable.transform.position,
                    interactable.radius, ref best, ref bestDistance, ref bestFacing, facing,
                    playerPosition);
            }
            foreach (ResourceNode node in ResourceNode.All)
            {
                if (node == null || !node.IsRipe) continue;
                ConsiderTarget(node, node.transform.position, node.interactionRadius,
                    ref best, ref bestDistance, ref bestFacing, facing, playerPosition);
            }
            return best;
        }

        private static void ConsiderTarget(object candidate, Vector3 position, float radius,
            ref object best, ref float bestDistance, ref float bestFacing, Vector3 facing,
            Vector3 playerPosition)
        {
            Vector3 toTarget = position - playerPosition;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance >= radius) return;

            float facingScore = distance > 0.001f
                ? Vector3.Dot(facing, toTarget / distance)
                : 1f;
            bool closer = distance < bestDistance - 0.25f;
            bool nearTie = Mathf.Abs(distance - bestDistance) <= 0.25f;
            if (!closer && !(nearTie && facingScore > bestFacing)) return;

            best = candidate;
            bestDistance = distance;
            bestFacing = facingScore;
        }

        public void ApplyAppearance(Appearance appearance)
        {
            _look.Apply(appearance);
        }

        /// <summary>Arrival sequence movement toward the town square.</summary>
        public bool AutoWalkToward(Vector3 goal, float speedScale = 0.75f)
        {
            Vector3 to = goal - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.4f) return true;
            _body.SimpleMove(to.normalized * (moveSpeed * speedScale));
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to.normalized), turnSpeed * Time.deltaTime);
            return false;
        }
    }
}
