using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wispmere;

namespace Wispmere.Editor
{
    public static class ArtDirectionSceneBuilder
    {
        private const string ScenePath = "Assets/Wispmere/Scenes/ART_DIRECTION_TEST.unity";
        private const string SceneName = "ART_DIRECTION_TEST";

        private static readonly Color GroundColor = Hex("527D4E");
        private static readonly Color Cream = Hex("F0D7A5");
        private static readonly Color Wood = Hex("79513A");
        private static readonly Color DeepWood = Hex("4B332A");
        private static readonly Color Turquoise = Hex("45B6A5");
        private static readonly Color Emerald = Hex("247A58");
        private static readonly Color Indigo = Hex("363D78");
        private static readonly Color Violet = Hex("8053A3");
        private static readonly Color Gold = Hex("E7B84E");

        private static readonly Dictionary<Color, Material> SceneMaterials = new Dictionary<Color, Material>();
        private static readonly Dictionary<string, Mesh> SharedMeshes = new Dictionary<string, Mesh>();

        [MenuItem("Wispmere/Art Direction/Create Test Scene")]
        public static void Build()
        {
            SceneMaterials.Clear();
            SharedMeshes.Clear();
            EnsureFolder("Assets/Wispmere/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("B6C8B5");
            RenderSettings.ambientIntensity = 0.72f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex("AFC9D3");
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 72f;

            CreateLighting();
            var content = new GameObject("Eight Core Style Tests");
            CreateTerrain(content.transform);
            CreateHouse(content.transform);
            CreateTree(content.transform);
            CreateRock(content.transform);
            CreateOreNode(content.transform);
            CreateGrassCover(content.transform);
            CreateMagicalPlant(content.transform);
            CreatePlayer(content.transform);
            CreateNpc(content.transform);
            CreateCamera(content.transform.Find("Player Character - Existing Customizer"));

            SceneManager.SetActiveScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Validate(scene);

            SetSceneInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Wispmere] Art direction test scene created and validated: " + ScenePath);
        }

        private static void CreateLighting()
        {
            var sun = new GameObject("Late Afternoon Sun", typeof(Light));
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex("FFE0AA");
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.76f;
        }

        private static void CreateTerrain(Transform root)
        {
            var terrain = new GameObject("Clearing Terrain");
            terrain.transform.SetParent(root, false);

            const int columns = 34;
            const int rows = 28;
            const float width = 24f;
            const float depth = 20f;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];

            for (int z = 0; z <= rows; z++)
            {
                for (int x = 0; x <= columns; x++)
                {
                    float px = -width * 0.5f + width * x / columns;
                    float pz = -depth * 0.5f + depth * z / rows;
                    int index = z * (columns + 1) + x;
                    vertices[index] = new Vector3(px, GroundHeight(px, pz), pz);
                    uv[index] = new Vector2(x / (float)columns, z / (float)rows);
                }
            }

            int triangle = 0;
            for (int z = 0; z < rows; z++)
                for (int x = 0; x < columns; x++)
                {
                    int a = z * (columns + 1) + x;
                    int b = a + columns + 1;
                    triangles[triangle++] = a;
                    triangles[triangle++] = b;
                    triangles[triangle++] = a + 1;
                    triangles[triangle++] = a + 1;
                    triangles[triangle++] = b;
                    triangles[triangle++] = b + 1;
                }

            var mesh = new Mesh { name = "Rolling Clearing Mesh" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var ground = new GameObject("Soft Rolling Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            ground.transform.SetParent(terrain.transform, false);
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            ground.GetComponent<MeshRenderer>().sharedMaterial = Mat(GroundColor);
            ground.GetComponent<MeshCollider>().sharedMesh = mesh;

            CreatePatch(terrain.transform, "Meadow Patch", new Vector3(-1.5f, 0f, -1.8f),
                new Vector3(7.2f, 0.08f, 4.6f), Hex("629255"));
            CreatePatch(terrain.transform, "Mint Grass Patch", new Vector3(5.5f, 0f, -3.6f),
                new Vector3(4.2f, 0.07f, 2.9f), Hex("71A65B"));
            CreatePatch(terrain.transform, "Golden Grass Patch", new Vector3(-6.5f, 0f, 1f),
                new Vector3(3.5f, 0.06f, 2.8f), Hex("9A9A55"));

            CreateGroundScatter(terrain.transform);
        }

        private static void CreatePatch(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            position.y = GroundHeight(position.x, position.z) + 0.025f;
            var patch = Ico(name, parent, position, scale, new[] { color, Color.Lerp(color, Color.white, 0.08f) }, 1);
            patch.transform.localRotation = Quaternion.Euler(0f, 17f, 0f);
        }

        private static void CreateGroundScatter(Transform parent)
        {
            var cover = new GameObject("Scattered Small Stones And Flowers");
            cover.transform.SetParent(parent, false);
            var positions = new[]
            {
                new Vector2(-8.4f, -3.2f), new Vector2(-7.6f, -2.8f), new Vector2(-8.1f, -2.4f),
                new Vector2(7.7f, -1.6f), new Vector2(8.1f, -1.2f), new Vector2(7.3f, -1.1f),
                new Vector2(-2.8f, 5.3f), new Vector2(-2.1f, 5.5f), new Vector2(1.9f, 5.4f),
                new Vector2(2.6f, 5.1f), new Vector2(-9.1f, 2.2f), new Vector2(9f, 2.5f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                float x = positions[i].x;
                float z = positions[i].y;
                if (i % 3 == 0)
                {
                    Ico("Fieldstone " + i, cover.transform,
                        new Vector3(x, GroundHeight(x, z) + 0.08f, z),
                        new Vector3(0.43f, 0.24f, 0.36f),
                        new[] { Hex("788579"), Hex("8B9688"), Hex("69776D") }, 0);
                }
                else
                {
                    CreateFlower(cover.transform, "Meadow Flower " + i,
                        new Vector3(x, GroundHeight(x, z), z),
                        i % 2 == 0 ? Gold : Hex("E995AC"), 0.34f);
                }
            }
        }

        private static void CreateHouse(Transform parent)
        {
            var house = new GameObject("House - Crooked Lantern Cottage");
            house.transform.SetParent(parent, false);
            house.transform.position = GroundPosition(-6f, 2.4f);

            Cube(house.transform, "Raised Stone Foundation", new Vector3(0f, 0.25f, 0f),
                new Vector3(3.55f, 0.5f, 3.1f), Hex("8A8274"));
            var walls = Cube(house.transform, "Warm Cream Crooked Walls", new Vector3(0f, 1.62f, 0f),
                new Vector3(3.15f, 2.35f, 2.75f), Cream);
            walls.transform.localRotation = Quaternion.Euler(0f, -3.5f, -1.8f);

            Cube(house.transform, "Turquoise Front Gable", new Vector3(0f, 2.92f, -0.03f),
                new Vector3(1.75f, 0.86f, 0.1f), Turquoise, Quaternion.Euler(0f, 0f, 45f));
            Cube(house.transform, "Blue Gable Accent", new Vector3(0f, 2.92f, 0.04f),
                new Vector3(1.75f, 0.86f, 0.1f), Indigo, Quaternion.Euler(0f, 0f, -45f));

            Cube(house.transform, "Oversized Front Roof", new Vector3(0f, 3.2f, -0.72f),
                new Vector3(3.95f, 0.25f, 2f), Indigo, Quaternion.Euler(27f, 0f, 0f));
            Cube(house.transform, "Oversized Rear Roof", new Vector3(0f, 3.2f, 0.72f),
                new Vector3(3.95f, 0.25f, 2f), Indigo, Quaternion.Euler(-27f, 0f, 0f));
            Cube(house.transform, "Ridge Cap", new Vector3(0f, 3.63f, 0f),
                new Vector3(4.05f, 0.18f, 0.22f), Violet);

            Cube(house.transform, "Tall Chimney", new Vector3(-1.05f, 3.42f, 0.45f),
                new Vector3(0.48f, 1.4f, 0.52f), Hex("AD7155"));
            Cube(house.transform, "Chimney Cap", new Vector3(-1.05f, 4.16f, 0.45f),
                new Vector3(0.66f, 0.18f, 0.68f), Hex("D18C58"));

            Cube(house.transform, "Painted Teal Door", new Vector3(0.12f, 0.91f, -1.42f),
                new Vector3(0.68f, 1.34f, 0.12f), Hex("327A6E"));
            Cube(house.transform, "Brass Door Knob", new Vector3(0.35f, 0.88f, -1.51f),
                new Vector3(0.1f, 0.1f, 0.05f), Gold);
            Cube(house.transform, "Door Canopy", new Vector3(0.12f, 1.7f, -1.48f),
                new Vector3(1.05f, 0.14f, 0.42f), Hex("BD744A"));

            CreateHouseWindow(house.transform, "Round Window", new Vector3(-0.8f, 2.15f, -1.42f), 0.58f);
            CreateHouseWindow(house.transform, "Tall Window", new Vector3(1.05f, 1.98f, -1.42f), 0.42f);
            Cube(house.transform, "Left Turquoise Shutter", new Vector3(-1.25f, 2.15f, -1.51f),
                new Vector3(0.18f, 0.7f, 0.12f), Turquoise);
            Cube(house.transform, "Right Turquoise Shutter", new Vector3(-0.35f, 2.15f, -1.51f),
                new Vector3(0.18f, 0.7f, 0.12f), Turquoise);

            Cube(house.transform, "Hanging Sign Bracket", new Vector3(1.45f, 2.58f, -1.42f),
                new Vector3(0.1f, 0.78f, 0.12f), DeepWood);
            var sign = Cube(house.transform, "Lantern Sign", new Vector3(1.45f, 2.23f, -1.62f),
                new Vector3(0.56f, 0.45f, 0.12f), Gold);
            sign.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            Ico("Sign Jewel", house.transform, new Vector3(1.45f, 2.23f, -1.71f),
                new Vector3(0.18f, 0.2f, 0.08f), new[] { Violet, Hex("9C68C0") }, 0);

            Cube(house.transform, "Front Step", new Vector3(0.12f, 0.1f, -1.73f),
                new Vector3(1.1f, 0.2f, 0.65f), Hex("A19179"));
        }

        private static void CreateHouseWindow(Transform parent, string name, Vector3 position, float size)
        {
            Ico(name + " Brass Frame", parent, position, new Vector3(size + 0.17f, size + 0.17f, 0.11f),
                new[] { Gold, Hex("C8964C") }, 1);
            Ico(name + " Blue Glass", parent, position + Vector3.forward * -0.075f,
                new Vector3(size, size, 0.08f), new[] { Hex("81CAD0"), Hex("A2DCE0") }, 1);
            Cube(parent, name + " Mullion", position + Vector3.forward * -0.13f,
                new Vector3(0.055f, size, 0.035f), DeepWood);
            Cube(parent, name + " Crossbar", position + Vector3.forward * -0.13f,
                new Vector3(size, 0.055f, 0.035f), DeepWood);
        }

        private static void CreateTree(Transform parent)
        {
            var tree = new GameObject("Tree - Turquoise Crown");
            tree.transform.SetParent(parent, false);
            tree.transform.position = GroundPosition(5.8f, 2.5f);

            CylinderBetween(tree.transform, "Exaggerated Warm Trunk",
                new Vector3(0f, 0f, 0f), new Vector3(0.1f, 3.25f, 0f), 0.48f, Wood);
            CylinderBetween(tree.transform, "Left Branch",
                new Vector3(0f, 1.8f, 0f), new Vector3(-1.15f, 3.65f, 0f), 0.24f, Wood);
            CylinderBetween(tree.transform, "Right Branch",
                new Vector3(0.08f, 2.1f, 0f), new Vector3(1.35f, 3.48f, 0.08f), 0.22f, Wood);
            CylinderBetween(tree.transform, "Upper Branch",
                new Vector3(0.02f, 2.45f, 0f), new Vector3(-0.1f, 4.15f, 0.05f), 0.19f, Wood);

            Ico("Crown Base", tree.transform, new Vector3(0f, 4.1f, 0f),
                new Vector3(3.5f, 2.65f, 2.65f), new[] { Emerald, Hex("338C64"), Hex("2D9B79") }, 1);
            Ico("Crown Left", tree.transform, new Vector3(-1.05f, 3.82f, 0.05f),
                new Vector3(2.15f, 1.95f, 1.95f), new[] { Hex("238D6C"), Turquoise, Emerald }, 1);
            Ico("Crown Right", tree.transform, new Vector3(1.15f, 3.72f, 0.1f),
                new Vector3(2.25f, 2f, 1.9f), new[] { Hex("25896D"), Hex("42B9A0"), Hex("27835E") }, 1);
            Ico("Crown Top", tree.transform, new Vector3(0.16f, 4.85f, -0.05f),
                new Vector3(2.35f, 1.78f, 2.05f), new[] { Hex("3AA77A"), Hex("52C3A2"), Emerald }, 1);

            for (int i = 0; i < 5; i++)
            {
                float angle = i * 1.71f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * 1.12f, 3.56f + (i % 2) * 0.8f,
                    Mathf.Sin(angle) * 0.94f);
                Ico("Blossom Accent " + i, tree.transform, pos, Vector3.one * 0.22f,
                    new[] { Hex("E98EAD"), Hex("F2B2C4") }, 0);
            }
        }

        private static void CreateRock(Transform parent)
        {
            var rock = new GameObject("Rock - Faceted Slate");
            rock.transform.SetParent(parent, false);
            rock.transform.position = GroundPosition(-4.1f, -2f);
            Ico("Large Faceted Rock", rock.transform, new Vector3(0f, 0.62f, 0f),
                new Vector3(2.15f, 1.35f, 1.55f),
                new[] { Hex("78847F"), Hex("92988C"), Hex("66736F"), Hex("A49D87") }, 1);
            Ico("Rock Shoulder", rock.transform, new Vector3(0.92f, 0.38f, -0.2f),
                new Vector3(0.82f, 0.88f, 0.95f),
                new[] { Hex("808A82"), Hex("9A9C8E"), Hex("6C7D78") }, 0);
        }

        private static void CreateOreNode(Transform parent)
        {
            var ore = new GameObject("Resource Node - Moonstone Ore");
            ore.transform.SetParent(parent, false);
            ore.transform.position = GroundPosition(3.2f, -1f);

            Ico("Dark Ore Base", ore.transform, new Vector3(0f, 0.4f, 0f),
                new Vector3(1.75f, 0.92f, 1.55f),
                new[] { Hex("44485D"), Hex("555A70"), Hex("383D55") }, 1);
            CreateCrystal(ore.transform, "Tall Moonstone", new Vector3(-0.35f, 0.48f, 0f),
                new Vector3(0.42f, 1.9f, 0.42f), Hex("78C9C7"), Hex("B0E5D8"));
            CreateCrystal(ore.transform, "Violet Moonstone", new Vector3(0.42f, 0.42f, -0.18f),
                new Vector3(0.38f, 1.48f, 0.38f), Hex("9C82D5"), Hex("C2A9E5"));
            CreateCrystal(ore.transform, "Small Moonstone", new Vector3(0.28f, 0.4f, 0.44f),
                new Vector3(0.28f, 1.05f, 0.3f), Hex("62B8B7"), Hex("A2DFD0"));
            CreateCrystal(ore.transform, "Side Shard", new Vector3(-0.68f, 0.34f, -0.25f),
                new Vector3(0.26f, 0.92f, 0.3f), Hex("9A7BCC"), Hex("C2A0DC"));
            Ico("Ore Marker", ore.transform, new Vector3(0f, 2.35f, 0f),
                new Vector3(0.19f, 0.19f, 0.19f), new[] { Gold, Hex("F5D67D") }, 0);
        }

        private static void CreateCrystal(Transform parent, string name, Vector3 position, Vector3 scale,
            Color body, Color light)
        {
            var crystal = new GameObject(name);
            crystal.transform.SetParent(parent, false);
            crystal.transform.localPosition = position;
            crystal.transform.localRotation = Quaternion.Euler(0f, position.x * 37f, position.z * 28f);
            crystal.transform.localScale = scale;
            var filter = crystal.AddComponent<MeshFilter>();
            var renderer = crystal.AddComponent<MeshRenderer>();
            filter.sharedMesh = CrystalMesh(name);
            renderer.sharedMaterials = new[] { Mat(body), Mat(light), Mat(Color.Lerp(body, Color.white, 0.2f)) };
        }

        private static Mesh CrystalMesh(string name)
        {
            const string cacheKey = "Crystal";
            if (SharedMeshes.TryGetValue(cacheKey, out var cached))
                return cached;

            var mesh = new Mesh { name = name + " Faceted Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(-0.42f, 0.68f, -0.42f), new Vector3(0.42f, 0.68f, -0.42f),
                new Vector3(0.42f, 0.68f, 0.42f), new Vector3(-0.42f, 0.68f, 0.42f),
                new Vector3(-0.06f, 1f, 0.03f),
            };
            mesh.subMeshCount = 3;
            mesh.SetTriangles(new[]
            {
                0, 1, 4, 1, 5, 4, 1, 2, 5, 2, 6, 5,
                2, 3, 6, 3, 7, 6, 3, 0, 7, 0, 4, 7,
                4, 5, 8, 5, 6, 8, 6, 7, 8, 7, 4, 8
            }, 0);
            mesh.SetTriangles(new[] { 0, 4, 8, 1, 8, 5, 2, 6, 8, 3, 8, 7 }, 1);
            mesh.SetTriangles(new[] { 0, 3, 2, 0, 2, 1 }, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SharedMeshes.Add(cacheKey, mesh);
            return mesh;
        }

        private static void CreateGrassCover(Transform parent)
        {
            var grass = new GameObject("Grass And Ground Cover");
            grass.transform.SetParent(parent, false);
            grass.transform.position = GroundPosition(-6.4f, -3.25f);

            var clusters = new[]
            {
                new Vector3(-1.15f, 0f, -0.2f), new Vector3(-0.4f, 0f, 0.45f),
                new Vector3(0.45f, 0f, -0.32f), new Vector3(1.15f, 0f, 0.15f),
                new Vector3(0f, 0f, 1.05f), new Vector3(-1.38f, 0f, 0.95f)
            };
            for (int cluster = 0; cluster < clusters.Length; cluster++)
            {
                Vector3 center = clusters[cluster];
                for (int blade = 0; blade < 5; blade++)
                {
                    float angle = (blade * 72f + cluster * 29f) * Mathf.Deg2Rad;
                    float reach = 0.18f + (blade % 3) * 0.09f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
                    float height = 0.55f + ((blade + cluster) % 3) * 0.14f;
                    var leaf = Ico("Chunky Grass Blade", grass.transform,
                        center + offset + Vector3.up * (height * 0.34f),
                        new Vector3(0.16f, height, 0.16f),
                        new[]
                        {
                            cluster % 2 == 0 ? Emerald : Hex("6A9B4D"),
                            cluster % 2 == 0 ? Hex("3C9C69") : Hex("8BA94E"),
                        }, 0);
                    leaf.transform.localRotation = Quaternion.Euler(0f, blade * 72f, (blade % 2 == 0 ? -1f : 1f) * 13f);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) * 0.44f;
                CreateFlower(grass.transform, "Ground Cover Flower " + i,
                    new Vector3(x, 0f, (i % 2) * 0.55f - 0.28f),
                    i % 2 == 0 ? Hex("F1BE57") : Hex("E792AD"), 0.42f);
            }
        }

        private static void CreateMagicalPlant(Transform parent)
        {
            var plant = new GameObject("Magical Plant - Lantern Bloom");
            plant.transform.SetParent(parent, false);
            plant.transform.position = GroundPosition(6.8f, -3.35f);

            Ico("Plant Root", plant.transform, new Vector3(0f, 0.13f, 0f),
                new Vector3(1.05f, 0.28f, 0.95f),
                new[] { Hex("55516E"), Hex("6B5A78") }, 0);

            CylinderBetween(plant.transform, "Curving Violet Stem",
                new Vector3(0f, 0.15f, 0f), new Vector3(0.08f, 1.32f, 0f),
                0.12f, Violet);
            CylinderBetween(plant.transform, "Left Curled Stem",
                new Vector3(0f, 0.6f, 0f), new Vector3(-0.78f, 1.08f, 0.02f),
                0.075f, Hex("725099"));
            CylinderBetween(plant.transform, "Right Curled Stem",
                new Vector3(0.04f, 0.83f, 0f), new Vector3(0.82f, 1.44f, 0.04f),
                0.075f, Hex("725099"));

            CreateLeaf(plant.transform, "Lower Left Violet Leaf", new Vector3(-0.58f, 0.55f, 0f),
                new Vector3(0.9f, 0.42f, 0.22f), Hex("634286"), 27f);
            CreateLeaf(plant.transform, "Lower Right Teal Leaf", new Vector3(0.58f, 0.76f, 0f),
                new Vector3(0.95f, 0.4f, 0.22f), Hex("377E84"), -24f);
            CreateLeaf(plant.transform, "Upper Violet Leaf", new Vector3(-0.46f, 1.08f, 0.05f),
                new Vector3(0.78f, 0.38f, 0.2f), Hex("80519B"), 34f);

            CreateFlower(plant.transform, "Warm Lantern Flower",
                new Vector3(0.08f, 1.55f, -0.02f), Hex("ED9B42"), 0.82f);
            CreateFlower(plant.transform, "Small Gold Flower",
                new Vector3(0.84f, 1.42f, 0.02f), Gold, 0.52f);

            var glow = Ico("Soft Blue Magical Heart", plant.transform,
                new Vector3(0.08f, 1.57f, -0.1f), Vector3.one * 0.17f,
                new[] { Hex("8CCFE2"), Hex("B2E5EB") }, 0);
            var glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.sharedMaterial.EnableKeyword("_EMISSION");
            glowRenderer.sharedMaterial.SetColor("_EmissionColor", Hex("4D9DB5") * 0.26f);
        }

        private static void CreateLeaf(Transform parent, string name, Vector3 position, Vector3 scale,
            Color color, float angle)
        {
            var leaf = Ico(name, parent, position, scale,
                new[] { color, Color.Lerp(color, Hex("B78AC7"), 0.15f) }, 1);
            leaf.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void CreateFlower(Transform parent, string name, Vector3 position, Color color, float size)
        {
            var flower = new GameObject(name);
            flower.transform.SetParent(parent, false);
            flower.transform.localPosition = position;
            Ico("Flower Center", flower.transform, Vector3.up * size * 0.58f,
                Vector3.one * size * 0.2f, new[] { Hex("F7CF68"), Gold }, 0);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f * Mathf.Deg2Rad;
                Vector3 petal = new Vector3(Mathf.Cos(angle), 0.35f, Mathf.Sin(angle)) * size * 0.33f;
                Ico("Petal", flower.transform, Vector3.up * size * 0.5f + petal,
                    new Vector3(size * 0.3f, size * 0.2f, size * 0.24f),
                    new[] { color, Color.Lerp(color, Color.white, 0.13f) }, 0);
            }
        }

        private static void CreatePlayer(Transform parent)
        {
            var player = new GameObject("Player Character - Existing Customizer");
            player.transform.SetParent(parent, false);
            player.transform.position = GroundPosition(-1.6f, -3.7f);
            player.transform.rotation = Quaternion.Euler(0f, 28f, 0f);
            player.transform.localScale = Vector3.one * 1.18f;
            var customizer = player.AddComponent<CharacterCustomizer>();
            customizer.Apply(new Appearance
            {
                body = "broad",
                skin = "honey",
                hair = "leafcut",
                hairColor = "mossgreen",
                eyes = "teal",
                outfit = "wayfarer",
            });
        }

        private static void CreateNpc(Transform parent)
        {
            var npc = new GameObject("NPC - Tansy, Wayfinder");
            npc.transform.SetParent(parent, false);
            npc.transform.position = GroundPosition(1.45f, -3.6f);
            npc.transform.rotation = Quaternion.Euler(0f, -20f, 0f);

            Cube(npc.transform, "Indigo Boots", new Vector3(-0.16f, 0.12f, 0f),
                new Vector3(0.29f, 0.2f, 0.4f), DeepWood);
            Cube(npc.transform, "Indigo Boots Right", new Vector3(0.16f, 0.12f, 0f),
                new Vector3(0.29f, 0.2f, 0.4f), DeepWood);
            Cylinder(npc.transform, "A-line Turquoise Coat", new Vector3(0f, 0.76f, 0f),
                new Vector3(0.88f, 0.94f, 0.57f), Hex("398E86"));
            Cube(npc.transform, "Coral Scarf", new Vector3(0f, 1.13f, -0.05f),
                new Vector3(0.68f, 0.2f, 0.56f), Hex("D87669"));
            Cylinder(npc.transform, "Brass Belt", new Vector3(0f, 0.55f, 0f),
                new Vector3(0.92f, 0.12f, 0.6f), Gold);
            Cube(npc.transform, "Brass Compass", new Vector3(0.42f, 0.58f, -0.04f),
                new Vector3(0.2f, 0.25f, 0.16f), Hex("C99045"));
            Cylinder(npc.transform, "Neck", new Vector3(0f, 1.3f, 0f),
                new Vector3(0.25f, 0.26f, 0.24f), Hex("BF805F"));
            Ico("Expressive Face", npc.transform, new Vector3(0f, 1.69f, 0f),
                new Vector3(0.64f, 0.69f, 0.57f),
                new[] { Hex("DDA37B"), Hex("E7B38A") }, 1);
            Ico("Leafcut Hair", npc.transform, new Vector3(0f, 2.02f, 0.06f),
                new Vector3(0.68f, 0.28f, 0.58f),
                new[] { Hex("49334B"), Hex("63415E") }, 1);
            Ico("Hair Side Lock", npc.transform, new Vector3(-0.27f, 1.78f, -0.12f),
                new Vector3(0.2f, 0.55f, 0.18f),
                new[] { Hex("49334B"), Hex("63415E") }, 0);
            Ico("Hair Side Lock Right", npc.transform, new Vector3(0.27f, 1.78f, -0.12f),
                new Vector3(0.2f, 0.55f, 0.18f),
                new[] { Hex("49334B"), Hex("63415E") }, 0);

            CreateNpcEye(npc.transform, "Left Eye", new Vector3(-0.14f, 1.72f, -0.27f));
            CreateNpcEye(npc.transform, "Right Eye", new Vector3(0.14f, 1.72f, -0.27f));
            Ico("Friendly Smile", npc.transform, new Vector3(0f, 1.54f, -0.28f),
                new Vector3(0.16f, 0.045f, 0.035f), new[] { Hex("8E504E") }, 0);
            CylinderBetween(npc.transform, "Left Sleeve",
                new Vector3(-0.38f, 1.07f, 0f), new Vector3(-0.58f, 0.63f, -0.1f),
                0.25f, Hex("398E86"));
            CylinderBetween(npc.transform, "Right Sleeve",
                new Vector3(0.38f, 1.07f, 0f), new Vector3(0.58f, 0.63f, -0.1f),
                0.25f, Hex("398E86"));
            Ico("Satchel", npc.transform, new Vector3(-0.48f, 0.55f, 0.13f),
                new Vector3(0.38f, 0.46f, 0.32f), new[] { Hex("B7784F"), Hex("D19559") }, 1);
            Cube(npc.transform, "Satchel Strap", new Vector3(-0.22f, 0.93f, 0.2f),
                new Vector3(0.09f, 0.85f, 0.08f), Gold, Quaternion.Euler(0f, 0f, -36f));
        }

        private static void CreateNpcEye(Transform parent, string name, Vector3 position)
        {
            Ico(name + " White", parent, position, new Vector3(0.12f, 0.15f, 0.055f),
                new[] { Hex("FFF3D6") }, 0);
            Ico(name + " Iris", parent, position + new Vector3(0f, -0.005f, -0.045f),
                new Vector3(0.065f, 0.095f, 0.035f), new[] { Hex("375C65") }, 0);
        }

        private static void CreateCamera(Transform target)
        {
            var cameraObject = new GameObject("Art Direction Presentation Camera", typeof(Camera), typeof(AudioListener));
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 85f;
            camera.allowMSAA = true;
            camera.allowHDR = false;
            camera.renderingPath = RenderingPath.Forward;
            camera.depthTextureMode = DepthTextureMode.None;
            camera.backgroundColor = Hex("AFC9D3");
            camera.clearFlags = CameraClearFlags.SolidColor;
            var follow = cameraObject.AddComponent<ArtDirectionCameraFollow>();
            follow.target = target;
            follow.distance = 24f;
            follow.pitch = 43f;
            follow.yawOffset = 28f;
            follow.focusHeight = 1.35f;
            follow.lookAheadDistance = 2f;
            follow.positionSmoothTime = 0.22f;
            follow.rotationSharpness = 10f;
            follow.collisionRadius = 0.3f;
            follow.collisionPadding = 0.2f;
            follow.minimumDistance = 4f;
            follow.SnapToTarget();
        }

        private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale,
            Color color, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (rotation.HasValue) go.transform.localRotation = rotation.Value;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            RemoveCollider(go);
            return go;
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(scale.x, scale.y * 0.5f, scale.z);
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            RemoveCollider(go);
            return go;
        }

        private static void CylinderBetween(Transform parent, string name, Vector3 start, Vector3 end,
            float diameter, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            Vector3 delta = end - start;
            go.transform.localPosition = (start + end) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            go.transform.localScale = new Vector3(diameter, delta.magnitude * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            RemoveCollider(go);
        }

        private static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        private static GameObject Ico(string name, Transform parent, Vector3 position, Vector3 scale,
            Color[] colors, int subdivisions)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var filter = go.GetComponent<MeshFilter>();
            filter.sharedMesh = IcoMesh(name + " Mesh", subdivisions, colors.Length);
            var renderer = go.GetComponent<MeshRenderer>();
            var materials = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++) materials[i] = Mat(colors[i]);
            renderer.sharedMaterials = materials;
            return go;
        }

        private static Mesh IcoMesh(string name, int subdivisions, int materialCount)
        {
            string cacheKey = "Ico-" + subdivisions + "-" + materialCount;
            if (SharedMeshes.TryGetValue(cacheKey, out var cached))
                return cached;

            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
            var faces = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };

            for (int pass = 0; pass < subdivisions; pass++)
            {
                var cache = new Dictionary<long, int>();
                var refined = new List<int>(faces.Count * 4);
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i];
                    int b = faces[i + 1];
                    int c = faces[i + 2];
                    int ab = Midpoint(a, b, vertices, cache);
                    int bc = Midpoint(b, c, vertices, cache);
                    int ca = Midpoint(c, a, vertices, cache);
                    refined.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = refined;
            }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.subMeshCount = materialCount;
            for (int material = 0; material < materialCount; material++)
            {
                var indices = new List<int>();
                for (int face = 0; face < faces.Count / 3; face++)
                    if (face % materialCount == material)
                    {
                        indices.Add(faces[face * 3]);
                        indices.Add(faces[face * 3 + 1]);
                        indices.Add(faces[face * 3 + 2]);
                    }
                mesh.SetTriangles(indices, material);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SharedMeshes.Add(cacheKey, mesh);
            return mesh;
        }

        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            uint low = (uint)Mathf.Min(a, b);
            uint high = (uint)Mathf.Max(a, b);
            long key = ((long)low << 32) | high;
            if (cache.TryGetValue(key, out int index)) return index;
            index = vertices.Count;
            vertices.Add((vertices[a] + vertices[b]).normalized);
            cache.Add(key, index);
            return index;
        }

        private static Material Mat(Color color)
        {
            if (SceneMaterials.TryGetValue(color, out var existing))
                return existing;

            var material = new Material(Shader.Find("Standard"));
            material.name = "Art Test - " + ColorUtility.ToHtmlStringRGB(color);
            material.color = color;
            material.SetFloat("_Glossiness", 0.24f);
            SceneMaterials.Add(color, material);
            return material;
        }

        private static Vector3 GroundPosition(float x, float z)
        {
            return new Vector3(x, GroundHeight(x, z), z);
        }

        private static float GroundHeight(float x, float z)
        {
            float rolling = 0.14f * Mathf.Sin(x * 0.43f) * Mathf.Cos(z * 0.36f)
                + 0.08f * Mathf.Sin((x + z) * 0.61f);
            float distantMound = 0.52f * Mathf.Exp(-((x - 9.2f) * (x - 9.2f) + (z - 6.8f) * (z - 6.8f)) / 8f);
            float rearRise = 0.28f * Mathf.Exp(-(z - 8.3f) * (z - 8.3f) / 7f);
            return rolling + distantMound + rearRise;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetSceneInBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.path != path) scenes.Add(scene);
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void Validate(Scene scene)
        {
            var root = FindRoot(scene, "Eight Core Style Tests");
            Require(root != null, "The core art-test root is missing.");
            string[] required =
            {
                "Player Character - Existing Customizer",
                "NPC - Tansy, Wayfinder",
                "House - Crooked Lantern Cottage",
                "Tree - Turquoise Crown",
                "Rock - Faceted Slate",
                "Resource Node - Moonstone Ore",
                "Grass And Ground Cover",
                "Magical Plant - Lantern Bloom",
                "Clearing Terrain"
            };
            foreach (string name in required)
                Require(root.transform.Find(name) != null, "Missing required style test: " + name);

            Require(root.transform.childCount == 9, "Expected eight core objects and one terrain support object.");
            int coreCount = 0;
            foreach (Transform child in root.transform)
            {
                if (child.name == "Clearing Terrain") continue;
                coreCount++;
                Require(child.GetComponentsInChildren<Renderer>(true).Length > 0,
                    "Core visual has no visible renderer: " + child.name);
            }
            Require(coreCount == 8, "Expected exactly eight core visual tests, found " + coreCount + ".");
            var player = root.transform.Find("Player Character - Existing Customizer");
            Require(player.GetComponent<CharacterCustomizer>() != null, "Existing CharacterCustomizer is missing from the player visual.");
            var terrain = root.transform.Find("Clearing Terrain/Soft Rolling Ground");
            Require(terrain != null && terrain.GetComponent<MeshCollider>() != null,
                "Rolling clearing mesh or its ground collider is missing.");
            Require(FindRoot(scene, "Art Direction Presentation Camera") != null, "Presentation camera is missing.");
            Require(FindRoot(scene, "Late Afternoon Sun") != null, "Warm directional lighting is missing.");
            var cameraObject = FindRoot(scene, "Art Direction Presentation Camera");
            var camera = cameraObject.GetComponent<Camera>();
            var follow = cameraObject.GetComponent<ArtDirectionCameraFollow>();
            Require(camera != null && camera.allowMSAA && !camera.allowHDR
                && camera.nearClipPlane >= 0.1f && camera.farClipPlane <= 100f,
                "Presentation camera clarity or clipping settings are invalid.");
            Require(follow != null && follow.target == player && follow.pitch >= 35f && follow.pitch <= 50f
                && follow.distance >= 18f && follow.distance <= 32f,
                "Presentation follow-camera setup is invalid.");
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException("[Wispmere] Art scene validation failed: " + message);
        }
    }
}
