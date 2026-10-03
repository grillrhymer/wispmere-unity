using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// Builds the traveler from primitives under named anchors
    /// (HairAnchor, HeadMesh, TorsoMesh) and applies an Appearance.
    /// Assign a CharacterArtSet to swap any part for real 3D art —
    /// anchors keep their names so props/hats survive the swap.
    /// Used by the creator preview AND the in-world player.
    /// </summary>
    [ExecuteInEditMode]
    public class CharacterCustomizer : MonoBehaviour
    {
        [Header("Optional: 3D overrides. Empty = procedural placeholders.")]
        public CharacterArtSet artSet;

        public Transform HairAnchor { get; private set; }
        public Transform HeadMesh { get; private set; }
        public Transform TorsoMesh { get; private set; }

        private Material _skinMat;
        private Material _hairMat;
        private Material _eyeMat;
        private Material _outfitMat;
        private Material _trimMat;
        private Material _bootMat;
        private Renderer[] _skinRenderers;
        private Renderer[] _eyeRenderers;
        private Renderer _outfitRenderer;

        private Appearance _current;

        private void Awake()
        {
            Rebuild();
        }

        public void Apply(Appearance appearance)
        {
            _current = appearance;
            if (HairAnchor == null) Rebuild();
            ApplyColors(appearance);
            RebuildHair(appearance);
        }

        private static Material PlainMat(Color c)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Standard");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[Wispmere] No supported URP/Lit or Standard shader is available for the character placeholder.");
                return null;
            }

            var m = new Material(shader);
            m.color = c;
            return m;
        }

        private static GameObject Part(string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return go;
        }

        public void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(transform.GetChild(i).gameObject);
                else Destroy(transform.GetChild(i).gameObject);
#else
                Destroy(transform.GetChild(i).gameObject);
#endif
            }

            _skinMat = PlainMat(Color.white);
            _hairMat = PlainMat(Color.white);
            _eyeMat = PlainMat(Color.black);
            _outfitMat = PlainMat(Color.blue);
            _trimMat = PlainMat(Color.yellow);
            _bootMat = PlainMat(new Color(0.23f, 0.17f, 0.12f));

            var rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);

            Part("BootL", PrimitiveType.Cube, new Vector3(-0.11f, 0.07f, 0f), new Vector3(0.16f, 0.14f, 0.2f), _bootMat, rig);
            Part("BootR", PrimitiveType.Cube, new Vector3(0.11f, 0.07f, 0f), new Vector3(0.16f, 0.14f, 0.2f), _bootMat, rig);
            var torso = Part("TorsoMesh", PrimitiveType.Cube, new Vector3(0f, 0.48f, 0f), new Vector3(0.5f, 0.5f, 0.3f), _outfitMat, rig);
            TorsoMesh = torso.transform;
            _outfitRenderer = torso.GetComponent<Renderer>();
            Part("Trim", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(0.52f, 0.07f, 0.32f), _trimMat, rig);
            var armL = Part("ArmL", PrimitiveType.Cube, new Vector3(-0.33f, 0.5f, 0f), new Vector3(0.13f, 0.4f, 0.16f), _skinMat, rig);
            var armR = Part("ArmR", PrimitiveType.Cube, new Vector3(0.33f, 0.5f, 0f), new Vector3(0.13f, 0.4f, 0.16f), _skinMat, rig);
            var head = Part("HeadMesh", PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0f), new Vector3(0.42f, 0.42f, 0.4f), _skinMat, rig);
            HeadMesh = head.transform;
            _skinRenderers = new[]
            {
                armL.GetComponent<Renderer>(),
                armR.GetComponent<Renderer>(),
                head.GetComponent<Renderer>(),
            };
            var eyeL = Part("EyeL", PrimitiveType.Sphere, new Vector3(-0.1f, 0.95f, 0.19f), new Vector3(0.07f, 0.09f, 0.04f), _eyeMat, rig);
            var eyeR = Part("EyeR", PrimitiveType.Sphere, new Vector3(0.1f, 0.95f, 0.19f), new Vector3(0.07f, 0.09f, 0.04f), _eyeMat, rig);
            _eyeRenderers = new[] { eyeL.GetComponent<Renderer>(), eyeR.GetComponent<Renderer>() };

            var anchor = new GameObject("HairAnchor").transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = new Vector3(0f, 1.12f, 0f);
            HairAnchor = anchor;

            Apply(_current.body == null ? Appearance.Default() : _current);
        }

        private void ApplyColors(Appearance a)
        {
            var skin = AppearanceLibrary.Find(AppearanceLibrary.Skins, a.skin);
            var hair = AppearanceLibrary.Find(AppearanceLibrary.HairColors, a.hairColor);
            var eyes = AppearanceLibrary.Find(AppearanceLibrary.EyeColors, a.eyes);
            var outfit = AppearanceLibrary.Find(AppearanceLibrary.Outfits, a.outfit);

            if (_skinMat != null) _skinMat.color = skin.color;
            _hairMat.color = hair.color;
            if (_eyeMat != null) _eyeMat.color = eyes.color;
            if (_outfitMat != null) _outfitMat.color = outfit.color;

            Material skinMaterial = artSet != null ? artSet.FindMaterial(artSet.skinMaterials, a.skin) : null;
            Material outfitMaterial = artSet != null ? artSet.FindMaterial(artSet.outfitMaterials, a.outfit) : null;
            Material eyeMaterial = artSet != null ? artSet.FindMaterial(artSet.eyeMaterials, a.eyes) : null;
            AssignMaterial(_skinRenderers, skinMaterial != null ? skinMaterial : _skinMat);
            AssignMaterial(_eyeRenderers, eyeMaterial != null ? eyeMaterial : _eyeMat);
            if (_outfitRenderer != null)
                _outfitRenderer.sharedMaterial = outfitMaterial != null ? outfitMaterial : _outfitMat;

            float torsoW = a.body == "broad" ? 0.68f : a.body == "round" ? 0.58f : 0.5f;
            if (TorsoMesh != null) TorsoMesh.localScale = new Vector3(torsoW, 0.5f, 0.3f);
        }

        private static void AssignMaterial(Renderer[] renderers, Material material)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer != null) renderer.sharedMaterial = material;
        }

        private void RebuildHair(Appearance a)
        {
            for (int i = HairAnchor.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(HairAnchor.GetChild(i).gameObject);
                else Destroy(HairAnchor.GetChild(i).gameObject);
#else
                Destroy(HairAnchor.GetChild(i).gameObject);
#endif
            }

            if (artSet != null)
            {
                var prefab = artSet.FindHair(a.hair);
                if (prefab != null)
                {
#if UNITY_EDITOR
                    var inst = Application.isPlaying ? Instantiate(prefab, HairAnchor) : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, HairAnchor);
#else
                    var inst = Instantiate(prefab, HairAnchor);
#endif
                    inst.transform.localPosition = Vector3.zero;
                    return;
                }
            }

            switch (a.hair)
            {
                case "bob":
                    Part("Hair", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.5f, 0.42f, 0.46f), _hairMat, HairAnchor);
                    Part("HairL", PrimitiveType.Cube, new Vector3(-0.22f, -0.14f, 0f), new Vector3(0.1f, 0.3f, 0.34f), _hairMat, HairAnchor);
                    Part("HairR", PrimitiveType.Cube, new Vector3(0.22f, -0.14f, 0f), new Vector3(0.1f, 0.3f, 0.34f), _hairMat, HairAnchor);
                    break;
                case "curls":
                    for (int i = -2; i <= 2; i++)
                        Part("Curl", PrimitiveType.Sphere, new Vector3(i * 0.13f, (i % 2 == 0 ? 0.02f : -0.04f), 0f), Vector3.one * 0.2f, _hairMat, HairAnchor);
                    break;
                case "pony":
                    Part("Hair", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.46f, 0.36f, 0.42f), _hairMat, HairAnchor);
                    Part("Tail", PrimitiveType.Cube, new Vector3(0.2f, -0.28f, -0.16f), new Vector3(0.12f, 0.44f, 0.12f), _hairMat, HairAnchor);
                    break;
                case "tuft":
                    Part("Hair", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.44f, 0.32f, 0.4f), _hairMat, HairAnchor);
                    for (int i = -2; i <= 2; i++)
                        Part("Spike", PrimitiveType.Cube, new Vector3(i * 0.09f, 0.28f - Mathf.Abs(i) * 0.04f, 0f), new Vector3(0.07f, 0.2f, 0.07f), _hairMat, HairAnchor);
                    break;
                default: // leafcut
                    Part("Hair", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.48f, 0.4f, 0.44f), _hairMat, HairAnchor);
                    Part("Sprout", PrimitiveType.Cube, Vector3.up * 0.3f, new Vector3(0.08f, 0.24f, 0.08f), _hairMat, HairAnchor);
                    break;
            }
        }
    }
}
