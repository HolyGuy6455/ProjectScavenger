using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Scavenger.Audio;
using Scavenger.Network.DTOs;

public class DataConverterEditor : EditorWindow
{
    [MenuItem("Tools/Data/Convert TSV to Binary")]
    public static void ShowWindow()
    {
        GetWindow<DataConverterEditor>("Data Converter");
    }

    [MenuItem("Tools/Data/Fill CharLvl TSV (Interpolate)")]
    public static void FillCharLvlTsv()
    {
        string tsvPath = Path.Combine(Application.dataPath, "Data", "Scavenger - CharLvl.tsv");
        if (!File.Exists(tsvPath))
        {
            Debug.LogError("CharLvl TSV not found at " + tsvPath);
            return;
        }

        string[] lines = File.ReadAllLines(tsvPath);
        if (lines.Length < 2) return;

        string header = lines[0];
        
        List<CharLvlData> existingData = new List<CharLvlData>();
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] parts = line.Split('\t');
            if (parts.Length >= 5)
            {
                CharLvlData data = new CharLvlData();
                int.TryParse(parts[0], out data.id);
                int.TryParse(parts[1], out data.charId);
                int.TryParse(parts[2], out data.level);
                int.TryParse(parts[3], out data.atk);
                int.TryParse(parts[4], out data.hp);
                existingData.Add(data);
            }
        }

        Dictionary<int, List<CharLvlData>> charGroups = new Dictionary<int, List<CharLvlData>>();
        foreach (var data in existingData)
        {
            if (!charGroups.ContainsKey(data.charId)) charGroups[data.charId] = new List<CharLvlData>();
            charGroups[data.charId].Add(data);
        }

        List<CharLvlData> newData = new List<CharLvlData>();
        int newId = 1;

        foreach (var kvp in charGroups)
        {
            int charId = kvp.Key;
            List<CharLvlData> keyframes = kvp.Value;
            keyframes.Sort((a, b) => a.level.CompareTo(b.level));

            if (keyframes.Count == 0) continue;

            int minLevel = keyframes[0].level;
            int maxLevel = keyframes[keyframes.Count - 1].level;

            for (int lvl = minLevel; lvl <= maxLevel; lvl++)
            {
                CharLvlData prev = keyframes[0];
                CharLvlData next = keyframes[keyframes.Count - 1];

                foreach (var kf in keyframes)
                {
                    if (kf.level <= lvl) prev = kf;
                }
                
                next = keyframes.Find(k => k.level >= lvl);
                if (next == null) next = keyframes[keyframes.Count - 1];

                float t = 0f;
                if (next.level > prev.level)
                {
                    t = (float)(lvl - prev.level) / (next.level - prev.level);
                }

                CharLvlData interpolated = new CharLvlData();
                interpolated.id = newId++;
                interpolated.charId = charId;
                interpolated.level = lvl;
                interpolated.atk = Mathf.RoundToInt(Mathf.Lerp(prev.atk, next.atk, t));
                interpolated.hp = Mathf.RoundToInt(Mathf.Lerp(prev.hp, next.hp, t));

                newData.Add(interpolated);
            }
        }

        using (StreamWriter writer = new StreamWriter(tsvPath, false))
        {
            writer.WriteLine(header);
            foreach (var d in newData)
            {
                writer.WriteLine($"{d.id}\t{d.charId}\t{d.level}\t{d.atk}\t{d.hp}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"Successfully interpolated CharLvl TSV. Total rows: {newData.Count}");
    }

    private void OnGUI()
    {
        GUILayout.Label("Data Converter", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Convert Ammo Data"))
        {
            ConvertData<AmmoDataList, AmmoData>("Ammo", "Scavenger - Ammo.tsv");
        }

        if (GUILayout.Button("Convert Item Data"))
        {
            ConvertData<ItemDataList, ItemData>("Item", "Scavenger - Item.tsv");
        }

        if (GUILayout.Button("Convert User Currency Data"))
        {
            ConvertData<UserCurrencyDataList, UserCurrencyData>("UserCurrency", "Scavenger - UserCurrency.tsv");
        }

        if (GUILayout.Button("Convert Char Data"))
        {
            ConvertData<CharDataList, CharData>("Char", "Scavenger - Char.tsv");
        }

        if (GUILayout.Button("Convert Skill Data"))
        {
            ConvertData<SkillDataList, SkillData>("Skill", "Scavenger - Skill.tsv");
        }

        if (GUILayout.Button("Convert Char Lvl Data"))
        {
            ConvertData<CharLvlDataList, CharLvlData>("CharLvl", "Scavenger - CharLvl.tsv");
        }

        if (GUILayout.Button("Convert Level Data"))
        {
            ConvertData<LevelDataList, LevelData>("Level", "Scavenger - Level.tsv");
        }

        if (GUILayout.Button("Convert Quest Data"))
        {
            ConvertQuestData();
        }

        GUILayout.Space(20);


        if (GUILayout.Button("Convert Loot Data"))
        {
            ConvertData<LootDataList, LootData>("Loot", "Scavenger - Loot.tsv");
        }
        
        if (GUILayout.Button("Convert Enemy Data"))
        {
            ConvertData<EnemyDataList, EnemyData>("Enemy", "Scavenger - Enemy.tsv");
        }
        
        if (GUILayout.Button("Convert Obstacle Data"))
        {
            ConvertData<ObstacleDataList, ObstacleData>("Obstacle", "Scavenger - Obstacle.tsv");
        }
        
        if (GUILayout.Button("Convert MobGroup Data (JSON -> Bytes)"))
        {
            ConvertMobGroupData();
        }
        
        GUILayout.Space(10);

        if (GUILayout.Button("Convert All Data"))
        {
            ConvertAllData();
        }
    }

    private void ConvertAllData()
    {
        ConvertData<AmmoDataList, AmmoData>("Ammo", "Scavenger - Ammo.tsv");
        ConvertData<ItemDataList, ItemData>("Item", "Scavenger - Item.tsv");
        ConvertData<UserCurrencyDataList, UserCurrencyData>("UserCurrency", "Scavenger - UserCurrency.tsv");
        ConvertData<CharDataList, CharData>("Char", "Scavenger - Char.tsv");
        ConvertData<CharLvlDataList, CharLvlData>("CharLvl", "Scavenger - CharLvl.tsv");
        ConvertData<LevelDataList, LevelData>("Level", "Scavenger - Level.tsv");
        ConvertData<SkillDataList, SkillData>("Skill", "Scavenger - Skill.tsv");
        ConvertQuestData();

        ConvertData<LootDataList, LootData>("Loot", "Scavenger - Loot.tsv");
        ConvertData<EnemyDataList, EnemyData>("Enemy", "Scavenger - Enemy.tsv");
        ConvertData<ObstacleDataList, ObstacleData>("Obstacle", "Scavenger - Obstacle.tsv");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("All Data Converted Successfully.");
    }

    [MenuItem("Tools/Data/Copy Master Data To Server")]
    public static void CopyMasterDataToServer()
    {
        string clientDataPath = Application.dataPath + "/Data/";
        // Assuming ScavengerServer is adjacent to ClaudePractice
        string serverResourcesPath = System.IO.Path.GetFullPath(Application.dataPath + "/../../ScavengerServer/Resources/");
        string serverTmpPath = System.IO.Path.GetFullPath(Application.dataPath + "/../../ScavengerServer/tmp/");

        if (!System.IO.Directory.Exists(serverResourcesPath))
        {
            System.IO.Directory.CreateDirectory(serverResourcesPath);
        }

        try
        {
            // Copy game items, skills, char levels, levels
            string clientJsonPath = Application.dataPath + "/Data/Json/";
            System.IO.File.Copy(clientJsonPath + "Item.json", serverResourcesPath + "Item.json", true);
            System.IO.File.Copy(clientJsonPath + "UserCurrency.json", serverResourcesPath + "UserCurrencys.json", true);
            System.IO.File.Copy(clientJsonPath + "Skill.json", serverResourcesPath + "Skill.json", true);
            System.IO.File.Copy(clientJsonPath + "Char.json", serverResourcesPath + "Char.json", true);
            System.IO.File.Copy(clientJsonPath + "CharLvl.json", serverResourcesPath + "CharLvl.json", true);
            System.IO.File.Copy(clientJsonPath + "Level.json", serverResourcesPath + "Level.json", true);
            System.IO.File.Copy(clientJsonPath + "Quest.json", serverResourcesPath + "Quest.json", true);

            Debug.Log($"[DataConverter] Master data successfully copied to server: {serverResourcesPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataConverter] Failed to copy master data to server: {e.Message}");
        }
    }

    private void ConvertMobGroupData()
    {
        string folderPath = Application.dataPath + "/GameData/MobGroups/";
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[DataConverter] Mob Groups Folder not found: {folderPath}");
            return;
        }

        string[] files = Directory.GetFiles(folderPath, "*.json");
        MobGroupDataList dataList = new MobGroupDataList();
        
        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                MobGroupData data = JsonUtility.FromJson<MobGroupData>(json);
                if (data != null)
                {
                    dataList.Groups.Add(data);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataConverter] Failed to load mob group {file}: {e.Message}");
            }
        }

        string jsonContent = JsonUtility.ToJson(dataList);
        byte[] encryptedBytes = CryptoUtility.Encrypt(jsonContent);

        string dataPath = Application.dataPath + "/Resources_moved/Data";
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        string savePath = Path.Combine(dataPath, "MobGroupData.bytes");
        File.WriteAllBytes(savePath, encryptedBytes);
        
        UnityEditor.AssetDatabase.Refresh();
        
        Debug.Log($"Successfully converted {dataList.Groups.Count} Mob Groups to: {savePath}");
    }

    private void ConvertQuestData()
    {
        ConvertData<QuestDataList, QuestData>("Quest", "Scavenger - Quest.tsv", ParseQuestTsv, ExportQuestJson);
    }

    private List<QuestData> ParseQuestTsv(string tsvContent)
    {
        var quests = new List<QuestData>();
        string[] lines = tsvContent.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return quests;

        string[] headers = lines[0].Split('\t');
        var columns = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        var rewardSlots = new SortedSet<int>();
        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i].Trim().TrimStart('\uFEFF');
            if (header.StartsWith("#")) continue;
            columns[header] = i;
            if (header.StartsWith("rewardType_", System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(header.Substring("rewardType_".Length), out int slot) && slot > 0)
            {
                rewardSlots.Add(slot);
            }
        }

        foreach (string required in new[] { "id", "name", "desc", "cycleType", "targetType", "targetId", "targetCount" })
        {
            if (!columns.ContainsKey(required))
                throw new System.FormatException($"Quest TSV is missing required column '{required}'.");
        }
        foreach (int slot in rewardSlots)
        {
            if (!columns.ContainsKey($"rewardId_{slot}") || !columns.ContainsKey($"rewardItemCount_{slot}"))
                throw new System.FormatException($"Quest TSV reward slot {slot} is missing its ID or count column.");
        }

        var ids = new HashSet<int>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Split('\t');
            string ReadCell(string column) => columns.TryGetValue(column, out int index) && index < values.Length
                ? values[index].Trim() : string.Empty;
            int ReadInt(string column)
            {
                string value = ReadCell(column);
                return string.IsNullOrEmpty(value) || value == "_" ? 0
                    : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            try
            {
                QuestCycleType cycle;
                switch (ReadCell("cycleType").ToUpperInvariant())
                {
                    // The client calls one-time quests MAIN; the TSV and server call them ONCE.
                    case "ONCE": cycle = QuestCycleType.MAIN; break;
                    case "DAILY": cycle = QuestCycleType.DAILY; break;
                    case "WEEKLY": cycle = QuestCycleType.WEEKLY; break;
                    default: throw new System.FormatException($"Unknown quest cycle '{ReadCell("cycleType")}'.");
                }

                var rewards = new List<QuestReward>();
                foreach (int slot in rewardSlots)
                {
                    string type = ReadCell($"rewardType_{slot}");
                    int rewardId = ReadInt($"rewardId_{slot}");
                    int count = ReadInt($"rewardItemCount_{slot}");
                    if ((string.IsNullOrEmpty(type) || type == "_") && rewardId == 0 && count == 0) continue;
                    if (string.IsNullOrEmpty(type) || type == "_" || rewardId <= 0 || count <= 0)
                        throw new System.FormatException($"Incomplete reward in slot {slot}.");
                    rewards.Add(new QuestReward { Type = type, Id = rewardId, Count = count });
                }

                int id = ReadInt("id");
                if (id <= 0 || !ids.Add(id))
                    throw new System.FormatException($"Invalid or duplicate quest ID {id}.");

                quests.Add(new QuestData
                {
                    id = id,
                    isComplete = ReadCell("isComplete"),
                    Name = ReadCell("name"),
                    Desc = ReadCell("desc"),
                    CycleType = cycle,
                    PreQuestId = ReadInt("preQuestId"),
                    TargetType = ReadInt("targetType"),
                    TargetId = ReadInt("targetId"),
                    TargetCount = ReadInt("targetCount"),
                    Rewards = rewards.ToArray(),
                    Shortcut = ReadCell("shortcut"),
                    ShortcutValue = ReadCell("shortcut_value"),
                    StartDate = ReadCell("start_date"),
                    EndDate = ReadCell("end_date")
                });
            }
            catch (System.Exception e)
            {
                throw new System.FormatException($"Quest TSV row {i + 1}: {e.Message}", e);
            }
        }
        return quests;
    }

    [System.Serializable]
    private class ServerQuestData
    {
        public int id;
        public string is_complete;
        public string name;
        public string desc;
        public string cycleType;
        public int preQuestId;
        public int targetType;
        public int targetId;
        public int targetCount;
        public QuestReward[] rewards;
        public string shortcut;
        public string shortcut_value;
        public string start_date;
        public string end_date;
    }

    [System.Serializable]
    private class ServerQuestDataList
    {
        public List<ServerQuestData> quests = new List<ServerQuestData>();
    }

    private string ExportQuestJson(QuestDataList dataList)
    {
        var serverData = new ServerQuestDataList();
        foreach (var quest in dataList.quests)
        {
            serverData.quests.Add(new ServerQuestData
            {
                id = quest.id,
                is_complete = quest.isComplete,
                name = quest.Name,
                desc = quest.Desc,
                cycleType = quest.CycleType == QuestCycleType.MAIN ? "ONCE" : quest.CycleType.ToString(),
                preQuestId = quest.PreQuestId,
                targetType = quest.TargetType,
                targetId = quest.TargetId,
                targetCount = quest.TargetCount,
                rewards = quest.Rewards,
                shortcut = quest.Shortcut,
                shortcut_value = quest.ShortcutValue,
                start_date = quest.StartDate,
                end_date = quest.EndDate
            });
        }
        return JsonUtility.ToJson(serverData);
    }

    private void ConvertData<TList, TData>(string dataName, string tsvFileName,
        System.Func<string, List<TData>> parser = null, System.Func<TList, string> jsonExporter = null)
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
        var list = parser != null ? parser(tsvContent) : ParseTsv<TData>(tsvContent);
        
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
        string exportedJson = jsonExporter != null ? jsonExporter(dataList) : jsonContent;
        byte[] encryptedBytes = CryptoUtility.Encrypt(jsonContent);

        // 출력 경로는 Resources_moved/Data 로 변경
        string dataPath = Application.dataPath + "/Resources_moved/Data";
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        // 확장자를 .bytes 로 변경
        string savePath = Path.Combine(dataPath, $"{dataName}Data.bytes");
        File.WriteAllBytes(savePath, encryptedBytes);
        
        string jsonPath = Application.dataPath + "/Data/Json";
        if (!Directory.Exists(jsonPath)) Directory.CreateDirectory(jsonPath);
        File.WriteAllText(Path.Combine(jsonPath, $"{dataName}.json"), exportedJson);
        
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
                        else if (field.FieldType == typeof(bool))
                        {
                            field.SetValue(obj, val.Equals("TRUE", System.StringComparison.OrdinalIgnoreCase) || val.Equals("1"));
                        }
                        else if (field.FieldType == typeof(string))
                        {
                            field.SetValue(obj, val);
                        }
                        else if (field.FieldType.IsEnum)
                        {
                            field.SetValue(obj, ParseEnumValue(field.FieldType, val));
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
                        else if (field.FieldType == typeof(string[]))
                        {
                            var parts = val.Split(',');
                            string[] arr = new string[parts.Length];
                            for (int p = 0; p < parts.Length; p++)
                            {
                                arr[p] = parts[p].Trim();
                            }
                            field.SetValue(obj, arr);
                        }
                    }
                    catch (System.Exception e)
                    {
                        // A typo must not silently replace an existing sound with None in exported data.
                        if (field.FieldType == typeof(SoundKey))
                            throw new System.FormatException($"Invalid SoundKey '{val}' in {typeof(T).Name}, row {i + 1}, column '{header}'. Conversion aborted before writing files.", e);

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
                                            parsedValue = ParseEnumValue(elementType, val);
                                        }

                                        if (parsedValue != null)
                                        {
                                            array.SetValue(parsedValue, index - 1);
                                        }
                                    }
                                    catch (System.Exception e)
                                    {
                                        if (elementType == typeof(SoundKey))
                                            throw new System.FormatException($"Invalid SoundKey '{val}' in {typeof(T).Name}, row {i + 1}, column '{header}'. Conversion aborted before writing files.", e);

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

    private static object ParseEnumValue(System.Type enumType, string value)
    {
        object parsed = value == "_"
            ? System.Enum.ToObject(enumType, 0)
            : System.Enum.Parse(enumType, value);

        // Sound keys are individual registered IDs; other enums retain legacy numeric/flags support.
        if (enumType == typeof(SoundKey) && (value.Contains(",") || !System.Enum.IsDefined(enumType, parsed)))
            throw new System.FormatException($"'{value}' is not a registered SoundKey.");

        return parsed;
    }
}
