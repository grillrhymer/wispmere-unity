using System.Collections.Generic;
using UnityEngine;

namespace Wispmere
{
    public class PlacedWorldObject : MonoBehaviour
    {
        private static readonly Dictionary<Color, Material> SharedMaterials =
            new Dictionary<Color, Material>();
        public string kind;

        public static PlacedWorldObject Create(string objectKind, Vector3 position,
            float yaw, Transform parent)
        {
            GameObject root = new GameObject(objectKind == "workbench" ? "Placed Workbench" : "Placed Tent");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            PlacedWorldObject placed = root.AddComponent<PlacedWorldObject>();
            placed.kind = objectKind;
            BuildVisual(root.transform, objectKind, false);

            BoxCollider bounds = root.AddComponent<BoxCollider>();
            bounds.center = objectKind == "workbench"
                ? new Vector3(0f, 0.45f, 0f)
                : new Vector3(0f, 0.55f, 0f);
            bounds.size = objectKind == "workbench"
                ? new Vector3(1.55f, 0.9f, 1.05f)
                : new Vector3(1.95f, 1.1f, 1.65f);

            if (objectKind == "workbench")
            {
                Interactable station = root.AddComponent<Interactable>();
                station.label = "Workbench";
                station.text = "";
                station.radius = 2.1f;
                station.isWorkbench = true;
            }
            return placed;
        }

        public static GameObject CreatePreview(string objectKind, Transform parent)
        {
            GameObject preview = new GameObject("Placement Preview");
            preview.transform.SetParent(parent, false);
            BuildVisual(preview.transform, objectKind, true);
            return preview;
        }

        public static Vector3 FootprintHalfExtents(string objectKind)
        {
            return objectKind == "workbench"
                ? new Vector3(0.82f, 0.46f, 0.57f)
                : new Vector3(1.02f, 0.56f, 0.87f);
        }

        public static void SetPreviewValidity(GameObject preview, bool valid)
        {
            if (preview == null) return;
            Color tint = valid
                ? new Color(0.35f, 1f, 0.45f, 0.48f)
                : new Color(1f, 0.28f, 0.22f, 0.48f);
            foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial.color = tint;
        }

        public static void DestroyPreview(GameObject preview)
        {
            if (preview == null) return;
            HashSet<Material> materials = new HashSet<Material>();
            foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>())
                if (renderer.sharedMaterial != null)
                    materials.Add(renderer.sharedMaterial);
            Object.Destroy(preview);
            foreach (Material material in materials) Object.Destroy(material);
        }

        private static void BuildVisual(Transform root, string objectKind, bool preview)
        {
            Material wood = MakeMaterial(new Color(0.39f, 0.25f, 0.14f), preview);
            Material lightWood = MakeMaterial(new Color(0.61f, 0.42f, 0.23f), preview);
            Material canvas = MakeMaterial(new Color(0.72f, 0.63f, 0.43f), preview);

            if (objectKind == "workbench")
            {
                AddPart(root, "Tabletop", new Vector3(0f, 0.82f, 0f),
                    new Vector3(1.55f, 0.16f, 1.05f), lightWood, preview);
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    AddPart(root, "Leg", new Vector3(x * 0.62f, 0.4f, z * 0.39f),
                        new Vector3(0.14f, 0.8f, 0.14f), wood, preview);
                AddPart(root, "LowerShelf", new Vector3(0f, 0.3f, 0f),
                    new Vector3(1.2f, 0.1f, 0.72f), wood, preview);
            }
            else if (objectKind == "tent")
            {
                AddPart(root, "TentRoofLeft", new Vector3(0f, 0.63f, 0f),
                    new Vector3(2f, 0.1f, 1.18f), canvas, preview,
                    Quaternion.Euler(0f, 0f, -38f));
                AddPart(root, "TentRoofRight", new Vector3(0f, 0.63f, 0f),
                    new Vector3(2f, 0.1f, 1.18f), canvas, preview,
                    Quaternion.Euler(0f, 0f, 38f));
                AddPart(root, "TentEnd", new Vector3(0f, 0.3f, -0.53f),
                    new Vector3(0.1f, 0.64f, 0.94f), lightWood, preview,
                    Quaternion.Euler(0f, 0f, 0f));
            }
            else
            {
                Debug.LogError("[Wispmere] Cannot build unsupported placed object: " + objectKind);
            }
        }

        private static Material MakeMaterial(Color color, bool preview)
        {
            if (!preview && SharedMaterials.TryGetValue(color, out Material existing))
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[Wispmere] No supported shader is available for placed objects.");
                return null;
            }

            Material material = new Material(shader);
            material.color = preview
                ? new Color(color.r, color.g, color.b, 0.48f)
                : color;
            if (preview)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            else
                SharedMaterials[color] = material;
            return material;
        }

        private static void AddPart(Transform root, string name, Vector3 position,
            Vector3 size, Material material, bool preview, Quaternion? rotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (preview)
            {
                Collider collider = part.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }
        }
    }
}
