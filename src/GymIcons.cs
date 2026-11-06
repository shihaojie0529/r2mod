using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RiftSurvivor
{
    internal static class GymIcons
    {
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        internal static Sprite Get(string name)
        {
            if (icons.TryGetValue(name, out var icon)) return icon;
            using (var stream = typeof(GymIcons).Assembly.GetManifestResourceStream("RiftSurvivor.icons." + name + ".png"))
            using (var memory = new MemoryStream())
            {
                if (stream == null) throw new FileNotFoundException("Missing skill icon", name);
                stream.CopyTo(memory);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, memory.ToArray())) throw new InvalidDataException(name);
                texture.wrapMode = TextureWrapMode.Clamp;
                icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                if (name.StartsWith("wei-", StringComparison.Ordinal)) icon.name = "WeiGe_" + name.Substring(4);
                else if (name.StartsWith("jie-", StringComparison.Ordinal)) icon.name = "JieGe_" + name.Substring(4);
                else if (name.StartsWith("xh-", StringComparison.Ordinal)) icon.name = "Xiaohei_" + name.Substring(3);
                else icon.name = "CuiGe_" + name;
                icons.Add(name, icon);
                return icon;
            }
        }
    }
}
