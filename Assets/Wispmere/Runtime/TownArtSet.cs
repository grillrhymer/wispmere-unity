using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// Key → prefab map for every world object. Keys used by TownBuilder:
    /// building/inn, building/hall, building/shop, building/house,
    /// building/house2, building/cottage, building/cottage2,
    /// building/damaged, well, tree/teal, tree/turquoise,
    /// node/wood, node/stone, node/fiber, garden, sign, stall, arch, square.
    /// Any missing key spawns a labeled primitive — the game always runs.
    /// </summary>
    [CreateAssetMenu(fileName = "TownArtSet", menuName = "Wispmere/Town Art Set")]
    public class TownArtSet : ScriptableObject
    {
        [Serializable]
        public struct PrefabEntry
        {
            [Tooltip("e.g. building/inn, node/wood, tree/teal")]
            public string key;
            public GameObject prefab;
            public float scale;
        }

        [Serializable]
        public struct MaterialReplacement
        {
            public Material source;
            public Material replacement;
        }

        public List<PrefabEntry> entries = new List<PrefabEntry>();
        public List<MaterialReplacement> materialReplacements = new List<MaterialReplacement>();

        public bool TryGet(string key, out GameObject prefab, out float scale)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].key == key)
                {
                    prefab = entries[i].prefab;
                    scale = entries[i].scale > 0f ? entries[i].scale : 1f;
                    return prefab != null;
                }
            }
            prefab = null;
            scale = 1f;
            return false;
        }

        public bool TryGetReplacement(Material source, out Material replacement)
        {
            for (int i = 0; i < materialReplacements.Count; i++)
            {
                if (materialReplacements[i].source == source)
                {
                    replacement = materialReplacements[i].replacement;
                    return replacement != null;
                }
            }
            replacement = null;
            return false;
        }
    }
}
