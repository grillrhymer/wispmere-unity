using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Wispmere
{
    /// <summary>
    /// Flow: creator or continue → arrival and dialogue → explore town →
    /// inspect the Old Workshop → gather materials. Esc pauses (save + title).
    /// Arrival autopilot + dialogue locking mirror the web build exactly.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Wiring")]
        public TownBuilder town;
        public PlayerController player;
        public CameraFollow cam;
        public DialogueManager Dialogue;
        public GameHUD hud;
        public GameObject mainMenuPanel;
        public Button newGameButton;
        public Button continueButton;
        public GameObject creatorPanel;
        public GameObject pausePanel;

        [Header("Flow")]
        public GameObject playerPrefab;

        [Header("Arrival (spawn to the central square)")]
        public float arriveStopDistance = 2.6f;
        public float arrivalWalkSpeedScale = 1.2f;

        private enum Phase { Idle, Walkin, Reveal, Free }
        private Phase _phase = Phase.Idle;
        private Vector3 _square;
        private bool _paused;
        private bool _inventoryOpen;
        private bool _workbenchOpen;
        private bool _placementActive;
        private string _placementItem;
        private string _placementLabel;
        private GameObject _placementPreview;
        private bool _placementValid;
        private Vector3 _placementPosition;
        private float _placementYaw;
        private Transform _activeWorkbench;
        private bool _workshopDiscovered;

        private const string Line1 = "Wispmere. The streets are quiet, but the lights could be lit again.";
        private const string Line2 = "Someone left more than empty houses behind. I'll find out why—and help this town find its way back.";
        private const string Objective = "Explore the town";
        private const string WorkshopObjective = "Gather materials for the Old Workshop";

        public bool CanOpenInventory
        {
            get { return _phase == Phase.Free && !_paused && !_workbenchOpen && !_placementActive; }
        }
        public bool IsPlacementActive { get { return _placementActive; } }
        public bool PlacementIsValid { get { return _placementValid; } }
        public bool IsWorkbenchOpen { get { return _workbenchOpen; } }
        public bool CanControlCamera
        {
            get { return _phase == Phase.Free && !_paused && !_inventoryOpen && !_workbenchOpen; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (Dialogue != null)
            {
                Dialogue.OnQueueOpened += OnDialogueOpened;
                Dialogue.OnQueueEmpty += OnDialogueClosed;
            }
        }

        private void Start()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OpenCreator);
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (player != null)
            {
                player.InputLocked = true;
                player.gameObject.SetActive(false);
            }
            ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (Dialogue != null)
            {
                Dialogue.OnQueueOpened -= OnDialogueOpened;
                Dialogue.OnQueueEmpty -= OnDialogueClosed;
            }
        }

        private void Update()
        {
            Gamepad gamepad = Gamepad.current;
            if (_phase == Phase.Idle && gamepad != null
                && gamepad.buttonEast.wasPressedThisFrame)
            {
                HandleCancelInput();
                return;
            }
            if (_phase != Phase.Free || player == null) return;
            if (_inventoryOpen || _workbenchOpen || _placementActive)
            {
                hud.HidePrompt();
                return;
            }

            object target = player.FindTarget();
            if (target is Interactable)
            {
                Interactable interactable = (Interactable)target;
                hud.ShowPrompt(interactable.isWorkbench
                    ? "A  Use " + interactable.label
                    : "A  Talk / Inspect " + interactable.label);
            }
            else if (target is ResourceNode)
            {
                var node = (ResourceNode)target;
                string action = node.kind == "ore" ? "Mine "
                    : node.kind == "hardwood" ? "Chop "
                    : node.kind == "wood" ? "Gather " : "Collect ";
                string requirement = node.requiredTool != GatherTool.None
                    && !ResourceManager.Instance.HasTool(node.requiredTool)
                    ? " (Requires " + node.requiredTool + ")"
                    : "";
                hud.ShowPrompt("A  " + action + node.label + requirement);
            }
            else
                hud.HidePrompt();
        }

        // ----- entries -----

        public void OpenCreator()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (creatorPanel != null) creatorPanel.SetActive(true);
            SelectFirstInteractable(creatorPanel);
        }

        public void BeginAdventure(string playerName, Appearance appearance)
        {
            var data = new SaveSystem.SaveData
            {
                player = playerName,
                appearance = appearance,
                createdAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            data.resources["wood"] = 0;
            data.resources["stone"] = 0;
            data.resources["fiber"] = 0;
            data.resources["ore"] = 0;
            data.hasAxe = false;
            data.hasPickaxe = false;
            data.workshopDiscovered = false;
            SaveSystem.Save(data);
            EnterWorld(data, fromArrival: true);
        }

        public void Continue()
        {
            var data = SaveSystem.Load();
            if (data == null)
            {
                Debug.LogWarning("[Wispmere] No readable save is available to continue.");
                return;
            }
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            EnterWorld(data, fromArrival: !data.hasSavedPosition);
        }

        private void EnterWorld(SaveSystem.SaveData data, bool fromArrival)
        {
            Time.timeScale = 1f;
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (creatorPanel != null) creatorPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            _paused = false;
            _inventoryOpen = false;
            CloseWorkbench();
            CancelPlacement();
            if (hud != null && hud.inventoryPanel != null) hud.inventoryPanel.SetActive(false);
            EnsurePlayer(data);
            _workshopDiscovered = data.workshopDiscovered;
            player.InputLocked = fromArrival;
            ResourceManager.Instance.LoadInto(data.resources);
            ResourceManager.Instance.SetToolOwnership(data.hasAxe, data.hasPickaxe);
            RestoreResourceNodeStates(data.resourceNodes);
            RestorePlacedObjects(data.placedObjects);
            hud.RefreshAll();

            if (fromArrival)
            {
                Vector3 spawn = WorldLayout.ToUnity(town.Layout.spawn.x, town.Layout.spawn.y);
                player.transform.position = spawn;
                _square = WorldLayout.ToUnity(town.Layout.square.x, town.Layout.square.y);
                cam.SnapToTarget();
                StartCoroutine(ArrivalRoutine(data.player));
            }
            else
            {
                player.transform.position = data.pos;
                _phase = Phase.Free;
                cam.SnapToTarget();
                hud.ShowObjective(_workshopDiscovered ? WorkshopObjective : Objective);
            }
        }

        private void EnsurePlayer(SaveSystem.SaveData data)
        {
            if (player != null)
            {
                player.ApplyAppearance(data.appearance);
                player.gameObject.SetActive(true);
                return;
            }
            var go = Instantiate(playerPrefab);
            go.name = "Player";
            player = go.GetComponent<PlayerController>();
            player.ApplyAppearance(data.appearance);
            if (cam != null) cam.target = player.transform;
        }

        // ----- arrival -----

        private IEnumerator ArrivalRoutine(string playerName)
        {
            _phase = Phase.Walkin;
            player.InputLocked = true;
            yield return null;
            while (!player.AutoWalkToward(_square, arrivalWalkSpeedScale))
                yield return null;

            _phase = Phase.Reveal;
            cam.SetReveal(true);
            yield return new WaitForSeconds(1.6f);

            bool done = false;
            System.Action onEmpty = () => done = true;
            Dialogue.OnQueueEmpty += onEmpty;
            Dialogue.Show(playerName, Line1);
            Dialogue.Show(playerName, Line2);
            while (!done) yield return null;
            Dialogue.OnQueueEmpty -= onEmpty;

            cam.SetReveal(false);
            _phase = Phase.Free;
            player.InputLocked = false;
            hud.ShowObjective(_workshopDiscovered ? WorkshopObjective : Objective);
            SaveGame();
        }

        // ----- interaction / pause / save -----

        public void DiscoverRestorationTarget(Interactable target)
        {
            if (target == null || !target.isRestorationTarget || _workshopDiscovered) return;
            _workshopDiscovered = true;
            hud.ShowObjective(WorkshopObjective);
            SaveGame();
        }

        public void TryInteract()
        {
            if (_paused || _inventoryOpen || _workbenchOpen || _placementActive || player == null) return;
            if (Dialogue.IsOpen) { Dialogue.Advance(); return; }
            if (_phase != Phase.Free) return;
            object target = player.FindTarget();
            if (target is Interactable) ((Interactable)target).Interact();
            else if (target is ResourceNode) ((ResourceNode)target).Interact();
        }

        public void HandleCancelInput()
        {
            if (_placementActive)
            {
                CancelPlacement();
                return;
            }
            if (_workbenchOpen)
            {
                CloseWorkbench();
                return;
            }
            if (_inventoryOpen)
            {
                hud.CloseInventory();
                return;
            }
            if (_paused)
            {
                Resume();
                return;
            }
            if (Dialogue != null && Dialogue.IsOpen)
            {
                Dialogue.Advance();
                return;
            }
            if (creatorPanel != null && creatorPanel.activeSelf)
            {
                creatorPanel.SetActive(false);
                if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
                SelectFirstInteractable(mainMenuPanel);
            }
        }

        public bool TryCraftTool(GatherTool tool)
        {
            if (ResourceManager.Instance == null)
            {
                Debug.LogError("[Wispmere] Cannot craft a tool without a ResourceManager.", this);
                return false;
            }
            if (!ResourceManager.Instance.TryCraftTool(tool))
            {
                hud.ShowCraftingFeedback(ResourceManager.Instance.HasTool(tool)
                    ? tool + " is already owned."
                    : "Need 1 Wood and 1 Stone to craft this tool.");
                return false;
            }

            hud.ShowCraftingFeedback(tool + " crafted and added to your belongings.");
            SaveGame();
            return true;
        }

        public bool TryCraftItem(string item, Dictionary<string, int> costs)
        {
            if (ResourceManager.Instance == null)
            {
                Debug.LogError("[Wispmere] Cannot craft an item without a ResourceManager.", this);
                return false;
            }
            if (!ResourceManager.Instance.TryCraftItem(item, costs))
            {
                hud.ShowCraftingFeedback("Not enough materials to craft " + item + ".");
                return false;
            }

            hud.ShowCraftingFeedback(item.Replace('_', ' ') + " crafted and added to your inventory.");
            SaveGame();
            return true;
        }

        public bool TryCraftWorkbenchItem(string item, Dictionary<string, int> costs)
        {
            if (!_workbenchOpen || _activeWorkbench == null)
            {
                Debug.LogError("[Wispmere] Advanced recipes can only be crafted at an open Workbench.", this);
                return false;
            }
            return TryCraftItem(item, costs);
        }

        public void OpenWorkbench(Transform station)
        {
            if (station == null || _phase != Phase.Free || _paused || _inventoryOpen || _placementActive)
                return;
            _activeWorkbench = station;
            _workbenchOpen = true;
            player.InputLocked = true;
            hud.OpenWorkbench();
        }

        public void CloseWorkbench()
        {
            _workbenchOpen = false;
            _activeWorkbench = null;
            if (hud != null) hud.CloseWorkbenchPanel();
            UpdatePlayerInputLock();
        }

        public bool StartPlacement(string item, string label)
        {
            if (_phase != Phase.Free || _paused || _workbenchOpen || _placementActive
                || ResourceManager.Instance == null || ResourceManager.Instance.Get(item) < 1)
                return false;
            if (item != "workbench" && item != "tent")
            {
                Debug.LogError("[Wispmere] Placement is only supported for Workbench and Tent.", this);
                return false;
            }

            if (_inventoryOpen)
            {
                _inventoryOpen = false;
                if (hud.inventoryPanel != null) hud.inventoryPanel.SetActive(false);
            }
            _placementItem = item;
            _placementLabel = label;
            _placementActive = true;
            _placementValid = false;
            _placementYaw = 0f;
            _placementPreview = PlacedWorldObject.CreatePreview(item, transform);
            if (_placementPreview == null)
            {
                _placementActive = false;
                UpdatePlayerInputLock();
                Debug.LogError("[Wispmere] Could not create the placement preview for " + item + ".", this);
                return false;
            }
            player.InputLocked = true;
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            _placementPosition = player.transform.position + forward * 3f;
            _placementPosition.y = 0f;
            if (_placementPreview != null)
            {
                _placementPreview.transform.SetPositionAndRotation(_placementPosition,
                    Quaternion.Euler(0f, _placementYaw, 0f));
            }
            EventSystem.current?.SetSelectedGameObject(null);
            hud.ShowPlacementStatus("Move with Left Stick, A to place, B to cancel, LB/RB to rotate.");
            return true;
        }

        public void UpdatePlacement(Vector2 moveInput, bool confirm, bool cancel, float yawInput)
        {
            if (!_placementActive) return;
            if (cancel)
            {
                CancelPlacement();
                return;
            }

            _placementYaw = Mathf.Repeat(_placementYaw + yawInput * 90f * Time.unscaledDeltaTime, 360f);
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(player.transform.right, Vector3.up).normalized;
            Vector3 movement = right * moveInput.x + forward * moveInput.y;
            if (movement.sqrMagnitude > 1f) movement.Normalize();
            _placementPosition += movement * (4f * Time.unscaledDeltaTime);
            _placementPosition.y = 0f;
            _placementValid = IsInsideWorldBounds(_placementPosition)
                && Vector3.Distance(player.transform.position, _placementPosition) >= 1.8f
                && !OverlapsWorldObject(_placementPosition, _placementItem, _placementYaw);

            if (_placementPreview != null)
            {
                _placementPreview.SetActive(true);
                _placementPreview.transform.SetPositionAndRotation(_placementPosition,
                    Quaternion.Euler(0f, _placementYaw, 0f));
            }
            PlacedWorldObject.SetPreviewValidity(_placementPreview, _placementValid);

            hud.ShowPlacementStatus(_placementValid
                ? "Valid " + _placementLabel + " site — A to place, LB/RB to rotate, B to cancel."
                : "Invalid site — move with Left Stick; avoid the player and structures.",
                _placementValid);
            if (confirm)
            {
                if (_placementValid) ConfirmPlacement();
                else hud.ShowPlacementStatus("Invalid placement. Move to a clear patch of ground, or Esc to cancel.", false);
            }
        }

        private bool IsInsideWorldBounds(Vector3 position)
        {
            WorldLayout.LayoutData layout = town.Layout;
            Vector3 half = PlacedWorldObject.FootprintHalfExtents(_placementItem);
            Vector2 center = WorldLayout.ToLayout(position);
            return center.x - half.x / WorldLayout.Scale >= 0f
                && center.x + half.x / WorldLayout.Scale <= layout.layoutPx.w
                && center.y - half.z / WorldLayout.Scale >= 0f
                && center.y + half.z / WorldLayout.Scale <= layout.layoutPx.h;
        }

        private bool OverlapsWorldObject(Vector3 position, string item, float yaw)
        {
            Vector3 half = PlacedWorldObject.FootprintHalfExtents(item);
            Collider[] hits = Physics.OverlapBox(position + Vector3.up * half.y,
                half, Quaternion.Euler(0f, yaw, 0f), Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (hit == null || hit.gameObject.name == "Ground") continue;
                if (hit.transform == player.transform || hit.transform.IsChildOf(player.transform))
                    continue;
                if (hit.GetComponentInParent<PlacedWorldObject>() != null) return true;
                Transform townRoot = town.transform.Find("TownRoot");
                if (hit.GetComponentInParent<TownBuilder>() != null
                    || (townRoot != null && hit.transform.IsChildOf(townRoot)))
                    return true;
            }
            return false;
        }

        private void ConfirmPlacement()
        {
            PlacedWorldObject placed = PlacedWorldObject.Create(_placementItem, _placementPosition,
                _placementYaw, town.transform.Find("TownRoot"));
            if (!ResourceManager.Instance.TryConsumeItem(_placementItem, 1))
            {
                Destroy(placed.gameObject);
                hud.ShowPlacementStatus("You no longer have a " + _placementLabel + " to place.", false);
                CancelPlacement();
                return;
            }

            FinishPlacement();
            SaveGame();
        }

        public void CancelPlacement()
        {
            if (!_placementActive) return;
            FinishPlacement();
        }

        private void FinishPlacement()
        {
            PlacedWorldObject.DestroyPreview(_placementPreview);
            _placementPreview = null;
            _placementActive = false;
            _placementValid = false;
            UpdatePlayerInputLock();
            if (hud != null) hud.HidePlacementStatus();
            _placementItem = "";
            _placementLabel = "";
        }

        private void RestorePlacedObjects(List<SaveSystem.SaveData.PlacedObjectData> savedObjects)
        {
            PlacedWorldObject[] existingObjects = FindObjectsByType<PlacedWorldObject>(
                FindObjectsSortMode.None);
            HashSet<PlacedWorldObject> retained = new HashSet<PlacedWorldObject>();
            Transform parent = town.transform.Find("TownRoot");
            if (savedObjects != null)
            {
                foreach (SaveSystem.SaveData.PlacedObjectData saved in savedObjects)
                {
                    if (saved == null || (saved.kind != "workbench" && saved.kind != "tent"))
                    {
                        Debug.LogWarning("[Wispmere] Skipping an unsupported saved placed object.");
                        continue;
                    }

                    PlacedWorldObject match = null;
                    foreach (PlacedWorldObject existing in existingObjects)
                    {
                        if (existing == null || retained.Contains(existing) || existing.kind != saved.kind)
                            continue;
                        if (Vector3.Distance(existing.transform.position, saved.position) < 0.01f
                            && Mathf.Abs(Mathf.DeltaAngle(existing.transform.eulerAngles.y, saved.yaw)) < 0.1f)
                        {
                            match = existing;
                            break;
                        }
                    }
                    if (match == null)
                        match = PlacedWorldObject.Create(saved.kind, saved.position, saved.yaw, parent);
                    retained.Add(match);
                }
            }

            foreach (PlacedWorldObject existing in existingObjects)
                if (existing != null && !retained.Contains(existing))
                    Destroy(existing.gameObject);
        }

        private static void RestoreResourceNodeStates(
            List<SaveSystem.SaveData.ResourceNodeState> savedStates)
        {
            if (savedStates == null || savedStates.Count == 0) return;
            Dictionary<string, long> regrowTimes = new Dictionary<string, long>();
            foreach (SaveSystem.SaveData.ResourceNodeState saved in savedStates)
            {
                if (saved == null || string.IsNullOrEmpty(saved.id)
                    || saved.regrowsAtUtcTicks <= 0L)
                {
                    Debug.LogWarning("[Wispmere] Skipping an invalid saved resource-node state.");
                    continue;
                }
                regrowTimes[saved.id] = saved.regrowsAtUtcTicks;
            }

            foreach (ResourceNode node in ResourceNode.All)
            {
                if (node == null) continue;
                if (regrowTimes.TryGetValue(node.GetPersistenceId(), out long regrowsAtUtcTicks))
                    node.RestoreDepletion(regrowsAtUtcTicks);
            }
        }

        private void UpdatePlayerInputLock()
        {
            if (player != null)
                player.InputLocked = _inventoryOpen || _workbenchOpen || _placementActive
                    || _paused || Dialogue.IsOpen || _phase != Phase.Free;
        }

        public void SetInventoryOpen(bool open)
        {
            if (open && !CanOpenInventory) return;
            _inventoryOpen = open;
            UpdatePlayerInputLock();
        }

        public void TogglePause()
        {
            if (_phase != Phase.Free) return;
            if (_placementActive)
            {
                CancelPlacement();
                return;
            }
            if (_workbenchOpen)
            {
                CloseWorkbench();
                return;
            }
            if (_inventoryOpen)
            {
                hud.CloseInventory();
                return;
            }
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f;
            player.InputLocked = _paused || Dialogue.IsOpen;
            if (pausePanel != null)
            {
                pausePanel.SetActive(_paused);
                if (_paused) SelectFirstInteractable(pausePanel);
                else EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        public void Resume()
        {
            _paused = false;
            _inventoryOpen = false;
            Time.timeScale = 1f;
            UpdatePlayerInputLock();
            if (pausePanel != null) pausePanel.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void SaveGame()
        {
            if (player == null) return;
            var data = SaveSystem.Load() ?? new SaveSystem.SaveData();
            data.resources = ResourceManager.Instance.Snapshot();
            data.hasAxe = ResourceManager.Instance.HasTool(GatherTool.Axe);
            data.hasPickaxe = ResourceManager.Instance.HasTool(GatherTool.Pickaxe);
            data.pos = player.transform.position;
            data.hasSavedPosition = true;
            data.workshopDiscovered = _workshopDiscovered;
            data.placedObjects = new List<SaveSystem.SaveData.PlacedObjectData>();
            foreach (PlacedWorldObject placed in FindObjectsByType<PlacedWorldObject>(
                FindObjectsSortMode.None))
            {
                data.placedObjects.Add(new SaveSystem.SaveData.PlacedObjectData
                {
                    kind = placed.kind,
                    position = placed.transform.position,
                    yaw = placed.transform.eulerAngles.y,
                });
            }
            data.resourceNodes = new List<SaveSystem.SaveData.ResourceNodeState>();
            foreach (ResourceNode node in ResourceNode.All)
            {
                if (node == null || node.IsRipe) continue;
                data.resourceNodes.Add(new SaveSystem.SaveData.ResourceNodeState
                {
                    id = node.GetPersistenceId(),
                    regrowsAtUtcTicks = node.RegrowsAtUtcTicks,
                });
            }
            data.scene = "town";
            SaveSystem.Save(data);
        }

        public void SaveAndTitle()
        {
            SaveGame();
            ReturnToMainMenu();
        }

        public void ReturnToMainMenu()
        {
            _paused = false;
            _inventoryOpen = false;
            CloseWorkbench();
            CancelPlacement();
            Time.timeScale = 1f;
            if (hud != null && hud.inventoryPanel != null) hud.inventoryPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (player != null)
            {
                player.InputLocked = true;
                player.gameObject.SetActive(false);
            }
            ShowMainMenu();
        }

        private void ShowMainMenu()
        {
            _phase = Phase.Idle;
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (creatorPanel != null) creatorPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (continueButton != null) continueButton.interactable = SaveSystem.HasSave();
            SelectFirstInteractable(mainMenuPanel);
        }

        private static void SelectFirstInteractable(GameObject panel)
        {
            if (panel == null || EventSystem.current == null) return;
            Selectable[] selectables = panel.GetComponentsInChildren<Selectable>(false);
            foreach (Selectable selectable in selectables)
            {
                if (selectable == null || !selectable.interactable || !selectable.gameObject.activeInHierarchy)
                    continue;
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                return;
            }
            EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnDialogueOpened()
        {
            UpdatePlayerInputLock();
        }

        private void OnDialogueClosed()
        {
            UpdatePlayerInputLock();
        }
    }
}
