using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class DataConverterEditor : EditorWindow
{
    [MenuItem("Tools/Data/Convert TSV to Binary")]
    public static void ShowWindow()
    {
        GetWindow<DataConverterEditor>("Data Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Data Converter", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Convert Gun Data"))
        {
            ConvertData<GunDataList, GunData>("Gun", "Scavenger - GunData.tsv");
        }

        if (GUILayout.Button("Convert Item Data"))
        {
            ConvertData<ItemDataList, ItemData>("Item", "Scavenger - ItemData.tsv");
        }

        if (GUILayout.Button("Convert Char Data"))
        {
            ConvertData<CharDataList, CharData>("Char", "Scavenger - CharData.tsv");
        }

        if (GUILayout.Button("Convert Skill Data"))
        {
            ConvertData<SkillDataList, SkillData>("Skill", "Scavenger - SkillData.tsv");
        }

        if (GUILayout.Button("Convert Ultimate Skill Data"))
        {
            ConvertData<UltimateSkillDataList, UltimateSkillData>("UltimateSkill", "Scavenger - UltimateSkillData.tsv");
        }
        
        GUILayout.Space(10);

        if (GUILayout.Button("Convert All Data"))
        {
            ConvertAllData();
        }
    }

    private void ConvertAllData()
    {
        ConvertData<GunDataList, GunData>("Gun", "Scavenger - GunData.tsv");
        ConvertData<ItemDataList, ItemData>("Item", "Scavenger - ItemData.tsv");
        ConvertData<CharDataList, CharData>("Char", "Scavenger - CharData.tsv");
        ConvertData<SkillDataList, SkillData>("Skill", "Scavenger - SkillData.tsv");
        ConvertData<UltimateSkillDataList, UltimateSkillData>("UltimateSkill", "Scavenger - UltimateSkillData.tsv");
        AssetDatabase.Refresh();
        Debug.Log("Finished Convert All Data operation.");
    }

    private void ConvertData<TList, TData>(string dataName, string tsvFileName) 
        where TList : IDataList<TData>, new()
        where TData : class, IData, new()
    {
        string tsvPath = Path.Combine(Application.dataPath, "Data", tsvFileName);
        if (!File.Exists(tsvPath))
        {
            tsvPath = EditorUtility.OpenFilePanel($"Select {dataName} TSV", Application.dataPath + "/Data", "tsv");
            if (string.IsNullOrEmpty(tsvPath)) return;
        }

        string tsvContent = File.ReadAllText(tsvPath);
        TList dataList = new TList();
        var list = ParseTsv<TData>(tsvContent);
        
        // Find the List<TData> field and set it
        var fields = typeof(TList).GetFields();
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(List<TData>))
            {
                field.SetValue(dataList, list);
                break;
            }
        }

        string jsonContent = JsonUtility.ToJson(dataList);
        byte[] encryptedBytes = CryptoUtility.Encrypt(jsonContent);

        string dataPath = Application.dataPath + "/Data";
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        string savePath = Path.Combine(dataPath, $"{dataName}Data.dat");
        File.WriteAllBytes(savePath, encryptedBytes);
        
        AssetDatabase.Refresh();
        Debug.Log($"Successfully converted and encrypted {dataName}Data to: {savePath}");
    }

    private List<T> ParseTsv<T>(string tsvContent) where T : new()
    {
        List<T> list = new List<T>();
        string[] lines = tsvContent.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return list;

        string[] headers = lines[0].Split('\t');
        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split('\t');
            T obj = new T();
            for (int j = 0; j < headers.Length && j < values.Length; j++)
            {
                string header = headers[j].Trim();
                string val = values[j].Trim();
                if (string.IsNullOrEmpty(val)) continue;
                if (header.StartsWith("#")) continue;

                var field = typeof(T).GetField(header);
                if (field != null)
                {
                    try
                    {
                        if (field.FieldType == typeof(int))
                        {
                            if (val == "_") field.SetValue(obj, 0);
                            else field.SetValue(obj, int.Parse(val));
                        }
                        else if (field.FieldType == typeof(float))
                        {
                            field.SetValue(obj, float.Parse(val));
                        }
                        else if (field.FieldType == typeof(string))
                        {
                            field.SetValue(obj, val);
                        }
                        else if (field.FieldType.IsEnum)
                        {
                            if (val == "_") field.SetValue(obj, System.Enum.ToObject(field.FieldType, 0));
                            else field.SetValue(obj, System.Enum.ToObject(field.FieldType, int.Parse(val)));
                        }
                        else if (field.FieldType == typeof(int[]))
                        {
                            var parts = val.Split(',');
                            int[] arr = new int[parts.Length];
                            for (int p = 0; p < parts.Length; p++)
                            {
                                arr[p] = int.Parse(parts[p].Trim());
                            }
                            field.SetValue(obj, arr);
                        }
                        else if (field.FieldType == typeof(ItemAmount[]))
                        {
                            var parts = val.Split(new[] { ")," }, System.StringSplitOptions.RemoveEmptyEntries);
                            ItemAmount[] arr = new ItemAmount[parts.Length];
                            for (int p = 0; p < parts.Length; p++)
                            {
                                string clean = parts[p].Replace("(", "").Replace(")", "").Trim();
                                var nums = clean.Split(',');
                                if (nums.Length == 2)
                                {
                                    arr[p] = new ItemAmount { itemId = int.Parse(nums[0]), amount = int.Parse(nums[1]) };
                                }
                            }
                            field.SetValue(obj, arr);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to parse field {header} with value {val} in class {typeof(T).Name}. Error: {e.Message}");
                    }
                }
            }

            list.Add(obj);
        }
        return list;
    }
}
