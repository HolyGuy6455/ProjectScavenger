using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Scavenger.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class LocalizationToolWindow : EditorWindow
{
    private class ScannedTmpItem
    {
        public TMP_Text tmp;
        public string relativePath;
        public bool isReferencedByScript;
        public string referencingFieldInfo;
        public LocalizedText localizedText;
        public string pendingKey;
    }

    private class FontUsageLocation
    {
        public string assetPath;
        public string objectPath;
        public string sampleText;
        public bool hasKoreanText;
    }

    private class FontAuditEntry
    {
        public TMP_FontAsset fontAsset;
        public string assetPath;
        public string populationMode;
        public bool hasSourceFontFile;
        public bool supportsKorean;
        public bool supportsJapanese;
        public int bakedCharCount;
        public int bakedKoreanCount;
        public int totalUseCount;
        public int koreanTextUseCount;
        public int englishOrNumberUseCount;
        public bool expanded;
        public TMP_FontAsset replaceTargetFont;
        public readonly List<FontUsageLocation> usages = new List<FontUsageLocation>();
    }

    private int _selectedTab;
    private readonly string[] _tabNames =
    {
        "1. 언어 전환 & TMP 스캐너",
        "2. 프로젝트 폰트 전수 조사 (Auditor)",
        "3. 언어별 폰트 매핑 설정 (Font Map)"
    };

    // Tab 1 state
    private GameObject _scanTarget;
    private readonly List<ScannedTmpItem> _scannedItems = new List<ScannedTmpItem>();
    private Vector2 _tab1ScrollPos;
    private bool _hideScriptReferenced;

    // Tab 2 state
    private readonly List<FontAuditEntry> _fontAuditList = new List<FontAuditEntry>();
    private Vector2 _tab2ScrollPos;
    private bool _onlyShowUsedFonts = true;
    private bool _onlyShowProblemFonts;
    private bool _excludeFigmaFolder = true;

    // Tab 3 state
    private LocalizationFontConfig _fontConfig;
    private SerializedObject _fontConfigSerialized;
    private Vector2 _tab3ScrollPos;

    [MenuItem("Tools/Data/Localization Manager Window")]
    public static void ShowWindow()
    {
        var window = GetWindow<LocalizationToolWindow>("Localization Tool");
        window.minSize = new Vector2(720, 520);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        DrawLanguageToolbarHeader();
        EditorGUILayout.Space(6);

        _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(28));
        EditorGUILayout.Space(6);

        switch (_selectedTab)
        {
            case 0:
                DrawTabTmpScanner();
                break;
            case 1:
                DrawTabFontAuditor();
                break;
            case 2:
                DrawTabFontMappingConfig();
                break;
        }
    }

    private void DrawLanguageToolbarHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"현재 활성 언어: {LocalizationManager.CurrentLanguage}", EditorStyles.boldLabel, GUILayout.Width(170));
        if (GUILayout.Button("KO (한국어)")) SetLanguageAndPreview(LanguageCode.KO);
        if (GUILayout.Button("EN (영어)")) SetLanguageAndPreview(LanguageCode.EN);
        if (GUILayout.Button("JA (일본어)")) SetLanguageAndPreview(LanguageCode.JA);
        GUILayout.Space(10);
        if (GUILayout.Button("TSV 캐시 새로고침"))
        {
            LocalizationManager.ReloadEditorTsvCache();
            Debug.Log("[LocalizationTool] Editor TSV Cache reloaded.");
        }
        if (GUILayout.Button("TSV -> .bytes 빌드"))
        {
            LocalizationEditorUtility.ConvertLocalizationTsvToBytes();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawTabTmpScanner()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("프리팹 / 씬 TMP 스캐너 (고정 UI vs 코드 제어 분류)", EditorStyles.boldLabel);
        _scanTarget = (GameObject)EditorGUILayout.ObjectField("스캔 대상 (비우면 현재 프리팹/선택 오브젝트)", _scanTarget, typeof(GameObject), true);
        _hideScriptReferenced = EditorGUILayout.Toggle("코드 제어(SerializeField) TMP 숨기기", _hideScriptReferenced);

        if (GUILayout.Button("TMP 컴포넌트 스캔하기", GUILayout.Height(26)))
        {
            ScanTargetTmpComponents();
        }
        EditorGUILayout.EndVertical();

        if (_scannedItems.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"스캔 결과: {_scannedItems.Count}개 TMP 발견", EditorStyles.boldLabel);
            _tab1ScrollPos = EditorGUILayout.BeginScrollView(_tab1ScrollPos);

            foreach (var item in _scannedItems)
            {
                if (item.tmp == null) continue;
                if (_hideScriptReferenced && item.isReferencedByScript) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                string badge = item.isReferencedByScript
                    ? $"[Code 제어: {item.referencingFieldInfo}]"
                    : "[고정 UI 후보]";

                if (GUILayout.Button(item.relativePath, EditorStyles.miniButtonLeft, GUILayout.Width(220)))
                {
                    Selection.activeGameObject = item.tmp.gameObject;
                    EditorGUIUtility.PingObject(item.tmp.gameObject);
                }

                EditorGUILayout.LabelField(badge, GUILayout.Width(210));
                string fontName = item.tmp.font != null ? item.tmp.font.name : "(None)";
                EditorGUILayout.LabelField($"Font: {fontName}", GUILayout.Width(170));
                EditorGUILayout.SelectableLabel(item.tmp.text?.Replace("\n", "\\n") ?? string.Empty, GUILayout.Height(18));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                item.pendingKey = EditorGUILayout.TextField("번역키", item.pendingKey);

                string btnLabel = item.localizedText != null ? "키 변경 + TSV 등록" : "LocalizedText 부착 + TSV 등록";
                GUI.enabled = !string.IsNullOrEmpty(item.pendingKey);
                if (GUILayout.Button(btnLabel, GUILayout.Width(190)))
                {
                    ApplyLocalizedTextAndRegister(item);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawTabFontAuditor()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("프로젝트 전체 TMP 폰트 사용처 & 언어 지원 전수 조사", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "모든 프리팹에서 어떤 TMP 폰트가 몇 번 쓰였는지, 영문 전용 폰트에 한글이 잘못 들어가 깨지는 곳은 없는지 한눈에 진단하고 일괄 교체할 수 있습니다.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        _excludeFigmaFolder = EditorGUILayout.ToggleLeft("Assets/Figma 폴더(원본 임포트용) 제외", _excludeFigmaFolder, GUILayout.Width(240));
        _onlyShowUsedFonts = EditorGUILayout.ToggleLeft("사용 1회 이상만 보기", _onlyShowUsedFonts, GUILayout.Width(150));
        _onlyShowProblemFonts = EditorGUILayout.ToggleLeft("한글 깨짐 위험 폰트만 보기", _onlyShowProblemFonts, GUILayout.Width(190));
        if (GUILayout.Button("프로젝트 전체 폰트 스캔 실행", GUILayout.Height(26)))
        {
            RunProjectFontAudit();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        if (_fontAuditList.Count == 0)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("상단의 '프로젝트 전체 폰트 스캔 실행' 버튼을 눌러주세요.");
            return;
        }

        _tab2ScrollPos = EditorGUILayout.BeginScrollView(_tab2ScrollPos);

        foreach (var entry in _fontAuditList)
        {
            if (entry.fontAsset == null) continue;
            if (_onlyShowUsedFonts && entry.totalUseCount == 0) continue;
            bool hasKoreanMismatch = !entry.supportsKorean && entry.koreanTextUseCount > 0;
            if (_onlyShowProblemFonts && !hasKoreanMismatch) continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(entry.expanded ? "▼" : "▶", GUILayout.Width(24)))
            {
                entry.expanded = !entry.expanded;
            }

            if (GUILayout.Button(entry.fontAsset.name, EditorStyles.boldLabel, GUILayout.Width(210)))
            {
                Selection.activeObject = entry.fontAsset;
                EditorGUIUtility.PingObject(entry.fontAsset);
            }

            string langSpec = entry.supportsKorean ? "[한/영 가능]" : "[영문/숫자 전용]";
            if (entry.supportsJapanese) langSpec += "[일 가능]";
            string modeSpec = entry.hasSourceFontFile
                ? $"{entry.populationMode}(TTF연결됨)"
                : $"{entry.populationMode}(TTF끊김/Baked:{entry.bakedCharCount}자)";

            EditorGUILayout.LabelField($"{langSpec} | {modeSpec}", GUILayout.Width(250));
            EditorGUILayout.LabelField($"총 {entry.totalUseCount}회 (한글:{entry.koreanTextUseCount} / 영·숫자:{entry.englishOrNumberUseCount})", GUILayout.Width(220));
            EditorGUILayout.EndHorizontal();

            if (hasKoreanMismatch)
            {
                EditorGUILayout.HelpBox(
                    $"경고: '{entry.fontAsset.name}' 폰트는 한글 글리프가 없는 영문 전용 폰트인데, 한글 텍스트 {entry.koreanTextUseCount}곳에 지정되어 있어 한글이 깨집니다!",
                    MessageType.Warning);
            }

            if (entry.expanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                entry.replaceTargetFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                    "교체할 대상 폰트", entry.replaceTargetFont, typeof(TMP_FontAsset), false);

                GUI.enabled = entry.replaceTargetFont != null && entry.replaceTargetFont != entry.fontAsset;
                if (GUILayout.Button($"한글 포함({entry.koreanTextUseCount}곳)만 교체", GUILayout.Width(160)))
                {
                    BatchReplaceFontInPrefabs(entry, onlyKoreanTexts: true);
                }
                if (GUILayout.Button($"전체({entry.totalUseCount}곳) 일괄 교체", GUILayout.Width(150)))
                {
                    BatchReplaceFontInPrefabs(entry, onlyKoreanTexts: false);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
                int maxShow = Mathf.Min(entry.usages.Count, 50);
                for (int i = 0; i < maxShow; i++)
                {
                    var u = entry.usages[i];
                    EditorGUILayout.BeginHorizontal();
                    string tag = u.hasKoreanText ? "[한글]" : "[영/숫]";
                    if (GUILayout.Button(Path.GetFileName(u.assetPath), EditorStyles.miniButtonLeft, GUILayout.Width(210)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(u.assetPath);
                        if (asset != null)
                        {
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                        }
                    }
                    EditorGUILayout.LabelField($"{tag} {u.objectPath}", GUILayout.Width(260));
                    EditorGUILayout.SelectableLabel(u.sampleText, GUILayout.Height(18));
                    EditorGUILayout.EndHorizontal();
                }

                if (entry.usages.Count > maxShow)
                {
                    EditorGUILayout.LabelField($"... 외 {entry.usages.Count - maxShow}개 더 있음");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTabFontMappingConfig()
    {
        EnsureFontConfigLoaded();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("언어별 폰트 1:1 매핑 설정 (Resources/LocalizationFontConfig.asset)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "번역키마다 폰트를 지정하는 대신, UI가 원래 사용하던 '원본 폰트(Source Font)'를 기준으로 각 언어(KO/EN/JA) 전환 시 대체할 짝꿍 폰트를 매핑합니다. 비워둔 칸은 원본 폰트를 그대로 유지합니다.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("폰트 매핑 에셋 선택 (Ping)", GUILayout.Height(24)))
        {
            Selection.activeObject = _fontConfig;
            EditorGUIUtility.PingObject(_fontConfig);
        }
        if (GUILayout.Button("프로젝트 주요 사용 폰트 자동 목록 채우기", GUILayout.Height(24)))
        {
            AutoPopulateFontMappingRules();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        if (_fontConfig == null) return;

        if (_fontConfigSerialized == null || _fontConfigSerialized.targetObject != _fontConfig)
        {
            _fontConfigSerialized = new SerializedObject(_fontConfig);
        }

        _fontConfigSerialized.Update();
        _tab3ScrollPos = EditorGUILayout.BeginScrollView(_tab3ScrollPos);

        EditorGUILayout.PropertyField(_fontConfigSerialized.FindProperty("defaultJaFallbackFont"),
            new GUIContent("일본어(JA) 기본 대체 폰트"));
        EditorGUILayout.Space(6);
        EditorGUILayout.PropertyField(_fontConfigSerialized.FindProperty("rules"),
            new GUIContent("원본 폰트별 매핑 규칙 (Rules)"), true);

        EditorGUILayout.EndScrollView();

        if (_fontConfigSerialized.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_fontConfig);
        }
    }

    private void EnsureFontConfigLoaded()
    {
        if (_fontConfig != null) return;

        string assetPath = "Assets/Resources/LocalizationFontConfig.asset";
        _fontConfig = AssetDatabase.LoadAssetAtPath<LocalizationFontConfig>(assetPath);
        if (_fontConfig == null)
        {
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }
            _fontConfig = CreateInstance<LocalizationFontConfig>();
            AssetDatabase.CreateAsset(_fontConfig, assetPath);
            AssetDatabase.SaveAssets();
        }
        LocalizationManager.FontConfig = _fontConfig;
    }

    private void AutoPopulateFontMappingRules()
    {
        EnsureFontConfigLoaded();
        if (_fontAuditList.Count == 0)
        {
            RunProjectFontAudit();
        }

        Undo.RecordObject(_fontConfig, "Auto Populate Font Mapping Rules");
        foreach (var audit in _fontAuditList)
        {
            if (audit.fontAsset == null || audit.totalUseCount == 0) continue;
            bool exists = _fontConfig.rules.Exists(r => r != null && r.sourceFont == audit.fontAsset);
            if (!exists)
            {
                string memo = audit.supportsKorean
                    ? $"한글 가능 ({audit.totalUseCount}회 사용)"
                    : $"영문 전용 ({audit.totalUseCount}회 사용, 한글:{audit.koreanTextUseCount})";

                _fontConfig.rules.Add(new LocalizationFontConfig.FontMappingRule
                {
                    sourceFont = audit.fontAsset,
                    memo = memo
                });
            }
        }

        EditorUtility.SetDirty(_fontConfig);
        AssetDatabase.SaveAssets();
    }

    private void RunProjectFontAudit()
    {
        _fontAuditList.Clear();
        var fontMap = new Dictionary<TMP_FontAsset, FontAuditEntry>();

        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" });
        foreach (string guid in fontGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null) continue;

            int charCount = fa.characterTable != null ? fa.characterTable.Count : 0;
            int korBaked = 0;
            int jpBaked = 0;
            if (fa.characterTable != null)
            {
                foreach (var ch in fa.characterTable)
                {
                    uint u = ch.unicode;
                    if (u >= 0xAC00 && u <= 0xD7A3) korBaked++;
                    if (u >= 0x3040 && u <= 0x30FF) jpBaked++;
                }
            }

            bool hasSource = fa.sourceFontFile != null;
            bool supportsKor = korBaked > 0 || (hasSource && fa.sourceFontFile.HasCharacter('가'));
            bool supportsJp = jpBaked > 0 || (hasSource && fa.sourceFontFile.HasCharacter('あ'));

            var entry = new FontAuditEntry
            {
                fontAsset = fa,
                assetPath = path,
                populationMode = fa.atlasPopulationMode.ToString(),
                hasSourceFontFile = hasSource,
                supportsKorean = supportsKor,
                supportsJapanese = supportsJp,
                bakedCharCount = charCount,
                bakedKoreanCount = korBaked
            };

            fontMap[fa] = entry;
            _fontAuditList.Add(entry);
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string pGuid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(pGuid);
            if (path.Contains("_Recovery")) continue;
            if (_excludeFigmaFolder && path.StartsWith("Assets/Figma/", StringComparison.OrdinalIgnoreCase)) continue;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            var tmps = go.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in tmps)
            {
                if (tmp == null || tmp.font == null) continue;
                if (!fontMap.TryGetValue(tmp.font, out var entry)) continue;

                string txt = tmp.text ?? string.Empty;
                bool hasKor = ContainsKorean(txt);

                entry.totalUseCount++;
                if (hasKor) entry.koreanTextUseCount++;
                else entry.englishOrNumberUseCount++;

                entry.usages.Add(new FontUsageLocation
                {
                    assetPath = path,
                    objectPath = GetRelativePath(go.transform, tmp.transform),
                    sampleText = txt.Replace("\r", "").Replace("\n", "\\n"),
                    hasKoreanText = hasKor
                });
            }
        }

        _fontAuditList.Sort((a, b) => b.totalUseCount.CompareTo(a.totalUseCount));
    }

    private void BatchReplaceFontInPrefabs(FontAuditEntry entry, bool onlyKoreanTexts)
    {
        if (entry == null || entry.fontAsset == null || entry.replaceTargetFont == null) return;

        var prefabPaths = new HashSet<string>();
        foreach (var u in entry.usages)
        {
            if (!onlyKoreanTexts || u.hasKoreanText)
            {
                prefabPaths.Add(u.assetPath);
            }
        }

        int replacedCount = 0;
        foreach (string path in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var tmps = root.GetComponentsInChildren<TMP_Text>(true);
            bool modified = false;

            foreach (var tmp in tmps)
            {
                if (tmp != null && tmp.font == entry.fontAsset)
                {
                    if (!onlyKoreanTexts || ContainsKorean(tmp.text))
                    {
                        tmp.font = entry.replaceTargetFont;
                        var loc = tmp.GetComponent<LocalizedText>();
                        if (loc != null)
                        {
                            loc.OriginalFont = entry.replaceTargetFont;
                        }
                        modified = true;
                        replacedCount++;
                    }
                }
            }

            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[LocalizationTool] '{entry.fontAsset.name}' -> '{entry.replaceTargetFont.name}' 일괄 교체 완료 ({replacedCount}개 TMP 변경).");
        RunProjectFontAudit();
    }

    private static bool ContainsKorean(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x3131 && c <= 0x318E))
            {
                return true;
            }
        }
        return false;
    }

    private void SetLanguageAndPreview(LanguageCode lang)
    {
        LocalizationManager.SetLanguage(lang);
        if (!Application.isPlaying)
        {
            var allLocalized = FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var loc in allLocalized)
            {
                if (loc != null && loc.TargetText != null)
                {
                    loc.RefreshFont(lang);
                    if (!string.IsNullOrEmpty(loc.LocalizationKey))
                    {
                        loc.TargetText.text = LocalizationManager.GetEditorPreview(loc.LocalizationKey, lang);
                    }
                    EditorUtility.SetDirty(loc.TargetText);
                }
            }
            SceneView.RepaintAll();
        }
    }

    private void ScanTargetTmpComponents()
    {
        _scannedItems.Clear();

        GameObject root = _scanTarget;
        if (root == null)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                root = prefabStage.prefabContentsRoot;
            }
            else if (Selection.activeGameObject != null)
            {
                root = Selection.activeGameObject;
            }
        }

        if (root == null)
        {
            Debug.LogWarning("[LocalizationTool] 스캔할 대상 오브젝트를 선택하거나 프리팹 편집 화면을 열어주세요.");
            return;
        }

        var fieldMap = BuildReferencedTmpMap(root);
        var tmps = root.GetComponentsInChildren<TMP_Text>(true);

        foreach (var tmp in tmps)
        {
            var loc = tmp.GetComponent<LocalizedText>();
            bool isRef = fieldMap.TryGetValue(tmp, out string refInfo);

            _scannedItems.Add(new ScannedTmpItem
            {
                tmp = tmp,
                relativePath = GetRelativePath(root.transform, tmp.transform),
                isReferencedByScript = isRef,
                referencingFieldInfo = refInfo,
                localizedText = loc,
                pendingKey = loc != null ? loc.LocalizationKey : string.Empty
            });
        }
    }

    private void ApplyLocalizedTextAndRegister(ScannedTmpItem item)
    {
        if (item.tmp == null || string.IsNullOrEmpty(item.pendingKey)) return;

        string key = item.pendingKey.Trim();
        string currentText = item.tmp.text != null ? item.tmp.text.Trim() : string.Empty;
        string location = LocalizedTextEditor.DetectLocationName(item.tmp.gameObject);

        LocalizationEditorUtility.UpsertEntry(
            key: key,
            sourceType: "UI",
            location: location,
            memo: item.relativePath,
            ko: currentText
        );

        var loc = item.tmp.GetComponent<LocalizedText>();
        if (loc == null)
        {
            loc = Undo.AddComponent<LocalizedText>(item.tmp.gameObject);
        }

        Undo.RecordObject(loc, "Assign Localization Key");
        if (loc.OriginalFont == null) loc.OriginalFont = item.tmp.font;
        loc.PreviewText = currentText;
        loc.LocalizationKey = key;
        loc.SyncEditorPreviewFromTsv();
        EditorUtility.SetDirty(loc);
        EditorUtility.SetDirty(item.tmp.gameObject);
        item.localizedText = loc;
    }

    private static Dictionary<TMP_Text, string> BuildReferencedTmpMap(GameObject root)
    {
        var map = new Dictionary<TMP_Text, string>();
        var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var mb in behaviours)
        {
            if (mb == null || mb is TMP_Text || mb is LocalizedText) continue;
            Type type = mb.GetType();
            foreach (var field in type.GetFields(flags))
            {
                if (typeof(TMP_Text).IsAssignableFrom(field.FieldType))
                {
                    if (field.GetValue(mb) is TMP_Text tmp && tmp != null)
                    {
                        map[tmp] = $"{type.Name}.{field.Name}";
                    }
                }
            }
        }
        return map;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root) return root.name;
        var parts = new List<string>();
        Transform cur = target;
        while (cur != null && cur != root)
        {
            parts.Add(cur.name);
            cur = cur.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }
}
