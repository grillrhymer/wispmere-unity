#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Wispmere.Editor
{
    /// <summary>One-click helpers: build the town, mint art-set assets.</summary>
    public static class TownBuilderEditor
    {
        [MenuItem("Wispmere/Build Town From Json")]
        public static void BuildTown()
        {
            var builder = Object.FindObjectOfType<TownBuilder>();
            if (builder == null)
            {
                Debug.LogError("[Wispmere] No TownBuilder in the scene. See SCENE_SETUP.md step 1.");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Build Wispmere town");
            builder.Build();
            Debug.Log("[Wispmere] Town built. Swap placeholders via a Town Art Set.");
        }

        [MenuItem("Wispmere/Create/Town Art Set")]
        public static void CreateTownArtSet()
        {
            CreateAsset<TownArtSet>("TownArtSet");
        }

        [MenuItem("Wispmere/Create/Character Art Set")]
        public static void CreateCharacterArtSet()
        {
            CreateAsset<CharacterArtSet>("CharacterArtSet");
        }

        private static void CreateAsset<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            string path = "Assets/" + name + ".asset";
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[Wispmere] Created " + path + " — drag it onto the matching slot.");
        }
    }
}
#endif
