#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wispmere;

namespace Wispmere.Editor
{
    public static class OccaVillageArtSetBuilder
    {
        private const string ArtSetPath = "Assets/Wispmere/Art/OccaVillageTownArtSet.asset";
        private const string PrefabFolder = "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
        private const string FantasyTownPrefabFolder = "Assets/FantasyEnvironments/Environments/Town/Prefabs/";
        private const string FantasyNaturePrefabFolder = "Assets/FantasyEnvironments/Environments/Prefabs/";
        private const string SourceMaterialFolder = "Assets/OccaSoftware/Low Poly Fantasy Village/Materials/";
        private const string PaletteTexturePath = "Assets/OccaSoftware/Low Poly Fantasy Village/Textures/Gradient.png";
        private const string CompatibleMaterialFolder = "Assets/Wispmere/Art/Materials";

        private struct Mapping
        {
            public string key;
            public string prefab;
            public string folder;
            public float scale;

            public Mapping(string key, string prefab, float scale)
            {
                this.key = key;
                this.prefab = prefab;
                this.folder = PrefabFolder;
                this.scale = scale;
            }

            public Mapping(string key, string prefab, float scale, string folder)
            {
                this.key = key;
                this.prefab = prefab;
                this.folder = folder;
                this.scale = scale;
            }
        }

        [MenuItem("Wispmere/Integrate/Apply Occa Village Art Set %#o")]
        public static void ApplyToOpenScene()
        {
            TownBuilder town = UnityEngine.Object.FindAnyObjectByType<TownBuilder>();
            if (town == null)
            {
                Debug.LogError("[Wispmere] No TownBuilder is present in the open scene.");
                return;
            }

            TownArtSet artSet = CreateOrUpdate();
            if (artSet == null) return;

            Undo.RecordObject(town, "Apply Occa village art set");
            town.artSet = artSet;
            town.Build();
            EditorSceneManager.MarkSceneDirty(town.gameObject.scene);
            EditorSceneManager.SaveScene(town.gameObject.scene);
            Debug.Log("[Wispmere] Applied Occa village visuals and saved the gameplay scene.");
        }

        public static TownArtSet CreateOrUpdate()
        {
            EnsureFolder("Assets/Wispmere/Art");
            EnsureFolder(CompatibleMaterialFolder);

            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null || !standardShader.isSupported)
            {
                Debug.LogError("[Wispmere] The Built-in Standard shader is unavailable; Occa materials cannot be made compatible.");
                return null;
            }

            Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexturePath);
            Material sourceColor = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialFolder + "Color.mat");
            Material sourceLight = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialFolder + "Light.mat");
            if (palette == null || sourceColor == null || sourceLight == null)
            {
                Debug.LogError("[Wispmere] Occa material conversion requires Color.mat, Light.mat, and Textures/Gradient.png.");
                return null;
            }

            Material colorMaterial = CreateOrUpdateCompatibleMaterial(
                "Assets/Wispmere/Art/Materials/OccaVillageColor.mat", standardShader,
                palette, Color.white, Color.black);
            Material lightMaterial = CreateOrUpdateCompatibleMaterial(
                "Assets/Wispmere/Art/Materials/OccaVillageLight.mat", standardShader,
                palette, new Color(0.9063317f, 0.80552274f, 0.4704541f),
                new Color(3.9955149f, 3.052479f, 0.939104f, 5f));
            if (colorMaterial == null || lightMaterial == null) return null;

            TownArtSet artSet = AssetDatabase.LoadAssetAtPath<TownArtSet>(ArtSetPath);
            if (artSet == null)
            {
                artSet = ScriptableObject.CreateInstance<TownArtSet>();
                AssetDatabase.CreateAsset(artSet, ArtSetPath);
            }

            var entries = new List<TownArtSet.PrefabEntry>();
            foreach (Mapping mapping in Mappings())
            {
                string path = mapping.folder + mapping.prefab + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError("[Wispmere] Required OccaSoftware prefab is missing: " + path);
                    return null;
                }

                entries.Add(new TownArtSet.PrefabEntry
                {
                    key = mapping.key,
                    prefab = prefab,
                    scale = mapping.scale,
                });
            }

            Undo.RecordObject(artSet, "Update Occa village art set");
            artSet.entries = entries;
            artSet.materialReplacements = new List<TownArtSet.MaterialReplacement>
            {
                new TownArtSet.MaterialReplacement { source = sourceColor, replacement = colorMaterial },
                new TownArtSet.MaterialReplacement { source = sourceLight, replacement = lightMaterial },
            };
            EditorUtility.SetDirty(artSet);
            AssetDatabase.SaveAssets();
            return artSet;
        }

        private static Material CreateOrUpdateCompatibleMaterial(string path, Shader shader,
            Texture2D palette, Color tint, Color emission)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = palette;
            material.color = tint;
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static IEnumerable<Mapping> Mappings()
        {
            yield return new Mapping("building/inn", "House_2", 0.30f);
            yield return new Mapping("building/hall", "House_3", 0.30f);
            yield return new Mapping("building/shop", "House_1", 0.30f);
            yield return new Mapping("building/house", "House_2", 0.30f);
            yield return new Mapping("building/house2", "House_3", 0.30f);
            yield return new Mapping("building/cottage", "House_1", 0.30f);
            yield return new Mapping("building/cottage2", "House_2", 0.30f);
            yield return new Mapping("building/damaged", "House_3", 0.30f);

            yield return new Mapping("tree/teal", "Tree_1", 0.48f);
            yield return new Mapping("tree/turquoise", "Tree_5", 0.43f);
            yield return new Mapping("node/stone", "Rock_2", 0.9f);

            yield return new Mapping("path/large1", "Path_1", 0.4f);
            yield return new Mapping("path/large2", "Path_2", 0.4f);
            for (int i = 1; i <= 16; i++)
                yield return new Mapping("path/piece" + i, "Path Piece_" + i, 1f);

            for (int i = 1; i <= 8; i++)
                yield return new Mapping("decoration/tree" + i, "Tree_" + i,
                    i == 3 || i == 4 || i == 5 || i == 8 ? 0.43f : 0.48f);
            for (int i = 1; i <= 5; i++)
                yield return new Mapping("decoration/pine" + i, "Pine Tree_" + i, 0.45f);
            for (int i = 1; i <= 4; i++)
                yield return new Mapping("decoration/rock" + i, "Rock_" + i, 1f);
            for (int i = 1; i <= 5; i++)
                yield return new Mapping("decoration/flower" + i, "Flower_" + i, 0.45f);

            yield return new Mapping("decoration/fence", "Fence", 0.55f);
            yield return new Mapping("decoration/bench", "Bench", 0.7f);
            yield return new Mapping("decoration/lantern", "Lantern", 0.55f);
            yield return new Mapping("decoration/crate", "Crate", 0.45f);
            yield return new Mapping("decoration/flower-pot", "Flower Pot", 0.55f);
            yield return new Mapping("decoration/bridge", "Bridge", 0.65f);
            yield return new Mapping("decoration/boat", "Boat", 0.65f);

            yield return new Mapping("decoration/windmill", "Windmill", 0.18f, FantasyTownPrefabFolder);
            yield return new Mapping("decoration/cart", "cart1", 0.4f, FantasyTownPrefabFolder);
            yield return new Mapping("decoration/work-barrel", "storage_barrel", 0.65f, FantasyTownPrefabFolder);
            yield return new Mapping("decoration/work-basket", "storage_basket", 0.65f, FantasyTownPrefabFolder);
            yield return new Mapping("decoration/meadow-grass", "Grass1", 0.28f, FantasyNaturePrefabFolder);
            yield return new Mapping("decoration/meadow-fern", "Fern1", 0.3f, FantasyNaturePrefabFolder);
            yield return new Mapping("decoration/meadow-flower", "Flower1", 0.3f, FantasyNaturePrefabFolder);

            yield return new Mapping("fantasy/tree-birch-1", "Birch_tree1", 0.22f, "Assets/FantasyEnvironments/Environments/Ambient-Occlusion-Trees/Prefabs/");
            yield return new Mapping("fantasy/tree-birch-2", "Birch_tree2", 0.22f, "Assets/FantasyEnvironments/Environments/Ambient-Occlusion-Trees/Prefabs/");
            yield return new Mapping("fantasy/tree-oak-2", "Oak_tree2", 0.22f, "Assets/FantasyEnvironments/Environments/Ambient-Occlusion-Trees/Prefabs/");
            yield return new Mapping("fantasy/tree-deciduous-3", "Deciduous_tree3", 0.22f, "Assets/FantasyEnvironments/Environments/Ambient-Occlusion-Trees/Prefabs/");
            yield return new Mapping("fantasy/tree-willow-1", "Willow_tree1", 0.25f, "Assets/FantasyEnvironments/Environments/Ambient-Occlusion-Trees/Prefabs/");
            yield return new Mapping("fantasy/tree-pine-2", "Pine_tree2", 0.24f, "Assets/FantasyEnvironments/Environments/Ambient-Occlusion-Trees/Prefabs/");
            yield return new Mapping("fantasy/grass-1", "Grass1", 0.52f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/grass-2", "Grass2", 0.52f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/grass-3", "Grass3", 0.52f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/grass-4", "Grass4", 0.52f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/fern-1", "Fern1", 0.45f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/fern-2", "Fern2", 0.45f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/fern-3", "Fern3", 0.45f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/flower-2", "Flower2", 0.4f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/flower-3", "Flower3", 0.4f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/flower-5", "Flower5", 0.4f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/flower-6", "Flower6", 0.4f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/flower-8", "Flower8", 0.4f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/plant-2", "Plant2", 0.36f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/plant-4", "Plant4", 0.36f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/bush-1", "Bush1", 0.36f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/rock-1", "Rock1", 0.7f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/rock-2", "Rock2", 0.02f, FantasyNaturePrefabFolder);
            yield return new Mapping("fantasy/rock-3", "Rock3", 0.7f, FantasyNaturePrefabFolder);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
