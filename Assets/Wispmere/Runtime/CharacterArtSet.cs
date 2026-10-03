using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// 3D art overrides for the character. Create via
    /// Right-click → Create → Wispmere → Character Art Set, then drag models /
    /// materials into rows and assign to CharacterCustomizer. Any empty row
    /// falls back to the procedural placeholder rig — the game always runs.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterArtSet", menuName = "Wispmere/Character Art Set")]
    public class CharacterArtSet : ScriptableObject
    {
        [Serializable]
        public struct HairEntry { public string styleId; public GameObject prefab; }

        [Serializable]
        public struct MaterialEntry { public string id; public Material material; }

        [Header("One prefab per hair style id (leafcut, bob, curls, pony, tuft).")]
        [Header("Each prefab is built to fit the HairAnchor (head-top origin).")]
        public List<HairEntry> hairPrefabs = new List<HairEntry>();

        [Header("Skin materials per skin id (overrides the swatch color).")]
        public List<MaterialEntry> skinMaterials = new List<MaterialEntry>();

        [Header("Tunic materials per outfit id (overrides the swatch color).")]
        public List<MaterialEntry> outfitMaterials = new List<MaterialEntry>();

        [Header("Eye materials per eye id (overrides the swatch color).")]
        public List<MaterialEntry> eyeMaterials = new List<MaterialEntry>();

        public GameObject FindHair(string styleId)
        {
            for (int i = 0; i < hairPrefabs.Count; i++)
                if (hairPrefabs[i].styleId == styleId) return hairPrefabs[i].prefab;
            return null;
        }

        public Material FindMaterial(List<MaterialEntry> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id) return list[i].material;
            return null;
        }
    }
}
