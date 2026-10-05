using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wispmere.Editor
{
    public static class ResourceNodeSceneSetup
    {
        private const string ScenePath = "Assets/Wispmere/Scenes/Wispmere.unity";

        private sealed class TransformSnapshot
        {
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public readonly Vector3 scale;

            public TransformSnapshot(Transform transform)
            {
                position = transform.position;
                rotation = transform.rotation;
                scale = transform.localScale;
            }

            public bool Matches(Transform transform)
            {
                return Vector3.Distance(position, transform.position) < 0.0001f
                    && Quaternion.Angle(rotation, transform.rotation) < 0.001f
                    && Vector3.Distance(scale, transform.localScale) < 0.0001f;
            }
        }

        [MenuItem("Wispmere/Resources/Configure Tree and Ore Nodes")]
        public static void ConfigureAndSaveScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Scene scene = SceneManager.GetActiveScene();
            Dictionary<string, TransformSnapshot> snapshots =
                new Dictionary<string, TransformSnapshot>();

            ConfigureNode(scene, "HardwoodTree1", "hardwood", "Hardwood Tree", 4,
                GatherTool.Axe, 1.7f, snapshots);
            ConfigureNode(scene, "HardwoodTree2", "hardwood", "Hardwood Tree", 4,
                GatherTool.Axe, 1.7f, snapshots);
            ConfigureNode(scene, "HardwoodTree3", "hardwood", "Hardwood Tree", 4,
                GatherTool.Axe, 1.7f, snapshots);

            ConfigureNode(scene, "Rock_Meadow_Northeast", "ore", "Ore Boulder", 2,
                GatherTool.Pickaxe, 5.5f, snapshots);
            ConfigureNode(scene, "Fantasy_Meadow_Rock_North", "ore", "Ore Boulder", 2,
                GatherTool.Pickaxe, 5.5f, snapshots);
            ConfigureNode(scene, "Wilderness_Edge_Rock_East", "ore", "Ore Boulder", 2,
                GatherTool.Pickaxe, 5.5f, snapshots);

            foreach (KeyValuePair<string, TransformSnapshot> snapshot in snapshots)
            {
                GameObject target = FindUnique(scene, snapshot.Key);
                if (!snapshot.Value.Matches(target.transform))
                    throw new InvalidOperationException(
                        "[Wispmere] Resource setup changed the transform of " + snapshot.Key + ".");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("[Wispmere] Could not save the resource-node scene changes.");
            Debug.Log("[Wispmere] Configured three Axe-gated Hardwood trees and three Pickaxe-gated Ore boulders. "
                + "Rock_South remains unchanged for review because it is intermediate in size.");
        }

        private static void ConfigureNode(Scene scene, string objectName, string kind,
            string label, int amount, GatherTool tool, float interactionRadius,
            Dictionary<string, TransformSnapshot> snapshots)
        {
            GameObject target = FindUnique(scene, objectName);
            snapshots.Add(objectName, new TransformSnapshot(target.transform));

            ResourceNode node = target.GetComponent<ResourceNode>();
            if (node == null) node = Undo.AddComponent<ResourceNode>(target);
            Undo.RecordObject(node, "Configure " + objectName + " resource node");
            node.kind = kind;
            node.label = label;
            node.amount = amount;
            node.regrowSeconds = 24f;
            node.interactionRadius = interactionRadius;
            node.requiredTool = tool;
            node.persistenceId = objectName;
            EditorUtility.SetDirty(node);
            PrefabUtility.RecordPrefabInstancePropertyModifications(node);

            if (kind == "ore")
            {
                foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
                {
                    Undo.RecordObject(collider, "Enable " + objectName + " interaction");
                    collider.enabled = true;
                    EditorUtility.SetDirty(collider);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
            }
        }

        private static GameObject FindUnique(Scene scene, string objectName)
        {
            GameObject match = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != objectName) continue;
                    if (match != null)
                        throw new InvalidOperationException("[Wispmere] Found multiple scene objects named "
                            + objectName + ".");
                    match = child.gameObject;
                }
            }

            if (match == null)
                throw new InvalidOperationException("[Wispmere] Could not find " + objectName
                    + " in the Wispmere gameplay scene.");
            return match;
        }
    }
}
