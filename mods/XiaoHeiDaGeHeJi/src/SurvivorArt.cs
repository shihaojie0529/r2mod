using System.IO;
using UnityEngine;

namespace RiftSurvivor
{
    internal static class SurvivorArt
    {
        internal static Texture2D Portrait { get; } = LoadPortrait();

        private static Texture2D LoadPortrait()
        {
            using (var input = typeof(SurvivorArt).Assembly.GetManifestResourceStream("RiftSurvivor.portrait.png"))
            using (var buffer = new MemoryStream())
            {
                input.CopyTo(buffer);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, buffer.ToArray()))
                    throw new InvalidDataException("Unable to decode the survivor portrait.");
                texture.name = "RiftRunnerPortrait";
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }
        }
    }
}
