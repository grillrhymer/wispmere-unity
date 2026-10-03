using UnityEngine;
using UnityEngine.UI;

namespace Wispmere
{
    /// <summary>
    /// Resource counters, objective banner, interaction prompt, and inventory.
    /// The generator skins these with small parchment-toned HUD plates.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Wiring")]
        public Text resourcesText;
        public Text objectiveText;
        public Button promptButton;
        public Text promptText;
        public Button inventoryButton;
        public GameObject inventoryPanel;
        public Button inventoryCloseButton;
        public Text[] inventoryQuantityTexts;
        public Button axeCraftButton;
        public Button pickaxeCraftButton;
        public Text craftingFeedbackText;

        private bool _subscribed;

        private void OnEnable()
        {
            if (promptButton != null)
            {
                promptButton.onClick.RemoveAllListeners();
                promptButton.onClick.AddListener(() => GameManager.Instance.TryInteract());
                promptButton.gameObject.SetActive(false);
            }
            if (inventoryButton != null)
            {
                inventoryButton.onClick.RemoveListener(ToggleInventory);
                inventoryButton.onClick.AddListener(ToggleInventory);
            }
            if (inventoryCloseButton != null)
            {
                inventoryCloseButton.onClick.RemoveListener(CloseInventory);
                inventoryCloseButton.onClick.AddListener(CloseInventory);
            }
            if (axeCraftButton != null)
            {
                axeCraftButton.onClick.RemoveListener(CraftAxe);
                axeCraftButton.onClick.AddListener(CraftAxe);
            }
            if (pickaxeCraftButton != null)
            {
                pickaxeCraftButton.onClick.RemoveListener(CraftPickaxe);
                pickaxeCraftButton.onClick.AddListener(CraftPickaxe);
            }
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (objectiveText != null) objectiveText.gameObject.SetActive(false);
            SubscribeToResources();
        }

        private void Start()
        {
            if (ResourceManager.Instance == null)
            {
                Debug.LogError("[Wispmere] GameHUD requires a ResourceManager in the scene.", this);
                return;
            }
            SubscribeToResources();
            RefreshAll();
        }

        private void OnDisable()
        {
            if (_subscribed && ResourceManager.Instance != null)
            {
                ResourceManager.Instance.OnChanged -= OnResourceChanged;
                ResourceManager.Instance.OnToolsChanged -= RefreshInventory;
            }
            _subscribed = false;
        }

        private void SubscribeToResources()
        {
            if (_subscribed || ResourceManager.Instance == null) return;
            ResourceManager.Instance.OnChanged += OnResourceChanged;
            ResourceManager.Instance.OnToolsChanged += RefreshInventory;
            _subscribed = true;
        }

        private void OnResourceChanged(string kind, int count)
        {
            var rm = ResourceManager.Instance;
            if (resourcesText != null)
            {
                resourcesText.text = string.Format("WOOD: {0}    STONE: {1}    FIBER: {2}    ORE: {3}",
                    rm.Get("wood"), rm.Get("stone"), rm.Get("fiber"), rm.Get("ore"));
            }
            RefreshInventory();
        }

        public void RefreshAll()
        {
            OnResourceChanged("", 0);
        }

        public void ShowObjective(string text)
        {
            objectiveText.text = "OBJECTIVE  ·  " + text.ToUpperInvariant();
            objectiveText.gameObject.SetActive(true);
        }

        public void ShowPrompt(string label)
        {
            promptText.text = label;
            promptButton.gameObject.SetActive(true);
        }

        public void HidePrompt()
        {
            promptButton.gameObject.SetActive(false);
        }

        public void ToggleInventory()
        {
            inventoryPanel.SetActive(!inventoryPanel.activeSelf);
            if (inventoryPanel.activeSelf) RefreshInventory();
        }

        public void CloseInventory()
        {
            inventoryPanel.SetActive(false);
        }

        public void CraftAxe()
        {
            CraftTool(GatherTool.Axe);
        }

        public void CraftPickaxe()
        {
            CraftTool(GatherTool.Pickaxe);
        }

        public void ShowCraftingFeedback(string message)
        {
            craftingFeedbackText.text = message;
        }

        private void CraftTool(GatherTool tool)
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[Wispmere] Cannot craft a tool without the active GameManager.", this);
                return;
            }
            GameManager.Instance.TryCraftTool(tool);
        }

        private void RefreshInventory()
        {
            ResourceManager rm = ResourceManager.Instance;
            if (rm == null || inventoryQuantityTexts == null) return;

            string[] kinds = { "wood", "stone", "fiber", "ore" };
            for (int i = 0; i < inventoryQuantityTexts.Length && i < kinds.Length; i++)
            {
                if (inventoryQuantityTexts[i] != null)
                    inventoryQuantityTexts[i].text = rm.Get(kinds[i]).ToString();
            }

            RefreshCraftButton(axeCraftButton, GatherTool.Axe, rm);
            RefreshCraftButton(pickaxeCraftButton, GatherTool.Pickaxe, rm);
        }

        private static void RefreshCraftButton(Button button, GatherTool tool, ResourceManager rm)
        {
            if (button == null) return;
            bool owned = rm.HasTool(tool);
            button.interactable = !owned && rm.CanCraftTool(tool);
            Text buttonText = button.GetComponentInChildren<Text>();
            if (buttonText != null) buttonText.text = owned ? "Owned" : "Craft";
        }
    }
}
