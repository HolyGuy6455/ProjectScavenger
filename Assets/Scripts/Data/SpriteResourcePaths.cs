using System.IO;

/// <summary>
/// Converts legacy table image names to Resources-relative paths at the data boundary.
/// UI code and SpriteManager can then use the stored path without knowing its category.
/// </summary>
public static class SpriteResourcePaths
{
    public static void Normalize(IData data)
    {
        switch (data)
        {
            case ItemData item:
                item.uiResource = NormalizePath(item.uiResource, "Items");
                break;
            case CharData character:
                character.resourceSlot = NormalizePath(character.resourceSlot, "char");
                character.resourceStand = NormalizePath(character.resourceStand, "char");
                character.resourceSilhouette = NormalizePath(character.resourceSilhouette, "char");
                break;
            case SkillData skill:
                skill.resourceIcon = NormalizePath(skill.resourceIcon, "skill");
                skill.resourceIcon2 = NormalizePath(skill.resourceIcon2, "skill");
                break;
            case UserCurrencyData currency:
                currency.ui_resources = NormalizePath(currency.ui_resources, "UserCurrency");
                break;
        }
    }

    private static string NormalizePath(string value, string legacyFolder)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "_") return string.Empty;

        value = value.Trim().Replace('\\', '/');
        int separator = value.IndexOf('#');
        string path = separator >= 0 ? value.Substring(0, separator).Trim() : value;
        string slice = separator >= 0 ? "#" + value.Substring(separator + 1).Trim() : string.Empty;
        path = Path.ChangeExtension(path, null);
        if (string.IsNullOrEmpty(path)) return string.Empty;

        // An explicit folder is authoritative; only old basename-only values need migration.
        if (!path.Contains("/")) path = legacyFolder + "/" + path;
        return path + slice;
    }
}
