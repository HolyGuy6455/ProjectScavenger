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
            ConvertData<GunDataList, GunData>("Gun", "Scavenger - Gun.tsv");
        }

        if (GUILayout.Button("Convert Item Data"))
        {
            ConvertData<ItemDataList, ItemData>("Item", "Scavenger - Item.tsv");
        }

        if (GUILayout.Button("Convert Char Data"))
        {
            ConvertData<CharDataList, CharData>("Char", "Scavenger - Char.tsv");
        }

        if (GUILayout.Button("Convert Skill Data"))
        {
            ConvertData<SkillDataList, SkillData>("Skill", "Scavenger - Skill.tsv");
        }

        if (GUILayout.Button("Convert Ultimate Skill Data"))
        {
            ConvertData<UltimateSkillDataList, UltimateSkillData>("UltimateSkill", "Scavenger - UltimateSkill.tsv");
        }

        if (GUILayout.Button("Convert Loot Data"))
        {
            ConvertData<LootDataList, LootData>("Loot", "Scavenger - Loot.tsv");
        }
        
        GUILayout.Space(10);

        if (GUILayout.Button("Convert All Data"))
        {
            ConvertAllData();
        }
    }

    private void ConvertAllData()
    {
        ConvertData<GunDataList, GunData>("Gun", "Scavenger - Gun.tsv");
        ConvertData<ItemDataList, ItemData>("Item", "Scavenger - Item.tsv");
        ConvertData<CharDataList, CharData>("Char", "Scavenger - Char.tsv");
        ConvertData<SkillDataList, SkillData>("Skill", "Scavenger - Skill.tsv");
        ConvertData<UltimateSkillDataList, UltimateSkillData>("UltimateSkill", "Scavenger - UltimateSkill.tsv");
        ConvertData<LootDataList, LootData>("Loot", "Scavenger - Loot.tsv");
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

        // 출력 경로는 Resources/Data 로 변경
        string dataPath = Application.dataPath + "/Resources/Data";
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        // 확장자를 .bytes 로 변경
        string savePath = Path.Combine(dataPath, $"{dataName}Data.bytes");
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

        Dictionary<string, int> arraySizes = new Dictionary<string, int>();
        for (int j = 0; j < headers.Length; j++)
        {
            string header = headers[j].Trim();
            if (header.StartsWith("#")) continue;

            int underscoreIndex = header.LastIndexOf('_');
            if (underscoreIndex > 0 && underscoreIndex < header.Length - 1)
            {
                string arrayName = header.Substring(0, underscoreIndex);
                string indexStr = header.Substring(underscoreIndex + 1);
                if (int.TryParse(indexStr, out int index))
                {
                    if (!arraySizes.ContainsKey(arrayName) || arraySizes[arrayName] < index)
                    {
                        arraySizes[arrayName] = index;
                    }
                }
            }
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split('\t');
            T obj = new T();

            foreach (var kvp in arraySizes)
            {
                var field = typeof(T).GetField(kvp.Key);
                if (field != null && field.FieldType.IsArray)
                {
                    var elementType = field.FieldType.GetElementType();
                    var array = System.Array.CreateInstance(elementType, kvp.Value);
                    field.SetValue(obj, array);
                }
            }

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
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to parse field {header} with value {val} in class {typeof(T).Name}. Error: {e.Message}");
                    }
                }
                else
                {
                    bool isMatched = false;
                    int underscoreIndex = header.LastIndexOf('_');
                    if (underscoreIndex > 0 && underscoreIndex < header.Length - 1)
                    {
                        string arrayName = header.Substring(0, underscoreIndex);
                        string indexStr = header.Substring(underscoreIndex + 1);
                        if (int.TryParse(indexStr, out int index))
                        {
                            var arrayField = typeof(T).GetField(arrayName);
                            if (arrayField != null && arrayField.FieldType.IsArray)
                            {
                                isMatched = true;
                                var array = (System.Array)arrayField.GetValue(obj);
                                if (array != null && index - 1 >= 0 && index - 1 < array.Length)
                                {
                                    var elementType = arrayField.FieldType.GetElementType();
                                    try
                                    {
                                        object parsedValue = null;
                                        if (elementType == typeof(int))
                                        {
                                            parsedValue = val == "_" ? 0 : int.Parse(val);
                                        }
                                        else if (elementType == typeof(float))
                                        {
                                            parsedValue = float.Parse(val);
                                        }
                                        else if (elementType == typeof(string))
                                        {
                                            parsedValue = val;
                                        }
                                        else if (elementType.IsEnum)
                                        {
                                            parsedValue = val == "_" ? System.Enum.ToObject(elementType, 0) : System.Enum.ToObject(elementType, int.Parse(val));
                                        }

                                        if (parsedValue != null)
                                        {
                                            array.SetValue(parsedValue, index - 1);
                                        }
                                    }
                                    catch (System.Exception e)
                                    {
                                        Debug.LogWarning($"Failed to parse array element {header} with value {val} in class {typeof(T).Name}. Error: {e.Message}");
                                    }
                                }
                            }
                        }
                    }

                    if (!isMatched)
                    {
                        Debug.LogWarning($"[DataConverter] '{header}' 컬럼을 클래스 '{typeof(T).Name}'의 어떤 필드와도 매칭할 수 없어 버려집니다. 변수명이나 데이터 구조를 확인하세요.");
                    }
                }
            }

            list.Add(obj);
        }
        return list;
    }
}
