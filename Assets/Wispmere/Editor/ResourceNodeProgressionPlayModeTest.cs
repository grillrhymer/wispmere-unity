using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wispmere.Editor
{
    [InitializeOnLoad]
    public static class ResourceNodeProgressionPlayModeTest
    {
        private const string RunningKey = "Wispmere.ResourceNodeTest.Running";
        private const string PhaseKey = "Wispmere.ResourceNodeTest.Phase";
        private const string StartTimeKey = "Wispmere.ResourceNodeTest.StartTime";
        private const string HadSaveKey = "Wispmere.ResourceNodeTest.HadSave";
        private const string BackupPathKey = "Wispmere.ResourceNodeTest.BackupPath";
        private const string SaveFileName = "wispmere_save_v1.json";
        private const string ScenePath = "Assets/Wispmere/Scenes/Wispmere.unity";

        private static readonly string[] HardwoodTrees =
        {
            "HardwoodTree1", "HardwoodTree2", "HardwoodTree3",
        };

        private static readonly string[] OreBoulders =
        {
            "Rock_Meadow_Northeast",
            "Fantasy_Meadow_Rock_North",
            "Wilderness_Edge_Rock_East",
        };

        private static readonly Dictionary<string, Vector3> TreePositions =
            new Dictionary<string, Vector3>
            {
                { "HardwoodTree1", new Vector3(9.4199467f, 0.195099f, 11.779207f) },
                { "HardwoodTree2", new Vector3(4.9199467f, 0.195099f, 11.779207f) },
                { "HardwoodTree3", new Vector3(0.4199467f, 0.195099f, 11.779207f) },
            };

        static ResourceNodeProgressionPlayModeTest()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        [MenuItem("Wispmere/Test/Run Resource Progression Test")]
        public static void RunBatchMode()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("RunBatchMode requires a batch-mode Unity Editor.");
            if (SessionState.GetBool(RunningKey, false))
                throw new InvalidOperationException("The resource progression test is already running.");

            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            string backupPath = Path.Combine("Library", "ResourceNodeSaveBackup.json");
            SessionState.SetBool(HadSaveKey, File.Exists(savePath));
            SessionState.SetString(BackupPathKey, backupPath);
            if (File.Exists(savePath)) File.Copy(savePath, backupPath, true);

            SaveSystem.Save(new SaveSystem.SaveData
            {
                player = "Resource test",
                pos = WorldLayout.ToUnity(800f, 600f),
                hasSavedPosition = true,
                hasAxe = false,
                hasPickaxe = false,
            });
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(RunningKey, true);
            Debug.Log("[Wispmere] Starting resource-node progression Play Mode test.");
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) > 60d)
                    throw new TimeoutException("Timed out during resource-node progression validation.");

                GameManager manager = GameManager.Instance;
                Require(manager != null && manager.hud != null && manager.player != null,
                    "The gameplay manager did not initialize.");
                if (SessionState.GetInt(PhaseKey, 0) == 0)
                {
                    if (manager.mainMenuPanel == null || !manager.mainMenuPanel.activeSelf) return;
                    manager.Continue();
                    SetPhase(1);
                    return;
                }
                if (SessionState.GetInt(PhaseKey, 0) == 1)
                {
                    if (!manager.player.gameObject.activeSelf || manager.player.InputLocked) return;
                    ValidateAndHarvest(manager);
                    manager.Continue();
                    SetPhase(2);
                    return;
                }

                ValidateSavedDepletion(manager);
                Finish(true,
                    "Three unchanged Axe-gated Hardwood trees, three oversized Pickaxe-gated Ore boulders, existing Stone/Ore nodes, inventory counts, non-repeat harvests, and depletion persistence passed.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static void ValidateAndHarvest(GameManager manager)
        {
            ResourceManager resources = ResourceManager.Instance;
            ResourceNode[] trees = FindNodes(HardwoodTrees);
            ResourceNode[] boulders = FindNodes(OreBoulders);
            foreach (ResourceNode tree in trees)
            {
                Require(tree.kind == "hardwood" && tree.amount == 4
                    && tree.requiredTool == GatherTool.Axe,
                    tree.name + " is not configured as a four-Hardwood Axe node.");
                Require(Vector3.Distance(tree.transform.position, TreePositions[tree.name]) < 0.0001f
                    && Vector3.Distance(tree.transform.localScale, Vector3.one * 0.48f) < 0.0001f
                    && Quaternion.Angle(tree.transform.rotation, Quaternion.identity) < 0.001f,
                    tree.name + " was moved, rotated, or scaled.");
            }

            foreach (ResourceNode boulder in boulders)
            {
                Require(boulder.kind == "ore" && boulder.amount == 2
                    && boulder.requiredTool == GatherTool.Pickaxe
                    && boulder.GetComponentsInChildren<Collider>(true).Length > 0,
                    boulder.name + " is not an interactive Pickaxe-gated Ore node.");
                foreach (Collider collider in boulder.GetComponentsInChildren<Collider>(true))
                    Require(collider.enabled, boulder.name + " has a disabled interaction collider.");
            }

            ResourceNode stone = FindNode("Node_rock1");
            ResourceNode existingOre = FindNode("Node_rock2");
            Require(stone.kind == "stone" && stone.amount == 2
                && stone.requiredTool == GatherTool.None,
                "The existing Stone node was changed.");
            Require(existingOre.kind == "ore" && existingOre.requiredTool == GatherTool.Pickaxe,
                "The existing Pickaxe-to-Ore progression changed.");

            resources.SetToolOwnership(false, false);
            trees[0].Interact();
            boulders[0].Interact();
            Require(resources.Get("hardwood") == 0 && resources.Get("ore") == 0
                && trees[0].IsRipe && boulders[0].IsRipe,
                "A resource was harvested without its required tool.");

            resources.SetToolOwnership(true, true);
            foreach (ResourceNode tree in trees)
            {
                int before = resources.Get("hardwood");
                tree.Interact();
                Require(resources.Get("hardwood") == before + 4 && !tree.IsRipe,
                    tree.name + " did not yield exactly four Hardwood and deplete.");
                tree.Interact();
                Require(resources.Get("hardwood") == before + 4,
                    tree.name + " yielded Hardwood more than once while depleted.");
            }

            foreach (ResourceNode boulder in boulders)
            {
                int before = resources.Get("ore");
                boulder.Interact();
                Require(resources.Get("ore") == before + boulder.amount && !boulder.IsRipe,
                    boulder.name + " did not yield Ore and deplete.");
                boulder.Interact();
                Require(resources.Get("ore") == before + boulder.amount,
                    boulder.name + " yielded Ore more than once while depleted.");
            }

            int oreBefore = resources.Get("ore");
            existingOre.Interact();
            Require(resources.Get("ore") == oreBefore + existingOre.amount && !existingOre.IsRipe,
                "The existing Pickaxe-gated Ore node no longer works.");
            stone.Interact();
            Require(resources.Get("stone") == stone.amount && !stone.IsRipe,
                "The existing Stone node no longer gives Stone.");

            Require(manager.hud.inventoryQuantityTexts[4].text == "12"
                && manager.hud.inventoryQuantityTexts[3].text == "7"
                && manager.hud.inventoryQuantityTexts[1].text == "2",
                "The inventory did not reflect gathered Hardwood, Ore, and Stone.");

            SaveSystem.SaveData saved = SaveSystem.Load();
            Require(saved != null && saved.resources["hardwood"] == 12
                && saved.resources["ore"] == 7 && saved.resources["stone"] == 2
                && saved.resourceNodes.Count == 8,
                "Gathered counts or depleted-node states were not saved.");
        }

        private static void ValidateSavedDepletion(GameManager manager)
        {
            foreach (string id in HardwoodTrees)
                Require(!FindNode(id).IsRipe, id + " became ripe after Continue.");
            foreach (string id in OreBoulders)
                Require(!FindNode(id).IsRipe, id + " became ripe after Continue.");
            Require(!FindNode("Node_rock2").IsRipe
                && !FindNode("Node_rock1").IsRipe,
                "Existing resource-node depletion did not persist through Continue.");
            Require(ResourceManager.Instance.Get("hardwood") == 12
                && ResourceManager.Instance.Get("ore") == 7
                && ResourceManager.Instance.Get("stone") == 2
                && manager.hud.inventoryQuantityTexts[4].text == "12"
                && manager.hud.inventoryQuantityTexts[3].text == "7",
                "Saved resource counts or their HUD values did not survive Continue.");
        }

        private static ResourceNode[] FindNodes(string[] ids)
        {
            ResourceNode[] result = new ResourceNode[ids.Length];
            for (int i = 0; i < ids.Length; i++) result[i] = FindNode(ids[i]);
            return result;
        }

        private static ResourceNode FindNode(string id)
        {
            ResourceNode result = null;
            foreach (ResourceNode node in ResourceNode.All)
            {
                if (node == null || node.GetPersistenceId() != id) continue;
                Require(result == null, "Found duplicate resource nodes with identity " + id + ".");
                result = node;
            }
            Require(result != null, "Could not find resource node " + id + ".");
            return result;
        }

        private static void SetPhase(int phase)
        {
            SessionState.SetInt(PhaseKey, phase);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
        }

        private static void Finish(bool passed, string message)
        {
            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            string backupPath = SessionState.GetString(BackupPathKey, "");
            if (SessionState.GetBool(HadSaveKey, false) && File.Exists(backupPath))
                File.Copy(backupPath, savePath, true);
            else if (File.Exists(savePath))
                File.Delete(savePath);
            if (File.Exists(backupPath)) File.Delete(backupPath);

            SessionState.SetBool(RunningKey, false);
            if (passed) Debug.Log("[Wispmere] RESOURCE NODE TEST PASSED: " + message);
            else Debug.LogError("[Wispmere] RESOURCE NODE TEST FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Wispmere] " + message);
        }
    }
}
