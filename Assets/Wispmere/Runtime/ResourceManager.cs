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
    /// Resource wallet and owned gathering tools. Resource counts remain the
    /// single source of truth for the HUD, inventory, and crafting.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public event Action<string, int> OnChanged;
        public event Action OnToolsChanged;

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>
        {
            { "wood", 0 }, { "stone", 0 }, { "fiber", 0 }, { "ore", 0 },
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
                && Get("wood") >= 1 && Get("stone") >= 1;
        }

        public bool TryCraftTool(GatherTool tool)
        {
            if (tool == GatherTool.None)
            {
                Debug.LogWarning("[Wispmere] Cannot craft an unspecified tool.", this);
                return false;
            }
            if (!CanCraftTool(tool)) return false;

            _counts["wood"]--;
            _counts["stone"]--;
            _ownedTools.Add(tool);
            if (OnChanged != null)
            {
                OnChanged("wood", _counts["wood"]);
                OnChanged("stone", _counts["stone"]);
            }
            if (OnToolsChanged != null) OnToolsChanged();
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
