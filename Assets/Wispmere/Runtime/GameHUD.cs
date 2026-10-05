using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

namespace Wispmere
{
    /// <summary>
    /// Resource counters, objective banner, interaction prompt, and a compact
    /// resource inventory and crafting panel.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private const int InventorySlotCount = 22;

        private struct RecipeCost
        {
            public readonly string item;
            public readonly int amount;

            public RecipeCost(string item, int amount)
            {
                this.item = item;
                this.amount = amount;
            }
        }

        private sealed class CraftRecipe
        {
            public readonly string id;
            public readonly string label;
            public readonly string icon;
            public readonly GatherTool tool;
            public readonly Dictionary<string, int> costs;

            public CraftRecipe(string id, string label, string icon, GatherTool tool,
                params RecipeCost[] costs)
            {
                this.id = id;
                this.label = label;
                this.icon = icon;
                this.tool = tool;
                this.costs = new Dictionary<string, int>();
                foreach (RecipeCost cost in costs) this.costs[cost.item] = cost.amount;
            }
        }

        private sealed class WorkbenchRecipe
        {
            public readonly string id;
            public readonly string label;
            public readonly string icon;
            public readonly string description;
            public readonly Dictionary<string, int> costs;

            public WorkbenchRecipe(string id, string label, string icon, string description,
                params RecipeCost[] costs)
            {
                this.id = id;
                this.label = label;
                this.icon = icon;
                this.description = description;
                this.costs = new Dictionary<string, int>();
                foreach (RecipeCost cost in costs) this.costs[cost.item] = cost.amount;
            }
        }

        private static readonly CraftRecipe[] Recipes =
        {
            new CraftRecipe("axe", "Axe", "A", GatherTool.Axe),
            new CraftRecipe("pickaxe", "Pickaxe", "P", GatherTool.Pickaxe),
            new CraftRecipe("workbench", "Workbench", "W", GatherTool.None,
                Cost("wood", 4), Cost("stone", 2), Cost("hardwood", 1), Cost("ore", 1)),
            new CraftRecipe("campfire", "Campfire", "C", GatherTool.None,
                Cost("wood", 3), Cost("stone", 2), Cost("fiber", 1)),
            new CraftRecipe("storage_chest", "Storage Chest", "S", GatherTool.None,
                Cost("wood", 4), Cost("fiber", 2)),
            new CraftRecipe("fence", "Fence", "F", GatherTool.None,
                Cost("wood", 2), Cost("stone", 1)),
        };

        private static readonly WorkbenchRecipe[] WorkbenchRecipes =
        {
            new WorkbenchRecipe("metal_axe", "Metal Axe", "A", "An upgraded axe.",
                Cost("wood", 2), Cost("stone", 1), Cost("hardwood", 1), Cost("ore", 2)),
            new WorkbenchRecipe("metal_pickaxe", "Metal Pickaxe", "P", "An upgraded pickaxe.",
                Cost("wood", 2), Cost("stone", 1), Cost("hardwood", 1), Cost("ore", 2)),
            new WorkbenchRecipe("tent", "Tent", "T", "A simple placeable shelter.",
                Cost("wood", 3), Cost("fiber", 3), Cost("hardwood", 1)),
        };

        private static readonly string[] ResourceIds = { "wood", "stone", "fiber", "ore", "hardwood" };
        private static readonly string[] ResourceLabels = { "Wood", "Stone", "Fiber", "Ore", "Hardwood" };
        private static readonly string[] ResourceIcons = { "W", "S", "F", "O", "H" };
        private static readonly Color[] ResourceColors =
        {
            new Color(0.44f, 0.29f, 0.17f),
            new Color(0.40f, 0.45f, 0.48f),
            new Color(0.30f, 0.48f, 0.29f),
            new Color(0.63f, 0.44f, 0.26f),
            new Color(0.53f, 0.31f, 0.16f),
        };

        private static RecipeCost Cost(string item, int amount)
        {
            return new RecipeCost(item, amount);
        }

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
        public GameObject workbenchPanel;
        public Button workbenchCloseButton;
        public Button placeSelectedButton;
        public Button[] WorkbenchRecipeButtons { get { return _workbenchRecipeButtons; } }
        public Button PlaceSelectedButton { get { return placeSelectedButton; } }

        public Button[] InventorySlotButtons { get; private set; }
        public Text[] InventorySlotQuantityTexts { get; private set; }
        public Button[] CraftingRecipeButtons { get { return _recipeButtons; } }
        public int SelectedInventorySlot { get; private set; }

        private Text[] _slotNameTexts;
        private Text[] _slotIconTexts;
        private Image[] _slotIconImages;
        private Text[] _recipeCostTexts;
        private Text[] _recipeStatusTexts;
        private Button[] _recipeButtons;
        private Text[] _workbenchRecipeCosts;
        private Text[] _workbenchRecipeStatuses;
        private Button[] _workbenchRecipeButtons;
        private Text _workbenchFeedbackText;
        private Text _placementStatusText;
        private GameObject _placementStatusPanel;
        private Text _selectedItemText;
        private Canvas _canvas;
        private int _layoutWidth;
        private int _layoutHeight;
        private bool _subscribed;

        private void Awake()
        {
            BuildInventoryPanel();
            BuildWorkbenchPanel();
            BuildPlacementStatus();
        }

        private void OnEnable()
        {
            if (promptButton != null)
            {
                promptButton.onClick.RemoveAllListeners();
                promptButton.interactable = false;
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
            WireRecipeButtons();
            WireWorkbenchButtons();

            inventoryPanel.SetActive(false);
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

        private void Update()
        {
            if (_canvas == null) return;
            SyncSelectedInventorySlot();
            int width = Mathf.RoundToInt(_canvas.pixelRect.width);
            int height = Mathf.RoundToInt(_canvas.pixelRect.height);
            if (width == _layoutWidth && height == _layoutHeight) return;

            BuildInventoryPanel();
            BuildWorkbenchPanel();
            WireRecipeButtons();
            WireWorkbenchButtons();
            RefreshInventory();
            RefreshWorkbench();
            if (inventoryPanel.activeSelf) SelectInventorySlot(SelectedInventorySlot);
            if (workbenchPanel != null && workbenchPanel.activeSelf)
                SelectFirst(_workbenchRecipeButtons);
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
            ResourceManager rm = ResourceManager.Instance;
            if (resourcesText != null)
            {
                resourcesText.text = string.Format(
                    "WOOD: {0}    STONE: {1}    FIBER: {2}    ORE: {3}    HARDWOOD: {4}",
                    rm.Get("wood"), rm.Get("stone"), rm.Get("fiber"), rm.Get("ore"),
                    rm.Get("hardwood"));
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
            if (inventoryPanel.activeSelf)
            {
                CloseInventory();
                return;
            }

            GameManager manager = GameManager.Instance;
            if (manager == null || !manager.CanOpenInventory) return;
            inventoryPanel.SetActive(true);
            manager.SetInventoryOpen(true);
            RefreshInventory();
            SelectInventorySlot(SelectedInventorySlot);
        }

        public void CloseInventory()
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            ClearSelectionWithin(inventoryPanel);
            if (GameManager.Instance != null) GameManager.Instance.SetInventoryOpen(false);
        }

        public void OpenWorkbench()
        {
            if (workbenchPanel == null)
            {
                Debug.LogError("[Wispmere] Workbench crafting panel was not built.", this);
                return;
            }
            workbenchPanel.SetActive(true);
            RefreshWorkbench();
            SelectFirst(_workbenchRecipeButtons);
        }

        public void CloseWorkbenchPanel()
        {
            if (workbenchPanel != null) workbenchPanel.SetActive(false);
            ClearSelectionWithin(workbenchPanel);
        }

        private void SelectInventorySlot(int slot)
        {
            if (EventSystem.current == null || InventorySlotButtons == null
                || slot < 0 || slot >= InventorySlotButtons.Length)
                return;
            EventSystem.current.SetSelectedGameObject(InventorySlotButtons[slot].gameObject);
        }

        private static void SelectFirst(Button[] buttons)
        {
            if (EventSystem.current == null || buttons == null) return;
            foreach (Button button in buttons)
            {
                if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
                    continue;
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                return;
            }
            EventSystem.current.SetSelectedGameObject(null);
        }

        private static void ClearSelectionWithin(GameObject panel)
        {
            if (panel == null || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(panel.transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void SyncSelectedInventorySlot()
        {
            if (inventoryPanel == null || !inventoryPanel.activeSelf || InventorySlotButtons == null
                || EventSystem.current == null)
                return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return;
            for (int i = 0; i < InventorySlotButtons.Length; i++)
            {
                if (InventorySlotButtons[i] == null
                    || selected != InventorySlotButtons[i].gameObject
                    || SelectedInventorySlot == i)
                    continue;
                SelectedInventorySlot = i;
                RefreshInventory();
                return;
            }
        }

        public void ShowPlacementStatus(string message, bool valid = false)
        {
            if (_placementStatusPanel == null) return;
            _placementStatusPanel.SetActive(true);
            _placementStatusText.text = message;
            _placementStatusPanel.GetComponent<Image>().color = valid
                ? new Color(0.12f, 0.3f, 0.18f, 0.94f)
                : new Color(0.38f, 0.18f, 0.15f, 0.94f);
        }

        public void HidePlacementStatus()
        {
            if (_placementStatusPanel != null) _placementStatusPanel.SetActive(false);
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
            if (craftingFeedbackText != null) craftingFeedbackText.text = message;
            RefreshInventory();
        }

        private void CraftRecipeAt(int index)
        {
            CraftRecipe recipe = Recipes[index];
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[Wispmere] Cannot craft without the active GameManager.", this);
                return;
            }

            if (recipe.tool != GatherTool.None)
            {
                manager.TryCraftTool(recipe.tool);
                return;
            }

            manager.TryCraftItem(recipe.id, recipe.costs);
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
            if (rm == null || InventorySlotButtons == null) return;

            for (int slot = 0; slot < InventorySlotCount; slot++)
            {
                string id;
                string label;
                string icon;
                Color iconColor;
                int quantity;
                bool occupied = TryGetSlotItem(slot, rm, out id, out label, out icon, out iconColor,
                    out quantity);

                _slotNameTexts[slot].text = occupied ? label : "Empty";
                InventorySlotQuantityTexts[slot].text = occupied ? quantity.ToString() : "";
                _slotIconTexts[slot].text = occupied ? icon : "";
                _slotIconImages[slot].color = occupied
                    ? iconColor
                    : new Color(0.31f, 0.30f, 0.27f, 0.72f);
                InventorySlotButtons[slot].interactable = true;
                InventorySlotButtons[slot].GetComponent<Image>().color = slot == SelectedInventorySlot
                    ? new Color(0.78f, 0.64f, 0.40f, 1f)
                    : new Color(0.23f, 0.27f, 0.26f, 1f);
            }

            if (inventoryQuantityTexts != null)
                for (int i = 0; i < inventoryQuantityTexts.Length && i < ResourceIds.Length; i++)
                    inventoryQuantityTexts[i] = InventorySlotQuantityTexts[i];

            if (_selectedItemText != null)
            {
                string id;
                string label;
                string icon;
                Color color;
                int quantity;
                _selectedItemText.text = TryGetSlotItem(SelectedInventorySlot, rm,
                    out id, out label, out icon, out color, out quantity)
                    ? "Selected: " + label + "  × " + quantity
                    : "Select an item to view it here.";
            }

            RefreshRecipes(rm);
            RefreshWorkbench();
            RefreshPlaceSelectedButton(rm);
        }

        private static bool TryGetSlotItem(int slot, ResourceManager rm, out string id,
            out string label, out string icon, out Color color, out int quantity)
        {
            if (slot < ResourceIds.Length)
            {
                id = ResourceIds[slot];
                label = ResourceLabels[slot];
                icon = ResourceIcons[slot];
                color = ResourceColors[slot];
                quantity = rm.Get(id);
                return true;
            }

            int recipeIndex = slot - ResourceIds.Length;
            if (recipeIndex < Recipes.Length)
            {
                CraftRecipe recipe = Recipes[recipeIndex];
                bool owned = recipe.tool != GatherTool.None
                    ? rm.HasTool(recipe.tool)
                    : rm.Get(recipe.id) > 0;
                id = recipe.id;
                label = recipe.label;
                icon = recipe.icon;
                color = new Color(0.62f, 0.47f, 0.27f);
                quantity = recipe.tool != GatherTool.None ? (owned ? 1 : 0) : rm.Get(recipe.id);
                return owned;
            }

            int advancedItemIndex = recipeIndex - Recipes.Length;
            string[] advancedIds = { "metal_axe", "metal_pickaxe", "tent" };
            string[] advancedLabels = { "Metal Axe", "Metal Pickaxe", "Tent" };
            string[] advancedIcons = { "M", "M", "T" };
            if (advancedItemIndex >= 0 && advancedItemIndex < advancedIds.Length)
            {
                id = advancedIds[advancedItemIndex];
                label = advancedLabels[advancedItemIndex];
                icon = advancedIcons[advancedItemIndex];
                color = new Color(0.62f, 0.47f, 0.27f);
                quantity = rm.Get(id);
                return quantity > 0;
            }

            id = label = icon = "";
            color = Color.clear;
            quantity = 0;
            return false;
        }

        private void RefreshRecipes(ResourceManager rm)
        {
            for (int i = 0; i < Recipes.Length; i++)
            {
                CraftRecipe recipe = Recipes[i];
                bool ownedTool = recipe.tool != GatherTool.None && rm.HasTool(recipe.tool);
                Dictionary<string, int> costs = recipe.tool != GatherTool.None
                    ? rm.GetToolRecipeCosts(recipe.tool)
                    : recipe.costs;
                bool canAfford = rm.CanAfford(costs);
                _recipeCostTexts[i].text = FormatCosts(costs, rm);
                _recipeStatusTexts[i].text = ownedTool ? "Already owned"
                    : canAfford ? "Ready to craft" : "Need more materials";
                _recipeStatusTexts[i].color = ownedTool
                    ? new Color(0.54f, 0.72f, 0.48f)
                    : canAfford ? new Color(0.88f, 0.76f, 0.49f)
                    : new Color(0.92f, 0.58f, 0.48f);
                _recipeButtons[i].interactable = !ownedTool;
                Text buttonText = _recipeButtons[i].GetComponentInChildren<Text>();
                buttonText.text = ownedTool ? "Owned" : "Craft";
            }

            RefreshLegacyCraftButton(axeCraftButton, GatherTool.Axe, rm);
            RefreshLegacyCraftButton(pickaxeCraftButton, GatherTool.Pickaxe, rm);
        }

        private static string FormatCosts(Dictionary<string, int> costs, ResourceManager rm)
        {
            string result = "";
            foreach (KeyValuePair<string, int> cost in costs)
            {
                if (result.Length > 0) result += "   ·   ";
                result += DisplayName(cost.Key) + " " + rm.Get(cost.Key) + "/" + cost.Value;
            }
            return result;
        }

        private static string DisplayName(string id)
        {
            return id == "storage_chest" ? "Storage Chest"
                : char.ToUpperInvariant(id[0]) + id.Substring(1);
        }

        private static void RefreshLegacyCraftButton(Button button, GatherTool tool, ResourceManager rm)
        {
            if (button == null) return;
            bool owned = rm.HasTool(tool);
            button.interactable = !owned;
            Text buttonText = button.GetComponentInChildren<Text>();
            if (buttonText != null) buttonText.text = owned ? "Owned" : "Craft";
        }

        private void SelectSlot(int slot)
        {
            SelectedInventorySlot = slot;
            RefreshInventory();
        }

        private void RefreshPlaceSelectedButton(ResourceManager rm)
        {
            if (placeSelectedButton == null) return;
            string id;
            string label;
            string icon;
            Color color;
            int quantity;
            bool occupied = TryGetSlotItem(SelectedInventorySlot, rm,
                out id, out label, out icon, out color, out quantity);
            bool placeable = occupied && (id == "workbench" || id == "tent") && quantity > 0;
            placeSelectedButton.interactable = placeable && GameManager.Instance != null
                && GameManager.Instance.CanOpenInventory;
            Text buttonText = placeSelectedButton.GetComponentInChildren<Text>();
            buttonText.text = placeable ? "Place" : "Place item";
        }

        private void BeginPlacementForSelectedItem()
        {
            ResourceManager rm = ResourceManager.Instance;
            if (rm == null) return;
            string id;
            string label;
            string icon;
            Color color;
            int quantity;
            if (!TryGetSlotItem(SelectedInventorySlot, rm,
                out id, out label, out icon, out color, out quantity)
                || (id != "workbench" && id != "tent") || quantity < 1)
            {
                ShowCraftingFeedback("Select a Workbench or Tent to place.");
                return;
            }
            if (GameManager.Instance == null
                || !GameManager.Instance.StartPlacement(id, label))
                ShowCraftingFeedback("Could not start placement for " + label + ".");
        }

        private void WireRecipeButtons()
        {
            if (_recipeButtons == null) return;
            for (int i = 0; i < _recipeButtons.Length; i++)
            {
                int recipeIndex = i;
                _recipeButtons[i].onClick.RemoveAllListeners();
                _recipeButtons[i].onClick.AddListener(() => CraftRecipeAt(recipeIndex));
            }
            if (inventoryCloseButton != null)
            {
                inventoryCloseButton.onClick.RemoveAllListeners();
                inventoryCloseButton.onClick.AddListener(CloseInventory);
            }
            if (placeSelectedButton != null)
            {
                placeSelectedButton.onClick.RemoveAllListeners();
                placeSelectedButton.onClick.AddListener(BeginPlacementForSelectedItem);
            }
        }

        private void WireWorkbenchButtons()
        {
            if (_workbenchRecipeButtons != null)
            {
                for (int i = 0; i < _workbenchRecipeButtons.Length; i++)
                {
                    int recipeIndex = i;
                    _workbenchRecipeButtons[i].onClick.RemoveAllListeners();
                    _workbenchRecipeButtons[i].onClick.AddListener(
                        () => CraftWorkbenchRecipeAt(recipeIndex));
                }
            }
            if (workbenchCloseButton != null)
            {
                workbenchCloseButton.onClick.RemoveAllListeners();
                workbenchCloseButton.onClick.AddListener(
                    () => GameManager.Instance.CloseWorkbench());
            }
        }

        private void CraftWorkbenchRecipeAt(int index)
        {
            WorkbenchRecipe recipe = WorkbenchRecipes[index];
            if (GameManager.Instance == null)
            {
                Debug.LogError("[Wispmere] Cannot craft without the active GameManager.", this);
                return;
            }
            if (GameManager.Instance.TryCraftWorkbenchItem(recipe.id, recipe.costs))
                _workbenchFeedbackText.text = recipe.label + " crafted.";
            else
                _workbenchFeedbackText.text = "Not enough materials for " + recipe.label + ".";
        }

        private void BuildInventoryPanel()
        {
            if (inventoryPanel == null)
            {
                Debug.LogError("[Wispmere] GameHUD requires an inventory panel.", this);
                return;
            }

            bool wasOpen = inventoryPanel.activeSelf;
            for (int i = inventoryPanel.transform.childCount - 1; i >= 0; i--)
                Destroy(inventoryPanel.transform.GetChild(i).gameObject);

            _canvas = inventoryPanel.GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                Debug.LogError("[Wispmere] The inventory panel must be inside a Canvas.", this);
                return;
            }
            Canvas.ForceUpdateCanvases();
            Rect pixelRect = _canvas.pixelRect;
            _layoutWidth = Mathf.RoundToInt(pixelRect.width);
            _layoutHeight = Mathf.RoundToInt(pixelRect.height);
            float scale = Mathf.Max(0.01f, _canvas.scaleFactor);
            float unit = 1f / scale;
            float panelWidth = pixelRect.width * 0.92f;
            float panelHeight = pixelRect.height * 0.94f;

            RectTransform panelRect = inventoryPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.04f, 0.03f);
            panelRect.anchorMax = new Vector2(0.96f, 0.97f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = inventoryPanel.GetComponent<Image>();
            if (panelImage == null) panelImage = inventoryPanel.AddComponent<Image>();
            panelImage.color = new Color(0.075f, 0.095f, 0.09f, 0.97f);
            panelImage.raycastTarget = true;

            float padding = 18f;
            float headerHeight = 42f;
            float footerHeight = 42f;
            float sectionHeadingHeight = 27f;
            float contentTop = panelHeight * 0.5f - padding - headerHeight - sectionHeadingHeight - 8f;
            float contentBottom = -panelHeight * 0.5f + padding + footerHeight;
            float contentHeight = Mathf.Max(1f, contentTop - contentBottom);
            float innerWidth = panelWidth - padding * 2f;
            float paneGap = 18f;
            float availableWidth = innerWidth - paneGap;
            float inventoryPaneWidth = availableWidth * 0.40f;
            float craftingPaneWidth = availableWidth - inventoryPaneWidth;
            float inventoryPaneCenter = -panelWidth * 0.5f + padding + inventoryPaneWidth * 0.5f;
            float craftingPaneCenter = -panelWidth * 0.5f + padding + inventoryPaneWidth
                + paneGap + craftingPaneWidth * 0.5f;

            Font font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            CreateText("InventoryTitle", inventoryPanel.transform, "SATCHEL", font, 31,
                new Vector2(0f, (panelHeight * 0.5f - padding - headerHeight * 0.5f) * unit),
                new Vector2(320f * unit, headerHeight * unit), TextAnchor.MiddleCenter,
                new Color(0.98f, 0.89f, 0.67f), FontStyle.Bold);
            inventoryCloseButton = CreateButton("InventoryCloseButton", inventoryPanel.transform,
                "Close", font,
                new Vector2((panelWidth * 0.5f - padding - 43f) * unit,
                    (panelHeight * 0.5f - padding - headerHeight * 0.5f) * unit),
                new Vector2(78f * unit, 32f * unit));

            float sectionY = contentTop + (sectionHeadingHeight * 0.5f + 4f);
            CreateText("InventorySectionHeading", inventoryPanel.transform, "INVENTORY", font, 21,
                new Vector2(inventoryPaneCenter * unit, sectionY * unit),
                new Vector2(inventoryPaneWidth * unit, sectionHeadingHeight * unit), TextAnchor.MiddleLeft,
                new Color(0.97f, 0.87f, 0.65f), FontStyle.Bold);
            CreateText("CraftingSectionHeading", inventoryPanel.transform, "CRAFTING", font, 21,
                new Vector2(craftingPaneCenter * unit, sectionY * unit),
                new Vector2(craftingPaneWidth * unit, sectionHeadingHeight * unit), TextAnchor.MiddleLeft,
                new Color(0.97f, 0.87f, 0.65f), FontStyle.Bold);
            _selectedItemText = CreateText("SelectedItem", inventoryPanel.transform,
                "Select an item to view it here.", font, 15,
                new Vector2(inventoryPaneCenter * unit,
                    (contentBottom - footerHeight * 0.5f) * unit),
                new Vector2((inventoryPaneWidth - 100f) * unit, footerHeight * unit),
                TextAnchor.MiddleLeft,
                new Color(0.89f, 0.86f, 0.76f),
                FontStyle.Normal);
            placeSelectedButton = CreateButton("PlaceSelectedButton", inventoryPanel.transform,
                "Place item", font,
                new Vector2((inventoryPaneCenter + inventoryPaneWidth * 0.5f - 44f) * unit,
                    (contentBottom - footerHeight * 0.5f) * unit),
                new Vector2(84f * unit, 30f * unit));
            craftingFeedbackText = CreateText("CraftingFeedback", inventoryPanel.transform,
                "Tools unlock new gathering opportunities.", font, 15,
                new Vector2(craftingPaneCenter * unit,
                    (contentBottom - footerHeight * 0.5f) * unit),
                new Vector2(craftingPaneWidth * unit, footerHeight * unit), TextAnchor.MiddleLeft,
                new Color(0.96f, 0.85f, 0.61f),
                FontStyle.Normal);

            InventorySlotButtons = new Button[InventorySlotCount];
            InventorySlotQuantityTexts = new Text[InventorySlotCount];
            _slotNameTexts = new Text[InventorySlotCount];
            _slotIconTexts = new Text[InventorySlotCount];
            _slotIconImages = new Image[InventorySlotCount];
            inventoryQuantityTexts = new Text[ResourceIds.Length];

            const int slotColumns = 3;
            const int slotRows = (InventorySlotCount + slotColumns - 1) / slotColumns;
            float slotGap = 5f;
            float slotWidth = Mathf.Min(118f,
                (inventoryPaneWidth - slotGap * (slotColumns - 1) - 12f) / slotColumns);
            float slotHeight = Mathf.Clamp((contentHeight - (slotRows - 1) * slotGap) / slotRows,
                24f, 82f);
            float gridWidth = slotWidth * slotColumns + slotGap * (slotColumns - 1);
            float gridLeft = inventoryPaneCenter - gridWidth * 0.5f;
            for (int slot = 0; slot < InventorySlotCount; slot++)
            {
                int selectedSlot = slot;
                int column = slot % slotColumns;
                int row = slot / slotColumns;
                float x = gridLeft + slotWidth * 0.5f + column * (slotWidth + slotGap);
                float y = contentTop - slotHeight * 0.5f - row * (slotHeight + slotGap);
                string slotName = slot < ResourceIds.Length
                    ? ResourceLabels[slot] + "InventoryRow"
                    : "InventorySlot" + slot;
                GameObject slotObject = CreateUiObject(slotName, inventoryPanel.transform,
                    new Vector2(x * unit, y * unit), new Vector2(slotWidth * unit, slotHeight * unit));
                Image background = slotObject.AddComponent<Image>();
                background.color = new Color(0.23f, 0.27f, 0.26f, 1f);
                Button slotButton = slotObject.AddComponent<Button>();
                slotButton.targetGraphic = background;
                slotButton.transition = Selectable.Transition.None;
                slotButton.onClick.AddListener(() => SelectSlot(selectedSlot));
                InventorySlotButtons[slot] = slotButton;

                float iconSize = Mathf.Min(27f, slotHeight * 0.45f);
                GameObject iconObject = CreateUiObject("ItemIcon", slotObject.transform,
                    new Vector2(-slotWidth * 0.22f * unit, slotHeight * 0.19f * unit),
                    new Vector2(iconSize * unit, iconSize * unit));
                _slotIconImages[slot] = iconObject.AddComponent<Image>();
                _slotIconImages[slot].raycastTarget = false;
                _slotIconTexts[slot] = CreateText("IconLabel", iconObject.transform, "", font,
                    Mathf.RoundToInt(16f / scale),
                    Vector2.zero, new Vector2(iconSize * unit, iconSize * unit), TextAnchor.MiddleCenter, Color.white,
                    FontStyle.Bold);
                _slotIconTexts[slot].raycastTarget = false;
                _slotNameTexts[slot] = CreateText("ItemName", slotObject.transform, "Empty", font,
                    Mathf.RoundToInt(12f / scale),
                    new Vector2(0f, -slotHeight * 0.25f * unit),
                    new Vector2((slotWidth - 6f) * unit, slotHeight * 0.53f * unit), TextAnchor.MiddleCenter,
                    new Color(0.96f, 0.91f, 0.79f), FontStyle.Normal);
                _slotNameTexts[slot].raycastTarget = false;
                string quantityName = slot < ResourceIds.Length
                    ? ResourceLabels[slot] + "Quantity"
                    : "ItemQuantity";
                InventorySlotQuantityTexts[slot] = CreateText(quantityName, slotObject.transform, "",
                    font, Mathf.RoundToInt(12f / scale),
                    new Vector2(slotWidth * 0.26f * unit, slotHeight * 0.32f * unit),
                    new Vector2(slotWidth * 0.5f * unit, 20f * unit),
                    TextAnchor.MiddleRight, new Color(1f, 0.91f, 0.67f), FontStyle.Bold);
                InventorySlotQuantityTexts[slot].raycastTarget = false;
                if (slot < inventoryQuantityTexts.Length)
                    inventoryQuantityTexts[slot] = InventorySlotQuantityTexts[slot];
            }

            _recipeCostTexts = new Text[Recipes.Length];
            _recipeStatusTexts = new Text[Recipes.Length];
            _recipeButtons = new Button[Recipes.Length];
            float recipeGap = 6f;
            float recipeHeight = Mathf.Clamp((contentHeight - recipeGap * (Recipes.Length - 1))
                / Recipes.Length, 52f, 84f);
            float recipeBlockHeight = recipeHeight * Recipes.Length + recipeGap * (Recipes.Length - 1);
            float recipeTop = contentTop - (contentHeight - recipeBlockHeight) * 0.5f;
            float buttonWidth = Mathf.Min(76f, craftingPaneWidth * 0.24f);
            float recipeIconSize = Mathf.Min(28f, recipeHeight * 0.52f);
            float textLeft = -craftingPaneWidth * 0.5f + 44f;
            float textRight = craftingPaneWidth * 0.5f - buttonWidth - 18f;
            float recipeTextWidth = Mathf.Max(40f, textRight - textLeft);
            for (int i = 0; i < Recipes.Length; i++)
            {
                CraftRecipe recipe = Recipes[i];
                float cardY = recipeTop - recipeHeight * 0.5f - i * (recipeHeight + recipeGap);
                string prefix = recipe.label.Replace(" ", "");
                GameObject card = CreateUiObject(prefix + "Recipe", inventoryPanel.transform,
                    new Vector2(craftingPaneCenter * unit, cardY * unit),
                    new Vector2(craftingPaneWidth * unit, recipeHeight * unit));
                Image cardImage = card.AddComponent<Image>();
                cardImage.color = new Color(0.17f, 0.21f, 0.20f, 0.97f);

                GameObject iconObject = CreateUiObject("RecipeIcon", card.transform,
                    new Vector2((-craftingPaneWidth * 0.5f + 22f) * unit, 0f),
                    new Vector2(recipeIconSize * unit, recipeIconSize * unit));
                Image iconImage = iconObject.AddComponent<Image>();
                iconImage.color = new Color(0.53f, 0.40f, 0.25f, 1f);
                iconImage.raycastTarget = false;
                Text iconText = CreateText("IconLabel", iconObject.transform, recipe.icon, font,
                    Mathf.RoundToInt(16f / scale),
                    Vector2.zero, new Vector2(recipeIconSize * unit, recipeIconSize * unit),
                    TextAnchor.MiddleCenter, Color.white,
                    FontStyle.Bold);
                iconText.raycastTarget = false;

                CreateText(recipe.id == "axe" ? "AxeCraftName"
                        : recipe.id == "pickaxe" ? "PickaxeCraftName" : "RecipeName",
                    card.transform, recipe.label, font, Mathf.RoundToInt(15f / scale),
                    new Vector2((textLeft + recipeTextWidth * 0.5f) * unit, recipeHeight * 0.27f * unit),
                    new Vector2(recipeTextWidth * unit, Mathf.Min(20f, recipeHeight * 0.3f) * unit),
                    TextAnchor.MiddleLeft,
                    new Color(0.98f, 0.91f, 0.77f), FontStyle.Bold);
                string costName = recipe.id == "axe" ? "AxeCraftCost"
                    : recipe.id == "pickaxe" ? "PickaxeCraftCost" : "RecipeCost";
                _recipeCostTexts[i] = CreateText(costName, card.transform, "", font,
                    Mathf.RoundToInt(12f / scale),
                    new Vector2((textLeft + recipeTextWidth * 0.5f) * unit, 0f),
                    new Vector2(recipeTextWidth * unit, 18f * unit), TextAnchor.MiddleLeft,
                    new Color(0.85f, 0.83f, 0.75f), FontStyle.Normal);
                _recipeStatusTexts[i] = CreateText("RecipeStatus", card.transform, "", font,
                    Mathf.RoundToInt(11f / scale),
                    new Vector2((textLeft + recipeTextWidth * 0.5f) * unit, -recipeHeight * 0.28f * unit),
                    new Vector2(recipeTextWidth * unit, 16f * unit), TextAnchor.MiddleLeft,
                    new Color(0.89f, 0.82f, 0.68f), FontStyle.Normal);
                Button recipeButton = CreateButton(recipe.id == "axe" ? "AxeCraftButton"
                    : recipe.id == "pickaxe" ? "PickaxeCraftButton" : "RecipeCraftButton",
                    card.transform, "Craft", font,
                    new Vector2((craftingPaneWidth * 0.5f - buttonWidth * 0.5f - 8f) * unit, 0f),
                    new Vector2(buttonWidth * unit, Mathf.Min(38f, recipeHeight - 10f) * unit));
                _recipeButtons[i] = recipeButton;
                if (recipe.tool == GatherTool.Axe) axeCraftButton = recipeButton;
                if (recipe.tool == GatherTool.Pickaxe) pickaxeCraftButton = recipeButton;
            }

            inventoryPanel.SetActive(wasOpen);
        }

        private void BuildWorkbenchPanel()
        {
            if (inventoryPanel == null || _canvas == null) return;
            bool wasOpen = workbenchPanel != null && workbenchPanel.activeSelf;
            if (workbenchPanel == null)
            {
                workbenchPanel = new GameObject("WorkbenchCraftingPanel", typeof(RectTransform));
                workbenchPanel.transform.SetParent(inventoryPanel.transform.parent, false);
            }
            for (int i = workbenchPanel.transform.childCount - 1; i >= 0; i--)
                Destroy(workbenchPanel.transform.GetChild(i).gameObject);

            Canvas.ForceUpdateCanvases();
            Rect pixelRect = _canvas.pixelRect;
            float scale = Mathf.Max(0.01f, _canvas.scaleFactor);
            float unit = 1f / scale;
            float panelWidth = pixelRect.width * 0.66f;
            float panelHeight = pixelRect.height * 0.64f;
            RectTransform panelRect = workbenchPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.17f, 0.18f);
            panelRect.anchorMax = new Vector2(0.83f, 0.82f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            Image background = workbenchPanel.GetComponent<Image>();
            if (background == null) background = workbenchPanel.AddComponent<Image>();
            background.color = new Color(0.075f, 0.095f, 0.09f, 0.97f);
            background.raycastTarget = true;

            Font font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            CreateText("WorkbenchTitle", workbenchPanel.transform, "WORKBENCH",
                font, Mathf.RoundToInt(28f / scale),
                new Vector2(0f, (panelHeight * 0.5f - 34f) * unit),
                new Vector2(panelWidth * 0.7f * unit, 44f * unit),
                TextAnchor.MiddleCenter, new Color(0.98f, 0.89f, 0.67f), FontStyle.Bold);
            workbenchCloseButton = CreateButton("WorkbenchCloseButton", workbenchPanel.transform,
                "Close", font,
                new Vector2((panelWidth * 0.5f - 62f) * unit,
                    (panelHeight * 0.5f - 34f) * unit),
                new Vector2(86f * unit, 34f * unit));
            _workbenchFeedbackText = CreateText("WorkbenchFeedback", workbenchPanel.transform,
                "Choose one of the three Workbench recipes.", font,
                Mathf.RoundToInt(15f / scale),
                new Vector2(0f, (-panelHeight * 0.5f + 27f) * unit),
                new Vector2(panelWidth * 0.88f * unit, 34f * unit),
                TextAnchor.MiddleCenter, new Color(0.96f, 0.85f, 0.61f), FontStyle.Normal);

            _workbenchRecipeCosts = new Text[WorkbenchRecipes.Length];
            _workbenchRecipeStatuses = new Text[WorkbenchRecipes.Length];
            _workbenchRecipeButtons = new Button[WorkbenchRecipes.Length];
            float top = panelHeight * 0.5f - 82f;
            float bottom = -panelHeight * 0.5f + 64f;
            float gap = 10f;
            float rowHeight = Mathf.Max(66f, (top - bottom - gap * 2f) / 3f);
            for (int i = 0; i < WorkbenchRecipes.Length; i++)
            {
                WorkbenchRecipe recipe = WorkbenchRecipes[i];
                float centerY = top - rowHeight * 0.5f - i * (rowHeight + gap);
                GameObject row = CreateUiObject(recipe.label.Replace(" ", "") + "WorkbenchRecipe",
                    workbenchPanel.transform,
                    new Vector2(0f, centerY * unit),
                    new Vector2((panelWidth - 52f) * unit, rowHeight * unit));
                Image rowImage = row.AddComponent<Image>();
                rowImage.color = new Color(0.17f, 0.21f, 0.20f, 0.97f);

                float rowWidth = panelWidth - 52f;
                float buttonWidth = Mathf.Min(104f, rowWidth * 0.28f);
                float contentLeft = -rowWidth * 0.5f + 54f;
                float contentRight = rowWidth * 0.5f - buttonWidth - 12f;
                float contentWidth = Mathf.Max(90f, contentRight - contentLeft);
                GameObject icon = CreateUiObject("RecipeIcon", row.transform,
                    new Vector2((-rowWidth * 0.5f + 27f) * unit, 0f),
                    new Vector2(38f * unit, 38f * unit));
                Image iconBackground = icon.AddComponent<Image>();
                iconBackground.color = new Color(0.62f, 0.46f, 0.27f, 1f);
                CreateText("IconLabel", icon.transform, recipe.icon, font,
                    Mathf.RoundToInt(17f / scale), Vector2.zero,
                    new Vector2(38f * unit, 38f * unit), TextAnchor.MiddleCenter,
                    new Color(1f, 0.94f, 0.81f), FontStyle.Bold);
                CreateText("RecipeName", row.transform, recipe.label, font,
                    Mathf.RoundToInt(19f / scale),
                    new Vector2((contentLeft + contentWidth * 0.5f) * unit,
                        rowHeight * 0.28f * unit),
                    new Vector2(contentWidth * unit, 25f * unit),
                    TextAnchor.MiddleLeft, new Color(0.98f, 0.91f, 0.77f), FontStyle.Bold);
                CreateText("RecipeDescription", row.transform, recipe.description, font,
                    Mathf.RoundToInt(14f / scale),
                    new Vector2((contentLeft + contentWidth * 0.5f) * unit,
                        rowHeight * 0.04f * unit),
                    new Vector2(contentWidth * unit, 19f * unit),
                    TextAnchor.MiddleLeft, new Color(0.85f, 0.83f, 0.75f), FontStyle.Normal);
                _workbenchRecipeCosts[i] = CreateText("RecipeCost", row.transform, "", font,
                    Mathf.RoundToInt(13f / scale),
                    new Vector2((contentLeft + contentWidth * 0.5f) * unit,
                        -rowHeight * 0.16f * unit),
                    new Vector2(contentWidth * unit, 31f * unit),
                    TextAnchor.MiddleLeft, new Color(0.9f, 0.86f, 0.74f), FontStyle.Normal);
                _workbenchRecipeStatuses[i] = CreateText("RecipeStatus", row.transform, "", font,
                    Mathf.RoundToInt(13f / scale),
                    new Vector2((contentLeft + contentWidth * 0.5f) * unit,
                        -rowHeight * 0.4f * unit),
                    new Vector2(contentWidth * unit, 16f * unit),
                    TextAnchor.MiddleLeft, new Color(0.89f, 0.82f, 0.68f), FontStyle.Normal);
                _workbenchRecipeButtons[i] = CreateButton("CraftButton", row.transform,
                    "Craft", font,
                    new Vector2((rowWidth * 0.5f - buttonWidth * 0.5f - 10f) * unit, 0f),
                    new Vector2(buttonWidth * unit, Mathf.Min(42f, rowHeight - 12f) * unit));
            }

            workbenchPanel.SetActive(wasOpen);
            WireWorkbenchButtons();
        }

        private void BuildPlacementStatus()
        {
            if (_canvas == null) return;
            if (_placementStatusPanel == null)
            {
                _placementStatusPanel = CreateUiObject("PlacementStatus", transform,
                    new Vector2(0f, 72f), new Vector2(920f, 52f));
                _placementStatusText = CreateText("PlacementInstructions",
                    _placementStatusPanel.transform, "", Font.CreateDynamicFontFromOSFont("Arial", 18),
                    17, Vector2.zero, new Vector2(900f, 48f), TextAnchor.MiddleCenter,
                    new Color(1f, 0.94f, 0.81f), FontStyle.Bold);
                _placementStatusPanel.AddComponent<Image>();
                _placementStatusPanel.SetActive(false);
            }
        }

        private void RefreshWorkbench()
        {
            ResourceManager rm = ResourceManager.Instance;
            if (rm == null || _workbenchRecipeButtons == null) return;
            for (int i = 0; i < WorkbenchRecipes.Length; i++)
            {
                WorkbenchRecipe recipe = WorkbenchRecipes[i];
                bool canAfford = rm.CanAfford(recipe.costs);
                _workbenchRecipeCosts[i].text = FormatCosts(recipe.costs, rm);
                _workbenchRecipeStatuses[i].text = canAfford ? "Ready to craft" : "Need more materials";
                _workbenchRecipeStatuses[i].color = canAfford
                    ? new Color(0.88f, 0.76f, 0.49f)
                    : new Color(0.92f, 0.58f, 0.48f);
                _workbenchRecipeButtons[i].interactable = true;
            }
        }

        private static Text CreateText(string name, Transform parent, string value, Font font,
            int fontSize, Vector2 position, Vector2 size, TextAnchor alignment, Color color,
            FontStyle style)
        {
            GameObject textObject = CreateUiObject(name, parent, position, size);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Font font,
            Vector2 position, Vector2 size)
        {
            GameObject buttonObject = CreateUiObject(name, parent, position, size);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.48f, 0.37f, 0.24f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.57f, 0.43f, 0.27f, 1f);
            colors.highlightedColor = new Color(0.72f, 0.57f, 0.35f, 1f);
            colors.pressedColor = new Color(0.41f, 0.32f, 0.22f, 1f);
            colors.disabledColor = new Color(0.34f, 0.33f, 0.29f, 0.7f);
            button.colors = colors;
            CreateText("Text", buttonObject.transform, label, font, 14, Vector2.zero, size,
                TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.81f), FontStyle.Bold);
            return button;
        }

        private static GameObject CreateUiObject(string name, Transform parent,
            Vector2 position, Vector2 size)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            RectTransform rect = result.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return result;
        }
    }
}
