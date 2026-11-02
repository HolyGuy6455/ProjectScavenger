using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class LocalizationEditorUtility
{
    public class MasterRow
    {
        public string key = string.Empty;
        public string sourceType = string.Empty;
        public string location = string.Empty;
        public string memo = string.Empty;
        public string ko = string.Empty;
        public string en = string.Empty;
        public string ja = string.Empty;
    }

    public const string DefaultHeader = "key\t#sourceType\t#location\t#memo\tko\ten\tja";

    public static string GetMasterTsvPath()
    {
        return LocalizationManager.GetMasterTsvFullPath();
    }

    public static List<MasterRow> LoadMasterRows()
    {
        var result = new List<MasterRow>();
        string path = GetMasterTsvPath();
        if (!File.Exists(path))
        {
            return result;
        }

        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2) return result;

        string[] headers = lines[0].Split('\t');
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            colMap[headers[i].Trim().TrimStart('\uFEFF')] = i;
        }

        string GetCell(string[] cols, string name)
        {
            if (colMap.TryGetValue(name, out int idx) && idx < cols.Length)
            {
                return cols[idx].Trim();
            }
            return string.Empty;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cols = lines[i].Split('\t');
            string key = GetCell(cols, "key");
            if (string.IsNullOrEmpty(key)) continue;

            result.Add(new MasterRow
            {
                key = key,
                sourceType = GetCell(cols, "#sourceType"),
                location = GetCell(cols, "#location"),
                memo = GetCell(cols, "#memo"),
                ko = GetCell(cols, "ko"),
                en = GetCell(cols, "en"),
                ja = GetCell(cols, "ja")
            });
        }

        return result;
    }

    public static void SaveMasterRows(List<MasterRow> rows)
    {
        string path = GetMasterTsvPath();
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var sb = new StringBuilder();
        sb.AppendLine(DefaultHeader);
        foreach (var r in rows)
        {
            if (r == null || string.IsNullOrEmpty(r.key)) continue;
            sb.Append(SanitizeCell(r.key)).Append('\t')
              .Append(SanitizeCell(r.sourceType)).Append('\t')
              .Append(SanitizeCell(r.location)).Append('\t')
              .Append(SanitizeCell(r.memo)).Append('\t')
              .Append(LocalizationManager.EscapeText(r.ko)).Append('\t')
              .Append(LocalizationManager.EscapeText(r.en)).Append('\t')
              .Append(LocalizationManager.EscapeText(r.ja)).AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        LocalizationManager.ReloadEditorTsvCache();
        AssetDatabase.ImportAsset("Assets/" + LocalizationManager.MasterTsvRelativePath);
    }

    public static bool TryGetRow(string key, out MasterRow found)
    {
        found = null;
        if (string.IsNullOrEmpty(key)) return false;
        var rows = LoadMasterRows();
        found = rows.Find(r => string.Equals(r.key, key, StringComparison.Ordinal));
        return found != null;
    }

    public static void UpsertEntry(string key, string sourceType, string location, string memo, string ko, string en = null, string ja = null)
    {
        if (string.IsNullOrEmpty(key)) return;

        var rows = LoadMasterRows();
        var existing = rows.Find(r => string.Equals(r.key, key, StringComparison.Ordinal));
        if (existing != null)
        {
            if (!string.IsNullOrEmpty(sourceType)) existing.sourceType = sourceType;
            if (!string.IsNullOrEmpty(location)) existing.location = location;
            if (!string.IsNullOrEmpty(memo)) existing.memo = memo;
            if (ko != null) existing.ko = ko;
            if (en != null) existing.en = en;
            if (ja != null) existing.ja = ja;
        }
        else
        {
            rows.Add(new MasterRow
            {
                key = key.Trim(),
                sourceType = sourceType ?? "UI",
                location = location ?? string.Empty,
                memo = memo ?? string.Empty,
                ko = ko ?? string.Empty,
                en = en ?? string.Empty,
                ja = ja ?? string.Empty
            });
        }

        SaveMasterRows(rows);
    }

    public static void ConvertLocalizationTsvToBytes()
    {
        var rows = LoadMasterRows();
        if (rows.Count == 0)
        {
            Debug.LogWarning("[LocalizationEditorUtility] Master Localization TSV is empty or missing.");
            return;
        }

        string dataPath = Path.Combine(Application.dataPath, "Resources_moved", "Data");
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        string jsonDir = Path.Combine(Application.dataPath, "Data", "Json");
        if (!Directory.Exists(jsonDir))
        {
            Directory.CreateDirectory(jsonDir);
        }

        foreach (LanguageCode lang in Enum.GetValues(typeof(LanguageCode)))
        {
            var dataList = new LocalizationDataList();
            foreach (var row in rows)
            {
                string val = lang switch
                {
                    LanguageCode.KO => row.ko,
                    LanguageCode.EN => row.en,
                    LanguageCode.JA => row.ja,
                    _ => row.ko
                };

                dataList.entries.Add(new LocalizationEntry
                {
                    key = row.key,
                    value = val ?? string.Empty
                });
            }

            string jsonContent = JsonUtility.ToJson(dataList, true);
            byte[] encryptedBytes = CryptoUtility.Encrypt(jsonContent);

            string addressName = LocalizationManager.GetAddressForLanguage(lang); // e.g., Localization_KOData
            string bytesSavePath = Path.Combine(dataPath, $"{addressName}.bytes");
            File.WriteAllBytes(bytesSavePath, encryptedBytes);

            string jsonSavePath = Path.Combine(jsonDir, $"Localization_{lang}.json");
            File.WriteAllText(jsonSavePath, jsonContent, new UTF8Encoding(false));
        }

        AssetDatabase.Refresh();

        foreach (LanguageCode lang in Enum.GetValues(typeof(LanguageCode)))
        {
            string addressName = LocalizationManager.GetAddressForLanguage(lang);
            string assetPath = $"Assets/Resources_moved/Data/{addressName}.bytes";
            EnsureAddressableEntry(assetPath, addressName);
        }

        AssetDatabase.SaveAssets();
        LocalizationManager.ReloadEditorTsvCache();
        Debug.Log($"[LocalizationEditorUtility] Converted {rows.Count} localization keys into KO/EN/JA .bytes & .json files.");
    }

    public static void EnsureAddressableEntry(string assetPath, string address)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) return;

        var group = settings.DefaultGroup;
        if (group == null) return;

        var entry = settings.CreateOrMoveEntry(guid, group, false, false);
        if (entry != null && entry.address != address)
        {
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
        }
    }

    private static string SanitizeCell(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\t", " ").Replace("\r", "").Replace("\n", " ").Trim();
    }
}

