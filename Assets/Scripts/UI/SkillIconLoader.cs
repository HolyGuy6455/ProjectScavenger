using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SkillIconLoader
{
    private static readonly Dictionary<string, Dictionary<string, Sprite>> SheetCache =
        new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        SheetCache.Clear();
    }

    // A plain key loads one sprite; "sheet#spriteName" selects a named slice.
    public static Sprite Load(string resourceIcon)
    {
        if (string.IsNullOrWhiteSpace(resourceIcon)) return null;

        int separator = resourceIcon.IndexOf('#');
        string resourcePath = separator >= 0 ? resourceIcon.Substring(0, separator) : resourceIcon;
        resourcePath = Path.ChangeExtension(resourcePath.Trim().Replace('\\', '/'), null);
        if (resourcePath.StartsWith("skill/", StringComparison.Ordinal))
        {
            resourcePath = resourcePath.Substring("skill/".Length);
        }
        if (string.IsNullOrEmpty(resourcePath)) return null;

        if (separator < 0)
        {
            Sprite sprite = Resources.Load<Sprite>($"skill/{resourcePath}");
            return sprite != null ? sprite : Resources.Load<Sprite>(resourcePath);
        }

        string spriteName = resourceIcon.Substring(separator + 1).Trim();
        if (string.IsNullOrEmpty(spriteName)) return null;

        if (!SheetCache.TryGetValue(resourcePath, out var spritesByName))
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>($"skill/{resourcePath}");
            if (sprites.Length == 0) sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites.Length == 0) return null;

            spritesByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (Sprite sprite in sprites)
            {
                if (sprite != null) spritesByName[sprite.name] = sprite;
            }
            SheetCache[resourcePath] = spritesByName;
        }

        return spritesByName.TryGetValue(spriteName, out var result) ? result : null;
    }
}
