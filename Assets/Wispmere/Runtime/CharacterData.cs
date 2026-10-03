using System;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// The player's chosen appearance. String ids on purpose: new hairstyles,
    /// outfits, etc. are data (AppearanceLibrary / CharacterArtSet), never
    /// code changes. Mirrors the web save's `appearance` object.
    /// </summary>
    [Serializable]
    public struct Appearance
    {
        public string body;
        public string skin;
        public string hair;
        public string hairColor;
        public string eyes;
        public string outfit;

        public static Appearance Default()
        {
            return new Appearance
            {
                body = "slim",
                skin = "honey",
                hair = "leafcut",
                hairColor = "mossgreen",
                eyes = "teal",
                outfit = "wayfarer",
            };
        }
    }
}
