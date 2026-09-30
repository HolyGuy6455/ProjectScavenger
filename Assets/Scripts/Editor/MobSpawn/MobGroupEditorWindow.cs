using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class MobGroupEditorWindow : EditorWindow
{
    private string dataPath => Application.dataPath + "/GameData/MobGroups/";
    
    // UI States
    private Vector2 scrollPos;
    private List<MobGroupData> cachedGroups = new List<MobGroupData>();
    private MobGroupData selectedGroup = null;
    
    // New/Edit State
    private string editGroupID = "";
    private string editMemo = "";
    private MobCategory editCategory = MobCategory.None;
    private int editDangerLevel = 1;
    private float editAreaSize = 5f;

    [MenuItem("Tools/Mob Group Dashboard")]
    public static void ShowWindow()
    {
        GetWindow<MobGroupEditorWindow>("Mob Group Dashboard");
    }

    private void OnEnable()
    {
        RefreshList();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        
        // Left Panel - List
        DrawListPanel();
        
        // Right Panel - Details & Actions
        DrawDetailPanel();
        
        GUILayout.EndHorizontal();
    }

    private void RefreshList()
    {
        cachedGroups.Clear();
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        string[] files = Directory.GetFiles(dataPath, "*.json");
        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                MobGroupData data = JsonUtility.FromJson<MobGroupData>(json);
                if (data != null)
                {
                    cachedGroups.Add(data);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to read json: {file}. Error: {e.Message}");
            }
        }
    }

    private void DrawListPanel()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(300));
        GUILayout.Label("Mob Groups (JSON)", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Refresh List"))
        {
            RefreshList();
        }

        scrollPos = GUILayout.BeginScrollView(scrollPos);
        foreach (var group in cachedGroups)
        {
            GUI.backgroundColor = (selectedGroup == group) ? Color.cyan : Color.white;
            if (GUILayout.Button($"{group.GroupID} ({group.Category})"))
            {
                SelectGroup(group);
            }
            GUI.backgroundColor = Color.white;
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void SelectGroup(MobGroupData group)
    {
        selectedGroup = group;
        editGroupID = group.GroupID;
        editMemo = group.Memo;
        editCategory = group.Category;
        editDangerLevel = group.DangerLevel;
        editAreaSize = group.AreaSize;
    }

    private void DrawDetailPanel()
    {
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        
        GUILayout.Label("Group Details & Editing", EditorStyles.boldLabel);
        
        editGroupID = EditorGUILayout.TextField("Group ID (File Name)", editGroupID);
        
        GUILayout.Label("Memo");
        editMemo = EditorGUILayout.TextArea(editMemo, GUILayout.Height(60));
        
        editCategory = (MobCategory)EditorGUILayout.EnumFlagsField("Category", editCategory);
        editDangerLevel = EditorGUILayout.IntSlider("Danger Level", editDangerLevel, 1, 10);
        editAreaSize = EditorGUILayout.FloatField("Area Size", editAreaSize);

        GUILayout.Space(10);
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save (Overwrite/New from Scene)", GUILayout.Height(30)))
        {
            SaveCurrentSceneToJSON();
        }
        
        if (selectedGroup != null && GUILayout.Button("Load to Scene", GUILayout.Height(30)))
        {
            LoadJSONToScene(selectedGroup);
        }
        
        if (selectedGroup != null && GUILayout.Button("Delete JSON", GUILayout.Height(30)))
        {
            DeleteSelectedGroup();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(20);
        EditorGUILayout.HelpBox("How to use:\n1. Place 'MobGroupAuthoring' script on a root object in the scene.\n2. Place dummy objects (name them as MobID) as children of the root.\n3. Type GroupID and properties above, then click 'Save'.", MessageType.Info);

        GUILayout.EndVertical();
    }

    private void SaveCurrentSceneToJSON()
    {
        if (string.IsNullOrEmpty(editGroupID))
        {
            EditorUtility.DisplayDialog("Error", "Group ID cannot be empty.", "OK");
            return;
        }

        MobGroupAuthoring authoringRoot = Object.FindFirstObjectByType<MobGroupAuthoring>();
        if (authoringRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Cannot find 'MobGroupAuthoring' component in the scene. Please add it to a center object.", "OK");
            return;
        }

        // Authoring 컴포넌트의 설정된 반지름을 AreaSize로 동기화하여 저장
        editAreaSize = authoringRoot.radius;

        MobGroupData newData = new MobGroupData
        {
            GroupID = editGroupID,
            Memo = editMemo,
            Category = editCategory,
            DangerLevel = editDangerLevel,
            AreaSize = authoringRoot.radius,
            SpawnInfos = new List<MobSpawnInfo>()
        };

        foreach (Transform child in authoringRoot.transform)
        {
            // 유니티에서 복제한 객체의 이름 뒤에 붙는 " (1)", " (Clone)" 등을 깔끔하게 제거합니다.
            string cleanName = child.name.Split(' ')[0];

            MobSpawnInfo info = new MobSpawnInfo
            {
                MobID = cleanName, // Use the cleaned name as the Mob ID
                LocalPosition = child.localPosition,
                LocalEulerAngles = child.localEulerAngles
            };
            newData.SpawnInfos.Add(info);
        }

        string json = JsonUtility.ToJson(newData, true);
        string filePath = dataPath + editGroupID + ".json";
        
        File.WriteAllText(filePath, json);
        AssetDatabase.Refresh();
        
        authoringRoot.CurrentLoadedGroupID = editGroupID;
        
        EditorUtility.DisplayDialog("Success", $"Saved to {filePath}\nContains {newData.SpawnInfos.Count} mobs.", "OK");
        RefreshList();
    }

    private void LoadJSONToScene(MobGroupData data)
    {
        MobGroupAuthoring authoringRoot = Object.FindFirstObjectByType<MobGroupAuthoring>();
        if (authoringRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Cannot find 'MobGroupAuthoring' component in the scene. Please add it to a center object.", "OK");
            return;
        }

        // 씬의 Authoring 컴포넌트 반지름을 로드한 데이터의 AreaSize로 맞춤
        authoringRoot.radius = data.AreaSize;

        // Clear existing children
        for (int i = authoringRoot.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(authoringRoot.transform.GetChild(i).gameObject);
        }

        // Spawn dummies or actual prefabs
        foreach (var info in data.SpawnInfos)
        {
            GameObject spawnObj = null;

            // 1. 같은 이름의 프리팹 검색 시도
            string[] guids = AssetDatabase.FindAssets($"{info.MobID} t:Prefab");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    spawnObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                }
            }

            // 2. 프리팹을 못 찾았으면 기본 큐브로 대체
            if (spawnObj == null)
            {
                spawnObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            }

            spawnObj.name = info.MobID;
            spawnObj.transform.SetParent(authoringRoot.transform);
            spawnObj.transform.localPosition = info.LocalPosition;
            spawnObj.transform.localEulerAngles = info.LocalEulerAngles;
        }

        authoringRoot.CurrentLoadedGroupID = data.GroupID;
        EditorUtility.DisplayDialog("Loaded", $"Loaded {data.SpawnInfos.Count} mobs from {data.GroupID} into the scene.", "OK");
    }

    private void DeleteSelectedGroup()
    {
        if (EditorUtility.DisplayDialog("Delete", $"Are you sure you want to delete {selectedGroup.GroupID}.json?", "Yes", "No"))
        {
            string filePath = dataPath + selectedGroup.GroupID + ".json";
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                AssetDatabase.Refresh();
                selectedGroup = null;
                RefreshList();
            }
        }
    }
}
