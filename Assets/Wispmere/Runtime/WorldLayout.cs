using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// Coordinate + save-schema bridge between the 2D layout data
    /// (WorldLayout.json, authored in web-prototype pixels) and the 3D scene.
    /// 1 Unity unit = 40 layout px; the town center sits at the origin.
    /// </summary>
    public static class WorldLayout
    {
        public const float Scale = 0.025f;
        public const float PreviousScale = 0.01f;
        public const int CoordinateVersion = 2;
        public const float OriginX = 800f;
        public const float OriginY = 600f;

        public static Vector3 ToUnity(float layoutX, float layoutY, float y = 0f)
        {
            return new Vector3((layoutX - OriginX) * Scale, y, (layoutY - OriginY) * Scale);
        }

        public static Vector2 ToLayout(Vector3 world)
        {
            return new Vector2(world.x / Scale + OriginX, world.z / Scale + OriginY);
        }

        [Serializable] public class SpawnPoint { public float x; public float y; }
        [Serializable] public class LayoutSize { public float w; public float h; }
        [Serializable] public class BuildingEntry
        {
            public string id;
            public string key;
            public string label;
            public string role;
            public float x;
            public float y;
            public float w;
            public float h;
        }
        [Serializable] public class PropEntry { public string id; public string key; public float x; public float y; public float w; public float h; public bool gatherable; }
        [Serializable] public class GardenEntry { public float x; public float y; }
        [Serializable] public class DecorationEntry
        {
            public string id;
            public string key;
            public float x;
            public float y;
            public float height;
            public float rotation;
            public float scale = 1f;
        }
        [Serializable] public class InteractableEntry { public string id; public string label; public string text; public float x; public float y; public float r; public bool restoration; }
        [Serializable] public class NodeEntry
        {
            public string id;
            public string kind;
            public string label;
            public string requiredTool;
            public int amount;
            public float x;
            public float y;
        }

        [Serializable]
        public class LayoutData
        {
            public string townName;
            public LayoutSize layoutPx;
            public SpawnPoint spawn;
            public SpawnPoint square;
            public BuildingEntry[] buildings;
            public PropEntry[] props;
            public GardenEntry[] gardens;
            public DecorationEntry[] decorations;
            public InteractableEntry[] interactables;
            public NodeEntry[] nodes;
        }

        public static LayoutData Parse(string json)
        {
            return JsonUtility.FromJson<LayoutData>(json);
        }
    }
}
