using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wispmere
{
    public enum GatherTool
    {
        None,
        Axe,
        Pickaxe,
    }

    /// <summary>
    /// Resource wallet, crafted inventory items, and owned gathering tools.
    /// Counts remain the single source of truth for the HUD and crafting.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public event Action<string, int> OnChanged;
        public event Action OnToolsChanged;

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>
        {
            { "wood", 0 }, { "stone", 0 }, { "fiber", 0 }, { "ore", 0 }, { "hardwood", 0 },
            { "workbench", 0 }, { "campfire", 0 }, { "storage_chest", 0 }, { "fence", 0 },
            { "metal_axe", 0 }, { "metal_pickaxe", 0 }, { "tent", 0 },
        };
        private readonly HashSet<GatherTool> _ownedTools = new HashSet<GatherTool>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public int Get(string kind)
        {
            return _counts.TryGetValue(kind, out int v) ? v : 0;
        }

        public void Add(string kind, int amount)
        {
            if (!_counts.ContainsKey(kind)) _counts[kind] = 0;
            _counts[kind] += amount;
            if (OnChanged != null) OnChanged(kind, _counts[kind]);
        }

        public void LoadInto(Dictionary<string, int> saved)
        {
            if (saved == null)
            {
                Debug.LogError("[Wispmere] Cannot load resources from a null save snapshot.", this);
                return;
            }

            _counts.Clear();
            _counts["wood"] = 0;
            _counts["stone"] = 0;
            _counts["fiber"] = 0;
            _counts["ore"] = 0;
            _counts["hardwood"] = 0;
            _counts["workbench"] = 0;
            _counts["campfire"] = 0;
            _counts["storage_chest"] = 0;
            _counts["fence"] = 0;
            _counts["metal_axe"] = 0;
            _counts["metal_pickaxe"] = 0;
            _counts["tent"] = 0;
            foreach (var kv in saved) _counts[kv.Key] = kv.Value;
            foreach (var kv in _counts)
                if (OnChanged != null) OnChanged(kv.Key, kv.Value);
        }

        public bool HasTool(GatherTool tool)
        {
            return tool != GatherTool.None && _ownedTools.Contains(tool);
        }

        public bool CanCraftTool(GatherTool tool)
        {
            return tool != GatherTool.None && !HasTool(tool)
                && CanAfford(GetToolRecipeCosts(tool));
        }

        public Dictionary<string, int> GetToolRecipeCosts(GatherTool tool)
        {
            if (tool == GatherTool.None)
            {
                Debug.LogError("[Wispmere] Cannot get recipe costs for an unspecified tool.", this);
                return new Dictionary<string, int>();
            }
            return new Dictionary<string, int> { { "wood", 1 }, { "stone", 1 } };
        }

        public bool TryCraftTool(GatherTool tool)
        {
            if (tool == GatherTool.None)
            {
                Debug.LogWarning("[Wispmere] Cannot craft an unspecified tool.", this);
                return false;
            }
            if (!CanCraftTool(tool)) return false;

            Dictionary<string, int> costs = GetToolRecipeCosts(tool);
            foreach (var cost in costs)
                _counts[cost.Key] -= cost.Value;
            _ownedTools.Add(tool);
            if (OnChanged != null)
                foreach (var cost in costs)
                    OnChanged(cost.Key, _counts[cost.Key]);
            if (OnToolsChanged != null) OnToolsChanged();
            return true;
        }

        public bool CanAfford(Dictionary<string, int> costs)
        {
            if (costs == null)
            {
                Debug.LogError("[Wispmere] Cannot check affordability for null recipe costs.", this);
                return false;
            }

            foreach (var cost in costs)
            {
                if (string.IsNullOrEmpty(cost.Key) || cost.Value <= 0)
                {
                    Debug.LogError("[Wispmere] Recipe costs require item names and positive quantities.", this);
                    return false;
                }
                if (Get(cost.Key) < cost.Value) return false;
            }
            return true;
        }

        public bool TryCraftItem(string item, Dictionary<string, int> costs)
        {
            if (string.IsNullOrEmpty(item) || costs == null || costs.Count == 0)
            {
                Debug.LogError("[Wispmere] Crafted items require an item id and non-empty costs.", this);
                return false;
            }
            if (!CanAfford(costs)) return false;

            foreach (var cost in costs)
            {
                _counts[cost.Key] -= cost.Value;
                if (OnChanged != null) OnChanged(cost.Key, _counts[cost.Key]);
            }

            Add(item, 1);
            return true;
        }

        public bool TryConsumeItem(string item, int amount)
        {
            if (string.IsNullOrEmpty(item) || amount <= 0)
            {
                Debug.LogError("[Wispmere] Item consumption requires an item id and positive quantity.", this);
                return false;
            }
            if (Get(item) < amount) return false;

            _counts[item] -= amount;
            if (OnChanged != null) OnChanged(item, _counts[item]);
            return true;
        }

        public void SetToolOwnership(bool hasAxe, bool hasPickaxe)
        {
            _ownedTools.Clear();
            if (hasAxe) _ownedTools.Add(GatherTool.Axe);
            if (hasPickaxe) _ownedTools.Add(GatherTool.Pickaxe);
            if (OnToolsChanged != null) OnToolsChanged();
        }

        public Dictionary<string, int> Snapshot()
        {
            return new Dictionary<string, int>(_counts);
        }
    }
}
