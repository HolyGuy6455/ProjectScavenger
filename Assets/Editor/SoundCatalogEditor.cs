using System;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using Scavenger.Audio;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Explicitly syncs SoundRef keys and exported FMOD GUIDs without depending on the FMOD editor cache.
/// Regenerate FMOD/event-manifest.json when FMOD event paths or GUIDs change.
/// </summary>
public static class SoundCatalogEditor
{
    [Serializable]
    private sealed class Manifest
    {
        public ManifestEvent[] events;
    }

    [Serializable]
    private sealed class ManifestEvent
    {
        public string path;
        public string guid;
    }

    [MenuItem("Tools/Audio/Sync Sound Catalog from SoundRef")]
    public static void Sync()
    {
        SoundCatalog candidate = null;
        try
        {
            SoundCatalog catalog = ResolveCatalog(out string assetPath);
            SoundCatalog.Entry[] entries = ReadExpectedEntries();
            candidate = ScriptableObject.CreateInstance<SoundCatalog>();
            candidate.ReplaceEntries(entries);
            ValidateEntries(candidate, entries);

            Undo.RecordObject(catalog, "Sync Sound Catalog");
            catalog.ReplaceEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log($"SoundCatalog synced and validated: {entries.Length} SoundRef keys at {assetPath} (Addressables: {SoundCatalog.Address}).", catalog);
        }
        catch (Exception exception)
        {
            Debug.LogError($"SoundCatalog sync failed: {exception.Message}");
        }
        finally
        {
            if (candidate != null)
                UnityEngine.Object.DestroyImmediate(candidate);
        }
    }

    [MenuItem("Tools/Audio/Validate Sound Catalog against SoundRef")]
    public static void Validate()
    {
        try
        {
            SoundCatalog catalog = ResolveCatalog(out _);
            SoundCatalog.Entry[] entries = ReadExpectedEntries();
            ValidateEntries(catalog, entries);
            Debug.Log($"SoundCatalog validation passed: all {entries.Length} keys, event paths and GUIDs match SoundRef and event-manifest.json.", catalog);
        }
        catch (Exception exception)
        {
            Debug.LogError($"SoundCatalog validation failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Resolves the existing catalog by its explicit Addressables address without creating,
    /// moving, registering, or saving any asset. Duplicate addresses must be fixed by the author.
    /// </summary>
    public static SoundCatalog ResolveCatalog(out string assetPath)
    {
        assetPath = null;
        // Check the configured modern settings object first. The package's Settings getter can
        // otherwise migrate legacy settings and save assets even when GetSettings(false) is used.
        if (!AddressableAssetSettingsDefaultObject.SettingsExists)
            throw new InvalidDataException("Addressables settings are missing. Open Window/Asset Management/Addressables/Groups and configure the existing SoundCatalog asset before syncing or validating.");

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
        if (settings == null)
            throw new InvalidDataException("Addressables settings could not be loaded. Wait for asset import to finish and retry.");

        AddressableAssetEntry match = null;
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null)
                continue;

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry == null || !string.Equals(entry.address, SoundCatalog.Address, StringComparison.Ordinal))
                    continue;

                if (match != null)
                    throw new InvalidDataException($"Multiple explicit Addressables entries use '{SoundCatalog.Address}' (GUIDs {match.guid} and {entry.guid}). Keep exactly one SoundCatalog entry with this address in Addressables Groups.");

                match = entry;
            }
        }

        if (match == null)
            throw new InvalidDataException($"No explicit Addressables entry has address '{SoundCatalog.Address}'. Register the existing SoundCatalog asset with that exact address in Addressables Groups; sync does not create replacement assets.");

        assetPath = AssetDatabase.GUIDToAssetPath(match.guid);
        if (string.IsNullOrEmpty(assetPath))
            throw new InvalidDataException($"Addressables entry '{SoundCatalog.Address}' points to missing asset GUID {match.guid}. Restore the catalog asset or repair this entry in Addressables Groups.");

        SoundCatalog catalog = AssetDatabase.LoadMainAssetAtPath(assetPath) as SoundCatalog;
        if (catalog == null)
            throw new InvalidDataException($"Addressables entry '{SoundCatalog.Address}' resolves to {assetPath}, which is not a SoundCatalog asset. Assign that address to the existing SoundCatalog asset in Addressables Groups.");

        return catalog;
    }

    private static SoundCatalog.Entry[] ReadExpectedEntries()
    {
        string fmodDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "FMOD"));
        string manifestPath = Path.Combine(fmodDirectory, "event-manifest.json");
        string tablePath = Path.Combine(fmodDirectory, "Scavenger - SoundRef.tsv");
        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        if (manifest == null || manifest.events == null || manifest.events.Length == 0)
            throw new InvalidDataException($"No events in {manifestPath}.");

        var eventsByPath = new Dictionary<string, FMOD.GUID>(StringComparer.Ordinal);
        var eventGuids = new HashSet<FMOD.GUID>();
        foreach (ManifestEvent entry in manifest.events)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.path)
                || !entry.path.StartsWith("event:/", StringComparison.Ordinal)
                || !Guid.TryParse(entry.guid, out Guid guid) || guid == Guid.Empty)
                throw new InvalidDataException("Manifest contains an invalid event path or GUID.");

            var fmodGuid = new FMOD.GUID(guid);
            if (eventsByPath.ContainsKey(entry.path) || !eventGuids.Add(fmodGuid))
                throw new InvalidDataException($"Duplicate manifest event path or GUID: {entry.path}.");

            eventsByPath.Add(entry.path, fmodGuid);
        }

        string[] lines = File.ReadAllLines(tablePath);
        if (lines.Length == 0)
            throw new InvalidDataException("SoundRef is empty.");

        string[] headers = lines[0].TrimStart('\uFEFF').Split('\t');
        int keyColumn = Array.IndexOf(headers, "사운드 키");
        int eventColumn = Array.IndexOf(headers, "이벤트 명칭");
        if (keyColumn < 0 || eventColumn < 0)
            throw new InvalidDataException("SoundRef requires the 사운드 키 and 이벤트 명칭 columns.");

        var entries = new List<SoundCatalog.Entry>();
        var keys = new HashSet<SoundKey>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        for (int line = 1; line < lines.Length; line++)
        {
            if (string.IsNullOrWhiteSpace(lines[line]))
                continue;

            string[] columns = lines[line].Split('\t');
            if (columns.Length <= Math.Max(keyColumn, eventColumn))
                throw new InvalidDataException($"SoundRef line {line + 1} has missing columns.");

            string keyName = columns[keyColumn].Trim();
            string eventPath = columns[eventColumn].Trim();
            // Rows without both fields are notes, such as the unused Ambience placeholder.
            if (keyName.Length == 0 && eventPath.Length == 0)
                continue;

            if (!Enum.TryParse(keyName, false, out SoundKey key) || key == SoundKey.None
                || !Enum.IsDefined(typeof(SoundKey), key) || key.ToString() != keyName)
                throw new InvalidDataException($"SoundRef line {line + 1}: unknown SoundKey '{keyName}'. Add a new stable enum ID first.");

            if (!keys.Add(key) || !paths.Add(eventPath))
                throw new InvalidDataException($"SoundRef line {line + 1}: duplicate key or event path for {keyName}.");

            if (!eventsByPath.TryGetValue(eventPath, out FMOD.GUID eventGuid))
                throw new InvalidDataException($"SoundRef line {line + 1}: '{eventPath}' is missing from event-manifest.json.");

            entries.Add(new SoundCatalog.Entry(key, new EventReference { Guid = eventGuid, Path = eventPath }));
        }

        foreach (SoundKey key in Enum.GetValues(typeof(SoundKey)))
        {
            if (key != SoundKey.None && !keys.Contains(key))
                throw new InvalidDataException($"SoundRef is missing key {key}.");
        }

        return entries.ToArray();
    }

    private static void ValidateEntries(SoundCatalog catalog, SoundCatalog.Entry[] expected)
    {
        if (!catalog.TryCreateLookup(out Dictionary<SoundKey, EventReference> lookup, out string error))
            throw new InvalidDataException(error);

        if (lookup.Count != expected.Length)
            throw new InvalidDataException("SoundCatalog and SoundRef have different numbers of keys.");

        foreach (SoundCatalog.Entry entry in expected)
        {
            if (!lookup.TryGetValue(entry.Key, out EventReference actual)
                || actual.Guid != entry.Event.Guid
                || !string.Equals(actual.Path, entry.Event.Path, StringComparison.Ordinal))
                throw new InvalidDataException($"SoundCatalog key {entry.Key} does not match its SoundRef path and manifest GUID.");
        }
    }

}
