using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads extensionless Resources paths, including "folder/sheet#slice".
/// Cached sprites stay available until scene cleanup, including while panels are inactive.
/// </summary>
public sealed class SpriteManager
{
    public static SpriteManager Instance { get; } = new SpriteManager();

    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Dictionary<string, Sprite>> sheetCache =
        new Dictionary<string, Dictionary<string, Sprite>>();
    private readonly HashSet<string> reportedFailures = new HashSet<string>();

    private SpriteManager() { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => Instance.ClearSceneCache();

    public Sprite LoadSprite(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "_") return null;

        Sprite sprite = null;
        int separator = path.IndexOf('#');
        if (separator < 0)
        {
            if (!spriteCache.TryGetValue(path, out sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                spriteCache[path] = sprite;
            }
        }
        else if (separator > 0 && separator < path.Length - 1)
        {
            string sheetPath = path.Substring(0, separator);
            string sliceName = path.Substring(separator + 1);
            if (!sheetCache.TryGetValue(sheetPath, out var sprites))
            {
                sprites = new Dictionary<string, Sprite>();
                foreach (var entry in Resources.LoadAll<Sprite>(sheetPath))
                {
                    sprites[entry.name] = entry;
                }
                sheetCache[sheetPath] = sprites;
            }
            sprites.TryGetValue(sliceName, out sprite);
        }

        if (sprite == null && reportedFailures.Add(path))
        {
            Debug.LogWarning($"[SpriteManager] Sprite not found at Resources/{path}.");
        }
        return sprite;
    }

    /// <summary>
    /// Drops cached references after leaving a scene, before UnloadUnusedAssets.
    /// Does not destroy sprites still used by UI. Never call when merely hiding a panel.
    /// </summary>
    public void ClearSceneCache()
    {
        spriteCache.Clear();
        sheetCache.Clear();
        reportedFailures.Clear();
    }
}
