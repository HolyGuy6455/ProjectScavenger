using Scavenger.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(LocalizedText))]
public class LocalizedTextEditor : Editor
{
    private SerializedProperty _targetTextProp;
    private SerializedProperty _originalFontProp;
    private SerializedProperty _localizationKeyProp;
    private SerializedProperty _previewTextProp;
    private string _memoInput = string.Empty;

    private void OnEnable()
    {
        _targetTextProp = serializedObject.FindProperty("targetText");
        _originalFontProp = serializedObject.FindProperty("originalFont");
        _localizationKeyProp = serializedObject.FindProperty("localizationKey");
        _previewTextProp = serializedObject.FindProperty("previewText");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var comp = (LocalizedText)target;

        EditorGUILayout.PropertyField(_targetTextProp, new GUIContent("Target TMP"));
        EditorGUILayout.PropertyField(_originalFontProp, new GUIContent("원본 폰트 (Original Font)"));

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_localizationKeyProp, new GUIContent("번역키 (Key)"));
        bool keyChanged = EditorGUI.EndChangeCheck();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_previewTextProp, new GUIContent("에디터 표시값 (Preview)"));
        bool previewChanged = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();

        string key = _localizationKeyProp.stringValue != null ? _localizationKeyProp.stringValue.Trim() : string.Empty;

        if (keyChanged && !string.IsNullOrEmpty(key))
        {
            if (LocalizationManager.TryGetFromEditorTsv(key, LanguageCode.KO, out string koVal))
            {
                Undo.RecordObject(comp, "Sync LocalizedText Preview");
                if (comp.TargetText != null) Undo.RecordObject(comp.TargetText, "Sync TMP Preview");
                comp.PreviewText = koVal;
                EditorUtility.SetDirty(comp);
                if (comp.TargetText != null) EditorUtility.SetDirty(comp.TargetText);
            }
        }
        else if (previewChanged && !Application.isPlaying)
        {
            if (comp.TargetText != null)
            {
                Undo.RecordObject(comp.TargetText, "Update TMP Preview Text");
                comp.TargetText.text = _previewTextProp.stringValue;
                EditorUtility.SetDirty(comp.TargetText);
            }
        }

        EditorGUILayout.Space(6);

        // TSV Status Box
        if (string.IsNullOrEmpty(key))
        {
            EditorGUILayout.HelpBox("번역키가 비어 있습니다. 키를 입력하거나 TSV에 등록하세요.", MessageType.Info);
        }
        else if (LocalizationEditorUtility.TryGetRow(key, out var row))
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"TSV 등록됨 [{row.sourceType}] {row.location}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("KO", string.IsNullOrEmpty(row.ko) ? "(비어있음)" : row.ko);
            EditorGUILayout.LabelField("EN", string.IsNullOrEmpty(row.en) ? "(비어있음)" : row.en);
            EditorGUILayout.LabelField("JA", string.IsNullOrEmpty(row.ja) ? "(비어있음)" : row.ja);
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox($"'{key}' 키가 아직 Scavenger - Localization.tsv에 없습니다.\n아래 버튼으로 현재 표시값을 바로 TSV에 등록할 수 있습니다.", MessageType.Warning);
        }

        EditorGUILayout.Space(4);
        _memoInput = EditorGUILayout.TextField("TSV 메모 (#memo)", _memoInput);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !string.IsNullOrEmpty(key);
        if (GUILayout.Button("TSV에서 KO 불러오기"))
        {
            LocalizationManager.ReloadEditorTsvCache();
            comp.SyncEditorPreviewFromTsv();
        }

        if (GUILayout.Button("현재 키 & 표시값을 TSV에 등록/수정"))
        {
            string preview = !string.IsNullOrEmpty(comp.PreviewText)
                ? comp.PreviewText
                : (comp.TargetText != null ? comp.TargetText.text : string.Empty);

            string location = DetectLocationName(comp.gameObject);
            string memo = string.IsNullOrEmpty(_memoInput) ? comp.gameObject.name : _memoInput;

            LocalizationEditorUtility.UpsertEntry(
                key: key,
                sourceType: "UI",
                location: location,
                memo: memo,
                ko: preview
            );

            comp.SyncEditorPreviewFromTsv();
            Debug.Log($"[LocalizedTextEditor] '{key}' saved to Scavenger - Localization.tsv (KO='{preview}', Location='{location}').");
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("언어 미리보기");
        if (GUILayout.Button("KO")) PreviewLanguage(comp, LanguageCode.KO);
        if (GUILayout.Button("EN")) PreviewLanguage(comp, LanguageCode.EN);
        if (GUILayout.Button("JA")) PreviewLanguage(comp, LanguageCode.JA);
        EditorGUILayout.EndHorizontal();
    }

    private static void PreviewLanguage(LocalizedText comp, LanguageCode lang)
    {
        if (Application.isPlaying)
        {
            LocalizationManager.SetLanguage(lang);
            return;
        }

        if (comp.TargetText == null) return;
        Undo.RecordObject(comp.TargetText, "Preview Localized Language");
        comp.RefreshFont(lang);
        if (!string.IsNullOrEmpty(comp.LocalizationKey))
        {
            comp.TargetText.text = LocalizationManager.GetEditorPreview(comp.LocalizationKey, lang);
        }
        EditorUtility.SetDirty(comp.TargetText);
    }

    public static string DetectLocationName(GameObject go)
    {
        if (go == null) return string.Empty;

        var prefabStage = PrefabStageUtility.GetPrefabStage(go);
        if (prefabStage != null && !string.IsNullOrEmpty(prefabStage.assetPath))
        {
            return System.IO.Path.GetFileName(prefabStage.assetPath);
        }

        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        if (!string.IsNullOrEmpty(assetPath))
        {
            return System.IO.Path.GetFileName(assetPath);
        }

        if (go.scene.IsValid() && !string.IsNullOrEmpty(go.scene.name))
        {
            return $"{go.scene.name}.unity";
        }

        return go.name;
    }
}

