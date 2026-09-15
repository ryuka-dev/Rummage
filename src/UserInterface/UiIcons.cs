using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>Small images embedded in the plugin assembly, loaded once into sprites.</summary>
    internal static class UiIcons
    {
        private const string AppliedOilsGunResource = "Ryuka.Sulfur.Rummage.UserInterface.Icons.AppliedOilsGun.png";

        private static Sprite appliedOilsGun;
        private static bool appliedOilsGunLoaded;

        /// <summary>The gun shown on the applied-oils checkbox, or null when it could not be loaded.</summary>
        public static Sprite AppliedOilsGun
        {
            get
            {
                if (!appliedOilsGunLoaded)
                {
                    appliedOilsGunLoaded = true;
                    appliedOilsGun = Load(AppliedOilsGunResource, "the applied-oils checkbox shows without its gun icon");
                }

                return appliedOilsGun;
            }
        }

        /// <summary>
        /// Decodes PNG bytes with UnityEngine.ImageConversion.LoadImage, called by reflection. Unity 6
        /// builds that module against netstandard 2.1, which a net472 project cannot reference
        /// without dragging in the netstandard 2.1 facades; the game always has the module loaded.
        /// </summary>
        private static bool LoadImage(Texture2D texture, byte[] bytes)
        {
            Type imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            MethodInfo loadImage = imageConversion != null
                ? imageConversion.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D), typeof(byte[]) }, null)
                : null;

            if (loadImage == null)
            {
                RummagePlugin.Log.LogWarning("UnityEngine.ImageConversion.LoadImage(Texture2D, byte[]) was not found; embedded icons cannot be decoded.");
                return false;
            }

            object decoded = loadImage.Invoke(null, new object[] { texture, bytes });
            return decoded is bool && (bool)decoded;
        }

        private static Sprite Load(string resource, string consequence)
        {
            try
            {
                byte[] bytes;
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        RummagePlugin.Log.LogWarning("Embedded image " + resource + " is missing from this build; " + consequence + ".");
                        return null;
                    }

                    using (MemoryStream buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        bytes = buffer.ToArray();
                    }
                }

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!LoadImage(texture, bytes))
                {
                    RummagePlugin.Log.LogWarning("Embedded image " + resource + " could not be decoded; " + consequence + ".");
                    return null;
                }

                texture.name = resource;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = resource;
                return sprite;
            }
            catch (Exception ex)
            {
                RummagePlugin.Log.LogWarning("Loading embedded image " + resource + " failed; " + consequence + ". " + ex.Message);
                return null;
            }
        }
    }
}
