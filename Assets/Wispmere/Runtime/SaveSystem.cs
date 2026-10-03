using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// Single-slot JSON save with player, resource, tool, and town progress.
    /// Stored at persistentDataPath.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "wispmere_save_v1.json";

        [Serializable]
        public class SaveData
        {
            public string player = "Wren";
            public Appearance appearance = Appearance.Default();
            public Dictionary<string, int> resources = new Dictionary<string, int>
            {
                { "wood", 0 }, { "stone", 0 }, { "fiber", 0 }, { "ore", 0 },
            };
            public bool hasAxe;
            public bool hasPickaxe;
            public string scene = "town";
            public Vector3 pos;
            public bool hasSavedPosition;
            public bool workshopDiscovered;
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
            public bool hasAxe;
            public bool hasPickaxe;
            public string scene;
            public float[] pos = new float[3];
            public bool hasSavedPosition;
            public bool workshopDiscovered;
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
                worldCoordinateVersion = WorldLayout.CoordinateVersion,
                town = data.town,
                createdAt = data.createdAt,
            };
            data.resources.TryGetValue("wood", out file.wood);
            data.resources.TryGetValue("stone", out file.stone);
            data.resources.TryGetValue("fiber", out file.fiber);
            data.resources.TryGetValue("ore", out file.ore);
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
