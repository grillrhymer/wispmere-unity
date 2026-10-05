using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wispmere;

namespace Wispmere.Editor
{
    [InitializeOnLoad]
    public static class PlayableSlicePlayModeTest
    {
        private const string RunningKey = "Wispmere.PlayableSliceTest.Running";
        private const string PhaseKey = "Wispmere.PlayableSliceTest.Phase";
        private const string StartTimeKey = "Wispmere.PlayableSliceTest.StartTime";
        private const string HadSaveKey = "Wispmere.PlayableSliceTest.HadSave";
        private const string BackupPathKey = "Wispmere.PlayableSliceTest.BackupPath";
        private const string WorkbenchRunningKey = "Wispmere.WorkbenchTest.Running";
        private const string WorkbenchPhaseKey = "Wispmere.WorkbenchTest.Phase";
        private const string WorkbenchStartTimeKey = "Wispmere.WorkbenchTest.StartTime";
        private const string SaveFileName = "wispmere_save_v1.json";
        private const string ScenePath = "Assets/Wispmere/Scenes/Wispmere.unity";
        private const double PhaseTimeoutSeconds = 75.0;

        private static Vector3 _playerStart;
        private static ResourceNode _expectedNode;
        private static Vector3 _cameraInputStart;
        private static Quaternion _cameraRotationStart;
        private static float _cameraYawStart;
        private static float _cameraBaseYaw;
        private static float _cameraZoomStart;
        private static GameObject _cameraTestObstacle;
        private static Vector3 _cameraPositionBeforeObstruction;
        private static bool _cameraMotionSampled;
        private static bool _arrivalDialogueChecked;
        private static bool _arrivalSecondLineChecked;
        private static Vector3 _savedWorkbenchPosition;
        private static Vector3 _savedTentPosition;

        static PlayableSlicePlayModeTest()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        [MenuItem("Wispmere/Test/Run Playable Town Slice Test")]
        public static void Run()
        {
            StartTest(true);
        }

        public static void RunBatchMode()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("RunBatchMode must be called from a batch-mode Unity Editor.");
            StartTest(false);
        }

        public static void RunWorkbenchMilestoneBatchMode()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException(
                    "RunWorkbenchMilestoneBatchMode requires a batch-mode Unity Editor.");
            if (SessionState.GetBool(RunningKey, false)
                || SessionState.GetBool(WorkbenchRunningKey, false))
            {
                Debug.LogError("[Wispmere] A playable-slice test is already running.");
                return;
            }

            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            string backupPath = Path.Combine("Library", "WorkbenchSaveBackup.json");
            SessionState.SetBool(HadSaveKey, File.Exists(savePath));
            SessionState.SetString(BackupPathKey, backupPath);
            if (File.Exists(savePath)) File.Copy(savePath, backupPath, true);

            var seed = new SaveSystem.SaveData
            {
                player = "Workbench test",
                hasAxe = true,
                hasPickaxe = true,
                pos = WorldLayout.ToUnity(800f, 600f),
                hasSavedPosition = true,
            };
            seed.resources["wood"] = 4;
            seed.resources["stone"] = 2;
            seed.resources["fiber"] = 0;
            seed.resources["ore"] = 1;
            seed.resources["hardwood"] = 1;
            SaveSystem.Save(seed);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetInt(WorkbenchPhaseKey, 0);
            SessionState.SetFloat(WorkbenchStartTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(WorkbenchRunningKey, true);
            Debug.Log("[Wispmere] Starting the focused Workbench/Tent Play Mode test.");
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static void StartTest(bool confirmSceneSave)
        {
            if (SessionState.GetBool(RunningKey, false))
            {
                Debug.LogError("[Wispmere] The playable town-slice test is already running.");
                return;
            }

            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[Wispmere] Starter scene is missing. Rebuild it before running this test.");
                return;
            }
            if (confirmSceneSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            string backupPath = Path.Combine("Library", "PlayableSliceSaveBackup.json");
            SessionState.SetBool(HadSaveKey, File.Exists(savePath));
            SessionState.SetString(BackupPathKey, backupPath);
            if (File.Exists(savePath)) File.Copy(savePath, backupPath, true);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(RunningKey, true);
            Debug.Log("[Wispmere] Starting the playable town-slice Play Mode test.");
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying) return;
            if (SessionState.GetBool(WorkbenchRunningKey, false))
            {
                try
                {
                    UpdateWorkbenchMilestoneTest();
                }
                catch (Exception exception)
                {
                    FinishWorkbenchMilestoneTest(false, exception.ToString());
                }
                return;
            }
            if (!SessionState.GetBool(RunningKey, false)) return;

            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f)
                    > PhaseTimeoutSeconds)
                {
                    throw new TimeoutException("Timed out during playable town-slice phase "
                        + SessionState.GetInt(PhaseKey, 0) + ".");
                }

                switch (SessionState.GetInt(PhaseKey, 0))
                {
                    case 0:
                        BeginNewGame();
                        SetPhase(1);
                        break;
                    case 1:
                        AdvanceArrivalDialogue();
                        break;
                    case 2:
                        StartMovementCheck();
                        SetPhase(3);
                        break;
                    case 3:
                        var manager = GameManager.Instance;
                        CheckDirectMovementReady(manager);
                        BeginGather(manager, "wood");
                        SetPhase(4);
                        break;
                    case 4:
                        if (!CheckGatherComplete("wood", 1)) return;
                        BeginGather(GameManager.Instance, "stone");
                        SetPhase(5);
                        break;
                    case 5:
                        if (!CheckGatherComplete("stone", 2)) return;
                        BeginGather(GameManager.Instance, "fiber");
                        SetPhase(6);
                        break;
                    case 6:
                        if (!CheckGatherComplete("fiber", 1)) return;
                        Require(GameManager.Instance.hud.resourcesText.text.Contains("WOOD: 1")
                            && GameManager.Instance.hud.resourcesText.text.Contains("STONE: 2")
                            && GameManager.Instance.hud.resourcesText.text.Contains("FIBER: 1")
                            && GameManager.Instance.hud.resourcesText.text.Contains("ORE: 0")
                            && GameManager.Instance.hud.resourcesText.text.Contains("HARDWOOD: 0"),
                            "The resource HUD did not display gathered counts.");
                        BeginGather(GameManager.Instance, "wood");
                        SetPhase(7);
                        break;
                    case 7:
                        if (!CheckGatherComplete("wood", 2)) return;
                        BeginWorkshopInteraction(GameManager.Instance);
                        SetPhase(8);
                        break;
                    case 8:
                        if (!GameManager.Instance.Dialogue.IsOpen)
                        {
                            RequirePhaseProgress("Workbench proximity interaction");
                            return;
                        }
                        CheckWorkshopInteraction();
                        VerifyInventoryPanel(GameManager.Instance);
                        MovePlayerToOpenCameraSpace();
                        SetPhase(9);
                        break;
                    case 9:
                        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 0.8)
                            return;
                        StartCameraInputCheck();
                        SetPhase(10);
                        break;
                    case 10:
                        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 0.65)
                            return;
                        CheckCameraOrbitAndMinimumZoom();
                        GameManager.Instance.cam.AdjustZoom(-100000f);
                        SetPhase(11);
                        break;
                    case 11:
                        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 0.65)
                            return;
                        Require(GameManager.Instance.cam.TargetZoomDistance
                            == GameManager.Instance.cam.thirdPersonMaximumDistance
                            && GameManager.Instance.cam.thirdPersonDistance
                                > GameManager.Instance.cam.thirdPersonMaximumDistance - 3f,
                            "Mouse-wheel zoom did not smoothly reach its maximum distance.");
                        BeginCameraObstructionChecks();
                        SetPhase(12);
                        break;
                    case 12:
                        if (!_cameraMotionSampled)
                        {
                                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 0.05)
                                    return;
                                Require(Vector3.Distance(_cameraPositionBeforeObstruction,
                                    GameManager.Instance.cam.transform.position) < 3f,
                                    "Camera snapped abruptly when the building obstruction appeared.");
                                _cameraMotionSampled = true;
                        }
                        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 0.9)
                                return;
                        CheckCameraObstruction("building-side view");
                        MovePlayerForCameraObstructionCheck(
                            GameManager.Instance.player.transform.position + Vector3.right * 5f
                                + Vector3.back * 3f);
                        SetPhase(13);
                        break;
                    case 13:
                        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 0.9)
                            return;
                        CheckCameraObstruction("moving around the building");
                        if (_cameraTestObstacle != null)
                            UnityEngine.Object.Destroy(_cameraTestObstacle);
                        CreateCameraTestObstacle(PrimitiveType.Capsule, "Camera Test Tree",
                            new Vector3(1.2f, 3.2f, 1.2f));
                        SetPhase(14);
                        break;
                    case 14:
                        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 0.9)
                            return;
                        CheckCameraObstruction("near-tree view");
                        if (_cameraTestObstacle != null)
                            UnityEngine.Object.Destroy(_cameraTestObstacle);
                        GameManager.Instance.Continue();
                        SetPhase(15);
                        break;
                    case 15:
                        CheckPlacedObjectPersistence(GameManager.Instance);
                        Finish(true, "Resource gathering, Axe-gated Hardwood, Pickaxe-gated Ore, Workbench placement and interaction, exactly three Workbench recipes, Metal Axe/Metal Pickaxe/Tent crafting, Tent placement, save/load placement persistence, inventory behavior, and existing camera checks all passed.");
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected playable town-slice test phase.");
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static void UpdateWorkbenchMilestoneTest()
        {
            if (EditorApplication.timeSinceStartup
                - SessionState.GetFloat(WorkbenchStartTimeKey, 0f) > PhaseTimeoutSeconds)
                throw new TimeoutException("Timed out during the focused Workbench/Tent test.");

            GameManager manager = GameManager.Instance;
            Require(manager != null && manager.hud != null,
                "The gameplay manager or HUD did not initialize.");
            switch (SessionState.GetInt(WorkbenchPhaseKey, 0))
            {
                case 0:
                    if (manager.mainMenuPanel == null || !manager.mainMenuPanel.activeSelf) return;
                    manager.Continue();
                    SetWorkbenchPhase(1);
                    break;
                case 1:
                    if (!manager.player.gameObject.activeSelf || manager.player.InputLocked) return;
                    GameHUD hud = manager.hud;
                    hud.ToggleInventory();
                    Require(hud.inventoryPanel.activeSelf
                        && hud.CraftingRecipeButtons.Length == 6
                        && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                            == hud.InventorySlotButtons[0].gameObject,
                        "Basic crafting did not open for Workbench creation.");
                    hud.CraftingRecipeButtons[2].onClick.Invoke();
                    Require(ResourceManager.Instance.Get("workbench") == 1,
                        "The existing recipe did not craft one Workbench.");
                    hud.InventorySlotButtons[7].onClick.Invoke();
                    Require(hud.PlaceSelectedButton.interactable,
                        "The crafted Workbench could not be selected for placement.");
                    hud.PlaceSelectedButton.onClick.Invoke();
                    RejectPlayerOverlappingPlacement(manager);
                    Require(manager.IsPlacementActive && ResourceManager.Instance.Get("workbench") == 1,
                        "An invalid player-overlapping site consumed the Workbench.");
                    manager.HandleCancelInput();
                    Require(ResourceManager.Instance.Get("workbench") == 1,
                        "Cancelling Workbench placement consumed the item.");
                    hud.ToggleInventory();
                    hud.InventorySlotButtons[7].onClick.Invoke();
                    hud.PlaceSelectedButton.onClick.Invoke();
                    Vector2 workbenchCursor = FindValidPlacementPoint(manager, "workbench");
                    manager.UpdatePlacement(workbenchCursor, true, false, 0f);
                    Require(!manager.IsPlacementActive && ResourceManager.Instance.Get("workbench") == 0,
                        "Valid Workbench placement did not consume exactly one item.");

                    PlacedWorldObject workbench = FindPlacedObject(manager, "workbench");
                    Require(workbench != null, "A physical Workbench was not created.");
                    _savedWorkbenchPosition = workbench.transform.position;
                    workbench.GetComponent<Interactable>().Interact();
                    Require(manager.IsWorkbenchOpen && hud.workbenchPanel.activeSelf
                        && hud.WorkbenchRecipeButtons.Length == 3
                        && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                            == hud.WorkbenchRecipeButtons[0].gameObject,
                        "Workbench interaction did not show exactly three advanced recipes.");
                    Require(hud.WorkbenchRecipeButtons[0].interactable
                        && hud.WorkbenchRecipeButtons[1].interactable
                        && hud.WorkbenchRecipeButtons[2].interactable
                        && hud.workbenchPanel.transform
                            .Find("MetalAxeWorkbenchRecipe/RecipeStatus").GetComponent<Text>().text
                            == "Need more materials"
                        && hud.workbenchPanel.transform
                            .Find("MetalAxeWorkbenchRecipe/RecipeCost").GetComponent<Text>().text
                            .Contains("Wood 0/2")
                        && hud.workbenchPanel.transform
                            .Find("MetalPickaxeWorkbenchRecipe/RecipeCost").GetComponent<Text>().text
                            .Contains("Ore 0/2")
                        && hud.workbenchPanel.transform
                            .Find("TentWorkbenchRecipe/RecipeCost").GetComponent<Text>().text
                            .Contains("Fiber 0/3"),
                        "Unaffordable Workbench recipes did not show materials and controller-selectable feedback.");
                    hud.workbenchCloseButton.onClick.Invoke();
                    Require(!manager.IsWorkbenchOpen && !manager.player.InputLocked,
                        "Closing the Workbench did not restore player input.");

                    ResourceManager.Instance.Add("wood", 7);
                    ResourceManager.Instance.Add("stone", 2);
                    ResourceManager.Instance.Add("fiber", 3);
                    ResourceManager.Instance.Add("ore", 4);
                    ResourceManager.Instance.Add("hardwood", 3);
                    workbench.GetComponent<Interactable>().Interact();
                    foreach (Button recipeButton in hud.WorkbenchRecipeButtons)
                        Require(recipeButton.interactable,
                            "An adequately funded Workbench recipe remained disabled.");
                    Require(hud.workbenchPanel.transform
                            .Find("MetalAxeWorkbenchRecipe/RecipeStatus").GetComponent<Text>().text
                            == "Ready to craft",
                        "An affordable Workbench recipe did not show its ready state.");
                    foreach (Button recipeButton in hud.WorkbenchRecipeButtons)
                        recipeButton.onClick.Invoke();
                    Require(ResourceManager.Instance.Get("metal_axe") == 1
                        && ResourceManager.Instance.Get("metal_pickaxe") == 1
                        && ResourceManager.Instance.Get("tent") == 1
                        && hud.workbenchPanel.transform.Find("WorkbenchFeedback")
                            .GetComponent<Text>().text == "Tent crafted.",
                        "The Workbench did not craft Metal Axe, Metal Pickaxe, and Tent.");
                    manager.CloseWorkbench();

                    hud.ToggleInventory();
                    hud.InventorySlotButtons[13].onClick.Invoke();
                    hud.PlaceSelectedButton.onClick.Invoke();
                    manager.HandleCancelInput();
                    Require(ResourceManager.Instance.Get("tent") == 1,
                        "Cancelling Tent placement consumed the Tent.");
                    hud.ToggleInventory();
                    hud.InventorySlotButtons[13].onClick.Invoke();
                    hud.PlaceSelectedButton.onClick.Invoke();
                    Vector2 tentCursor = FindValidPlacementPoint(manager, "tent");
                    manager.UpdatePlacement(tentCursor, true, false, 0f);
                    Require(ResourceManager.Instance.Get("tent") == 0,
                        "Valid Tent placement did not consume exactly one Tent.");
                    PlacedWorldObject tent = FindPlacedObject(manager, "tent");
                    Require(tent != null && tent.GetComponent<Interactable>() == null,
                        "The Tent has missing or unintended interactions.");
                    _savedTentPosition = tent.transform.position;
                    manager.SaveGame();
                    SaveSystem.SaveData save = SaveSystem.Load();
                    Require(save != null && save.placedObjects.Count == 2
                        && save.resources["workbench"] == 0 && save.resources["tent"] == 0,
                        "Placed object locations or consumed counts were not saved.");
                    manager.Continue();
                    SetWorkbenchPhase(2);
                    break;
                case 2:
                    CheckPlacedObjectPersistence(manager);
                    FinishWorkbenchMilestoneTest(true,
                        "Workbench creation/placement/cancellation, three advanced recipes, Tent placement, and Continue persistence passed.");
                    break;
                default:
                    throw new InvalidOperationException("Unexpected Workbench/Tent test phase.");
            }
        }

        private static void SetWorkbenchPhase(int phase)
        {
            SessionState.SetInt(WorkbenchPhaseKey, phase);
            SessionState.SetFloat(WorkbenchStartTimeKey, (float)EditorApplication.timeSinceStartup);
        }

        private static void FinishWorkbenchMilestoneTest(bool passed, string message)
        {
            RestoreSave();
            SessionState.SetBool(WorkbenchRunningKey, false);
            if (passed)
                Debug.Log("[Wispmere] WORKBENCH MILESTONE TEST PASSED: " + message);
            else
                Debug.LogError("[Wispmere] WORKBENCH MILESTONE TEST FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void BeginNewGame()
        {
            var manager = GameManager.Instance;
            Require(manager != null && manager.mainMenuPanel.activeSelf,
                "The existing startup menu did not appear.");
            VerifyInventoryLayout(manager.hud);
            Require(manager.town != null && manager.town.transform.Find("TownRoot") != null,
                "The existing town builder did not create the town.");
            Require(manager.player != null && !manager.player.gameObject.activeSelf,
                "The existing player did not remain behind the startup menu.");
            Require(manager.cam != null && manager.cam.target == manager.player.transform,
                "The elevated action-RPG camera is not wired to the existing player.");
            Require(manager.cam.thirdPersonCameraEnabled
                && manager.cam.thirdPersonPitch >= 8f && manager.cam.thirdPersonPitch <= 35f
                && manager.cam.thirdPersonDistance >= manager.cam.thirdPersonMinimumDistance,
                "The third-person camera controls are not enabled or tuned correctly.");
            Require(manager.cam.playOffset.y > 0f && manager.cam.playOffset.magnitude >= 24f,
                "The gameplay camera is not sufficiently elevated and pulled back.");
            VerifyThirdPersonGameplayCamera(manager.cam);

            manager.newGameButton.onClick.Invoke();
            var creator = manager.creatorPanel.GetComponent<CreatorUI>();
            Require(creator != null && creator.rowsParent.childCount == 6,
                "The existing character creator did not open with all appearance options.");
            creator.nameField.text = "Slice Tester";
            _arrivalDialogueChecked = false;
            _arrivalSecondLineChecked = false;
            creator.beginButton.onClick.Invoke();
            Require(manager.arrivalWalkSpeedScale >= 1.1f,
                "The arrival walk speed is not noticeably faster than player movement speed.");
            VerifyLegacySavePositionMigration();
            VerifyVillageBuildingRoles(manager.town);
            Require(manager.player.gameObject.activeSelf && manager.Dialogue != null,
                "Beginning the game did not start the existing arrival flow.");
        }

        private static void VerifyThirdPersonGameplayCamera(CameraFollow cameraFollow)
        {
            Camera camera = cameraFollow.GetComponent<Camera>();
            _cameraBaseYaw = cameraFollow.target.eulerAngles.y;
            float originalFieldOfView = camera.fieldOfView;
            float initialYaw = cameraFollow.thirdPersonYaw;
            float initialDistance = cameraFollow.thirdPersonDistance;
            Require(cameraFollow.thirdPersonCameraEnabled,
                "The third-person gameplay camera is not enabled by default.");

            PlayerController playerController = cameraFollow.target.GetComponent<PlayerController>();
            bool hasDeadzoneLookBinding = false;
            if (playerController != null && playerController.CameraLookAction != null)
            {
                foreach (var binding in playerController.CameraLookAction.bindings)
                {
                    if (binding.path == "<Gamepad>/rightStick"
                        && binding.processors.Contains("StickDeadzone")
                        && binding.processors.Contains("min=0.2"))
                    {
                        hasDeadzoneLookBinding = true;
                        break;
                    }
                }
            }
            Require(hasDeadzoneLookBinding,
                "The right-stick camera action is missing its configured Input System deadzone.");

            cameraFollow.SnapToTarget();
            Require(Mathf.Approximately(camera.fieldOfView, cameraFollow.thirdPersonFieldOfView),
                "The third-person gameplay camera did not apply its wider field of view.");
            Require(Mathf.Approximately(cameraFollow.TargetZoomDistance,
                    cameraFollow.thirdPersonDistance),
                "The third-person gameplay camera did not start at its moderate distance.");

            Quaternion initialPlayerRotation = cameraFollow.target.rotation;
            Vector3 cameraPositionBeforePlayerTurn = cameraFollow.transform.position;
            Quaternion cameraRotationBeforePlayerTurn = cameraFollow.transform.rotation;
            cameraFollow.target.rotation = Quaternion.Euler(0f,
                initialPlayerRotation.eulerAngles.y + 90f, 0f);
            cameraFollow.SnapToTarget();
            Require(Vector3.Distance(cameraFollow.transform.position, cameraPositionBeforePlayerTurn) < 0.01f
                && Quaternion.Angle(cameraFollow.transform.rotation, cameraRotationBeforePlayerTurn) < 0.1f,
                "Changing the player's facing direction unexpectedly rotated the gameplay camera.");
            cameraFollow.target.rotation = initialPlayerRotation;
            cameraFollow.SnapToTarget();

            cameraFollow.AdjustOrbit(Vector2.right * 100f);
            Require(!Mathf.Approximately(cameraFollow.thirdPersonYaw, initialYaw),
                "The third-person gameplay camera did not respond to horizontal orbit input.");
            cameraFollow.AdjustZoom(120f);
            Require(cameraFollow.TargetZoomDistance < cameraFollow.thirdPersonDistance,
                "The third-person gameplay camera did not respond to zoom input.");

            cameraFollow.thirdPersonYaw = initialYaw;
            cameraFollow.thirdPersonDistance = initialDistance;
            cameraFollow.SetThirdPersonCamera(false);
            cameraFollow.SnapToTarget();
            Require(Mathf.Approximately(camera.fieldOfView, originalFieldOfView),
                "Disabling the third-person gameplay camera did not restore the original field of view.");
            cameraFollow.SetThirdPersonCamera(true);
            cameraFollow.SnapToTarget();
            Require(Mathf.Approximately(camera.fieldOfView, cameraFollow.thirdPersonFieldOfView),
                "Re-enabling the third-person gameplay camera did not restore its field of view.");
        }

        private static void VerifyVillageBuildingRoles(TownBuilder town)
        {
            WorldLayout.BuildingEntry[] buildings = town.Layout.buildings;
            RequireBuildingRole(buildings, "shop", "General Store", "Business: General Store");
            RequireBuildingRole(buildings, "inn", "Restaurant", "Business: Restaurant");
            RequireBuildingRole(buildings, "house", "General Store Owner Home",
                "Future home of the General Store owner");
            RequireBuildingRole(buildings, "house2", "Restaurant Owner Home",
                "Future home of the Restaurant owner");

            Transform root = town.transform.Find("TownRoot");
            Require(root != null && root.Find("General Store") != null
                && root.Find("Restaurant") != null
                && root.Find("General Store Owner Home") != null
                && root.Find("Restaurant Owner Home") != null,
                "The four existing village buildings were not named for their assigned roles.");
        }

        private static void RequireBuildingRole(WorldLayout.BuildingEntry[] buildings, string id,
            string label, string role)
        {
            foreach (WorldLayout.BuildingEntry building in buildings)
            {
                if (building.id != id) continue;
                Require(building.label == label && building.role == role,
                    "Village building '" + id + "' is not assigned to '" + label + "'.");
                return;
            }
            Require(false, "Village building '" + id + "' is missing from the town layout.");
        }

        private static void VerifyLegacySavePositionMigration()
        {
            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            SaveSystem.SaveData currentSave = SaveSystem.Load();
            Require(currentSave != null, "New game did not write a readable save.");

            File.WriteAllText(savePath,
                "{\"player\":\"Legacy\",\"pos\":[2,0,3],\"hasSavedPosition\":true}");
            SaveSystem.SaveData migrated = SaveSystem.Load();
            Require(migrated != null
                && Vector3.Distance(migrated.pos, new Vector3(5f, 0f, 7.5f)) < 0.001f,
                "A legacy saved position was not converted to the expanded world scale.");

            SaveSystem.Save(currentSave);
        }

        private static void AdvanceArrivalDialogue()
        {
            var manager = GameManager.Instance;
            if (manager.Dialogue.IsOpen)
            {
                if (!_arrivalDialogueChecked)
                {
                    Require(manager.Dialogue.nameText.text == "Slice Tester"
                        && manager.Dialogue.bodyText.text.Contains("Wispmere"),
                        "The arrival introduction did not identify the player and Wispmere.");
                    Vector3 arrivalPoint = WorldLayout.ToUnity(manager.town.Layout.square.x,
                        manager.town.Layout.square.y);
                    Require(Vector3.Distance(manager.player.transform.position, arrivalPoint) < 1f
                        && Vector3.Distance(manager.player.transform.position,
                            WorldLayout.ToUnity(manager.town.Layout.spawn.x, manager.town.Layout.spawn.y)) > 10f,
                        "The player did not physically walk from the arrival point into town.");
                    RectTransform speakerRect = manager.Dialogue.nameText.rectTransform;
                    RectTransform bodyRect = manager.Dialogue.bodyText.rectTransform;
                    Require(speakerRect.anchoredPosition.y - speakerRect.rect.height * 0.5f
                        > bodyRect.anchoredPosition.y + bodyRect.rect.height * 0.5f,
                        "The character name overlaps the dialogue text.");
                    _arrivalDialogueChecked = true;
                }
                else if (!_arrivalSecondLineChecked)
                {
                    Require(manager.Dialogue.nameText.text == "Slice Tester"
                        && manager.Dialogue.bodyText.text.Contains("find out why")
                        && manager.Dialogue.bodyText.text.Contains("find its way back"),
                        "The second arrival line did not invite the player to investigate and restore Wispmere.");
                    _arrivalSecondLineChecked = true;
                }
                manager.Dialogue.Advance();
                return;
            }

            if (manager.player.InputLocked) return;
            Require(manager.hud.objectiveText.gameObject.activeSelf
                && manager.hud.objectiveText.text.Contains("EXPLORE THE TOWN"),
                "The first explore objective did not appear after arrival.");
            SetPhase(2);
        }

        private static void StartMovementCheck()
        {
            var manager = GameManager.Instance;
            _playerStart = manager.player.transform.position;
        }

        private static void CheckDirectMovementReady(GameManager manager)
        {
            float moved = Vector3.Distance(_playerStart, manager.player.transform.position);
            Require(moved < 0.1f && manager.player.MoveDir.sqrMagnitude < 0.001f,
                "The player moved without direct movement input.");
            Require(manager.player.GetComponent<CharacterController>() != null,
                "The existing character controller is missing.");
        }

        private static void BeginGather(GameManager manager, string kind)
        {
            ResourceNode target = null;
            ResourceNode preferredTree = null;
            float best = float.MaxValue;
            foreach (var node in ResourceNode.All)
            {
                if (node.kind != kind || !node.IsRipe) continue;
                if (kind == "wood" && node.gameObject.name == "tree3")
                {
                    preferredTree = node;
                    continue;
                }
                float distance = Vector3.Distance(manager.player.transform.position, node.transform.position);
                if (distance < best)
                {
                    best = distance;
                    target = node;
                }
            }

            if (preferredTree != null) target = preferredTree;
            Require(target != null, "No ripe " + kind + " gathering node exists.");
            _expectedNode = target;
            Vector3 direction = Vector3.ProjectOnPlane(manager.player.transform.position
                - target.transform.position, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.back;
            manager.player.transform.position = target.transform.position
                + direction * Mathf.Min(target.interactionRadius * 0.45f, 0.7f);
            manager.TryInteract();
        }

        private static bool CheckGatherComplete(string kind, int expectedCount)
        {
            if (ResourceManager.Instance.Get(kind) != expectedCount)
            {
                RequirePhaseProgress("Proximity gather " + kind);
                return false;
            }
            Require(_expectedNode != null && !_expectedNode.IsRipe,
                "The " + kind + " node did not deplete after gathering.");
            Require(GameManager.Instance.hud.resourcesText.text.Contains(
                    kind.ToUpperInvariant() + ": " + expectedCount),
                "The HUD did not update after gathering " + kind + ".");
            return true;
        }

        private static void RequirePhaseProgress(string operation)
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) > 12.0)
                throw new TimeoutException(operation + " did not complete within 12 seconds.");
        }

        private static void BeginWorkshopInteraction(GameManager manager)
        {
            Interactable workshop = null;
            foreach (var interactable in Interactable.All)
                if (interactable.isRestorationTarget)
                    workshop = interactable;
            Require(workshop != null && workshop.label == "Old Workshop",
                "The Old Workshop restoration target is missing.");

            Transform workshopVisual = manager.town.transform.Find("TownRoot/damaged");
            bool hasExpectedWorkshopVisual = workshopVisual != null
                && workshopVisual.Find("Workshop Restoration Sign") != null
                && (manager.town.artSet != null
                    || workshopVisual.Find("Workshop Roof Exposed Rafters") != null);
            Require(hasExpectedWorkshopVisual,
                "The workshop's unfinished visual markers are missing.");

            Vector3 approach = Vector3.ProjectOnPlane(manager.player.transform.position
                - workshop.transform.position, Vector3.up).normalized;
            if (approach.sqrMagnitude < 0.01f) approach = Vector3.back;
            manager.player.transform.position = workshop.transform.position
                + approach * (workshop.radius * 0.45f);
            manager.TryInteract();
            Require(manager.Dialogue.IsOpen,
                "Proximity interaction did not open the Old Workshop dialogue.");
        }

        private static void VerifyInventoryPanel(GameManager manager)
        {
            GameHUD hud = manager.hud;
            Require(hud.inventoryButton != null && hud.inventoryPanel != null
                && hud.inventoryCloseButton != null && hud.axeCraftButton != null
                && hud.pickaxeCraftButton != null && hud.inventoryQuantityTexts != null
                && hud.inventoryQuantityTexts.Length == 5
                && hud.InventorySlotButtons != null && hud.InventorySlotButtons.Length == 22
                && hud.CraftingRecipeButtons != null && hud.CraftingRecipeButtons.Length == 6,
                "The inventory, resource rows, or crafting controls are not wired.");
            Require(!hud.inventoryPanel.activeSelf,
                "The inventory panel should start closed.");
            VerifyInventoryLayout(hud);

            string resourcesBefore = hud.resourcesText.text;
            hud.inventoryButton.onClick.Invoke();
            Require(hud.inventoryPanel.activeSelf,
                "Clicking Inventory did not open the panel.");
            Require(hud.inventoryPanel.transform.Find("InventoryTitle") != null
                && hud.inventoryPanel.transform.Find("InventorySectionHeading") != null
                && hud.inventoryPanel.transform.Find("CraftingSectionHeading") != null
                && hud.inventoryPanel.transform.Find("WoodInventoryRow/WoodQuantity") != null
                && hud.inventoryPanel.transform.Find("StoneInventoryRow/StoneQuantity") != null
                && hud.inventoryPanel.transform.Find("FiberInventoryRow/FiberQuantity") != null
                && hud.inventoryPanel.transform.Find("OreInventoryRow/OreQuantity") != null
                && hud.inventoryPanel.transform.Find("HardwoodInventoryRow/HardwoodQuantity") != null
                && hud.inventoryPanel.transform.Find("AxeRecipe/AxeCraftCost") != null
                && hud.inventoryPanel.transform.Find("PickaxeRecipe/PickaxeCraftCost") != null
                && hud.inventoryPanel.transform.Find("WorkbenchRecipe/RecipeCost") != null
                && hud.inventoryPanel.transform.Find("CampfireRecipe/RecipeCost") != null
                && hud.inventoryPanel.transform.Find("StorageChestRecipe/RecipeCost") != null
                && hud.inventoryPanel.transform.Find("FenceRecipe/RecipeCost") != null,
                "The inventory grid or one of the six crafting recipe entries is missing.");
            Require(hud.resourcesText.text == resourcesBefore && manager.Dialogue.IsOpen,
                "Opening Inventory changed resource HUD data or interrupted the current dialogue.");
            Require(hud.inventoryQuantityTexts[0].text == "2"
                && hud.inventoryQuantityTexts[1].text == "2"
                && hud.inventoryQuantityTexts[2].text == "1"
                && hud.inventoryQuantityTexts[3].text == "0"
                && hud.inventoryQuantityTexts[4].text == "0",
                "The inventory rows did not show the gathered resource quantities.");
            Require(hud.InventorySlotQuantityTexts[5].text == ""
                && hud.inventoryPanel.transform.Find("WoodInventoryRow/ItemName").GetComponent<Text>().text
                    == "Wood",
                "The inventory did not show resource names or leave unowned tool slots empty.");
            hud.InventorySlotButtons[1].onClick.Invoke();
            Require(hud.SelectedInventorySlot == 1
                && hud.inventoryPanel.transform.Find("SelectedItem").GetComponent<Text>().text
                    .Contains("Stone"),
                "Selecting an inventory slot did not show its selected-item state.");
            Require(hud.inventoryPanel.transform.Find("AxeRecipe/AxeCraftCost").GetComponent<Text>().text
                    .Contains("Wood 2/1")
                && hud.inventoryPanel.transform.Find("AxeRecipe/RecipeStatus").GetComponent<Text>().text
                    == "Ready to craft",
                "The Axe recipe did not show current quantities against its material costs.");
            Require(hud.inventoryPanel.transform.Find("WorkbenchRecipe/RecipeCost").GetComponent<Text>()
                    .text.Contains("Hardwood 0/1")
                && hud.inventoryPanel.transform.Find("WorkbenchRecipe/RecipeCost").GetComponent<Text>()
                    .text.Contains("Ore 0/1")
                && hud.CraftingRecipeButtons[2].interactable
                && hud.inventoryPanel.transform.Find("WorkbenchRecipe/RecipeStatus").GetComponent<Text>()
                    .text == "Need more materials",
                "The Workbench recipe did not require Hardwood and Ore.");

            Dictionary<string, int> gathered = ResourceManager.Instance.Snapshot();
            var missingHardwood = new Dictionary<string, int>(gathered);
            missingHardwood["wood"] = 4;
            missingHardwood["stone"] = 2;
            missingHardwood["hardwood"] = 0;
            missingHardwood["ore"] = 1;
            ResourceManager.Instance.LoadInto(missingHardwood);
            Require(hud.CraftingRecipeButtons[2].interactable
                && hud.inventoryPanel.transform.Find("WorkbenchRecipe/RecipeStatus").GetComponent<Text>()
                    .text == "Need more materials",
                "The Workbench was craftable without its Hardwood ingredient.");
            var missingOre = new Dictionary<string, int>(gathered);
            missingOre["wood"] = 4;
            missingOre["stone"] = 2;
            missingOre["hardwood"] = 1;
            missingOre["ore"] = 0;
            ResourceManager.Instance.LoadInto(missingOre);
            Require(hud.CraftingRecipeButtons[2].interactable
                && hud.inventoryPanel.transform.Find("WorkbenchRecipe/RecipeStatus").GetComponent<Text>()
                    .text == "Need more materials",
                "The Workbench was craftable without Ore.");
            ResourceManager.Instance.LoadInto(gathered);
            var insufficient = new Dictionary<string, int>(gathered);
            insufficient["wood"] = 0;
            ResourceManager.Instance.LoadInto(insufficient);
            Require(hud.axeCraftButton.interactable && hud.pickaxeCraftButton.interactable
                && hud.inventoryPanel.transform.Find("AxeRecipe/RecipeStatus").GetComponent<Text>().text
                    == "Need more materials"
                && hud.inventoryPanel.transform.Find("PickaxeRecipe/RecipeStatus").GetComponent<Text>().text
                    == "Need more materials",
                "Unaffordable tool recipes were not navigable or clearly marked.");
            ResourceManager.Instance.LoadInto(gathered);
            Require(hud.axeCraftButton.interactable && hud.pickaxeCraftButton.interactable,
                "Craft buttons did not enable when the required materials were available.");

            hud.inventoryCloseButton.onClick.Invoke();
            Require(!hud.inventoryPanel.activeSelf,
                "The inventory Close button did not close the panel.");

            manager.Dialogue.Advance();
            ResourceNode hardwoodNode = null;
            foreach (ResourceNode node in ResourceNode.All)
                if (node != null && node.GetPersistenceId() == "HardwoodTree1")
                    hardwoodNode = node;
            Require(hardwoodNode != null && hardwoodNode.kind == "hardwood"
                && hardwoodNode.requiredTool == GatherTool.Axe && hardwoodNode.amount == 4,
                "HardwoodTree1 was not configured as a four-Hardwood Axe-gated resource node.");
            hardwoodNode.Interact();
            Require(manager.Dialogue.IsOpen && manager.Dialogue.bodyText.text == "Requires Axe."
                && ResourceManager.Instance.Get("hardwood") == 0 && hardwoodNode.IsRipe,
                "The Hardwood log could be harvested without an Axe.");
            manager.Dialogue.Advance();
            hud.axeCraftButton.onClick.Invoke();
            Require(ResourceManager.Instance.HasTool(GatherTool.Axe)
                && ResourceManager.Instance.Get("wood") == 1
                && ResourceManager.Instance.Get("stone") == 1,
                "Crafting an Axe did not grant ownership and consume exactly 1 Wood and 1 Stone.");
            Require(!hud.axeCraftButton.interactable && hud.pickaxeCraftButton.interactable
                && hud.craftingFeedbackText.text.Contains("Axe crafted"),
                "Axe crafting did not update its button state or show success feedback.");
            hardwoodNode.Interact();
            Require(ResourceManager.Instance.Get("hardwood") == hardwoodNode.amount
                && !hardwoodNode.IsRipe
                && hud.inventoryQuantityTexts[4].text == hardwoodNode.amount.ToString()
                && hud.resourcesText.text.Contains("HARDWOOD: " + hardwoodNode.amount),
                "An Axe did not harvest Hardwood into the inventory and HUD.");

            var oreNodeTransform = manager.town.transform.Find("TownRoot/Node_rock2");
            Require(oreNodeTransform != null, "The existing rubble resource node was not built.");
            var oreNode = oreNodeTransform.GetComponent<ResourceNode>();
            Require(oreNode != null && oreNode.kind == "ore"
                && oreNode.requiredTool == GatherTool.Pickaxe,
                "The rubble node was not configured as Pickaxe-gated Ore.");
            int oreBefore = ResourceManager.Instance.Get("ore");
            oreNode.Interact();
            Require(manager.Dialogue.IsOpen && manager.Dialogue.bodyText.text == "Requires Pickaxe."
                && ResourceManager.Instance.Get("ore") == oreBefore && oreNode.IsRipe,
                "Mining without a Pickaxe was not blocked with a clear requirement.");
            manager.Dialogue.Advance();

            hud.pickaxeCraftButton.onClick.Invoke();
            Require(ResourceManager.Instance.HasTool(GatherTool.Pickaxe)
                && ResourceManager.Instance.Get("wood") == 0
                && ResourceManager.Instance.Get("stone") == 0,
                "Crafting a Pickaxe did not grant ownership and consume exactly 1 Wood and 1 Stone.");
            Require(!hud.pickaxeCraftButton.interactable
                && hud.craftingFeedbackText.text.Contains("Pickaxe crafted"),
                "Pickaxe crafting did not update its button state or show success feedback.");
            Require(hud.InventorySlotQuantityTexts[5].text == "1"
                && hud.inventoryPanel.transform.Find("InventorySlot5/ItemName").GetComponent<Text>().text
                    == "Axe"
                && hud.InventorySlotQuantityTexts[6].text == "1"
                && hud.inventoryPanel.transform.Find("InventorySlot6/ItemName").GetComponent<Text>().text
                    == "Pickaxe",
                "Crafted tools did not appear in the inventory grid.");

            SaveSystem.SaveData craftedSave = SaveSystem.Load();
            Require(craftedSave != null && craftedSave.hasAxe && craftedSave.hasPickaxe
                && craftedSave.resources["wood"] == 0 && craftedSave.resources["stone"] == 0
                && craftedSave.resources["hardwood"] == hardwoodNode.amount,
                "Crafted tool ownership and material deductions were not persisted by SaveSystem.");
            ResourceManager.Instance.SetToolOwnership(false, false);
            Require(!ResourceManager.Instance.HasTool(GatherTool.Axe)
                && !ResourceManager.Instance.HasTool(GatherTool.Pickaxe),
                "The tool-ownership reload check could not clear its temporary state.");
            ResourceManager.Instance.SetToolOwnership(craftedSave.hasAxe, craftedSave.hasPickaxe);
            Require(ResourceManager.Instance.HasTool(GatherTool.Axe)
                && ResourceManager.Instance.HasTool(GatherTool.Pickaxe),
                "Saved tool ownership did not restore through ResourceManager.");

            oreNode.Interact();
            Require(ResourceManager.Instance.Get("ore") == oreBefore + oreNode.amount
                && !oreNode.IsRipe,
                "A Pickaxe did not allow the ore node to be mined for Ore.");
            Require(hud.inventoryQuantityTexts[3].text == (oreBefore + oreNode.amount).ToString()
                && hud.resourcesText.text.Contains("ORE: " + (oreBefore + oreNode.amount)),
                "Mining did not immediately update the inventory and resource HUD Ore counts.");
            manager.SaveGame();
            SaveSystem.SaveData minedSave = SaveSystem.Load();
            Require(minedSave != null && minedSave.resources["ore"] == oreBefore + oreNode.amount,
                "Ore quantity was not stored through the existing save system.");

            ResourceManager.Instance.Add("wood", 13);
            ResourceManager.Instance.Add("stone", 5);
            ResourceManager.Instance.Add("fiber", 3);
            ResourceManager.Instance.Add("ore", 5);
            hud.ToggleInventory();
            Require(manager.player.InputLocked && hud.inventoryPanel.activeSelf,
                "Opening the inventory did not stop player movement.");
            hud.CraftingRecipeButtons[2].onClick.Invoke();
            hud.CraftingRecipeButtons[3].onClick.Invoke();
            hud.CraftingRecipeButtons[4].onClick.Invoke();
            hud.CraftingRecipeButtons[5].onClick.Invoke();
            Require(ResourceManager.Instance.Get("workbench") == 1
                && ResourceManager.Instance.Get("campfire") == 1
                && ResourceManager.Instance.Get("storage_chest") == 1
                && ResourceManager.Instance.Get("fence") == 1
                && ResourceManager.Instance.Get("wood") == 0
                && ResourceManager.Instance.Get("stone") == 0
                && ResourceManager.Instance.Get("fiber") == 1
                && ResourceManager.Instance.Get("hardwood") == hardwoodNode.amount - 1,
                "Crafting a furniture or building recipe did not award its item and consume its materials.");

            hud.InventorySlotButtons[7].onClick.Invoke();
            Require(hud.PlaceSelectedButton.interactable,
                "Selecting the Workbench did not enable the placement action.");
            hud.PlaceSelectedButton.onClick.Invoke();
            Require(manager.IsPlacementActive && ResourceManager.Instance.Get("workbench") == 1,
                "Workbench placement mode did not start or consumed the item early.");
            RejectPlayerOverlappingPlacement(manager);
            Require(manager.IsPlacementActive && ResourceManager.Instance.Get("workbench") == 1,
                "Placement inside the player was accepted or consumed the Workbench.");
            manager.HandleCancelInput();
            Require(!manager.IsPlacementActive && ResourceManager.Instance.Get("workbench") == 1,
                "Cancelling Workbench placement consumed the item.");
            hud.ToggleInventory();
            hud.InventorySlotButtons[7].onClick.Invoke();
            hud.PlaceSelectedButton.onClick.Invoke();
            Vector2 workbenchCursor = FindValidPlacementPoint(manager, "workbench");
            manager.UpdatePlacement(workbenchCursor, true, false, 0f);
            Require(!manager.IsPlacementActive && ResourceManager.Instance.Get("workbench") == 0,
                "Valid Workbench placement did not consume exactly one Workbench.");
            PlacedWorldObject placedWorkbench = FindPlacedObject(manager, "workbench");
            Require(placedWorkbench != null,
                "Confirming placement did not create a physical Workbench.");
            _savedWorkbenchPosition = placedWorkbench.transform.position;
            SaveSystem.SaveData placedWorkbenchSave = SaveSystem.Load();
            Require(placedWorkbenchSave != null && placedWorkbenchSave.placedObjects.Count == 1
                && placedWorkbenchSave.placedObjects[0].kind == "workbench"
                && Vector3.Distance(placedWorkbenchSave.placedObjects[0].position,
                    _savedWorkbenchPosition) < 0.01f,
                "The placed Workbench was not persisted at its exact position.");
            placedWorkbench.GetComponent<Interactable>().Interact();
            Require(manager.IsWorkbenchOpen && hud.workbenchPanel.activeSelf
                && hud.WorkbenchRecipeButtons != null && hud.WorkbenchRecipeButtons.Length == 3
                && hud.workbenchPanel.transform.Find("WorkbenchTitle") != null
                && hud.workbenchPanel.transform.Find("MetalAxeWorkbenchRecipe") != null
                && hud.workbenchPanel.transform.Find("MetalPickaxeWorkbenchRecipe") != null
                && hud.workbenchPanel.transform.Find("TentWorkbenchRecipe") != null
                && hud.workbenchPanel.transform.childCount == 6,
                "Workbench interaction did not open a panel containing exactly three recipes.");
            hud.workbenchCloseButton.onClick.Invoke();
            Require(!manager.IsWorkbenchOpen && !hud.workbenchPanel.activeSelf
                && !manager.player.InputLocked,
                "Closing the Workbench panel did not restore gameplay input.");

            ResourceManager.Instance.Add("wood", 10);
            ResourceManager.Instance.Add("stone", 10);
            ResourceManager.Instance.Add("fiber", 10);
            ResourceManager.Instance.Add("ore", 5);
            ResourceManager.Instance.Add("hardwood", 5);
            placedWorkbench.GetComponent<Interactable>().Interact();
            Require(hud.WorkbenchRecipeButtons[0].interactable
                && hud.WorkbenchRecipeButtons[1].interactable
                && hud.WorkbenchRecipeButtons[2].interactable,
                "The Workbench did not enable affordable second-tier recipes.");
            hud.WorkbenchRecipeButtons[0].onClick.Invoke();
            hud.WorkbenchRecipeButtons[1].onClick.Invoke();
            hud.WorkbenchRecipeButtons[2].onClick.Invoke();
            Require(ResourceManager.Instance.Get("metal_axe") == 1
                && ResourceManager.Instance.Get("metal_pickaxe") == 1
                && ResourceManager.Instance.Get("tent") == 1,
                "The Workbench did not craft all three second-tier items.");
            manager.CloseWorkbench();

            hud.ToggleInventory();
            hud.InventorySlotButtons[13].onClick.Invoke();
            Require(hud.PlaceSelectedButton.interactable,
                "Selecting the crafted Tent did not enable the placement action.");
            hud.PlaceSelectedButton.onClick.Invoke();
            Require(manager.IsPlacementActive && ResourceManager.Instance.Get("tent") == 1,
                "Tent placement mode did not start or consumed the Tent early.");
            manager.HandleCancelInput();
            Require(ResourceManager.Instance.Get("tent") == 1,
                "Cancelling Tent placement consumed the item.");
            hud.ToggleInventory();
            hud.InventorySlotButtons[13].onClick.Invoke();
            hud.PlaceSelectedButton.onClick.Invoke();
            Vector2 tentCursor = FindValidPlacementPoint(manager, "tent");
            manager.UpdatePlacement(tentCursor, true, false, 0f);
            Require(ResourceManager.Instance.Get("tent") == 0,
                "Placing a Tent did not consume exactly one Tent.");
            PlacedWorldObject placedTent = FindPlacedObject(manager, "tent");
            Require(placedTent != null && placedTent.GetComponent<Interactable>() == null,
                "The placed Tent is missing or has unintended interaction functionality.");
            _savedTentPosition = placedTent.transform.position;
            SaveSystem.SaveData allPlacedSave = SaveSystem.Load();
            Require(allPlacedSave != null && allPlacedSave.placedObjects.Count == 2
                && allPlacedSave.resources["metal_axe"] == 1
                && allPlacedSave.resources["metal_pickaxe"] == 1,
                "Placed objects or crafted Metal tools were not persisted.");

            hud.CloseInventory();
            Require(!manager.player.InputLocked && !hud.inventoryPanel.activeSelf,
                "Closing the inventory did not restore player movement.");
            hud.ToggleInventory();
            hud.CloseInventory();
            hud.ToggleInventory();
            hud.CloseInventory();
            Require(!hud.inventoryPanel.activeSelf && ResourceManager.Instance.Get("workbench") == 0,
                "Repeatedly opening and closing inventory duplicated or lost the placed Workbench state.");
            manager.SaveGame();
            SaveSystem.SaveData recipeSave = SaveSystem.Load();
            Require(recipeSave != null && recipeSave.resources["workbench"] == 0
                && recipeSave.resources["campfire"] == 1
                && recipeSave.resources["storage_chest"] == 1
                && recipeSave.resources["fence"] == 1
                && recipeSave.resources["hardwood"] > 0
                && recipeSave.resources["metal_axe"] == 1
                && recipeSave.resources["metal_pickaxe"] == 1
                && recipeSave.resources["tent"] == 0
                && recipeSave.placedObjects.Count == 2,
                "Crafted inventory items were not persisted through SaveSystem.");
        }

        private static void RejectPlayerOverlappingPlacement(GameManager manager)
        {
            for (int i = 0; i < 100; i++)
            {
                manager.UpdatePlacement(Vector2.down, false, false, 0f);
                if (!manager.PlacementIsValid) break;
            }
            manager.UpdatePlacement(Vector2.zero, true, false, 0f);
            Require(manager.IsPlacementActive,
                "An invalid player-overlapping placement was accepted.");
        }

        private static Vector2 FindValidPlacementPoint(GameManager manager, string item)
        {
            Vector2[] directions =
            {
                Vector2.zero, Vector2.up, Vector2.right, Vector2.down, Vector2.left,
                new Vector2(1f, 1f), new Vector2(-1f, 1f),
                new Vector2(1f, -1f), new Vector2(-1f, -1f),
            };
            foreach (Vector2 direction in directions)
            {
                for (int step = 0; step < 100; step++)
                {
                    manager.UpdatePlacement(direction, false, false, 0f);
                    if (manager.PlacementIsValid) return Vector2.zero;
                }
            }

            throw new InvalidOperationException("[Wispmere] Could not find a valid " + item
                + " placement point by moving the preview with controller-style input.");
        }

        private static PlacedWorldObject FindPlacedObject(GameManager manager, string kind)
        {
            foreach (PlacedWorldObject placed in manager.town.GetComponentsInChildren<PlacedWorldObject>())
                if (placed.kind == kind)
                    return placed;
            return null;
        }

        private static void CheckPlacedObjectPersistence(GameManager manager)
        {
            int workbenchCount = 0;
            int tentCount = 0;
            foreach (PlacedWorldObject placed in manager.town.GetComponentsInChildren<PlacedWorldObject>())
            {
                if (placed.kind == "workbench")
                {
                    workbenchCount++;
                    Require(Vector3.Distance(placed.transform.position, _savedWorkbenchPosition) < 0.01f,
                        "The Workbench position changed after loading.");
                }
                else if (placed.kind == "tent")
                {
                    tentCount++;
                    Require(Vector3.Distance(placed.transform.position, _savedTentPosition) < 0.01f,
                        "The Tent position changed after loading.");
                }
            }
            Require(workbenchCount == 1 && tentCount == 1,
                "Loading duplicated or lost a placed Workbench or Tent.");
            Require(ResourceManager.Instance.Get("workbench") == 0
                && ResourceManager.Instance.Get("tent") == 0,
                "Loading duplicated a placed object's consumed inventory item.");
        }

        private static void VerifyInventoryLayout(GameHUD hud)
        {
            RectTransform panel = hud.inventoryPanel.GetComponent<RectTransform>();
            Canvas canvas = hud.inventoryPanel.GetComponentInParent<Canvas>();
            Require(canvas != null
                && panel.rect.width * canvas.scaleFactor <= canvas.pixelRect.width * 0.93f
                && panel.rect.height * canvas.scaleFactor <= canvas.pixelRect.height * 0.95f,
                "The inventory panel exceeds the current Game view bounds.");

            float lowestSlotBottom = float.MaxValue;
            foreach (Button slotButton in hud.InventorySlotButtons)
            {
                RectTransform slot = slotButton.GetComponent<RectTransform>();
                Vector2 position = slot.anchoredPosition;
                Require(position.x - slot.rect.width * 0.5f >= -panel.rect.width * 0.5f
                    && position.x + slot.rect.width * 0.5f <= panel.rect.width * 0.5f
                    && position.y - slot.rect.height * 0.5f >= -panel.rect.height * 0.5f
                    && position.y + slot.rect.height * 0.5f <= panel.rect.height * 0.5f,
                    "Inventory slot " + slotButton.name + " exceeds panel bounds (position "
                    + position + ", size " + slot.rect.size + ", panel " + panel.rect.size + ").");
                lowestSlotBottom = Mathf.Min(lowestSlotBottom,
                    position.y - slot.rect.height * 0.5f);
            }
            RectTransform selectedItem = (RectTransform)hud.inventoryPanel.transform.Find("SelectedItem");
            Require(selectedItem.anchoredPosition.y + selectedItem.rect.height * 0.5f
                    <= lowestSlotBottom,
                "The inventory footer overlaps the item grid.");

            RectTransform[] cards = new RectTransform[hud.CraftingRecipeButtons.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = hud.CraftingRecipeButtons[i].transform.parent.GetComponent<RectTransform>();
                float top = cards[i].anchoredPosition.y + cards[i].rect.height * 0.5f;
                float bottom = cards[i].anchoredPosition.y - cards[i].rect.height * 0.5f;
                Require(top <= panel.rect.height * 0.5f && bottom >= -panel.rect.height * 0.5f,
                    "A crafting recipe row is clipped by the inventory panel.");
                if (i > 0)
                {
                    float previousBottom = cards[i - 1].anchoredPosition.y
                        - cards[i - 1].rect.height * 0.5f;
                    Require(top < previousBottom,
                        "Crafting recipe rows overlap in the current Game view.");
                }
            }

            for (int i = 0; i < cards.Length; i++)
            {
                foreach (Transform child in cards[i])
                {
                    RectTransform childRect = child.GetComponent<RectTransform>();
                    if (childRect == null || child.name == "RecipeIcon") continue;
                    Rect rect = childRect.rect;
                    Vector2 position = childRect.anchoredPosition;
                    Require(position.x - rect.width * 0.5f >= -cards[i].rect.width * 0.5f
                        && position.x + rect.width * 0.5f <= cards[i].rect.width * 0.5f
                        && position.y - rect.height * 0.5f >= -cards[i].rect.height * 0.5f
                        && position.y + rect.height * 0.5f <= cards[i].rect.height * 0.5f,
                        "Text or a craft button is clipped by its recipe row.");
                }
            }
        }

        private static void CheckWorkshopInteraction()
        {
            var manager = GameManager.Instance;
            Interactable workshop = null;
            foreach (var interactable in Interactable.All)
                if (interactable.isRestorationTarget)
                    workshop = interactable;
            Require(workshop != null && workshop.label == "Old Workshop",
                "The Old Workshop restoration target is missing.");
            Require(manager.Dialogue.IsOpen && manager.Dialogue.nameText.text == "Old Workshop"
                && manager.Dialogue.bodyText.text.Contains("needs materials"),
                "The proximity-interacted workshop did not explain its restoration material requirement.");
            Require(manager.hud.objectiveText.text.Contains("GATHER MATERIALS FOR THE OLD WORKSHOP"),
                "Discovering the workshop did not update the objective.");

            var save = SaveSystem.Load();
            Require(save != null && save.workshopDiscovered,
                "Workshop discovery did not persist with the normal save.");
        }

        private static void MovePlayerToOpenCameraSpace()
        {
            var player = GameManager.Instance.player;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(0f, 0f, 0f);
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void StartCameraInputCheck()
        {
            var camera = GameManager.Instance.cam;
            _cameraInputStart = camera.transform.position;
            _cameraRotationStart = camera.transform.rotation;
            _cameraYawStart = camera.thirdPersonYaw;
            _cameraZoomStart = camera.thirdPersonDistance;
            camera.AdjustOrbit(new Vector2(80f, 1000f));
            camera.AdjustZoom(100000f);
            Require(Mathf.Abs(Mathf.DeltaAngle(_cameraYawStart, camera.thirdPersonYaw)) > 5f
                && camera.thirdPersonPitch == 8f
                && camera.TargetZoomDistance == camera.thirdPersonMinimumDistance,
                "Camera orbit or minimum zoom input did not respect its tuning limits.");
        }

        private static void CheckCameraOrbitAndMinimumZoom()
        {
            var camera = GameManager.Instance.cam;
            float cameraMoved = Vector3.Distance(camera.transform.position, _cameraInputStart);
            Require(camera.thirdPersonDistance < _cameraZoomStart - 1.5f && cameraMoved > 1f,
                "The camera did not ease toward its zoomed-in position. Start zoom="
                + _cameraZoomStart + ", current zoom=" + camera.thirdPersonDistance
                + ", target zoom=" + camera.TargetZoomDistance + ", camera displacement=" + cameraMoved
                + ", camera position=" + camera.transform.position + ".");
            Require(Quaternion.Angle(_cameraRotationStart, camera.transform.rotation) > 2f,
                "Camera orbit rotation did not ease toward the new angle.");
        }

        private static void BeginCameraObstructionChecks()
        {
            CameraFollow camera = GameManager.Instance.cam;
            float openSpaceDistance = Vector3.Distance(camera.transform.position, camera.target.position);
            Require(openSpaceDistance > camera.thirdPersonDistance * 0.65f,
                "In open space the camera did not maintain a comfortable distance from the player.");
            _cameraPositionBeforeObstruction = camera.transform.position;
            _cameraMotionSampled = false;
            CreateCameraTestObstacle(PrimitiveType.Cube, "Camera Test Building",
                new Vector3(5f, 8f, 2.5f));
        }

        private static void CreateCameraTestObstacle(PrimitiveType type, string obstacleName, Vector3 size)
        {
            CameraFollow camera = GameManager.Instance.cam;
            Vector3 focus = GetCameraTestFocus(camera);
            Vector3 desiredPosition = GetCameraTestDesiredPosition(camera);
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(desiredPosition - focus, Vector3.up).normalized;
            _cameraTestObstacle = GameObject.CreatePrimitive(type);
            _cameraTestObstacle.name = obstacleName;
            _cameraTestObstacle.transform.position = focus + (desiredPosition - focus) * 0.55f;
            _cameraTestObstacle.transform.rotation = Quaternion.LookRotation(horizontalDirection, Vector3.up);
            _cameraTestObstacle.transform.localScale = size;
            Physics.SyncTransforms();
        }

        private static void CheckCameraObstruction(string scenario)
        {
            CameraFollow camera = GameManager.Instance.cam;
            Vector3 focus = GetCameraTestFocus(camera);
            Vector3 toCamera = camera.transform.position - focus;
            float playerDistance = Vector3.Distance(camera.transform.position, camera.target.position);
            Require(playerDistance > camera.thirdPersonDistance * 0.6f,
                "Camera moved too close to the player during the " + scenario + ".");
            Require(Vector3.Angle(camera.transform.forward, -toCamera) < 15f,
                "Player framing was lost during the " + scenario + ".");

            RaycastHit[] hits = Physics.SphereCastAll(focus, camera.collisionRadius,
                toCamera.normalized, toCamera.magnitude, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == camera.target || hitTransform.IsChildOf(camera.target)) continue;
                Require(false, "Camera still clipped an obstruction during the " + scenario + ".");
            }
        }

        private static Vector3 GetCameraTestFocus(CameraFollow camera)
        {
            Vector3 cameraForward = Quaternion.Euler(0f,
                _cameraBaseYaw + camera.thirdPersonYaw, 0f) * Vector3.forward;
            return camera.target.position + cameraForward * camera.thirdPersonLookAheadDistance
                + Vector3.up * camera.thirdPersonFocusHeight;
        }

        private static Vector3 GetCameraTestDesiredPosition(CameraFollow camera)
        {
            Vector3 focus = GetCameraTestFocus(camera);
            float pitch = camera.thirdPersonPitch * Mathf.Deg2Rad;
            float yaw = (_cameraBaseYaw + camera.thirdPersonYaw) * Mathf.Deg2Rad;
            float horizontalDistance = Mathf.Cos(pitch) * camera.thirdPersonDistance;
            float verticalDistance = Mathf.Sin(pitch) * camera.thirdPersonDistance;
            Vector3 behind = new Vector3(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
            Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
            return focus + behind * horizontalDistance
                + right * camera.thirdPersonShoulderOffset
                + Vector3.up * verticalDistance;
        }

        private static void MovePlayerForCameraObstructionCheck(Vector3 position)
        {
            PlayerController player = GameManager.Instance.player;
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void SetPhase(int phase)
        {
            SessionState.SetInt(PhaseKey, phase);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
        }

        private static void Finish(bool passed, string message)
        {
            if (_cameraTestObstacle != null)
                UnityEngine.Object.Destroy(_cameraTestObstacle);
            RestoreSave();
            SessionState.SetBool(RunningKey, false);
            if (passed) Debug.Log("[Wispmere] PLAYABLE TOWN SLICE TEST PASSED: " + message);
            else Debug.LogError("[Wispmere] PLAYABLE TOWN SLICE TEST FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void RestoreSave()
        {
            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            string backupPath = SessionState.GetString(BackupPathKey, "");
            if (SessionState.GetBool(HadSaveKey, false) && File.Exists(backupPath))
                File.Copy(backupPath, savePath, true);
            else if (File.Exists(savePath))
                File.Delete(savePath);

            if (File.Exists(backupPath)) File.Delete(backupPath);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Wispmere] " + message);
        }
    }
}
