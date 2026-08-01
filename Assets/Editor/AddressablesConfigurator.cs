using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class AddressablesConfigurator
{
    [MenuItem("Tools/Configure Addressables and Build Settings")]
    public static void Configure()
    {
        // 1. Remove Stage001 from Build Settings
        var scenes = EditorBuildSettings.scenes.ToList();
        int removedCount = scenes.RemoveAll(s => s.path.Contains("Stage001.unity"));
        
        if (removedCount > 0)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Configurator] Stage001 removed from EditorBuildSettings.");
        }

        // 2. Add Stage001 to Addressables
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressable settings not found. Please create them via Window > Asset Management > Addressables > Groups.");
            return;
        }

        string scenePath = "Assets/Scenes/Stage001.unity";
        string guid = AssetDatabase.AssetPathToGUID(scenePath);
        
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"Scene not found at {scenePath}");
            return;
        }

        var group = settings.DefaultGroup;
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = "Stage001";
        
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();
        
        Debug.Log("[Configurator] Stage001 successfully marked as Addressable with address 'Stage001'!");
    }
}
