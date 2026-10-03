using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wispmere;

namespace Wispmere.Editor
{
    public static class StarterSceneBuilder
    {
        private const string ScenePath = "Assets/Wispmere/Scenes/Wispmere.unity";
        private const string RowPrefabPath = "Assets/Wispmere/Prefabs/CreatorRow.prefab";

        [MenuItem("Wispmere/Create/Rebuild Starter Scene")]
        public static void Build()
        {
            var layout = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Wispmere/Runtime/WorldLayout.json");
            var input = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                "Assets/Wispmere/Runtime/WispmereInput.inputactions");
            var artSet = OccaVillageArtSetBuilder.CreateOrUpdate();
            if (layout == null || input == null || artSet == null)
            {
                Debug.LogError("[Wispmere] Starter scene requires WorldLayout.json, WispmereInput.inputactions, and the OccaSoftware village art set.");
                return;
            }

            EnsureFolders();
            var rowPrefab = CreateRowPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var townObject = new GameObject("Town");
            var town = townObject.AddComponent<TownBuilder>();
            town.layoutJson = layout;
            town.artSet = artSet;
            town.autoBuild = true;

            var playerObject = new GameObject("Player");
            playerObject.tag = "Player";
            playerObject.transform.position = new Vector3(0f, 0f, 9.2f);
            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.28f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            playerObject.AddComponent<CharacterCustomizer>();
            var player = playerObject.AddComponent<PlayerController>();
            player.inputAsset = input;

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = playerObject.transform.position + new Vector3(8f, 16f, 20f);
            var cameraFollow = cameraObject.AddComponent<CameraFollow>();
            cameraFollow.target = playerObject.transform;
            cameraFollow.playOffset = new Vector3(8f, 16f, 20f);
            cameraFollow.revealOffset = new Vector3(10f, 23f, 28f);
            cameraFollow.focusHeight = 0.9f;
            cameraFollow.followSpeed = 4.5f;
            cameraFollow.usePolishedControls = true;
            cameraFollow.minimumZoom = 20f;
            cameraFollow.maximumZoom = 36f;
            cameraFollow.zoomDistance = 27.6f;
            cameraFollow.pitch = 38f;
            cameraFollow.yaw = 65f;
            var presentationCamera = cameraObject.GetComponent<Camera>();
            presentationCamera.fieldOfView = 48f;
            presentationCamera.nearClipPlane = 0.15f;
            presentationCamera.farClipPlane = 100f;
            presentationCamera.allowMSAA = true;

            var lightObject = new GameObject("Directional Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.88f, 0.68f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.69f, 0.76f, 0.7f);
            RenderSettings.ambientIntensity = 0.82f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.65f, 0.75f, 0.73f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 78f;

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var hudRoot = UiObject("HUD", canvas.transform);
            var hudRootRect = hudRoot.GetComponent<RectTransform>();
            hudRootRect.anchorMin = Vector2.zero;
            hudRootRect.anchorMax = Vector2.one;
            hudRootRect.offsetMin = Vector2.zero;
            hudRootRect.offsetMax = Vector2.zero;
            var resourcesPlate = HudPlate("ResourcesFrame", hudRoot.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f),
                new Vector2(455f, 62f), new Color(0.16f, 0.2f, 0.17f, 0.9f));
            var resourcesText = Text("ResourcesText", resourcesPlate.transform,
                "WOOD: 0    STONE: 0    FIBER: 0", 19, TextAnchor.MiddleLeft,
                new Rect(16f, 0f, 423f, 52f));
            StyleHudText(resourcesText, new Color(0.96f, 0.88f, 0.67f));
            var objectivePlate = HudPlate("ObjectiveFrame", hudRoot.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f),
                new Vector2(500f, 62f), new Color(0.19f, 0.2f, 0.28f, 0.88f));
            var objectiveText = Text("ObjectiveBanner", objectivePlate.transform, "OBJECTIVE",
                18, TextAnchor.MiddleLeft, new Rect(16f, 0f, 468f, 52f));
            StyleHudText(objectiveText, new Color(0.96f, 0.88f, 0.67f));
            var promptButton = Button("Prompt", hudRoot.transform, "Interact",
                new Rect(0f, 70f, 440f, 54f));
            var promptRect = promptButton.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 30f);
            promptRect.sizeDelta = new Vector2(440f, 54f);
            promptButton.GetComponent<Image>().color = new Color(0.28f, 0.35f, 0.29f, 0.94f);
            StyleHudText(promptButton.GetComponentInChildren<Text>(), new Color(0.98f, 0.91f, 0.74f));
            var hud = hudRoot.AddComponent<GameHUD>();
            hud.resourcesText = resourcesText;
            hud.objectiveText = objectiveText;
            hud.promptButton = promptButton;
            hud.promptText = promptButton.GetComponentInChildren<Text>();
            ConfigureInventoryHud(hud);

            var dialoguePanel = Panel("Dialogue", canvas.transform, new Color(0.07f, 0.09f, 0.11f, 0.95f),
                new Rect(0.12f, 0.04f, 0.76f, 0.24f));
            var dialogueName = Text("Name", dialoguePanel.transform, "Wren", 24,
                TextAnchor.MiddleLeft, new Rect(24f, 80f, 1100f, 40f));
            dialogueName.fontStyle = FontStyle.Bold;
            dialogueName.color = new Color(0.96f, 0.88f, 0.67f);
            var dialogueBody = Text("Body", dialoguePanel.transform, "", 20,
                TextAnchor.UpperLeft, new Rect(0f, -18f, 1080f, 100f));
            var dialogueAdvance = Button("Advance", dialoguePanel.transform, "Continue",
                new Rect(-105f, 40f, 180f, 48f), new Vector2(1f, 0f));
            var dialogue = dialoguePanel.AddComponent<DialogueManager>();
            dialogue.panel = dialoguePanel;
            dialogue.nameText = dialogueName;
            dialogue.bodyText = dialogueBody;
            dialogue.advanceButton = dialogueAdvance;

            var mainMenu = Panel("MainMenu", canvas.transform, new Color(0.05f, 0.08f, 0.1f, 0.94f),
                new Rect(0f, 0f, 1f, 1f));
            Text("Title", mainMenu.transform, "WISPMERE", 54, TextAnchor.MiddleCenter,
                new Rect(0f, 190f, 700f, 100f));
            Text("Subtitle", mainMenu.transform, "A quiet town waiting to begin again", 24,
                TextAnchor.MiddleCenter, new Rect(0f, 125f, 700f, 48f));
            var newGameButton = Button("NewGame", mainMenu.transform, "New Game",
                new Rect(0f, 35f, 360f, 64f));
            var continueButton = Button("Continue", mainMenu.transform, "Continue",
                new Rect(0f, -45f, 360f, 64f));

            var creator = Panel("Creator", canvas.transform, new Color(0.08f, 0.11f, 0.12f, 0.94f),
                new Rect(0.04f, 0.04f, 0.92f, 0.92f));
            Text("Title", creator.transform, "Create your traveler", 36, TextAnchor.MiddleLeft,
                new Rect(40f, -42f, 900f, 58f), new Vector2(0f, 1f));
            Text("Subtitle", creator.transform, "Choose a name and shape your traveler. Every choice updates the portrait.",
                18, TextAnchor.MiddleLeft, new Rect(40f, -90f, 970f, 32f), new Vector2(0f, 1f));
            Text("NameLabel", creator.transform, "Name", 20, TextAnchor.MiddleLeft,
                new Rect(40f, -135f, 180f, 32f), new Vector2(0f, 1f));
            var nameField = InputField("NameInput", creator.transform,
                new Rect(40f, -174f, 560f, 48f), new Vector2(0f, 1f));
            nameField.text = "Wren";

            var rowsObject = UiObject("Rows", creator.transform);
            SetRect(rowsObject.GetComponent<RectTransform>(), new Rect(40f, -230f, 1000f, 370f),
                new Vector2(0f, 1f));
            var rowsLayout = rowsObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 7f;
            rowsLayout.childAlignment = TextAnchor.UpperLeft;
            rowsLayout.childControlHeight = false;
            rowsLayout.childControlWidth = false;
            rowsLayout.childForceExpandHeight = false;
            rowsLayout.childForceExpandWidth = false;

            var previewRoot = new GameObject("PreviewRoot").transform;
            previewRoot.position = new Vector3(0f, -1000f, 0f);
            previewRoot.gameObject.SetActive(false);
            var previewFrame = UiObject("PreviewFrame", creator.transform);
            SetRect(previewFrame.GetComponent<RectTransform>(),
                new Rect(392f, -5f, 600f, 790f), new Vector2(0.5f, 0.5f));
            previewFrame.AddComponent<Image>().color = new Color(0.12f, 0.17f, 0.15f, 1f);
            Text("PreviewCaption", previewFrame.transform, "LIVE TRAVELER PREVIEW", 18,
                TextAnchor.MiddleCenter, new Rect(0f, 350f, 560f, 38f));
            var previewObject = UiObject("PreviewImage", previewFrame.transform);
            SetRect(previewObject.GetComponent<RectTransform>(),
                new Rect(0f, -18f, 510f, 680f), new Vector2(0.5f, 0.5f));
            var previewImage = previewObject.AddComponent<RawImage>();
            previewImage.color = Color.white;
            previewImage.raycastTarget = false;
            var warnText = Text("Warning", creator.transform, "Please enter a name.",
                18, TextAnchor.MiddleLeft, new Rect(40f, 96f, 500f, 32f), new Vector2(0f, 0f));
            warnText.color = new Color(1f, 0.58f, 0.4f);
            var beginButton = Button("Begin", creator.transform, "Begin Adventure",
                new Rect(40f, 32f, 300f, 60f), new Vector2(0f, 0f));
            var backButton = Button("Back", creator.transform, "Back",
                new Rect(360f, 32f, 180f, 60f), new Vector2(0f, 0f));

            var creatorUI = creator.AddComponent<CreatorUI>();
            creatorUI.rowsParent = rowsObject.transform;
            creatorUI.rowPrefab = rowPrefab;
            creatorUI.nameField = nameField;
            creatorUI.warnText = warnText;
            creatorUI.beginButton = beginButton;
            creatorUI.previewRoot = previewRoot;
            creatorUI.previewImage = previewImage;

            var pause = Panel("Pause", canvas.transform, new Color(0.04f, 0.06f, 0.08f, 0.94f),
                new Rect(0f, 0f, 1f, 1f));
            Text("Title", pause.transform, "Paused", 46, TextAnchor.MiddleCenter,
                new Rect(0f, 145f, 600f, 80f));
            var resumeButton = Button("Resume", pause.transform, "Resume",
                new Rect(0f, 45f, 340f, 60f));
            var saveTitleButton = Button("SaveAndTitle", pause.transform, "Save & Title",
                new Rect(0f, -35f, 340f, 60f));

            var systems = new GameObject("Game");
            systems.AddComponent<ResourceManager>();
            var manager = systems.AddComponent<GameManager>();
            manager.town = town;
            manager.player = player;
            manager.cam = cameraFollow;
            manager.Dialogue = dialogue;
            manager.hud = hud;
            manager.mainMenuPanel = mainMenu;
            manager.newGameButton = newGameButton;
            manager.continueButton = continueButton;
            manager.creatorPanel = creator;
            manager.pausePanel = pause;
            UnityEventTools.AddPersistentListener(backButton.onClick, manager.ReturnToMainMenu);
            UnityEventTools.AddPersistentListener(resumeButton.onClick, manager.Resume);
            UnityEventTools.AddPersistentListener(saveTitleButton.onClick, manager.SaveAndTitle);

            mainMenu.SetActive(true);
            creator.SetActive(false);
            pause.SetActive(false);
            dialoguePanel.SetActive(false);
            playerObject.SetActive(true);
            Selection.activeGameObject = systems;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var existing in EditorBuildSettings.scenes)
                if (existing.path != ScenePath) buildScenes.Add(existing);
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var reloadedPlayer = FindInScene<PlayerController>(scene);
            if (reloadedPlayer != null && reloadedPlayer.inputAsset == null)
            {
                var serializedPlayer = new SerializedObject(reloadedPlayer);
                serializedPlayer.FindProperty("inputAsset").objectReferenceValue = input;
                serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            Validate(scene);
            Debug.Log("[Wispmere] Starter scene created and added to Build Settings: " + ScenePath);
        }

        public static void BuildBatchMode()
        {
            if (!Application.isBatchMode)
                throw new System.InvalidOperationException("BuildBatchMode must be called from a batch-mode Unity Editor.");

            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Validate(Scene scene)
        {
            var manager = FindInScene<GameManager>(scene);
            var town = FindInScene<TownBuilder>(scene);
            var player = FindInScene<PlayerController>(scene);
            var camera = FindInScene<CameraFollow>(scene);
            var dialogue = FindInScene<DialogueManager>(scene);
            var hud = FindInScene<GameHUD>(scene);
            var creator = FindInScene<CreatorUI>(scene);
            var eventSystem = FindInScene<EventSystem>(scene);
            var inputModule = FindInScene<InputSystemUIInputModule>(scene);

            Require(manager != null, "GameManager is missing.");
            Require(town != null && town.layoutJson != null, "TownBuilder layout is not assigned.");
            Require(player != null && player.inputAsset != null, "Player input asset is not assigned.");
            Require(camera != null && player != null && camera.target == player.transform,
                "Camera target is not assigned to the player.");
            Require(dialogue != null && dialogue.panel != null && dialogue.nameText != null
                && dialogue.bodyText != null && dialogue.advanceButton != null, "Dialogue references are incomplete.");
            Require(hud != null && hud.resourcesText != null && hud.objectiveText != null
                && hud.promptButton != null && hud.promptText != null
                && hud.inventoryButton != null && hud.inventoryPanel != null
                && hud.inventoryCloseButton != null, "HUD references are incomplete.");
            Require(creator != null && creator.rowsParent != null && creator.rowPrefab != null
                && creator.nameField != null && creator.warnText != null && creator.beginButton != null
                && creator.previewRoot != null && creator.previewImage != null, "Creator references are incomplete.");
            Require(eventSystem != null && inputModule != null && inputModule.actionsAsset != null,
                "EventSystem is not wired to the Input System UI module.");
            Require(manager.town == town && manager.player == player && manager.cam == camera
                && manager.Dialogue == dialogue && manager.hud == hud && manager.creatorPanel != null
                && manager.pausePanel != null && manager.mainMenuPanel != null
                && manager.newGameButton != null && manager.continueButton != null,
                "GameManager scene references are incomplete.");
            Require(manager.mainMenuPanel.activeSelf && !manager.creatorPanel.activeSelf
                && !manager.pausePanel.activeSelf && !dialogue.panel.activeSelf,
                "The scene's initial menu/panel visibility is incorrect.");
            Require(creator.rowPrefab.label != null && creator.rowPrefab.optionsParent != null,
                "Creator row prefab references are incomplete.");
            Require(EditorBuildSettings.scenes.Length > 0
                && System.Array.Exists(EditorBuildSettings.scenes, entry => entry.path == ScenePath && entry.enabled),
                "Starter scene is not enabled in Build Settings.");
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException("[Wispmere] Starter scene validation failed: " + message);
        }

        private static CreatorRow CreateRowPrefab()
        {
            var root = UiObject("CreatorRow", null);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1000f, 52f);
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            var label = Text("RowLabel", root.transform, "Option", 18, TextAnchor.MiddleLeft,
                new Rect(0f, 0f, 140f, 48f));
            label.color = new Color(0.96f, 0.88f, 0.67f);
            var options = UiObject("Options", root.transform);
            options.GetComponent<RectTransform>().sizeDelta = new Vector2(850f, 44f);
            var optionsLayout = options.AddComponent<HorizontalLayoutGroup>();
            optionsLayout.spacing = 6f;
            optionsLayout.childAlignment = TextAnchor.MiddleLeft;
            optionsLayout.childControlHeight = false;
            optionsLayout.childControlWidth = false;
            optionsLayout.childForceExpandHeight = false;
            optionsLayout.childForceExpandWidth = false;

            var component = root.AddComponent<CreatorRow>();
            component.label = label;
            component.optionsParent = options.transform;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
            Object.DestroyImmediate(root);
            return prefab != null ? prefab.GetComponent<CreatorRow>() : null;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Wispmere/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Wispmere", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Wispmere/Scenes"))
                AssetDatabase.CreateFolder("Assets/Wispmere", "Scenes");
        }

        private static GameObject UiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject HudPlate(string name, Transform parent, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var plate = UiObject(name, parent);
            var rect = plate.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = plate.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return plate;
        }

        private static void StyleHudText(Text text, Color color)
        {
            text.color = color;
            text.raycastTarget = false;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.1f, 0.08f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        [MenuItem("Wispmere/Integrate/Add Inventory HUD")]
        public static void AddInventoryHudToOpenScene()
        {
            var hud = UnityEngine.Object.FindObjectOfType<GameHUD>();
            if (hud == null)
            {
                Debug.LogError("[Wispmere] Cannot add the inventory HUD because the open scene has no GameHUD.");
                return;
            }
            ConfigureInventoryHud(hud);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
            Debug.Log("[Wispmere] Built the inventory, resource list, and crafting controls in the open scene.");
        }

        private static void ConfigureInventoryHud(GameHUD hud)
        {
            Transform root = hud.transform;
            Button button = hud.inventoryButton;
            if (button == null)
            {
                Transform existingButton = root.Find("InventoryButton");
                button = existingButton != null
                    ? existingButton.GetComponent<Button>()
                    : null;
            }
            if (button == null)
                button = Button("InventoryButton", root, "Inventory",
                    new Rect(-28f, 30f, 250f, 72f), new Vector2(1f, 0f));

            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-28f, 30f);
            buttonRect.sizeDelta = new Vector2(250f, 72f);
            Image buttonImage = button.GetComponent<Image>();
            buttonImage.color = new Color(0.86f, 0.75f, 0.53f, 0.98f);
            var buttonOutline = button.GetComponent<Outline>();
            if (buttonOutline == null) buttonOutline = button.gameObject.AddComponent<Outline>();
            buttonOutline.effectColor = new Color(0.45f, 0.29f, 0.13f, 0.98f);
            buttonOutline.effectDistance = new Vector2(2f, -2f);
            buttonOutline.useGraphicAlpha = true;
            SetButtonColors(button, buttonImage.color,
                new Color(0.94f, 0.84f, 0.62f, 1f),
                new Color(0.73f, 0.59f, 0.37f, 1f));
            Text buttonText = button.GetComponentInChildren<Text>();
            buttonText.text = "Inventory";
            buttonText.fontSize = 23;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.color = new Color(0.24f, 0.17f, 0.1f, 1f);
            var buttonTextRect = buttonText.rectTransform;
            buttonTextRect.offsetMin = new Vector2(58f, 0f);
            buttonTextRect.offsetMax = new Vector2(-12f, 0f);
            if (button.transform.Find("SatchelIcon") == null) AddSatchelIcon(button.transform);

            GameObject inventoryPanel = hud.inventoryPanel;
            if (inventoryPanel == null)
            {
                Transform existingPanel = root.Find("InventoryPanel");
                inventoryPanel = existingPanel != null
                    ? existingPanel.gameObject
                    : Panel("InventoryPanel", root, new Color(0.84f, 0.75f, 0.58f, 0.99f),
                        new Rect(0.2f, 0.16f, 0.6f, 0.68f));
            }
            Image panelImage = inventoryPanel.GetComponent<Image>();
            if (panelImage == null) panelImage = inventoryPanel.AddComponent<Image>();
            panelImage.color = new Color(0.84f, 0.75f, 0.58f, 0.99f);
            for (int i = inventoryPanel.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(inventoryPanel.transform.GetChild(i).gameObject);

            var panelOutline = inventoryPanel.GetComponent<Outline>();
            if (panelOutline == null) panelOutline = inventoryPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.39f, 0.26f, 0.13f, 1f);
            panelOutline.effectDistance = new Vector2(4f, -4f);
            panelOutline.useGraphicAlpha = true;

            Text title = Text("InventoryTitle", inventoryPanel.transform, "INVENTORY", 32,
                TextAnchor.MiddleCenter, new Rect(0f, -34f, 400f, 54f),
                new Vector2(0.5f, 1f));
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.26f, 0.19f, 0.12f, 1f);

            Text inventoryHeading = Text("InventorySectionHeading", inventoryPanel.transform,
                "INVENTORY", 24, TextAnchor.MiddleLeft,
                new Rect(-490f, 220f, 440f, 42f), new Vector2(0f, 0.5f));
            inventoryHeading.fontStyle = FontStyle.Bold;
            inventoryHeading.color = new Color(0.34f, 0.23f, 0.13f, 1f);
            Text craftingHeading = Text("CraftingSectionHeading", inventoryPanel.transform,
                "CRAFTING", 24, TextAnchor.MiddleLeft,
                new Rect(30f, 220f, 440f, 42f), new Vector2(0f, 0.5f));
            craftingHeading.fontStyle = FontStyle.Bold;
            craftingHeading.color = new Color(0.34f, 0.23f, 0.13f, 1f);

            string[] resourceKinds = { "Wood", "Stone", "Fiber", "Ore" };
            Text[] quantities = new Text[resourceKinds.Length];
            for (int i = 0; i < resourceKinds.Length; i++)
            {
                float y = 157f - i * 66f;
                var row = UiObject(resourceKinds[i] + "InventoryRow", inventoryPanel.transform);
                SetRect(row.GetComponent<RectTransform>(),
                    new Rect(-270f, y, 438f, 54f), new Vector2(0.5f, 0.5f));
                var rowImage = row.AddComponent<Image>();
                rowImage.color = i % 2 == 0
                    ? new Color(0.96f, 0.88f, 0.71f, 0.66f)
                    : new Color(0.91f, 0.81f, 0.63f, 0.5f);
                rowImage.raycastTarget = false;
                Text resourceName = Text(resourceKinds[i] + "Name", row.transform,
                    resourceKinds[i], 22, TextAnchor.MiddleLeft,
                    new Rect(22f, 0f, 290f, 48f), new Vector2(0f, 0.5f));
                resourceName.color = new Color(0.29f, 0.22f, 0.15f, 1f);
                quantities[i] = Text(resourceKinds[i] + "Quantity", row.transform,
                    "0", 22, TextAnchor.MiddleRight,
                    new Rect(-20f, 0f, 78f, 48f), new Vector2(1f, 0.5f));
                quantities[i].fontStyle = FontStyle.Bold;
                quantities[i].color = new Color(0.22f, 0.17f, 0.12f, 1f);
            }

            Text axeName = Text("AxeCraftName", inventoryPanel.transform, "Axe", 24,
                TextAnchor.MiddleLeft, new Rect(30f, 142f, 245f, 42f),
                new Vector2(0f, 0.5f));
            axeName.fontStyle = FontStyle.Bold;
            axeName.color = new Color(0.29f, 0.2f, 0.12f, 1f);
            Text axeCost = Text("AxeCraftCost", inventoryPanel.transform, "1 Wood + 1 Stone",
                18, TextAnchor.MiddleLeft, new Rect(30f, 101f, 260f, 36f),
                new Vector2(0f, 0.5f));
            axeCost.color = new Color(0.39f, 0.3f, 0.2f, 1f);
            Button axeButton = Button("AxeCraftButton", inventoryPanel.transform, "Craft",
                new Rect(452f, 122f, 132f, 54f), new Vector2(1f, 0.5f));
            StyleCraftButton(axeButton);

            Text pickaxeName = Text("PickaxeCraftName", inventoryPanel.transform, "Pickaxe", 24,
                TextAnchor.MiddleLeft, new Rect(30f, -9f, 245f, 42f),
                new Vector2(0f, 0.5f));
            pickaxeName.fontStyle = FontStyle.Bold;
            pickaxeName.color = new Color(0.29f, 0.2f, 0.12f, 1f);
            Text pickaxeCost = Text("PickaxeCraftCost", inventoryPanel.transform,
                "1 Wood + 1 Stone", 18, TextAnchor.MiddleLeft,
                new Rect(30f, -50f, 260f, 36f), new Vector2(0f, 0.5f));
            pickaxeCost.color = new Color(0.39f, 0.3f, 0.2f, 1f);
            Button pickaxeButton = Button("PickaxeCraftButton", inventoryPanel.transform, "Craft",
                new Rect(452f, -28f, 132f, 54f), new Vector2(1f, 0.5f));
            StyleCraftButton(pickaxeButton);

            Text craftingFeedback = Text("CraftingFeedback", inventoryPanel.transform,
                "Tools you craft will stay in your satchel.", 17,
                TextAnchor.MiddleCenter, new Rect(0f, -150f, 800f, 38f));
            craftingFeedback.color = new Color(0.39f, 0.3f, 0.2f, 1f);

            var closeButton = Button("InventoryCloseButton", inventoryPanel.transform, "Close",
                new Rect(-24f, -22f, 132f, 48f), new Vector2(1f, 1f));
            closeButton.GetComponent<Image>().color = new Color(0.57f, 0.41f, 0.24f, 1f);
            SetButtonColors(closeButton, new Color(0.57f, 0.41f, 0.24f, 1f),
                new Color(0.68f, 0.5f, 0.3f, 1f),
                new Color(0.45f, 0.32f, 0.19f, 1f));
            Text closeText = closeButton.GetComponentInChildren<Text>();
            closeText.color = new Color(1f, 0.93f, 0.78f, 1f);
            closeText.fontStyle = FontStyle.Bold;
            inventoryPanel.SetActive(false);

            hud.inventoryButton = button;
            hud.inventoryPanel = inventoryPanel;
            hud.inventoryCloseButton = closeButton;
            hud.inventoryQuantityTexts = quantities;
            hud.axeCraftButton = axeButton;
            hud.pickaxeCraftButton = pickaxeButton;
            hud.craftingFeedbackText = craftingFeedback;
            EditorUtility.SetDirty(hud);
        }

        private static void StyleCraftButton(Button button)
        {
            Color normal = new Color(0.57f, 0.41f, 0.24f, 1f);
            button.GetComponent<Image>().color = normal;
            SetButtonColors(button, normal,
                new Color(0.68f, 0.5f, 0.3f, 1f),
                new Color(0.45f, 0.32f, 0.19f, 1f));
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.63f, 0.55f, 0.43f, 0.65f);
            button.colors = colors;
            Text text = button.GetComponentInChildren<Text>();
            text.color = new Color(1f, 0.93f, 0.78f, 1f);
            text.fontStyle = FontStyle.Bold;
        }

        private static void AddSatchelIcon(Transform button)
        {
            var icon = UiObject("SatchelIcon", button);
            var rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(16f, 0f);
            rect.sizeDelta = new Vector2(36f, 42f);
            AddIconShape(icon.transform, "BagBody", new Vector2(0f, -3f),
                new Vector2(30f, 29f), new Color(0.42f, 0.26f, 0.14f, 1f));
            AddIconShape(icon.transform, "BagTop", new Vector2(2f, 10f),
                new Vector2(26f, 5f), new Color(0.57f, 0.38f, 0.21f, 1f));
            AddIconShape(icon.transform, "BagHandle", new Vector2(8f, 17f),
                new Vector2(14f, 7f), new Color(0.42f, 0.26f, 0.14f, 1f));
            AddIconShape(icon.transform, "BagClasp", new Vector2(0f, -3f),
                new Vector2(6f, 8f), new Color(0.92f, 0.73f, 0.4f, 1f));
        }

        private static void AddIconShape(Transform parent, string name, Vector2 position,
            Vector2 size, Color color)
        {
            var shape = UiObject(name, parent);
            var rect = shape.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = shape.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void SetButtonColors(Button button, Color normal, Color highlighted,
            Color pressed)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = highlighted;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
        }

        private static GameObject Panel(string name, Transform parent, Color color, Rect rect)
        {
            var go = UiObject(name, parent);
            var rectTransform = go.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(rect.x, rect.y);
            rectTransform.anchorMax = new Vector2(rect.x + rect.width, rect.y + rect.height);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private static Text Text(string name, Transform parent, string value, int fontSize,
            TextAnchor alignment, Rect rect, Vector2? pivot = null)
        {
            var go = UiObject(name, parent);
            SetRect(go.GetComponent<RectTransform>(), rect, pivot ?? new Vector2(0.5f, 0.5f));
            var text = go.AddComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button Button(string name, Transform parent, string label, Rect rect,
            Vector2? pivot = null)
        {
            var go = UiObject(name, parent);
            SetRect(go.GetComponent<RectTransform>(), rect, pivot ?? new Vector2(0.5f, 0.5f));
            var image = go.AddComponent<Image>();
            image.color = new Color(0.23f, 0.34f, 0.3f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            Text("Text", go.transform, label, 20, TextAnchor.MiddleCenter, new Rect(0f, 0f, 0f, 0f));
            var textRect = go.transform.GetChild(0).GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private static InputField InputField(string name, Transform parent, Rect rect, Vector2 pivot)
        {
            var go = UiObject(name, parent);
            SetRect(go.GetComponent<RectTransform>(), rect, pivot);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.16f, 0.19f, 0.2f, 1f);
            var input = go.AddComponent<InputField>();
            input.targetGraphic = image;

            var text = Text("Text", go.transform, "", 20, TextAnchor.MiddleLeft, new Rect(12f, 0f, 0f, 0f));
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);
            input.textComponent = text;

            var placeholder = Text("Placeholder", go.transform, "Enter your name", 20,
                TextAnchor.MiddleLeft, new Rect(12f, 0f, 0f, 0f));
            placeholder.color = new Color(0.65f, 0.68f, 0.68f, 1f);
            var placeholderRect = placeholder.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(12f, 4f);
            placeholderRect.offsetMax = new Vector2(-12f, -4f);
            input.placeholder = placeholder;
            return input;
        }

        private static void SetRect(RectTransform rect, Rect value, Vector2 pivot)
        {
            rect.anchorMin = pivot;
            rect.anchorMax = pivot;
            rect.pivot = pivot;
            rect.anchoredPosition = new Vector2(value.x, value.y);
            rect.sizeDelta = new Vector2(value.width, value.height);
        }
    }
}
