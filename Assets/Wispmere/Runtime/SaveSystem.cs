using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// Single-slot JSON save with player, resource, crafted-item, tool, and town progress.
    /// Stored at persistentDataPath.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "wispmere_save_v1.json";

        [Serializable]
        public class SaveData
        {
            [Serializable]
            public class PlacedObjectData
            {
                public string kind;
                public Vector3 position;
                public float yaw;
            }

            [Serializable]
            public class ResourceNodeState
            {
                public string id;
                public long regrowsAtUtcTicks;
            }

            public string player = "Wren";
            public Appearance appearance = Appearance.Default();
            [NonSerialized]
            public Dictionary<string, int> resources = new Dictionary<string, int>
            {
                { "wood", 0 }, { "stone", 0 }, { "fiber", 0 }, { "ore", 0 }, { "hardwood", 0 },
                { "workbench", 0 }, { "campfire", 0 }, { "storage_chest", 0 }, { "fence", 0 },
                { "metal_axe", 0 }, { "metal_pickaxe", 0 }, { "tent", 0 },
            };
            public bool hasAxe;
            public bool hasPickaxe;
            public string scene = "town";
            public Vector3 pos;
            public bool hasSavedPosition;
            public bool workshopDiscovered;
            public List<PlacedObjectData> placedObjects = new List<PlacedObjectData>();
            public List<ResourceNodeState> resourceNodes = new List<ResourceNodeState>();
            public string town = "Wispmere";
            public long createdAt;
        }

        // JsonUtility can't do Dictionaries — serializable mirror for resources.
        [Serializable]
        private class SaveFile
        {
            public string player;
            public Appearance appearance;
            public int wood;
            public int stone;
            public int fiber;
            public int ore;
            public int hardwood;
            public int workbench;
            public int campfire;
            public int storageChest;
            public int fence;
            public int metalAxe;
            public int metalPickaxe;
            public int tent;
            public bool hasAxe;
            public bool hasPickaxe;
            public string scene;
            public float[] pos = new float[3];
            public bool hasSavedPosition;
            public bool workshopDiscovered;
            public List<SaveData.PlacedObjectData> placedObjects =
                new List<SaveData.PlacedObjectData>();
            public List<SaveData.ResourceNodeState> resourceNodes =
                new List<SaveData.ResourceNodeState>();
            public int worldCoordinateVersion;
            public string town;
            public long createdAt;
        }

        private static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static void Save(SaveData data)
        {
            var file = new SaveFile
            {
                player = data.player,
                appearance = data.appearance,
                hasAxe = data.hasAxe,
                hasPickaxe = data.hasPickaxe,
                scene = data.scene,
                hasSavedPosition = data.hasSavedPosition,
                workshopDiscovered = data.workshopDiscovered,
                placedObjects = data.placedObjects ?? new List<SaveData.PlacedObjectData>(),
                resourceNodes = data.resourceNodes ?? new List<SaveData.ResourceNodeState>(),
                worldCoordinateVersion = WorldLayout.CoordinateVersion,
                town = data.town,
                createdAt = data.createdAt,
            };
            data.resources.TryGetValue("wood", out file.wood);
            data.resources.TryGetValue("stone", out file.stone);
            data.resources.TryGetValue("fiber", out file.fiber);
            data.resources.TryGetValue("ore", out file.ore);
            data.resources.TryGetValue("hardwood", out file.hardwood);
            data.resources.TryGetValue("workbench", out file.workbench);
            data.resources.TryGetValue("campfire", out file.campfire);
            data.resources.TryGetValue("storage_chest", out file.storageChest);
            data.resources.TryGetValue("fence", out file.fence);
            data.resources.TryGetValue("metal_axe", out file.metalAxe);
            data.resources.TryGetValue("metal_pickaxe", out file.metalPickaxe);
            data.resources.TryGetValue("tent", out file.tent);
            file.pos[0] = data.pos.x; file.pos[1] = data.pos.y; file.pos[2] = data.pos.z;
            File.WriteAllText(Path, JsonUtility.ToJson(file, true));
        }

        public static SaveData Load()
        {
            if (!File.Exists(Path)) return null;
            try
            {
                var file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(Path));
                if (file == null) return null;
                var data = new SaveData
                {
                    player = file.player,
                    appearance = file.appearance,
                    hasAxe = file.hasAxe,
                    hasPickaxe = file.hasPickaxe,
                    scene = file.scene,
                    town = file.town,
                    createdAt = file.createdAt,
                    pos = new Vector3(file.pos[0], file.pos[1], file.pos[2]),
                    hasSavedPosition = file.hasSavedPosition
                        || file.pos[0] != 0f || file.pos[1] != 0f || file.pos[2] != 0f,
                    workshopDiscovered = file.workshopDiscovered,
                    placedObjects = file.placedObjects ?? new List<SaveData.PlacedObjectData>(),
                    resourceNodes = file.resourceNodes ?? new List<SaveData.ResourceNodeState>(),
                };
                if (data.hasSavedPosition && file.worldCoordinateVersion < WorldLayout.CoordinateVersion)
                {
                    float scaleRatio = WorldLayout.Scale / WorldLayout.PreviousScale;
                    data.pos = new Vector3(data.pos.x * scaleRatio, data.pos.y,
                        data.pos.z * scaleRatio);
                }
                data.resources["wood"] = file.wood;
                data.resources["stone"] = file.stone;
                data.resources["fiber"] = file.fiber;
                data.resources["ore"] = file.ore;
                data.resources["hardwood"] = file.hardwood;
                data.resources["workbench"] = file.workbench;
                data.resources["campfire"] = file.campfire;
                data.resources["storage_chest"] = file.storageChest;
                data.resources["fence"] = file.fence;
                data.resources["metal_axe"] = file.metalAxe;
                data.resources["metal_pickaxe"] = file.metalPickaxe;
                data.resources["tent"] = file.tent;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Wispmere] Save unreadable, starting fresh: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }

        public static bool HasSave()
        {
            return File.Exists(Path);
        }
    }
}
