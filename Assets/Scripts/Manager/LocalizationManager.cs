using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class LocalizationManager
{
    private const string PlayerPrefsLanguageKey = "Scavenger_Language";
    public const string MasterTsvRelativePath = "Data/Scavenger - Localization.tsv";

    private static readonly Dictionary<string, string> _table = new Dictionary<string, string>(StringComparer.Ordinal);
    private static LanguageCode _currentLanguage = LanguageCode.KO;
    private static bool _languageInitialized;
    private static LocalizationFontConfig _fontConfig;

    public static bool IsLoaded { get; private set; }

    public static event Action OnLanguageChanged;

    public static LocalizationFontConfig FontConfig
    {
        get
        {
            if (_fontConfig == null)
            {
                _fontConfig = Resources.Load<LocalizationFontConfig>(LocalizationFontConfig.ResourceLoadPath);
            }
            return _fontConfig;
        }
        set => _fontConfig = value;
    }

    public static TMP_FontAsset ResolveFont(TMP_FontAsset originalFont)
    {
        return ResolveFont(originalFont, CurrentLanguage);
    }

    public static TMP_FontAsset ResolveFont(TMP_FontAsset originalFont, LanguageCode language)
    {
        if (originalFont == null) return null;
        var config = FontConfig;
        if (config == null) return originalFont;
        return config.ResolveFont(originalFont, language);
    }

    public static LanguageCode CurrentLanguage
    {
        get
        {
            EnsureLanguageInitialized();
            return _currentLanguage;
        }
    }

    private static void EnsureLanguageInitialized()
    {
        if (_languageInitialized) return;
        _languageInitialized = true;

        int saved = PlayerPrefs.GetInt(PlayerPrefsLanguageKey, (int)LanguageCode.KO);
        if (Enum.IsDefined(typeof(LanguageCode), saved))
        {
            _currentLanguage = (LanguageCode)saved;
        }
        else
        {
            _currentLanguage = LanguageCode.KO;
        }
    }

    public static string GetAddressForLanguage(LanguageCode language)
    {
        return $"Localization_{language}Data";
    }

    public static void SetLanguage(LanguageCode language)
    {
        EnsureLanguageInitialized();
        _currentLanguage = language;
        PlayerPrefs.SetInt(PlayerPrefsLanguageKey, (int)language);
        PlayerPrefs.Save();

        if (Application.isPlaying && DataManager.Instance != null)
        {
            DataManager.Instance.StartCoroutine(LoadLanguageAsync(language));
        }
        else
        {
#if UNITY_EDITOR
            ReloadEditorTsvCache();
            ApplyEditorCacheToRuntimeTable(language);
#endif
            OnLanguageChanged?.Invoke();
        }
    }

    public static IEnumerator LoadLanguageAsync(LanguageCode language)
    {
        EnsureLanguageInitialized();
        _currentLanguage = language;

        string address = GetAddressForLanguage(language);
        var handle = Addressables.LoadAssetAsync<TextAsset>(address);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            ParseEncryptedLocalizationData(handle.Result.bytes);
            IsLoaded = true;
            Debug.Log($"[LocalizationManager] Loaded '{address}' ({_table.Count} keys).");
        }
        else
        {
            Debug.LogWarning($"[LocalizationManager] Failed to load '{address}' via Addressables. Falling back if available.");
#if UNITY_EDITOR
            ReloadEditorTsvCache();
            ApplyEditorCacheToRuntimeTable(language);
            IsLoaded = true;
#endif
        }

        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }

        OnLanguageChanged?.Invoke();
    }

    public static void ParseEncryptedLocalizationData(byte[] encryptedBytes)
    {
        _table.Clear();
        try
        {
            string decryptedJson = CryptoUtility.Decrypt(encryptedBytes);
            LocalizationDataList dataList = JsonUtility.FromJson<LocalizationDataList>(decryptedJson);
            if (dataList != null && dataList.entries != null)
            {
                foreach (var entry in dataList.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                    _table[entry.key] = UnescapeText(entry.value);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalizationManager] Failed to parse localization data: {e.Message}");
        }
    }

    /// <summary>
    /// 번역키에 해당하는 문자열을 반환합니다.
    /// 키가 존재하지 않거나 현재 언어의 번역 텍스트가 비어 있으면 '#KEY' 형태로 반환합니다.
    /// </summary>
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

#if UNITY_EDITOR
        if (_table.Count == 0)
        {
            EnsureEditorCacheLoaded();
            ApplyEditorCacheToRuntimeTable(CurrentLanguage);
        }
#endif

        if (_table.TryGetValue(key, out string value) && !IsEmptyTranslation(value))
        {
            return value;
        }

        return FormatMissingKey(key);
    }

    /// <summary>
    /// 번역키에 해당하는 포맷 문자열에 인자를 대입해 반환합니다.
    /// </summary>
    public static string Get(string key, params object[] args)
    {
        string raw = Get(key);
        if (args == null || args.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal))
        {
            return raw;
        }

        try
        {
            return string.Format(raw, args);
        }
        catch (FormatException e)
        {
            Debug.LogWarning($"[LocalizationManager] FormatException for key '{key}' with template '{raw}': {e.Message}");
            return raw;
        }
    }

    public static bool HasKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
#if UNITY_EDITOR
        if (!Application.isPlaying || (!IsLoaded && _table.Count == 0))
        {
            return HasKeyInEditorTsv(key);
        }
#endif
        return _table.TryGetValue(key, out string val) && !IsEmptyTranslation(val);
    }

    public static string UnescapeText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return raw.Replace("\\n", "\n").Replace("\\t", "\t");
    }

    public static string EscapeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n").Replace("\t", "\\t");
    }

    private static bool IsEmptyTranslation(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value == "_";
    }

    private static string FormatMissingKey(string key)
    {
        if (key.StartsWith("#", StringComparison.Ordinal))
        {
            return key;
        }
        return $"#{key}";
    }

#if UNITY_EDITOR
    private static readonly Dictionary<LanguageCode, Dictionary<string, string>> _editorCache
        = new Dictionary<LanguageCode, Dictionary<string, string>>();
    private static bool _editorCacheLoaded;
    private static DateTime _lastTsvWriteTimeUtc;

    public static string GetMasterTsvFullPath()
    {
        return Path.Combine(Application.dataPath, MasterTsvRelativePath);
    }

    public static void ReloadEditorTsvCache()
    {
        _editorCacheLoaded = false;
        EnsureEditorCacheLoaded();
    }

    private static void EnsureEditorCacheLoaded()
    {
        string path = GetMasterTsvFullPath();
        if (!File.Exists(path))
        {
            _editorCache.Clear();
            _editorCacheLoaded = true;
            return;
        }

        DateTime writeTime = File.GetLastWriteTimeUtc(path);
        if (_editorCacheLoaded && writeTime == _lastTsvWriteTimeUtc)
        {
            return;
        }

        _editorCache.Clear();
        foreach (LanguageCode lang in Enum.GetValues(typeof(LanguageCode)))
        {
            _editorCache[lang] = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length >= 2)
            {
                string[] headers = lines[0].Split('\t');
                int keyIndex = -1;
                var langColumnIndices = new Dictionary<LanguageCode, int>();

                for (int i = 0; i < headers.Length; i++)
                {
                    string h = headers[i].Trim().TrimStart('\uFEFF');
                    if (string.Equals(h, "key", StringComparison.OrdinalIgnoreCase))
                    {
                        keyIndex = i;
                    }
                    else if (Enum.TryParse<LanguageCode>(h, true, out var langCode))
                    {
                        langColumnIndices[langCode] = i;
                    }
                }

                if (keyIndex >= 0)
                {
                    for (int row = 1; row < lines.Length; row++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[row])) continue;
                        string[] cols = lines[row].Split('\t');
                        if (keyIndex >= cols.Length) continue;

                        string key = cols[keyIndex].Trim();
                        if (string.IsNullOrEmpty(key)) continue;

                        foreach (var kvp in langColumnIndices)
                        {
                            string cell = kvp.Value < cols.Length ? cols[kvp.Value].Trim() : string.Empty;
                            _editorCache[kvp.Key][key] = UnescapeText(cell);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalizationManager] Failed to read editor TSV cache: {e.Message}");
        }

        _lastTsvWriteTimeUtc = writeTime;
        _editorCacheLoaded = true;
    }

    private static void ApplyEditorCacheToRuntimeTable(LanguageCode language)
    {
        EnsureEditorCacheLoaded();
        _table.Clear();
        if (_editorCache.TryGetValue(language, out var dict))
        {
            foreach (var kvp in dict)
            {
                _table[kvp.Key] = kvp.Value;
            }
        }
    }

    public static bool HasKeyInEditorTsv(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        EnsureEditorCacheLoaded();
        return _editorCache.TryGetValue(LanguageCode.KO, out var dict) && dict.ContainsKey(key);
    }

    public static bool TryGetFromEditorTsv(string key, LanguageCode language, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(key)) return false;
        EnsureEditorCacheLoaded();
        if (_editorCache.TryGetValue(language, out var dict) && dict.TryGetValue(key, out string raw))
        {
            value = raw;
            return !IsEmptyTranslation(raw);
        }
        return false;
    }

    public static string GetEditorPreview(string key, LanguageCode language = LanguageCode.KO, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (TryGetFromEditorTsv(key, language, out string value))
        {
            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(value, args);
                }
                catch (FormatException)
                {
                    return value;
                }
            }
            return value;
        }
        return FormatMissingKey(key);
    }
#endif
}