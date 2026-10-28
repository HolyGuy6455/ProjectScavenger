using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Checks real sprite paths, sheet lookup, and scene cache lifetimes without changing assets.</summary>
public static class SpriteManagerRegression
{
    private const string ItemPath = "Items/item_expBook1";
    private const string ReloadPath = "skill/char01_skills#reload";
    private const string RollPath = "skill/char01_skills#roll";

    [MenuItem("Tools/UI/Validate Sprite Manager (Edit Mode)")]
    private static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Run sprite manager validation in Edit Mode so live UI caches are not changed.");

        var results = new List<string>();
        GameObject imageObject = null;
        try
        {
            SpriteManager manager = SpriteManager.Instance;
            Check(object.ReferenceEquals(manager, SpriteManager.Instance), "All callers must share the same sprite manager instance.");
            manager.ClearSceneCache();

            Sprite item = manager.LoadSprite(ItemPath);
            Check(item != null, "Items/item_expBook1 must resolve to the training cost icon.");
            Check(SpriteManager.Instance.LoadSprite(ItemPath) == item,
                "Repeated requests must return the same item sprite.");
            results.Add("PASS explicit item path resolves to the training cost icon");

            Sprite reload = SpriteManager.Instance.LoadSprite(ReloadPath);
            Sprite roll = SpriteManager.Instance.LoadSprite(RollPath);
            Check(reload != null && roll != null && reload != roll
                && reload.name == "reload" && roll.name == "roll"
                && reload.texture == roll.texture,
                "Named reload and roll icons must be distinct sprites from the same sheet.");
            Check(SpriteManager.Instance.LoadSprite(ReloadPath) == reload,
                "Repeated requests must return the same named slice.");
            results.Add("PASS sheet#name selects the correct icon");

            imageObject = new GameObject("Sprite manager validation (temporary)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            imageObject.SetActive(false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = item;
            SpriteManager.Instance.ClearSceneCache();
            Check(image.sprite == item && item != null && item.texture != null,
                "Clearing the manager cache must not destroy a sprite still referenced by an inactive Image.");
            Check(SpriteManager.Instance.LoadSprite(ItemPath) == item && SpriteManager.Instance.LoadSprite(ReloadPath) == reload,
                "Sprites still in use must remain loadable after scene cleanup.");
            results.Add("PASS scene cleanup preserves sprites referenced by inactive UI");

            CheckMissingAndEmptyPaths(results);
            CheckDataBoundary(results);
            return "[SpriteManagerRegression]\n" + string.Join("\n", results);
        }
        finally
        {
            if (imageObject != null) Object.DestroyImmediate(imageObject);
            SpriteManager.Instance.ClearSceneCache();
        }
    }

    private static void CheckMissingAndEmptyPaths(List<string> results)
    {
        SpriteManager.Instance.ClearSceneCache();
        var messages = new List<string>();
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception)
                messages.Add(message);
        };
        Application.logMessageReceived += capture;
        try
        {
            Check(SpriteManager.Instance.LoadSprite(null) == null && SpriteManager.Instance.LoadSprite("") == null
                && SpriteManager.Instance.LoadSprite("  ") == null && SpriteManager.Instance.LoadSprite("_") == null
                && messages.Count == 0,
                "Empty paths must return null without logging a warning.");
            Check(SpriteManager.Instance.LoadSprite("item_expBook1") == null,
                "A missing folder prefix must fail rather than silently searching Items/.");
            Check(messages.Count == 1 && messages[0].Contains("item_expBook1"),
                "The first missing resource must produce one warning identifying its path.");
            Check(SpriteManager.Instance.LoadSprite("item_expBook1") == null && messages.Count == 1,
                "Repeated requests for the same missing resource must not duplicate warnings.");
            SpriteManager.Instance.ClearSceneCache();
            Check(SpriteManager.Instance.LoadSprite("item_expBook1") == null && messages.Count == 2,
                "Scene cleanup must reset missing-resource diagnostics for a fresh session.");
            Check(SpriteManager.Instance.LoadSprite("#reload") == null
                && SpriteManager.Instance.LoadSprite("skill/char01_skills#") == null,
                "A sheet request must specify both a resource path and a slice name.");
        }
        finally
        {
            Application.logMessageReceived -= capture;
        }
        results.Add("PASS missing prefixes fail explicitly, warnings are deduplicated, and empty paths stay silent");
    }

    private static void CheckDataBoundary(List<string> results)
    {
        var item = new ItemData { uiResource = "  item_expBook1.png  " };
        var character = new CharData
        {
            resourceSlot = "char_001_slot.png",
            resourceStand = "char\\char_001_stand.png",
            resourceSilhouette = "_"
        };
        var skill = new SkillData
        {
            resourceIcon = "char01_skills.png#reload",
            resourceIcon2 = "skill/char01_skills#roll"
        };
        var currency = new UserCurrencyData { ui_resources = "user_money.png" };
        foreach (IData data in new IData[] { item, character, skill, currency })
        {
            SpriteResourcePaths.Normalize(data);
            SpriteResourcePaths.Normalize(data);
        }
        Check(item.uiResource == ItemPath && SpriteManager.Instance.LoadSprite(item.uiResource) != null,
            "Legacy item data must normalize idempotently to a path that the manager can load directly.");
        Check(character.resourceSlot == "char/char_001_slot"
            && character.resourceStand == "char/char_001_stand" && character.resourceSilhouette == "",
            "Character image fields must gain one prefix while absent silhouette images remain empty.");
        Check(skill.resourceIcon == ReloadPath && skill.resourceIcon2 == RollPath
            && SpriteManager.Instance.LoadSprite(skill.resourceIcon)?.name == "reload",
            "Legacy skill data must preserve its named slice and resolve through the same manager.");
        Check(currency.ui_resources == "UserCurrency/user_money",
            "Currency data must expose a complete Resources-relative path.");
        item.uiResource = "alternate/custom.png";
        SpriteResourcePaths.Normalize(item);
        Check(item.uiResource == "alternate/custom", "An explicit data folder must remain authoritative.");
        results.Add("PASS legacy and prefixed data normalize idempotently before direct sprite loading");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Sprite manager regression failed: " + message);
    }
}
