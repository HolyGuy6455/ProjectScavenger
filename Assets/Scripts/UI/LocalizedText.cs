using System;
using TMPro;
using UnityEngine;

namespace Scavenger.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TMP_FontAsset originalFont;
        [SerializeField] private string localizationKey;
        [SerializeField, TextArea(1, 3)] private string previewText;

        private object[] _runtimeArgs;

        public TMP_Text TargetText
        {
            get
            {
                if (targetText == null)
                {
                    targetText = GetComponent<TMP_Text>();
                }
                return targetText;
            }
        }

        public TMP_FontAsset OriginalFont
        {
            get
            {
                if (originalFont == null && TargetText != null)
                {
                    originalFont = TargetText.font;
                }
                return originalFont;
            }
            set => originalFont = value;
        }

        public string LocalizationKey
        {
            get => localizationKey;
            set => SetKey(value);
        }

        public string PreviewText
        {
            get => previewText;
            set
            {
                previewText = value;
                if (!Application.isPlaying && TargetText != null)
                {
                    TargetText.text = previewText;
                }
            }
        }

        private void Awake()
        {
            if (targetText == null)
            {
                targetText = GetComponent<TMP_Text>();
            }
            if (originalFont == null && targetText != null)
            {
                originalFont = targetText.font;
            }
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshText;
            if (Application.isPlaying)
            {
                RefreshText();
            }
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshText;
        }

        public void SetKey(string key, params object[] args)
        {
            localizationKey = key;
            _runtimeArgs = args != null && args.Length > 0 ? args : null;
            RefreshText();
        }

        public void SetArgs(params object[] args)
        {
            _runtimeArgs = args != null && args.Length > 0 ? args : null;
            RefreshText();
        }

        public void ClearKey()
        {
            localizationKey = string.Empty;
            _runtimeArgs = null;
            if (TargetText != null)
            {
                TargetText.text = string.Empty;
            }
        }

        public void RefreshFont(LanguageCode language)
        {
            if (TargetText == null) return;
            if (originalFont == null)
            {
                originalFont = TargetText.font;
            }
            if (originalFont != null)
            {
                TMP_FontAsset resolved = LocalizationManager.ResolveFont(originalFont, language);
                if (resolved != null && TargetText.font != resolved)
                {
                    TargetText.font = resolved;
                }
            }
        }

        public void RefreshText()
        {
            if (TargetText == null)
            {
                return;
            }

            RefreshFont(LocalizationManager.CurrentLanguage);

            if (string.IsNullOrEmpty(localizationKey))
            {
                return;
            }

            if (Application.isPlaying)
            {
                TargetText.text = _runtimeArgs != null && _runtimeArgs.Length > 0
                    ? LocalizationManager.Get(localizationKey, _runtimeArgs)
                    : LocalizationManager.Get(localizationKey);
            }
#if UNITY_EDITOR
            else
            {
                SyncEditorPreviewFromTsv();
            }
#endif
        }

#if UNITY_EDITOR
        private void Reset()
        {
            targetText = GetComponent<TMP_Text>();
            if (targetText != null)
            {
                if (originalFont == null) originalFont = targetText.font;
                if (string.IsNullOrEmpty(previewText)) previewText = targetText.text;
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;

            if (targetText == null)
            {
                targetText = GetComponent<TMP_Text>();
            }

            if (targetText == null) return;

            if (originalFont == null && targetText.font != null)
            {
                originalFont = targetText.font;
            }

            if (!string.IsNullOrEmpty(localizationKey) &&
                LocalizationManager.TryGetFromEditorTsv(localizationKey, LanguageCode.KO, out string koText))
            {
                previewText = koText;
                targetText.text = koText;
            }
            else if (!string.IsNullOrEmpty(previewText))
            {
                targetText.text = previewText;
            }
            else
            {
                previewText = targetText.text;
            }
        }

        public void SyncEditorPreviewFromTsv()
        {
            if (TargetText == null || string.IsNullOrEmpty(localizationKey)) return;

            if (LocalizationManager.TryGetFromEditorTsv(localizationKey, LanguageCode.KO, out string koText))
            {
                previewText = koText;
                TargetText.text = koText;
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(TargetText);
            }
        }
#endif
    }
}
