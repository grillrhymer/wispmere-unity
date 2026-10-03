using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wispmere;

namespace Wispmere.Editor
{
    [InitializeOnLoad]
    public static class StarterScenePlayModeSmokeTest
    {
        private const string RunningKey = "Wispmere.PlayModeSmokeTest.Running";
        private const string PhaseKey = "Wispmere.PlayModeSmokeTest.Phase";
        private const string StartTimeKey = "Wispmere.PlayModeSmokeTest.StartTime";
        private const string HadSaveKey = "Wispmere.PlayModeSmokeTest.HadSave";
        private const string BackupPathKey = "Wispmere.PlayModeSmokeTest.BackupPath";
        private const string SaveFileName = "wispmere_save_v1.json";
        private const double PhaseTimeoutSeconds = 30.0;

        static StarterScenePlayModeSmokeTest()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        [MenuItem("Wispmere/Test/Run Starter Scene Play Mode Smoke Test")]
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
                Debug.LogError("[Wispmere] A play-mode smoke test is already running.");
                return;
            }

            const string scenePath = "Assets/Wispmere/Scenes/Wispmere.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError("[Wispmere] Starter scene is missing. Run Wispmere → Create → Rebuild Starter Scene first.");
                return;
            }
            if (confirmSceneSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            string backupPath = Path.Combine("Library", "WispmerePlayModeSaveBackup.json");
            SessionState.SetBool(HadSaveKey, File.Exists(savePath));
            SessionState.SetString(BackupPathKey, backupPath);
            if (File.Exists(savePath)) File.Copy(savePath, backupPath, true);
            if (File.Exists(savePath)) File.Delete(savePath);

            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(RunningKey, true);
            Debug.Log("[Wispmere] Starting starter-scene play-mode smoke test.");
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
                    string state = "";
                    if (SessionState.GetInt(PhaseKey, 0) == 2 && GameManager.Instance != null
                        && GameManager.Instance.player != null)
                    {
                        state = " Player position: " + GameManager.Instance.player.transform.position
                            + "; input locked: " + GameManager.Instance.player.InputLocked
                            + "; dialogue open: " + GameManager.Instance.Dialogue.IsOpen + ".";
                    }
                    throw new TimeoutException("Timed out during smoke-test phase "
                        + SessionState.GetInt(PhaseKey, 0) + "." + state);
                }

                switch (SessionState.GetInt(PhaseKey, 0))
                {
                    case 0:
                        CheckMainMenuAndOpenCreator();
                        SetPhase(1);
                        break;
                    case 1:
                        BeginNewGame();
                        SetPhase(2);
                        break;
                    case 2:
                        AdvanceArrivalDialogue();
                        break;
                    case 3:
                        CheckFreeRoamAndPause();
                        SetPhase(4);
                        break;
                    case 4:
                        CheckSaveAndFinish();
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected smoke-test phase.");
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static void CheckMainMenuAndOpenCreator()
        {
            var manager = GameManager.Instance;
            Require(manager != null, "GameManager did not initialize.");
            Require(manager.mainMenuPanel.activeSelf, "Main menu is not visible at startup.");
            Require(manager.town.transform.Find("TownRoot") != null, "TownBuilder did not build the town.");
            CheckPlaceholderShapes(manager.town);
            Require(manager.player != null && !manager.player.gameObject.activeSelf,
                "Player should be hidden until a game starts.");
            Require(manager.newGameButton != null && manager.newGameButton.interactable,
                "New Game button is not wired.");
            Require(manager.continueButton != null && !manager.continueButton.interactable,
                "Continue should be disabled when no save exists.");
            manager.newGameButton.onClick.Invoke();
            Require(manager.creatorPanel.activeSelf, "New Game did not open the character creator.");
        }

        private static void CheckPlaceholderShapes(TownBuilder town)
        {
            Transform root = town.transform.Find("TownRoot");
            var tree = root.Find("tree1");
            Require(tree != null && tree.Find("Trunk") != null
                && tree.Find("Trunk").GetComponent<CapsuleCollider>() != null
                && tree.Find("Canopy") != null
                && tree.Find("Canopy").GetComponent<Renderer>() != null,
                "Fallback trees must have cylindrical trunks and visible canopies.");

            var house = root.Find("inn");
            Require(house != null && house.localScale.y >= 2f
                && house.Find("RoofLeft") != null && house.Find("RoofRight") != null
                && house.Find("Door") != null && house.Find("WindowLeft") != null,
                "Fallback houses must have full-height walls, roofs, doors, and windows.");

            Require(root.Find("Node_log1/Log") != null
                && root.Find("Node_rock1/Stone") != null
                && root.Find("Node_bush1/Fiber0") != null,
                "Fallback wood, stone, and fiber nodes must have distinguishable silhouettes.");
        }

        private static void BeginNewGame()
        {
            var creator = GameManager.Instance.creatorPanel.GetComponent<CreatorUI>();
            Require(creator != null, "Creator UI is missing.");
            Require(creator.rowsParent.childCount == 6, "Creator did not build all six appearance rows.");
            Require(creator.previewRoot.gameObject.activeSelf
                && creator.previewImage.texture is RenderTexture,
                "Creator did not show its isolated live character preview.");

            var preview = creator.previewRoot.GetComponentInChildren<CharacterCustomizer>();
            Require(preview != null, "Creator preview character is missing.");
            SelectLastOption(creator, preview, 0, "Body", AppearanceLibrary.Bodies);
            SelectLastOption(creator, preview, 1, "Skin", AppearanceLibrary.Skins);
            SelectLastOption(creator, preview, 2, "Hair style", AppearanceLibrary.HairStyles);
            SelectLastOption(creator, preview, 3, "Hair color", AppearanceLibrary.HairColors);
            SelectLastOption(creator, preview, 4, "Eye color", AppearanceLibrary.EyeColors);
            SelectLastOption(creator, preview, 5, "Outfit", AppearanceLibrary.Outfits);

            creator.nameField.text = "Smoke Tester";
            creator.beginButton.onClick.Invoke();
            Require(!GameManager.Instance.creatorPanel.activeSelf, "Beginning the game did not close the creator.");
            Require(GameManager.Instance.player.gameObject.activeSelf, "Beginning the game did not activate the player.");
            var save = SaveSystem.Load();
            Require(save != null && save.appearance.body == "broad" && save.appearance.skin == "moss"
                && save.appearance.hair == "tuft" && save.appearance.hairColor == "goldcap"
                && save.appearance.eyes == "moss" && save.appearance.outfit == "pondskipper",
                "Direct creator selections did not persist through the existing begin-adventure flow.");
            Vector3 spawn = WorldLayout.ToUnity(GameManager.Instance.town.Layout.spawn.x,
                GameManager.Instance.town.Layout.spawn.y);
            Require(Vector3.Distance(GameManager.Instance.player.transform.position, spawn) < 0.01f,
                "Beginning the game did not move the player to the town spawn. Expected "
                + spawn + ", got " + GameManager.Instance.player.transform.position + ".");
        }

        private static void SelectLastOption(CreatorUI creator, CharacterCustomizer preview,
            int rowIndex, string expectedLabel,
            AppearanceOption[] options)
        {
            var row = creator.rowsParent.GetChild(rowIndex).GetComponent<CreatorRow>();
            Require(row.label.text == expectedLabel, "Creator category label is incorrect: " + row.label.text);
            Require(row.optionsParent.childCount == options.Length,
                "Creator category does not expose every named option for " + expectedLabel + ".");
            int selectedIndex = options.Length - 1;
            row.optionsParent.GetChild(selectedIndex).GetComponent<Button>().onClick.Invoke();
            int selectedCount = 0;
            for (int i = 0; i < row.optionsParent.childCount; i++)
            {
                var button = row.optionsParent.GetChild(i).GetComponent<Button>();
                var label = button.GetComponentInChildren<Text>().text;
                if (label.StartsWith("✓ "))
                {
                    selectedCount++;
                    Require(label == "✓ " + options[i].label,
                    "Direct selection did not highlight the chosen " + expectedLabel + " option.");
                }
            }
            Require(selectedCount == 1,
                "Creator must show exactly one selected option for " + expectedLabel + ".");

            AppearanceOption selected = options[selectedIndex];
            switch (rowIndex)
            {
                case 0:
                {
                    float expectedWidth = selected.id == "broad" ? 0.68f
                    : selected.id == "round" ? 0.58f : 0.5f;
                    Require(Mathf.Abs(preview.TorsoMesh.localScale.x - expectedWidth) < 0.001f,
                    "Selecting a body type did not visibly reshape the traveler.");
                    break;
                }
                case 1:
                    Require(ColorsMatch(preview.HeadMesh.GetComponent<Renderer>().sharedMaterial.color,
                    selected.color), "Selecting a skin tone did not recolor the traveler.");
                    break;
                case 2:
                {
                    string expectedLastPart = selected.id == "curls" ? "Curl"
                    : selected.id == "bob" ? "HairR"
                    : selected.id == "pony" ? "Tail"
                    : selected.id == "tuft" ? "Spike" : "Sprout";
                    int hairCount = preview.HairAnchor.childCount;
                    Require(hairCount > 0 && preview.HairAnchor.GetChild(hairCount - 1).name == expectedLastPart,
                    "Selecting a hairstyle did not replace the preview hair.");
                    break;
                }
                case 3:
                {
                    var hairRenderer = preview.HairAnchor.GetComponentInChildren<Renderer>();
                    Require(hairRenderer != null && ColorsMatch(hairRenderer.sharedMaterial.color, selected.color),
                    "Selecting a hair color did not recolor the preview hair.");
                    break;
                }
                case 4:
                {
                    var eye = preview.transform.Find("Rig/EyeL");
                    Require(eye != null && ColorsMatch(eye.GetComponent<Renderer>().sharedMaterial.color,
                    selected.color), "Selecting an eye color did not recolor the preview eyes.");
                    break;
                }
                case 5:
                    Require(ColorsMatch(preview.TorsoMesh.GetComponent<Renderer>().sharedMaterial.color,
                    selected.color), "Selecting an outfit did not recolor the preview clothing.");
                    break;
            }
        }

        private static bool ColorsMatch(Color left, Color right)
        {
            return Vector4.Distance(left, right) < 0.001f;
        }

        private static void AdvanceArrivalDialogue()
        {
            var manager = GameManager.Instance;
            if (!manager.Dialogue.IsOpen) return;

            Require(manager.player.InputLocked, "Player movement is not locked during arrival dialogue.");
            manager.Dialogue.Advance();
            if (manager.Dialogue.IsOpen)
            {
                manager.Dialogue.Advance();
                Require(!manager.Dialogue.IsOpen, "Arrival dialogue did not close after both lines.");
                SetPhase(3);
            }
        }

        private static void CheckFreeRoamAndPause()
        {
            var manager = GameManager.Instance;
            Require(!manager.player.InputLocked, "Player movement was not restored after dialogue.");
            Require(manager.hud.objectiveText.gameObject.activeSelf,
                "Objective banner did not appear after arrival.");
            manager.TogglePause();
            Require(Time.timeScale == 0f && manager.player.InputLocked && manager.pausePanel.activeSelf,
                "Pause did not freeze gameplay and show the pause menu.");
            manager.Resume();
            Require(Time.timeScale == 1f && !manager.player.InputLocked && !manager.pausePanel.activeSelf,
                "Resume did not restore gameplay.");
            manager.SaveGame();
        }

        private static void CheckSaveAndFinish()
        {
            var save = SaveSystem.Load();
            Require(save != null && save.hasSavedPosition, "Saving did not persist the resume-position flag.");
            Finish(true, "Passed startup menu, town generation, creator, arrival dialogue, movement locking, pause/resume, and save/resume-state checks.");
        }

        private static void SetPhase(int phase)
        {
            SessionState.SetInt(PhaseKey, phase);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Wispmere] " + message);
        }

        private static void Finish(bool passed, string message)
        {
            RestoreSave();
            SessionState.SetBool(RunningKey, false);
            if (passed) Debug.Log("[Wispmere] PLAY-MODE SMOKE TEST PASSED: " + message);
            else Debug.LogError("[Wispmere] PLAY-MODE SMOKE TEST FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void RestoreSave()
        {
            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            string backupPath = SessionState.GetString(BackupPathKey, "");
            bool hadSave = SessionState.GetBool(HadSaveKey, false);

            if (hadSave && !string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                File.Copy(backupPath, savePath, true);
            else if (File.Exists(savePath))
                File.Delete(savePath);

            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }
}
