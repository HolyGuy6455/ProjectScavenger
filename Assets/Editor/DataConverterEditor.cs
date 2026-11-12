using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Scavenger.Audio;
using Scavenger.Network.DTOs;

public class DataConverterEditor : EditorWindow
{
    private static readonly string[] TabTitles =
    {
        "XLSX ↔ TSV 동기화",
        "TSV → Binary & 서버 배포"
    };

    private int _selectedTab;
    private Vector2 _syncScrollPos;
    private Vector2 _binaryScrollPos;
    private List<XlsxTsvConverter.SheetComparison> _comparisons;

    [MenuItem("Tools/Data/Data Converter (XLSX · TSV · Binary)", priority = 1)]
    public static void ShowWindow()
    {
        var window = GetWindow<DataConverterEditor>("Data Converter");
        window.minSize = new Vector2(620f, 520f);
        window.RefreshComparisons();
    }

    [MenuItem("Tools/Data/One-Click: XLSX -> TSV -> Binary", priority = 20)]
    public static void OneClickXlsxToBinaryMenu()
    {
        int imported = XlsxTsvConverter.ImportAllXlsxToTsv(refreshAssets: true);
        if (imported <= 0) return;
        ConvertAllDataStatic();
        RefreshOpenWindowIfAny();
    }

    [MenuItem("Tools/Data/Sync All XLSX -> TSV", priority = 21)]
    public static void SyncAllXlsxToTsvMenu()
    {
        XlsxTsvConverter.ImportAllXlsxToTsv(refreshAssets: true);
        RefreshOpenWindowIfAny();
    }

    [MenuItem("Tools/Data/Sync All TSV -> XLSX (Preserve Formulas)", priority = 22)]
    public static void SyncAllTsvToXlsxMenu()
    {
        XlsxTsvConverter.ExportAllTsvToXlsx(interactive: false);
        RefreshOpenWindowIfAny();
    }

    [MenuItem("Tools/Data/Convert All TSV -> Binary", priority = 23)]
    public static void ConvertAllTsvToBinaryMenu()
    {
        ConvertAllDataStatic();
    }

    [MenuItem("Tools/Data/Fill CharLvl TSV (Interpolate)", priority = 40)]
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
        RefreshOpenWindowIfAny();
        Debug.Log($"Successfully interpolated CharLvl TSV. Total rows: {newData.Count}");
    }

    [MenuItem("Tools/Data/Copy Master Data To Server", priority = 41)]
    public static void CopyMasterDataToServer()
    {
        // Assuming ScavengerServer is adjacent to the Unity project folder
        string serverResourcesPath = System.IO.Path.GetFullPath(Application.dataPath + "/../../ScavengerServer/Resources/");

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

    private void OnEnable()
    {
        RefreshComparisons();
    }

    private void OnFocus()
    {
        RefreshComparisons();
    }

    private static void RefreshOpenWindowIfAny()
    {
        if (HasOpenInstances<DataConverterEditor>())
        {
            var win = GetWindow<DataConverterEditor>(false, null, false);
            if (win != null)
            {
                win.RefreshComparisons();
                win.Repaint();
            }
        }
    }

    public void RefreshComparisons()
    {
        _comparisons = XlsxTsvConverter.CompareAll();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        // Top Tab Bar
        var tabStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fixedHeight = 28f,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        _selectedTab = GUILayout.Toolbar(_selectedTab, TabTitles, tabStyle, GUILayout.Height(28f));

        EditorGUILayout.Space(6);

        // Main Tab Body (expands to fill window above the bottom One-Click bar)
        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        if (_selectedTab == 0)
        {
            DrawXlsxTsvSyncTab();
        }
        else
        {
            DrawBinaryConvertTab();
        }
        EditorGUILayout.EndVertical();

        // Fixed Bottom One-Click Pipeline Footer
        DrawBottomOneClickFooter();
    }

    private void DrawXlsxTsvSyncTab()
    {
        if (_comparisons == null)
        {
            RefreshComparisons();
        }

        string xlsxPath = XlsxTsvConverter.GetXlsxPath();
        bool xlsxExists = File.Exists(xlsxPath);

        // 1. Excel Path & Status Box
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("엑셀 파일:", GUILayout.Width(58f));
        EditorGUILayout.SelectableLabel(xlsxPath, EditorStyles.textField, GUILayout.Height(18f));

        if (GUILayout.Button("찾기...", GUILayout.Width(54f)))
        {
            string startDir = File.Exists(xlsxPath) ? Path.GetDirectoryName(xlsxPath) : Application.dataPath;
            string picked = EditorUtility.OpenFilePanel("Select Scavenger.xlsx", startDir, "xlsx");
            if (!string.IsNullOrEmpty(picked))
            {
                XlsxTsvConverter.SetXlsxPath(picked);
                RefreshComparisons();
            }
        }

        if (GUILayout.Button("기본값", GUILayout.Width(50f)))
        {
            XlsxTsvConverter.SetXlsxPath(null);
            RefreshComparisons();
        }

        using (new EditorGUI.DisabledScope(!xlsxExists))
        {
            if (GUILayout.Button("엑셀 열기", GUILayout.Width(66f)))
            {
                EditorUtility.OpenWithDefaultApp(xlsxPath);
            }
        }

        if (GUILayout.Button("새로고침", GUILayout.Width(62f)))
        {
            RefreshComparisons();
        }
        EditorGUILayout.EndHorizontal();

        if (!xlsxExists)
        {
            EditorGUILayout.HelpBox("지정된 경로에 Scavenger.xlsx 파일이 없습니다. [찾기...] 버튼으로 엑셀 위치를 지정해 주세요.", MessageType.Warning);
        }
        else if (_comparisons != null)
        {
            int identical = _comparisons.Count(c => c.State == XlsxTsvConverter.SyncState.Identical);
            int commentDiff = _comparisons.Count(c => c.State == XlsxTsvConverter.SyncState.CommentOnlyDiff);
            int dataDiff = _comparisons.Count(c => c.State == XlsxTsvConverter.SyncState.DataDiff);
            int missingInXlsx = _comparisons.Count(c => c.State == XlsxTsvConverter.SyncState.MissingInXlsx);

            string summary = $"총 {_comparisons.Count}개 시트  |  일치 {identical}  ·  데이터 차이 {dataDiff}  ·  수식/주석표시 차이 {commentDiff}  ·  엑셀 미생성 {missingInXlsx}";
            EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("※ TSV → XLSX 내보내기 시 기존 셀 수식(VLOOKUP), 스타일, 보조 시트(DescList, SoundRef)는 덮어쓰지 않고 보존됩니다.", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // 2. Bulk Sync Buttons (Left: XLSX -> TSV, Right: TSV -> XLSX)
        using (new EditorGUI.DisabledScope(!xlsxExists))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 XLSX → TSV 가져오기", GUILayout.Height(28f)))
            {
                if (ConfirmBulkXlsxToTsv())
                {
                    XlsxTsvConverter.ImportAllXlsxToTsv(xlsxPath, refreshAssets: true);
                    RefreshComparisons();
                }
            }

            GUILayout.Space(8f);

            if (GUILayout.Button("전체 TSV → XLSX 내보내기 (수식 보존)", GUILayout.Height(28f)))
            {
                if (ConfirmBulkTsvToXlsx())
                {
                    XlsxTsvConverter.ExportAllTsvToXlsx(xlsxPath, interactive: true);
                    RefreshComparisons();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6);

        // 3. Split Columns for Individual Sheets (Left: XLSX -> TSV, Right: TSV -> XLSX)
        _syncScrollPos = EditorGUILayout.BeginScrollView(_syncScrollPos);
        EditorGUILayout.BeginHorizontal();

        // Left Column: XLSX -> TSV
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("XLSX → TSV (엑셀에서 가져오기)", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        if (_comparisons != null)
        {
            foreach (var comp in _comparisons)
            {
                bool canImport = xlsxExists &&
                                 comp.State != XlsxTsvConverter.SyncState.MissingInXlsx &&
                                 comp.State != XlsxTsvConverter.SyncState.XlsxNotFound;

                using (new EditorGUI.DisabledScope(!canImport))
                {
                    string label = FormatImportButtonLabel(comp);
                    if (GUILayout.Button(label, GUILayout.Height(23f)))
                    {
                        if (ConfirmSingleXlsxToTsv(comp))
                        {
                            XlsxTsvConverter.ImportSheetToTsv(comp.SheetName, xlsxPath, refreshAssets: true);
                            RefreshComparisons();
                        }
                    }
                }
            }
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(6f);

        // Right Column: TSV -> XLSX
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("TSV → XLSX (엑셀로 내보내기 · 수식 보존)", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        if (_comparisons != null)
        {
            foreach (var comp in _comparisons)
            {
                bool canExport = xlsxExists && comp.State != XlsxTsvConverter.SyncState.MissingInTsv;
                bool hasDiff = comp.State == XlsxTsvConverter.SyncState.DataDiff ||
                               comp.State == XlsxTsvConverter.SyncState.CommentOnlyDiff;

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(!canExport))
                {
                    string label = FormatExportButtonLabel(comp);
                    if (GUILayout.Button(label, GUILayout.Height(23f)))
                    {
                        if (ConfirmSingleTsvToXlsx(comp))
                        {
                            XlsxTsvConverter.ExportSingleTsvToXlsx(comp.SheetName, xlsxPath, interactive: true);
                            RefreshComparisons();
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(!hasDiff))
                {
                    if (GUILayout.Button("비교", GUILayout.Width(42f), GUILayout.Height(23f)))
                    {
                        XlsxTsvConverter.LogSheetDiff(comp.SheetName, xlsxPath);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    private void DrawBinaryConvertTab()
    {
        _binaryScrollPos = EditorGUILayout.BeginScrollView(_binaryScrollPos);

        // 1. Convert All TSV -> Binary
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("전체 마스터 데이터 빌드 (Assets/Data/*.tsv → .bytes & .json)", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        if (GUILayout.Button("Convert All TSV → Binary (.bytes & .json)", GUILayout.Height(30f)))
        {
            ConvertAllDataStatic();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // 2. Individual Table Converters (2-Column Grid)
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("개별 테이블 변환 (TSV → Binary)", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        var actions = new (string label, System.Action run)[]
        {
            ("Char Data", () => ConvertData<CharDataList, CharData>("Char", "Scavenger - Char.tsv")),
            ("CharLvl Data", () => ConvertData<CharLvlDataList, CharLvlData>("CharLvl", "Scavenger - CharLvl.tsv")),
            ("Level Data", () => ConvertData<LevelDataList, LevelData>("Level", "Scavenger - Level.tsv")),
            ("Ammo Data", () => ConvertData<AmmoDataList, AmmoData>("Ammo", "Scavenger - Ammo.tsv")),
            ("Skill Data", () => ConvertData<SkillDataList, SkillData>("Skill", "Scavenger - Skill.tsv")),
            ("Buff Data", () => ConvertData<BuffDataList, BuffData>("Buff", "Scavenger - Buff.tsv")),
            ("Item Data", () => ConvertData<ItemDataList, ItemData>("Item", "Scavenger - Item.tsv")),
            ("Loot Data", () => ConvertData<LootDataList, LootData>("Loot", "Scavenger - Loot.tsv")),
            ("Enemy Data", () => ConvertData<EnemyDataList, EnemyData>("Enemy", "Scavenger - Enemy.tsv")),
            ("Obstacle Data", () => ConvertData<ObstacleDataList, ObstacleData>("Obstacle", "Scavenger - Obstacle.tsv")),
            ("UserCurrency Data", () => ConvertData<UserCurrencyDataList, UserCurrencyData>("UserCurrency", "Scavenger - UserCurrency.tsv")),
            ("Quest Data", ConvertQuestDataStatic),
            ("Stage Data", () => ConvertData<StageDataList, StageData>("Stage", "Scavenger - Stage.tsv")),
            ("Localization (KO/EN/JA)", LocalizationEditorUtility.ConvertLocalizationTsvToBytes),
            ("MobGroup (JSON → Bytes)", ConvertMobGroupDataStatic)
        };

        for (int i = 0; i < actions.Length; i += 2)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Convert {actions[i].label}", GUILayout.Height(24f), GUILayout.ExpandWidth(true)))
            {
                actions[i].run();
            }

            if (i + 1 < actions.Length)
            {
                if (GUILayout.Button($"Convert {actions[i + 1].label}", GUILayout.Height(24f), GUILayout.ExpandWidth(true)))
                {
                    actions[i + 1].run();
                }
            }
            else
            {
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // 3. Utilities & Server Sync
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("유틸리티 & 서버 배포", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill CharLvl TSV (키프레임 선형 보간)", GUILayout.Height(26f)))
        {
            FillCharLvlTsv();
        }
        if (GUILayout.Button("Copy Master Data To Server (서버 JSON 복사)", GUILayout.Height(26f)))
        {
            CopyMasterDataToServer();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    private void DrawBottomOneClickFooter()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        string xlsxPath = XlsxTsvConverter.GetXlsxPath();
        bool xlsxExists = File.Exists(xlsxPath);

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.88f, 0.62f);

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };

        using (new EditorGUI.DisabledScope(!xlsxExists))
        {
            if (GUILayout.Button("[원클릭]  XLSX  →  전체 TSV  →  전체 Binary 빌드", btnStyle, GUILayout.Height(36f)))
            {
                if (ConfirmBulkXlsxToTsv())
                {
                    int imported = XlsxTsvConverter.ImportAllXlsxToTsv(xlsxPath, refreshAssets: true);
                    if (imported > 0)
                    {
                        ConvertAllDataStatic();
                        RefreshComparisons();
                    }
                }
            }
        }

        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndVertical();
    }

    private static string FormatImportButtonLabel(XlsxTsvConverter.SheetComparison comp)
    {
        switch (comp.State)
        {
            case XlsxTsvConverter.SyncState.Identical:
                return $"[일치]  {comp.SheetName}";
            case XlsxTsvConverter.SyncState.CommentOnlyDiff:
                return $"[주석차이]  {comp.SheetName}";
            case XlsxTsvConverter.SyncState.DataDiff:
                return $"[차이 {comp.DataDiffRows}행]  {comp.SheetName}  ({comp.XlsxRowCount} → {comp.TsvRowCount}행)";
            case XlsxTsvConverter.SyncState.MissingInXlsx:
                return $"[시트 없음]  {comp.SheetName}";
            case XlsxTsvConverter.SyncState.MissingInTsv:
                return $"[신규 TSV]  {comp.SheetName} ({comp.XlsxRowCount}행)";
            default:
                return comp.SheetName;
        }
    }

    private static string FormatExportButtonLabel(XlsxTsvConverter.SheetComparison comp)
    {
        switch (comp.State)
        {
            case XlsxTsvConverter.SyncState.Identical:
                return $"[일치]  {comp.SheetName}";
            case XlsxTsvConverter.SyncState.CommentOnlyDiff:
                return $"[주석차이]  {comp.SheetName}";
            case XlsxTsvConverter.SyncState.DataDiff:
                return $"[차이 {comp.DataDiffRows}행]  {comp.SheetName}  ({comp.TsvRowCount} → {comp.XlsxRowCount}행)";
            case XlsxTsvConverter.SyncState.MissingInXlsx:
                return $"[시트 신규생성]  {comp.SheetName} ({comp.TsvRowCount}행)";
            case XlsxTsvConverter.SyncState.MissingInTsv:
                return $"[TSV 없음]  {comp.SheetName}";
            default:
                return comp.SheetName;
        }
    }

    private bool ConfirmBulkXlsxToTsv()
    {
        if (_comparisons == null) RefreshComparisons();
        var newerTsvs = _comparisons
            .Where(c => c.State == XlsxTsvConverter.SyncState.DataDiff && c.IsTsvFileNewer)
            .Select(c => c.SheetName)
            .ToList();

        if (newerTsvs.Count == 0) return true;

        string list = string.Join(", ", newerTsvs);
        return EditorUtility.DisplayDialog(
            "최신 TSV 덮어쓰기 확인",
            $"다음 TSV 파일이 엑셀 파일보다 더 최근에 수정되었으며 데이터 차이가 있습니다:\n\n• {list}\n\n엑셀(XLSX) 내용으로 TSV 파일들을 덮어쓰시겠습니까?",
            "덮어쓰기 (XLSX → TSV)",
            "취소");
    }

    private bool ConfirmBulkTsvToXlsx()
    {
        if (_comparisons == null) RefreshComparisons();
        var newerXlsx = _comparisons
            .Where(c => c.State == XlsxTsvConverter.SyncState.DataDiff && c.IsXlsxFileNewer)
            .Select(c => c.SheetName)
            .ToList();

        if (newerXlsx.Count == 0) return true;

        string list = string.Join(", ", newerXlsx);
        return EditorUtility.DisplayDialog(
            "엑셀 업데이트 확인",
            $"Scavenger.xlsx 파일 저장 시각이 다음 TSV보다 최신이며 데이터 차이가 있습니다:\n\n• {list}\n\nTSV 내용으로 엑셀 시트를 업데이트하시겠습니까?\n(기존 VLOOKUP 수식과 보조 시트는 보존됩니다)",
            "업데이트 (TSV → XLSX)",
            "취소");
    }

    private static bool ConfirmSingleXlsxToTsv(XlsxTsvConverter.SheetComparison comp)
    {
        if (comp.State != XlsxTsvConverter.SyncState.DataDiff || !comp.IsTsvFileNewer) return true;

        return EditorUtility.DisplayDialog(
            "최신 TSV 덮어쓰기 확인",
            $"'{comp.SheetName}' TSV 파일이 엑셀보다 더 최근에 수정되었으며 {comp.DataDiffRows}개 행 차이가 있습니다.\n\n엑셀 내용으로 TSV를 덮어쓰시겠습니까?",
            "덮어쓰기",
            "취소");
    }

    private static bool ConfirmSingleTsvToXlsx(XlsxTsvConverter.SheetComparison comp)
    {
        if (comp.State != XlsxTsvConverter.SyncState.DataDiff || !comp.IsXlsxFileNewer) return true;

        return EditorUtility.DisplayDialog(
            "엑셀 시트 업데이트 확인",
            $"엑셀 파일 저장 시각이 '{comp.SheetName}' TSV보다 최신이며 {comp.DataDiffRows}개 행 차이가 있습니다.\n\nTSV 내용으로 엑셀 '{comp.SheetName}' 시트를 업데이트하시겠습니까?\n(기존 수식은 보존됩니다)",
            "업데이트",
            "취소");
    }

    public static void ConvertAllDataStatic()
    {
        LocalizationEditorUtility.ConvertLocalizationTsvToBytes();
        ConvertData<AmmoDataList, AmmoData>("Ammo", "Scavenger - Ammo.tsv");
        ConvertData<BuffDataList, BuffData>("Buff", "Scavenger - Buff.tsv");
        ConvertData<ItemDataList, ItemData>("Item", "Scavenger - Item.tsv");
        ConvertData<UserCurrencyDataList, UserCurrencyData>("UserCurrency", "Scavenger - UserCurrency.tsv");
        ConvertData<CharDataList, CharData>("Char", "Scavenger - Char.tsv");
        ConvertData<CharLvlDataList, CharLvlData>("CharLvl", "Scavenger - CharLvl.tsv");
        ConvertData<LevelDataList, LevelData>("Level", "Scavenger - Level.tsv");
        ConvertData<SkillDataList, SkillData>("Skill", "Scavenger - Skill.tsv");
        ConvertQuestDataStatic();
        ConvertData<StageDataList, StageData>("Stage", "Scavenger - Stage.tsv");

        ConvertData<LootDataList, LootData>("Loot", "Scavenger - Loot.tsv");
        ConvertData<EnemyDataList, EnemyData>("Enemy", "Scavenger - Enemy.tsv");
        ConvertData<ObstacleDataList, ObstacleData>("Obstacle", "Scavenger - Obstacle.tsv");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("All Data Converted Successfully.");
    }

    private static void ConvertMobGroupDataStatic()
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

    private static void ConvertQuestDataStatic()
    {
        ConvertData<QuestDataList, QuestData>("Quest", "Scavenger - Quest.tsv", ParseQuestTsv, ExportQuestJson);
    }

    private static List<QuestData> ParseQuestTsv(string tsvContent)
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

    private static string ExportQuestJson(QuestDataList dataList)
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

    private static void ConvertData<TList, TData>(string dataName, string tsvFileName,
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
        foreach (TData data in list)
        {
            SpriteResourcePaths.Normalize(data);
        }

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
        RegisterDataAssetInAddressables($"Assets/Resources_moved/Data/{dataName}Data.bytes", $"{dataName}Data");
        Debug.Log($"Successfully converted and encrypted {dataName}Data to: {savePath}");
    }

    private static void RegisterDataAssetInAddressables(string assetPath, string address)
    {
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null || settings.DefaultGroup == null) return;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) return;

        var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        if (entry != null && entry.address != address)
        {
            entry.address = address;
            settings.SetDirty(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }
    }

    private static List<T> ParseTsv<T>(string tsvContent) where T : new()
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
