using System;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// One customization option. Colors double as creator swatches; 3D art
    /// overrides live in CharacterArtSet (prefabs/materials per id).
    /// To add an option, append one line to the matching list below.
    /// </summary>
    [Serializable]
    public struct AppearanceOption
    {
        public string id;
        public string label;
        public Color color;
        public bool hasColor;

        public AppearanceOption(string id, string label)
        {
            this.id = id; this.label = label;
            this.color = Color.white; this.hasColor = false;
        }

        public AppearanceOption(string id, string label, Color color)
        {
            this.id = id; this.label = label;
            this.color = color; this.hasColor = true;
        }
    }

    public static class AppearanceLibrary
    {
        public static readonly AppearanceOption[] Bodies =
        {
            new AppearanceOption("slim", "Slim"),
            new AppearanceOption("round", "Round"),
            new AppearanceOption("broad", "Broad"),
        };

        public static readonly AppearanceOption[] Skins =
        {
            new AppearanceOption("porcelain", "Porcelain", new Color(0.97f, 0.85f, 0.69f)),
            new AppearanceOption("honey", "Honey", new Color(0.91f, 0.69f, 0.49f)),
            new AppearanceOption("amber", "Amber", new Color(0.73f, 0.48f, 0.27f)),
            new AppearanceOption("umber", "Umber", new Color(0.48f, 0.29f, 0.16f)),
            new AppearanceOption("moss", "Mossborn", new Color(0.66f, 0.78f, 0.60f)),
        };

        public static readonly AppearanceOption[] HairStyles =
        {
            new AppearanceOption("leafcut", "Leafcut"),
            new AppearanceOption("bob", "Bob"),
            new AppearanceOption("curls", "Curls"),
            new AppearanceOption("pony", "Ponytail"),
            new AppearanceOption("tuft", "Tuft"),
        };

        public static readonly AppearanceOption[] HairColors =
        {
            new AppearanceOption("mossgreen", "Moss", new Color(0.31f, 0.54f, 0.29f)),
            new AppearanceOption("ember", "Ember", new Color(0.79f, 0.33f, 0.18f)),
            new AppearanceOption("lilac", "Lilac", new Color(0.60f, 0.48f, 0.89f)),
            new AppearanceOption("inkberry", "Inkberry", new Color(0.17f, 0.14f, 0.25f)),
            new AppearanceOption("goldcap", "Goldcap", new Color(0.91f, 0.72f, 0.23f)),
        };

        public static readonly AppearanceOption[] EyeColors =
        {
            new AppearanceOption("teal", "Teal", new Color(0.12f, 0.48f, 0.59f)),
            new AppearanceOption("amber", "Amber", new Color(0.54f, 0.35f, 0.10f)),
            new AppearanceOption("violet", "Violet", new Color(0.42f, 0.29f, 0.89f)),
            new AppearanceOption("moss", "Moss", new Color(0.18f, 0.43f, 0.24f)),
        };

        public static readonly AppearanceOption[] Outfits =
        {
            new AppearanceOption("wayfarer", "Wayfarer", new Color(0.29f, 0.56f, 0.85f)),
            new AppearanceOption("hedgewitch", "Hedgewitch", new Color(0.43f, 0.29f, 0.62f)),
            new AppearanceOption("orchard", "Orchard", new Color(0.79f, 0.33f, 0.18f)),
            new AppearanceOption("pondskipper", "Pondskipper", new Color(0.18f, 0.62f, 0.54f)),
        };

        public static AppearanceOption Find(AppearanceOption[] list, string id)
        {
            for (int i = 0; i < list.Length; i++)
                if (list[i].id == id) return list[i];
            return list[0];
        }

        public static int IndexOf(AppearanceOption[] list, string id)
        {
            for (int i = 0; i < list.Length; i++)
                if (list[i].id == id) return i;
            return 0;
        }
    }
}
