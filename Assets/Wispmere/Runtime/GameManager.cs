using System.Collections;
using UnityEngine;
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
        private bool _workshopDiscovered;

        private const string Line1 = "Wispmere. The streets are quiet, but the lights could be lit again.";
        private const string Line2 = "Someone left more than empty houses behind. I'll find out why—and help this town find its way back.";
        private const string Objective = "Explore the town";
        private const string WorkshopObjective = "Gather materials for the Old Workshop";

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
            if (_phase != Phase.Free || player == null) return;

            if (!string.IsNullOrEmpty(player.MouseFeedback))
            {
                hud.ShowPrompt(player.MouseFeedback);
                return;
            }
            if (!string.IsNullOrEmpty(player.ClickTargetLabel))
            {
                hud.ShowPrompt("Moving to " + player.ClickTargetLabel + "…");
                return;
            }
            if (!string.IsNullOrEmpty(player.HoveredTargetLabel))
            {
                hud.ShowPrompt("Click: " + player.HoveredTargetLabel + "  ·  E to interact");
                return;
            }

            object target = player.FindTarget();
            if (target is Interactable)
                hud.ShowPrompt("E — " + ((Interactable)target).label);
            else if (target is ResourceNode)
            {
                var node = (ResourceNode)target;
                string action = node.kind == "ore" ? "Mine " : "Gather ";
                string requirement = node.requiredTool != GatherTool.None
                    && !ResourceManager.Instance.HasTool(node.requiredTool)
                    ? " (Requires " + node.requiredTool + ")"
                    : "";
                hud.ShowPrompt("E — " + action + node.label + requirement);
            }
            else
                hud.HidePrompt();
        }

        // ----- entries -----

        public void OpenCreator()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (creatorPanel != null) creatorPanel.SetActive(true);
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
            EnsurePlayer(data);
            _workshopDiscovered = data.workshopDiscovered;
            player.InputLocked = fromArrival;
            ResourceManager.Instance.LoadInto(data.resources);
            ResourceManager.Instance.SetToolOwnership(data.hasAxe, data.hasPickaxe);
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
            if (_paused || player == null) return;
            if (Dialogue.IsOpen) { Dialogue.Advance(); return; }
            if (_phase != Phase.Free) return;
            object target = player.FindTarget();
            if (target is Interactable) ((Interactable)target).Interact();
            else if (target is ResourceNode) ((ResourceNode)target).Interact();
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

        public void TogglePause()
        {
            if (_phase != Phase.Free) return;
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f;
            player.InputLocked = _paused || Dialogue.IsOpen;
            if (pausePanel != null) pausePanel.SetActive(_paused);
        }

        public void Resume()
        {
            _paused = false;
            Time.timeScale = 1f;
            player.InputLocked = Dialogue.IsOpen;
            if (pausePanel != null) pausePanel.SetActive(false);
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
            Time.timeScale = 1f;
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
        }

        private void OnDialogueOpened()
        {
            if (player != null) player.InputLocked = true;
        }

        private void OnDialogueClosed()
        {
            if (player != null) player.InputLocked = _paused || _phase != Phase.Free;
        }
    }
}
