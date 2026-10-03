using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        private const string SaveFileName = "wispmere_save_v1.json";
        private const string ScenePath = "Assets/Wispmere/Scenes/Wispmere.unity";
        private const double PhaseTimeoutSeconds = 75.0;

        private static Vector3 _playerStart;
        private static Vector3 _playerGoal;
        private static Vector3 _cameraStart;
        private static ResourceNode _expectedNode;
        private static Vector3 _cameraInputStart;
        private static Quaternion _cameraRotationStart;
        private static float _cameraYawStart;
        private static float _cameraZoomStart;
        private static GameObject _cameraTestObstacle;
        private static Vector3 _cameraPositionBeforeObstruction;
        private static bool _cameraMotionSampled;
        private static bool _arrivalDialogueChecked;
        private static bool _arrivalSecondLineChecked;

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
            if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying) return;

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
                        if (manager.player.HasPendingClickMovement)
                        {
                            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) > 8.0)
                                throw new TimeoutException("Click-to-move did not reach its ground destination.");
                            return;
                        }
                        CheckMouseGroundMovement();
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
                            && GameManager.Instance.hud.resourcesText.text.Contains("ORE: 0"),
                            "The resource HUD did not display click-gathered counts.");
                        BeginGather(GameManager.Instance, "wood");
                        SetPhase(7);
                        break;
                    case 7:
                        if (!CheckGatherComplete("wood", 2)) return;
                        BeginWorkshopClick(GameManager.Instance);
                        SetPhase(8);
                        break;
                    case 8:
                        if (!GameManager.Instance.Dialogue.IsOpen)
                        {
                            RequirePhaseProgress("Workshop click-to-interact");
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
                            == GameManager.Instance.cam.maximumZoom
                            && GameManager.Instance.cam.zoomDistance > GameManager.Instance.cam.maximumZoom - 3f,
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
                        Finish(true, "Creator, faster physical arrival, separated introduction name/text, assigned village buildings, smoothed camera follow/orbit/zoom, click-to-move, UI click blocking, wood/stone/fiber gathering, inventory quantities, Axe/Pickaxe crafting and persistence, pickaxe-gated ore mining, workshop interaction, and open-space/building/tree camera obstruction checks all passed.");
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

        private static void BeginNewGame()
        {
            var manager = GameManager.Instance;
            Require(manager != null && manager.mainMenuPanel.activeSelf,
                "The existing startup menu did not appear.");
            Require(manager.town != null && manager.town.transform.Find("TownRoot") != null,
                "The existing town builder did not create the town.");
            Require(manager.player != null && !manager.player.gameObject.activeSelf,
                "The existing player did not remain behind the startup menu.");
            Require(manager.cam != null && manager.cam.target == manager.player.transform,
                "The elevated action-RPG camera is not wired to the existing player.");
            Require(manager.cam.usePolishedControls && manager.cam.pitch >= 35f
                && manager.cam.pitch <= 50f && manager.cam.zoomDistance >= manager.cam.minimumZoom,
                "The polished elevated camera controls are not enabled or tuned correctly.");
            Require(manager.cam.playOffset.y > 0f && manager.cam.playOffset.magnitude >= 24f,
                "The gameplay camera is not sufficiently elevated and pulled back.");

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
            _cameraStart = manager.cam.transform.position;
            _playerGoal = _playerStart + new Vector3(2.2f, 0f, 0f);
            Ray groundRay = new Ray(manager.cam.transform.position, _playerGoal - manager.cam.transform.position);
            Require(!manager.player.TryHandleWorldClick(groundRay, true)
                && !manager.player.HasPendingClickMovement,
                "A click over UI was not blocked from issuing world movement.");
            Require(manager.player.TryHandleWorldClick(groundRay, false)
                && manager.player.HasPendingClickMovement,
                "Clicking walkable ground did not start click-to-move.");
        }

        private static void CheckMouseGroundMovement()
        {
            var manager = GameManager.Instance;
            float moved = Vector3.Distance(_playerStart, manager.player.transform.position);
            Require(moved > 1f,
                "Click-to-move did not move the player to the ground destination. Start="
                + _playerStart + ", goal=" + _playerGoal + ", end=" + manager.player.transform.position
                + ", moved=" + moved + ", camera=" + manager.cam.transform.position
                + ", pending=" + manager.player.HasPendingClickMovement + ".");
            Require(Vector3.Distance(_cameraStart, manager.cam.transform.position) > 0.1f,
                "The elevated gameplay camera did not follow the moving player.");
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
                    if (IsResourceNodeVisible(manager, node)) preferredTree = node;
                    continue;
                }
                float distance = Vector3.Distance(manager.player.transform.position, node.transform.position);
                if (distance < best && IsResourceNodeVisible(manager, node))
                {
                    best = distance;
                    target = node;
                }
            }

            if (preferredTree != null) target = preferredTree;
            Require(target != null, "No ripe " + kind + " gathering node exists.");
            _expectedNode = target;
            Vector3 aim = target.transform.position + Vector3.up * 0.35f;
            Ray targetRay = new Ray(manager.cam.transform.position, aim - manager.cam.transform.position);
            bool handled = manager.player.TryHandleWorldClick(targetRay, false);
            if (!handled || manager.player.ClickTargetLabel != target.label)
            {
                RaycastHit[] hits = Physics.RaycastAll(targetRay, 200f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                string hitNames = "";
                foreach (RaycastHit hit in hits)
                    hitNames += (hitNames.Length == 0 ? "" : ", ") + hit.collider.name
                        + "@" + hit.point + " d=" + hit.distance.ToString("F2");
                throw new InvalidOperationException("[Wispmere] Clicking the " + kind
                    + " node did not prioritize that resource target. Expected " + target.label
                    + " at " + target.transform.position + ", handled=" + handled
                    + ", selected=" + manager.player.ClickTargetLabel
                    + ", feedback=" + manager.player.MouseFeedback
                    + ", camera=" + manager.cam.transform.position
                    + ", player=" + manager.player.transform.position
                    + ", rayHits=[" + hitNames + "].");
            }
        }

        private static bool IsResourceNodeVisible(GameManager manager, ResourceNode target)
        {
            Vector3 aim = target.transform.position + Vector3.up * 0.35f;
            Ray ray = new Ray(manager.cam.transform.position, aim - manager.cam.transform.position);
            RaycastHit[] hits = Physics.RaycastAll(ray, 200f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                ResourceNode hitNode = hit.collider.GetComponentInParent<ResourceNode>();
                if (hitNode != null) return hitNode == target;
                if (hit.collider.gameObject.name == "Ground" || !hit.collider.isTrigger)
                    return false;
            }
            return false;
        }

        private static bool CheckGatherComplete(string kind, int expectedCount)
        {
            if (ResourceManager.Instance.Get(kind) != expectedCount)
            {
                RequirePhaseProgress("Click-to-gather " + kind);
                return false;
            }
            Require(_expectedNode != null && !_expectedNode.IsRipe,
                "The clicked " + kind + " node did not hide after gathering.");
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

        private static void BeginWorkshopClick(GameManager manager)
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

            Ray workshopRay = new Ray(manager.cam.transform.position,
                workshop.transform.position + Vector3.up * 0.9f - manager.cam.transform.position);
            Require(manager.player.TryHandleWorldClick(workshopRay, false)
                && manager.player.ClickTargetLabel == "Old Workshop",
                "Clicking the workshop did not select its interactable collider.");
        }

        private static void VerifyInventoryPanel(GameManager manager)
        {
            GameHUD hud = manager.hud;
            Require(hud.inventoryButton != null && hud.inventoryPanel != null
                && hud.inventoryCloseButton != null && hud.axeCraftButton != null
                && hud.pickaxeCraftButton != null && hud.inventoryQuantityTexts != null
                && hud.inventoryQuantityTexts.Length == 4,
                "The inventory, resource rows, or crafting controls are not wired.");
            Require(!hud.inventoryPanel.activeSelf,
                "The inventory panel should start closed.");

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
                && hud.inventoryPanel.transform.Find("AxeCraftCost") != null
                && hud.inventoryPanel.transform.Find("PickaxeCraftCost") != null,
                "The inventory panel is missing a resource row or crafting entry.");
            Require(hud.resourcesText.text == resourcesBefore && manager.Dialogue.IsOpen,
                "Opening Inventory changed resource HUD data or interrupted the current dialogue.");
            Require(hud.inventoryQuantityTexts[0].text == "2"
                && hud.inventoryQuantityTexts[1].text == "2"
                && hud.inventoryQuantityTexts[2].text == "1"
                && hud.inventoryQuantityTexts[3].text == "0",
                "The inventory rows did not show the gathered resource quantities.");

            Dictionary<string, int> gathered = ResourceManager.Instance.Snapshot();
            var insufficient = new Dictionary<string, int>(gathered);
            insufficient["wood"] = 0;
            ResourceManager.Instance.LoadInto(insufficient);
            Require(!hud.axeCraftButton.interactable && !hud.pickaxeCraftButton.interactable,
                "Craft buttons remained enabled without the required materials.");
            ResourceManager.Instance.LoadInto(gathered);
            Require(hud.axeCraftButton.interactable && hud.pickaxeCraftButton.interactable,
                "Craft buttons did not enable when the required materials were available.");

            hud.inventoryCloseButton.onClick.Invoke();
            Require(!hud.inventoryPanel.activeSelf,
                "The inventory Close button did not close the panel.");

            manager.Dialogue.Advance();
            hud.axeCraftButton.onClick.Invoke();
            Require(ResourceManager.Instance.HasTool(GatherTool.Axe)
                && ResourceManager.Instance.Get("wood") == 1
                && ResourceManager.Instance.Get("stone") == 1,
                "Crafting an Axe did not grant ownership and consume exactly 1 Wood and 1 Stone.");
            Require(!hud.axeCraftButton.interactable && hud.pickaxeCraftButton.interactable
                && hud.craftingFeedbackText.text.Contains("Axe crafted"),
                "Axe crafting did not update its button state or show success feedback.");

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

            SaveSystem.SaveData craftedSave = SaveSystem.Load();
            Require(craftedSave != null && craftedSave.hasAxe && craftedSave.hasPickaxe
                && craftedSave.resources["wood"] == 0 && craftedSave.resources["stone"] == 0,
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
                "The click-to-interact workshop did not explain its restoration material requirement.");
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
            _cameraYawStart = camera.yaw;
            _cameraZoomStart = camera.zoomDistance;
            camera.AdjustOrbit(new Vector2(80f, 1000f));
            camera.AdjustZoom(100000f);
            Require(Mathf.Abs(Mathf.DeltaAngle(_cameraYawStart, camera.yaw)) > 5f
                && camera.pitch == 35f
                && camera.TargetZoomDistance == camera.minimumZoom,
                "Camera orbit or minimum zoom input did not respect its tuning limits.");
        }

        private static void CheckCameraOrbitAndMinimumZoom()
        {
            var camera = GameManager.Instance.cam;
            float cameraMoved = Vector3.Distance(camera.transform.position, _cameraInputStart);
            Require(camera.zoomDistance < _cameraZoomStart - 3f && cameraMoved > 1f,
                "The camera did not ease toward its zoomed-in position. Start zoom="
                + _cameraZoomStart + ", current zoom=" + camera.zoomDistance
                + ", target zoom=" + camera.TargetZoomDistance + ", camera displacement=" + cameraMoved
                + ", camera position=" + camera.transform.position + ".");
            Require(Quaternion.Angle(_cameraRotationStart, camera.transform.rotation) > 2f,
                "Camera orbit rotation did not ease toward the new angle.");
        }

        private static void BeginCameraObstructionChecks()
        {
            CameraFollow camera = GameManager.Instance.cam;
            float openSpaceDistance = Vector3.Distance(camera.transform.position, camera.target.position);
            Require(openSpaceDistance > camera.zoomDistance * 0.65f,
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
            Require(playerDistance > camera.zoomDistance * 0.6f,
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
            Vector3 heading = Vector3.ProjectOnPlane(camera.target.forward, Vector3.up).normalized;
            return camera.target.position + Vector3.up * camera.focusHeight
                + heading * camera.lookAheadDistance;
        }

        private static Vector3 GetCameraTestDesiredPosition(CameraFollow camera)
        {
            Vector3 focus = GetCameraTestFocus(camera);
            float pitch = Mathf.Clamp(camera.pitch, 35f, 50f) * Mathf.Deg2Rad;
            float horizontalDistance = Mathf.Cos(pitch) * camera.zoomDistance;
            float verticalDistance = Mathf.Sin(pitch) * camera.zoomDistance;
            Vector3 offset = new Vector3(
                Mathf.Sin(camera.yaw * Mathf.Deg2Rad) * horizontalDistance,
                verticalDistance,
                Mathf.Cos(camera.yaw * Mathf.Deg2Rad) * horizontalDistance);
            return camera.target.position + offset;
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
