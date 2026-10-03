using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// Builds the whole prototype town from WorldLayout.json: buildings with
    /// colliders, props, gardens, interactables, and resource nodes.
    /// Visuals resolve via TownArtSet; anything unassigned becomes a
    /// labeled primitive placeholder. Run once via Wispmere → Build Town
    /// From Json, or automatically in Awake when autoBuild is on.
    /// Re-run any time art changes — Build() clears its previous output first.
    /// </summary>
    public class TownBuilder : MonoBehaviour
    {
        [Header("Data")]
        public TextAsset layoutJson;

        [Header("Optional art (empty = placeholders everywhere)")]
        public TownArtSet artSet;

        [Header("Options")]
        public bool autoBuild = true;

        private const string RootName = "TownRoot";

        private void Awake()
        {
            if (autoBuild && transform.Find(RootName) == null)
                Build();
        }

        public WorldLayout.LayoutData Layout
        {
            get { return WorldLayout.Parse(layoutJson.text); }
        }

        public void Build()
        {
            if (layoutJson == null)
            {
                Debug.LogError("[Wispmere] TownBuilder needs its Layout Json assigned.");
                return;
            }
            Clear();
            var data = Layout;
            var root = new GameObject(RootName).transform;
            root.SetParent(transform, false);

            BuildGround(root, data);
            if (!HasImportedPathArt()) BuildPaths(root, data);
            foreach (var b in data.buildings) BuildBuilding(root, b);
            foreach (var p in data.props) BuildProp(root, p);
            foreach (var g in data.gardens) SpawnKeyed(root, "garden", WorldLayout.ToUnity(g.x, g.y), Vector3.one, FallbackGarden);
            foreach (var it in data.interactables) BuildInteractable(root, it);
            foreach (var n in data.nodes) BuildNode(root, n);
            BuildSquare(root, data);
            BuildDecorations(root, data);
        }

        public void Clear()
        {
            var old = transform.Find(RootName);
            if (old == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(old.gameObject);
            else Destroy(old.gameObject);
#else
            Destroy(old.gameObject);
#endif
        }

        // ----- pieces -----

        private void BuildGround(Transform root, WorldLayout.LayoutData data)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.position = WorldLayout.ToUnity(data.layoutPx.w / 2f, data.layoutPx.h / 2f);
            ground.transform.localScale = new Vector3(data.layoutPx.w * WorldLayout.Scale / 10f, 1f, data.layoutPx.h * WorldLayout.Scale / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = PlainMat(new Color(0.37f, 0.68f, 0.37f));

        }

        private void BuildPaths(Transform root, WorldLayout.LayoutData data)
        {
            var paths = new GameObject("Worn Footpaths").transform;
            paths.SetParent(root, false);
            Color pathColor = new Color(0.58f, 0.48f, 0.34f);
            Color edging = new Color(0.43f, 0.4f, 0.32f);

            AddPath(paths, "Arrival Road", new[]
            {
                WorldLayout.ToUnity(data.spawn.x, data.spawn.y),
                WorldLayout.ToUnity(data.square.x, data.square.y)
            }, 1.25f, pathColor);
            AddPath(paths, "Workshop Approach", new[]
            {
                WorldLayout.ToUnity(data.square.x, data.square.y),
                WorldLayout.ToUnity(1050f, 500f),
                WorldLayout.ToUnity(1040f, 760f),
                WorldLayout.ToUnity(945f, 760f)
            }, 0.85f, pathColor);
            AddPath(paths, "West Shop Path", new[]
            {
                WorldLayout.ToUnity(data.square.x, data.square.y),
                WorldLayout.ToUnity(610f, 510f),
                WorldLayout.ToUnity(435f, 590f)
            }, 0.75f, edging);
            AddPath(paths, "East Garden Path", new[]
            {
                WorldLayout.ToUnity(data.square.x, data.square.y),
                WorldLayout.ToUnity(1050f, 560f),
                WorldLayout.ToUnity(1120f, 690f)
            }, 0.75f, edging);
        }

        private static void AddPath(Transform parent, string name, Vector3[] points, float width, Color color)
        {
            var path = new GameObject(name).transform;
            path.SetParent(parent, false);
            var material = PlainMat(color);
            for (int segment = 0; segment < points.Length - 1; segment++)
            {
                Vector3 start = points[segment];
                Vector3 end = points[segment + 1];
                float length = Vector3.Distance(start, end);
                int slabs = Mathf.Max(1, Mathf.CeilToInt(length / 0.38f));
                for (int i = 0; i <= slabs; i++)
                {
                    Vector3 position = Vector3.Lerp(start, end, i / (float)slabs);
                    var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slab.name = "Worn Paving Stone";
                    slab.transform.SetParent(path, false);
                    slab.transform.position = position + Vector3.up * 0.025f;
                    float variation = (i % 3 == 0) ? 0.12f : 0f;
                    slab.transform.localScale = new Vector3(width - variation, 0.045f, 0.34f);
                    slab.GetComponent<Renderer>().sharedMaterial = material;
                    RemoveCollider(slab);
                }
            }
        }

        private void BuildBuilding(Transform root, WorldLayout.BuildingEntry b)
        {
            Vector3 center = WorldLayout.ToUnity(b.x + b.w / 2f, b.y + b.h / 2f);
            Vector3 size = new Vector3(b.w * WorldLayout.Scale, 2.4f, b.h * WorldLayout.Scale);
            Transform t = SpawnKeyed(root, b.key, center, size,
                go => FallbackBuilding(go, b.id));
            t.name = string.IsNullOrEmpty(b.label) ? b.id : b.label;

            var collider = t.gameObject.GetComponent<Collider>();
            if (collider == null)
            {
                var box = t.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(size.x / t.localScale.x, size.y / t.localScale.y, size.z / t.localScale.z);
            }
            if (b.id == "damaged")
            {
                CreateWorkshopMarker(t);
            }
        }

        private void BuildProp(Transform root, WorldLayout.PropEntry p)
        {
            Vector3 center = WorldLayout.ToUnity(p.x + p.w / 2f, p.y + p.h / 2f);
            Vector3 size = new Vector3(p.w * WorldLayout.Scale, 1f, p.h * WorldLayout.Scale);
            var t = SpawnKeyed(root, p.key, center, size,
                p.key.StartsWith("tree/") ? FallbackTree(p.key)
                    : p.id == "well" ? FallbackLandmark : null);
            t.name = p.id;
            if (p.id == "well")
                t.position = WorldLayout.ToUnity(p.x + p.w / 2f, p.y + p.h / 2f);
            if (p.key.StartsWith("tree/") && t.GetComponentInChildren<Collider>() == null)
            {
                var trunk = t.gameObject.AddComponent<BoxCollider>();
                trunk.size = new Vector3(0.3f, 2f, 0.3f);
                trunk.center = Vector3.up;
            }
            if (p.gatherable)
            {
                var node = t.gameObject.AddComponent<ResourceNode>();
                node.kind = "wood";
                node.label = "Town Tree";
            }
        }

        private void BuildInteractable(Transform root, WorldLayout.InteractableEntry it)
        {
            Transform t = it.restoration ? root.Find("damaged") : null;
            if (t == null)
            {
                var marker = SpawnKeyed(root, it.id, WorldLayout.ToUnity(it.x, it.y),
                    Vector3.one, FallbackMarker);
                marker.name = "POI_" + it.id;
                t = marker;
                var clickCollider = t.gameObject.AddComponent<SphereCollider>();
                clickCollider.isTrigger = true;
                clickCollider.radius = 0.48f;
                clickCollider.center = Vector3.up * 0.65f;
            }

            var c = t.gameObject.AddComponent<Interactable>();
            c.label = it.label;
            c.text = it.text;
            c.radius = it.r * WorldLayout.Scale;
            c.isRestorationTarget = it.restoration;
        }

        private void BuildNode(Transform root, WorldLayout.NodeEntry n)
        {
            string visualKind = n.kind == "ore" ? "stone" : n.kind;
            var t = SpawnKeyed(root, "node/" + visualKind, WorldLayout.ToUnity(n.x, n.y),
                Vector3.one, FallbackNode(visualKind));
            t.name = "Node_" + n.id;
            var c = t.gameObject.AddComponent<ResourceNode>();
            c.kind = n.kind;
            c.label = n.label;
            c.amount = n.amount > 0 ? n.amount : 1;
            if (!string.IsNullOrEmpty(n.requiredTool)
                && !System.Enum.TryParse(n.requiredTool, true, out c.requiredTool))
            {
                Debug.LogError("[Wispmere] Resource node " + n.id
                    + " has an unknown required tool: " + n.requiredTool, t);
            }
            var clickCollider = t.gameObject.AddComponent<SphereCollider>();
            clickCollider.isTrigger = true;
            clickCollider.radius = 0.62f;
            clickCollider.center = Vector3.up * 0.35f;
        }

        private void BuildSquare(Transform root, WorldLayout.LayoutData data)
        {
            var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "Worn Stone Plaza";
            plaza.transform.SetParent(root, false);
            plaza.transform.position = WorldLayout.ToUnity(data.square.x, data.square.y) + Vector3.up * 0.025f;
            plaza.transform.localScale = new Vector3(5.6f, 0.035f, 5.6f);
            plaza.GetComponent<Renderer>().sharedMaterial = PlainMat(new Color(0.61f, 0.55f, 0.42f));
            RemoveCollider(plaza);

            var t = SpawnKeyed(root, "square", WorldLayout.ToUnity(data.square.x, data.square.y), new Vector3(2.6f, 0.1f, 1.1f), go =>
            {
                go.GetComponent<Renderer>().sharedMaterial = PlainMat(new Color(0.79f, 0.66f, 0.42f));
            });
            t.name = "Square";
            foreach (var collider in t.GetComponentsInChildren<Collider>())
                collider.enabled = false;
        }

        private bool HasImportedPathArt()
        {
            GameObject prefab;
            float scale;
            return artSet != null && artSet.TryGet("path/large1", out prefab, out scale);
        }

        private void BuildDecorations(Transform root, WorldLayout.LayoutData data)
        {
            if (artSet == null || data.decorations == null) return;

            foreach (var decoration in data.decorations)
            {
                GameObject prefab;
                float artScale;
                if (!artSet.TryGet(decoration.key, out prefab, out artScale))
                {
                    Debug.LogWarning("[Wispmere] Town decoration has no art-set prefab: "
                        + decoration.key, this);
                    continue;
                }

#if UNITY_EDITOR
                var instance = Application.isPlaying
                    ? Instantiate(prefab, root)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, root);
#else
                var instance = Instantiate(prefab, root);
#endif
                instance.name = decoration.id;
                ApplyArtMaterials(instance);
                instance.transform.position = WorldLayout.ToUnity(decoration.x, decoration.y,
                    decoration.height);
                instance.transform.rotation = Quaternion.Euler(0f, decoration.rotation, 0f);
                instance.transform.localScale *= artScale * Mathf.Max(0.01f, decoration.scale);
                AlignPrefabToGround(instance,
                    WorldLayout.ToUnity(decoration.x, decoration.y, decoration.height));

                foreach (var collider in instance.GetComponentsInChildren<Collider>())
                    collider.enabled = false;
            }
        }

        private static void AlignPrefabToGround(GameObject instance, Vector3 target)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            instance.transform.position += new Vector3(target.x - bounds.center.x,
                target.y - bounds.min.y, target.z - bounds.center.z);
        }

        // ----- spawning -----

        private System.Action<GameObject> FallbackGarden = go =>
        {
            go.GetComponent<Renderer>().enabled = false;
            RemoveCollider(go);
            CreateFallbackPart(go, "Raised Garden Bed", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, 0f), new Vector3(1.25f, 0.16f, 1f),
                PlainMat(new Color(0.4f, 0.35f, 0.25f)), Quaternion.identity);
            RemoveCollider(go.transform.Find("Raised Garden Bed").gameObject);
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 0.19f;
                float height = 0.42f + (i % 2) * 0.14f;
                var stem = CreateFallbackPart(go, "Overgrown Bed Growth " + i, PrimitiveType.Cylinder,
                    new Vector3(x, height * 0.5f + 0.13f, (i % 2) * 0.22f - 0.1f),
                    new Vector3(0.12f, height, 0.12f),
                    PlainMat(i % 2 == 0 ? new Color(0.3f, 0.52f, 0.31f) : new Color(0.45f, 0.56f, 0.34f)),
                    Quaternion.Euler(0f, 0f, (i % 2 == 0 ? -1f : 1f) * 12f));
                RemoveCollider(stem);
            }
        };

        private System.Action<GameObject> FallbackMarker = go =>
        {
            go.GetComponent<Renderer>().enabled = false;
            RemoveCollider(go);
        };

        private static System.Action<GameObject> FallbackLandmark = go =>
        {
            go.GetComponent<Renderer>().enabled = false;
            RemoveCollider(go);
            CreateFallbackPart(go, "Weathered Fountain Basin", PrimitiveType.Cylinder,
                new Vector3(0f, 0.23f, 0f), new Vector3(1.55f, 0.42f, 1.35f),
                PlainMat(new Color(0.57f, 0.58f, 0.49f)), Quaternion.identity);
            RemoveCollider(go.transform.Find("Weathered Fountain Basin").gameObject);
            CreateFallbackPart(go, "Clear Still Water", PrimitiveType.Cylinder,
                new Vector3(0f, 0.47f, 0f), new Vector3(1.2f, 0.05f, 1f),
                PlainMat(new Color(0.31f, 0.61f, 0.65f)), Quaternion.identity);
            RemoveCollider(go.transform.Find("Clear Still Water").gameObject);
            CreateFallbackPart(go, "Fountain Pedestal", PrimitiveType.Cylinder,
                new Vector3(0f, 0.62f, 0f), new Vector3(0.32f, 0.72f, 0.32f),
                PlainMat(new Color(0.63f, 0.6f, 0.48f)), Quaternion.identity);
            RemoveCollider(go.transform.Find("Fountain Pedestal").gameObject);
            CreateFallbackPart(go, "Dormant Fountain Crest", PrimitiveType.Sphere,
                new Vector3(0f, 1.02f, 0f), new Vector3(0.38f, 0.44f, 0.38f),
                PlainMat(new Color(0.35f, 0.64f, 0.61f)), Quaternion.identity);
            RemoveCollider(go.transform.Find("Dormant Fountain Crest").gameObject);
        };

        private static void FallbackBuilding(GameObject go, string id)
        {
            if (id == "damaged")
            {
                FallbackWorkshop(go);
                return;
            }

            Color wall = id == "hall" ? new Color(0.73f, 0.65f, 0.48f)
                : id == "inn" ? new Color(0.68f, 0.43f, 0.32f)
                : id == "shop" ? new Color(0.78f, 0.67f, 0.42f)
                : id.StartsWith("cottage") ? new Color(0.62f, 0.68f, 0.47f)
                : id.StartsWith("house") ? new Color(0.61f, 0.52f, 0.42f)
                : id == "damaged" ? new Color(0.43f, 0.39f, 0.34f)
                : new Color(0.65f, 0.59f, 0.48f);
            Color roof = id == "inn" ? new Color(0.37f, 0.22f, 0.24f)
                : id == "damaged" ? new Color(0.32f, 0.29f, 0.27f)
                : new Color(0.27f, 0.36f, 0.31f);
            go.GetComponent<Renderer>().sharedMaterial = PlainMat(wall);

            Vector3 size = go.transform.localScale;
            AddFallbackPart(go, "RoofLeft", PrimitiveType.Cube,
                new Vector3(0f, 0.52f, -0.2f),
                new Vector3(size.x * 1.12f, 0.2f, size.z * 0.62f),
                PlainMat(roof), Quaternion.Euler(28f, 0f, 0f));
            AddFallbackPart(go, "RoofRight", PrimitiveType.Cube,
                new Vector3(0f, 0.52f, 0.2f),
                new Vector3(size.x * 1.12f, 0.2f, size.z * 0.62f),
                PlainMat(roof), Quaternion.Euler(-28f, 0f, 0f));
            AddFallbackPart(go, "Door", PrimitiveType.Cube,
                new Vector3(0f, -0.31f, 0.51f), new Vector3(0.34f, 0.72f, 0.05f),
                PlainMat(new Color(0.3f, 0.22f, 0.17f)), Quaternion.identity);
            AddFallbackPart(go, "WindowLeft", PrimitiveType.Cube,
                new Vector3(-0.27f, 0.08f, 0.51f), new Vector3(0.28f, 0.3f, 0.05f),
                PlainMat(new Color(0.25f, 0.34f, 0.34f)), Quaternion.identity);
            AddFallbackPart(go, "WindowRight", PrimitiveType.Cube,
                new Vector3(0.27f, 0.08f, 0.51f), new Vector3(0.28f, 0.3f, 0.05f),
                PlainMat(new Color(0.25f, 0.34f, 0.34f)), Quaternion.identity);
            AddFallbackPart(go, "Old Window Shutters", PrimitiveType.Cube,
                new Vector3(-0.48f, 0.08f, 0.53f), new Vector3(0.13f, 0.38f, 0.07f),
                PlainMat(new Color(0.43f, 0.39f, 0.29f)), Quaternion.identity);
            AddFallbackPart(go, "Old Window Shutters Right", PrimitiveType.Cube,
                new Vector3(0.48f, 0.08f, 0.53f), new Vector3(0.13f, 0.38f, 0.07f),
                PlainMat(new Color(0.43f, 0.39f, 0.29f)), Quaternion.identity);
        }

        private static void FallbackWorkshop(GameObject go)
        {
            go.GetComponent<Renderer>().sharedMaterial = PlainMat(new Color(0.52f, 0.55f, 0.41f));
            Vector3 size = go.transform.localScale;
            AddFallbackPart(go, "Workshop Roof Intact", PrimitiveType.Cube,
                new Vector3(-0.2f, 0.54f, -0.18f),
                new Vector3(size.x * 0.78f, 0.22f, size.z * 0.68f),
                PlainMat(new Color(0.34f, 0.35f, 0.31f)), Quaternion.Euler(28f, 0f, 0f));
            AddFallbackPart(go, "Workshop Roof Exposed Rafters", PrimitiveType.Cube,
                new Vector3(0.39f, 0.48f, 0.22f),
                new Vector3(size.x * 0.45f, 0.14f, size.z * 0.56f),
                PlainMat(new Color(0.4f, 0.29f, 0.2f)), Quaternion.Euler(-24f, 0f, 0f));
            AddFallbackPart(go, "Workshop Closed Door", PrimitiveType.Cube,
                new Vector3(0f, -0.31f, 0.53f), new Vector3(0.4f, 0.78f, 0.07f),
                PlainMat(new Color(0.28f, 0.24f, 0.2f)), Quaternion.identity);
            for (int i = -1; i <= 1; i++)
                AddFallbackPart(go, "Door Board " + i, PrimitiveType.Cube,
                    new Vector3(i * 0.1f, -0.31f, 0.57f), new Vector3(0.035f, 0.74f, 0.025f),
                    PlainMat(new Color(0.49f, 0.37f, 0.25f)), Quaternion.identity);
            AddFallbackPart(go, "Workshop Dark Window", PrimitiveType.Cube,
                new Vector3(-0.34f, 0.1f, 0.53f), new Vector3(0.27f, 0.3f, 0.06f),
                PlainMat(new Color(0.23f, 0.31f, 0.3f)), Quaternion.identity);
            AddFallbackPart(go, "Workshop Boarded Window", PrimitiveType.Cube,
                new Vector3(-0.34f, 0.1f, 0.57f), new Vector3(0.33f, 0.075f, 0.035f),
                PlainMat(new Color(0.48f, 0.36f, 0.25f)), Quaternion.Euler(0f, 0f, 12f));
            AddFallbackPart(go, "Workshop Front Beam", PrimitiveType.Cube,
                new Vector3(0.48f, 0.28f, 0.53f), new Vector3(0.12f, 1.45f, 0.12f),
                PlainMat(new Color(0.47f, 0.34f, 0.23f)), Quaternion.Euler(0f, 0f, -6f));
        }

        private static void CreateWorkshopMarker(Transform workshop)
        {
            Vector3 parentScale = workshop.lossyScale;
            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "Workshop Restoration Sign";
            sign.transform.SetParent(workshop, false);
            sign.transform.localPosition = new Vector3(0.42f / parentScale.x,
                2.35f / parentScale.y, 2.1f / parentScale.z);
            sign.transform.localScale = new Vector3(0.42f / parentScale.x,
                0.31f / parentScale.y, 0.08f / parentScale.z);
            sign.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            sign.GetComponent<Renderer>().sharedMaterial = PlainMat(new Color(0.78f, 0.62f, 0.35f));
            RemoveCollider(sign);

            var seal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            seal.name = "Workshop Repair Seal";
            seal.transform.SetParent(workshop, false);
            seal.transform.localPosition = new Vector3(0.42f / parentScale.x,
                2.85f / parentScale.y, 2.05f / parentScale.z);
            seal.transform.localScale = new Vector3(0.18f / parentScale.x,
                0.18f / parentScale.y, 0.18f / parentScale.z);
            seal.GetComponent<Renderer>().sharedMaterial = PlainMat(new Color(0.47f, 0.69f, 0.57f));
            RemoveCollider(seal);
        }

        private static System.Action<GameObject> FallbackTree(string key)
        {
            Color leaves = key == "tree/turquoise"
                ? new Color(0.2f, 0.62f, 0.58f)
                : new Color(0.27f, 0.5f, 0.34f);
            return go =>
            {
                go.GetComponent<Renderer>().enabled = false;
                RemoveCollider(go);

                var trunk = CreateFallbackPart(go, "Trunk", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 1.2f, 0.24f),
                    PlainMat(new Color(0.34f, 0.23f, 0.15f)), Quaternion.identity);
                var trunkCollider = trunk.AddComponent<CapsuleCollider>();
                trunkCollider.height = 2f;
                trunkCollider.radius = 0.5f;

                CreateFallbackPart(go, "Canopy", PrimitiveType.Sphere,
                    new Vector3(0f, 0.88f, 0f), new Vector3(0.95f, 0.95f, 0.95f),
                    PlainMat(leaves), Quaternion.identity);
                CreateFallbackPart(go, "CanopyLeft", PrimitiveType.Sphere,
                    new Vector3(-0.28f, 0.69f, 0.06f), new Vector3(0.62f, 0.7f, 0.62f),
                    PlainMat(leaves * 0.88f), Quaternion.identity);
                CreateFallbackPart(go, "CanopyRight", PrimitiveType.Sphere,
                    new Vector3(0.29f, 0.7f, -0.04f), new Vector3(0.64f, 0.72f, 0.64f),
                    PlainMat(leaves * 1.08f), Quaternion.identity);
            };
        }

        private System.Action<GameObject> FallbackNode(string kind)
        {
            return go =>
            {
                go.GetComponent<Renderer>().enabled = false;
                RemoveCollider(go);

                if (kind == "wood")
                {
                    CreateFallbackPart(go, "Log", PrimitiveType.Cylinder,
                        new Vector3(0f, -0.28f, 0f), new Vector3(0.3f, 1.15f, 0.3f),
                        PlainMat(new Color(0.43f, 0.29f, 0.17f)), Quaternion.Euler(0f, 0f, 90f));
                    CreateFallbackPart(go, "LogEndLeft", PrimitiveType.Cylinder,
                        new Vector3(-0.56f, -0.28f, 0f), new Vector3(0.29f, 0.05f, 0.29f),
                        PlainMat(new Color(0.7f, 0.53f, 0.31f)), Quaternion.Euler(0f, 0f, 90f));
                    CreateFallbackPart(go, "LogEndRight", PrimitiveType.Cylinder,
                        new Vector3(0.56f, -0.28f, 0f), new Vector3(0.29f, 0.05f, 0.29f),
                        PlainMat(new Color(0.7f, 0.53f, 0.31f)), Quaternion.Euler(0f, 0f, 90f));
                }
                else if (kind == "stone")
                {
                    CreateFallbackPart(go, "Stone", PrimitiveType.Sphere,
                        new Vector3(0f, -0.22f, 0f), new Vector3(0.66f, 0.52f, 0.62f),
                        PlainMat(new Color(0.55f, 0.57f, 0.62f)), Quaternion.identity);
                    CreateFallbackPart(go, "StoneChip", PrimitiveType.Sphere,
                        new Vector3(0.28f, -0.25f, 0.1f), new Vector3(0.37f, 0.3f, 0.34f),
                        PlainMat(new Color(0.7f, 0.71f, 0.73f)), Quaternion.identity);
                }
                else
                {
                    for (int i = -1; i <= 1; i++)
                        CreateFallbackPart(go, "Fiber" + i, PrimitiveType.Cylinder,
                            new Vector3(i * 0.2f, -0.02f, (i % 2) * 0.12f),
                            new Vector3(0.08f, 0.55f + Mathf.Abs(i) * 0.1f, 0.08f),
                            PlainMat(new Color(0.29f, 0.7f - Mathf.Abs(i) * 0.08f, 0.37f)),
                            Quaternion.Euler(0f, 0f, i * 12f));
                }
            };
        }

        private static GameObject CreateFallbackPart(GameObject parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 worldScale, Material material, Quaternion localRotation)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPosition;
            float primitiveHeight = type == PrimitiveType.Cylinder ? 2f : 1f;
            part.transform.localScale = new Vector3(
                worldScale.x / parent.transform.localScale.x,
                worldScale.y / (parent.transform.localScale.y * primitiveHeight),
                worldScale.z / parent.transform.localScale.z);
            part.transform.localRotation = localRotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static void AddFallbackPart(GameObject parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 worldScale, Material material, Quaternion localRotation)
        {
            var part = CreateFallbackPart(parent, name, type, localPosition, worldScale, material, localRotation);
            RemoveCollider(part);
        }

        private static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(collider);
            else Destroy(collider);
#else
            Destroy(collider);
#endif
        }

        private Transform SpawnKeyed(Transform root, string key, Vector3 pos, Vector3 size, System.Action<GameObject> stylePrimitive)
        {
            GameObject prefab;
            float scale;
            if (artSet != null && artSet.TryGet(key, out prefab, out scale))
            {
#if UNITY_EDITOR
                var inst = Application.isPlaying
                    ? Instantiate(prefab, root)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, root);
#else
                var inst = Instantiate(prefab, root);
#endif
                inst.name = key.Replace('/', '_');
                ApplyArtMaterials(inst);
                inst.transform.position = pos;
                inst.transform.localScale *= scale;
                AlignPrefabToGround(inst, pos);
                return inst.transform;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = key.Replace('/', '_') + "_placeholder";
            go.transform.SetParent(root, false);
            go.transform.position = pos + Vector3.up * Mathf.Max(0.05f, size.y * 0.5f);
            go.transform.localScale = new Vector3(Mathf.Max(0.5f, size.x),
                Mathf.Max(0.1f, size.y), Mathf.Max(0.5f, size.z));
            if (stylePrimitive != null) stylePrimitive(go);
            return go.transform;
        }

        private void ApplyArtMaterials(GameObject instance)
        {
            if (artSet == null) return;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material replacement;
                    if (artSet.TryGetReplacement(materials[i], out replacement))
                    {
                        materials[i] = replacement;
                        changed = true;
                    }
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static Material PlainMat(Color c)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Standard");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[Wispmere] No supported URP/Lit or Standard shader is available for town placeholders.");
                return null;
            }

            var m = new Material(shader);
            m.color = c;
            return m;
        }
    }
}
