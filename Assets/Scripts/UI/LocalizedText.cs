using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private bool _boundsInitialized;

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

        public static void ApplyFont(TMP_Text tmpText)
        {
            if (tmpText == null) return;
            LocalizedText loc = tmpText.GetComponent<LocalizedText>();
            if (loc != null)
            {
                loc.RefreshFont(LocalizationManager.CurrentLanguage);
            }
            else if (tmpText.font != null)
            {
                EnsureAutoFitBoundsStatic(tmpText, tmpText.font, tmpText.text);
                TMP_FontAsset resolved = LocalizationManager.ResolveFont(tmpText.font, LocalizationManager.CurrentLanguage);
                if (resolved != null && tmpText.font != resolved)
                {
                    tmpText.font = resolved;
                }
            }
        }

        private void EnsureAutoFitBounds()
        {
            if (_boundsInitialized || TargetText == null) return;
            _boundsInitialized = true;

            string koRef = !string.IsNullOrEmpty(previewText) ? previewText : TargetText.text;
            EnsureAutoFitBoundsStatic(TargetText, OriginalFont != null ? OriginalFont : TargetText.font, koRef);
        }

        private static void EnsureAutoFitBoundsStatic(TMP_Text tmp, TMP_FontAsset baseFont, string referenceText)
        {
            if (tmp == null) return;

            ContentSizeFitter csf = tmp.GetComponent<ContentSizeFitter>();
            RectTransform rt = tmp.GetComponent<RectTransform>();
            RectTransform prt = rt != null ? rt.parent as RectTransform : null;

            float baseFontSize = tmp.enableAutoSizing && tmp.fontSizeMax > 0f ? tmp.fontSizeMax : tmp.fontSize;

            if (csf != null && csf.enabled && rt != null)
            {
                TMP_FontAsset prevFont = tmp.font;
                bool prevAuto = tmp.enableAutoSizing;
                float prevSize = tmp.fontSize;

                if (baseFont != null) tmp.font = baseFont;
                tmp.enableAutoSizing = false;
                tmp.fontSize = baseFontSize;

                string sample = !string.IsNullOrEmpty(referenceText) ? referenceText : ( !string.IsNullOrEmpty(tmp.text) ? tmp.text : "가나다라" );
                Vector2 pref = tmp.GetPreferredValues(sample, Mathf.Infinity, Mathf.Infinity);

                tmp.font = prevFont;
                tmp.enableAutoSizing = prevAuto;
                tmp.fontSize = prevSize;

                Vector2 newSize = rt.sizeDelta;
                float parentW = prt != null ? prt.rect.width : 0f;
                float parentH = prt != null ? prt.rect.height : 0f;

                if (csf.horizontalFit == ContentSizeFitter.FitMode.PreferredSize && Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x))
                {
                    float targetW = pref.x + 8f;
                    if (parentW > 20f)
                    {
                        if (rt.pivot.x <= 0.1f && rt.anchoredPosition.x >= 0f && rt.anchoredPosition.x < parentW - 20f)
                        {
                            targetW = Mathf.Max(pref.x + 4f, parentW - rt.anchoredPosition.x - 8f);
                            targetW = Mathf.Min(targetW, parentW - rt.anchoredPosition.x - 4f);
                        }
                        else if (Mathf.Abs(rt.pivot.x - 0.5f) < 0.1f && Mathf.Abs(rt.anchoredPosition.x) < 20f)
                        {
                            targetW = Mathf.Max(pref.x + 4f, parentW - 16f);
                        }
                    }
                    newSize.x = Mathf.Max(24f, targetW);
                }

                if (csf.verticalFit == ContentSizeFitter.FitMode.PreferredSize && Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y))
                {
                    float targetH = pref.y + 6f;
                    if (parentH > 20f && tmp.enableWordWrapping && csf.horizontalFit == ContentSizeFitter.FitMode.Unconstrained)
                    {
                        float availH = parentH - Mathf.Abs(rt.anchoredPosition.y) - 4f;
                        if (availH > 20f) targetH = availH;
                    }
                    newSize.y = Mathf.Max(18f, targetH);
                }

                csf.enabled = false;
                rt.sizeDelta = newSize;
            }

            if (!tmp.enableAutoSizing && baseFontSize > 0f)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMax = baseFontSize;
                tmp.fontSizeMin = baseFontSize > 8f
                    ? Mathf.Max(8f, Mathf.Round(baseFontSize * 0.45f))
                    : Mathf.Max(0.1f, Mathf.Round(baseFontSize * 45f) * 0.01f);
            }
            else if (tmp.enableAutoSizing && tmp.fontSizeMax <= 0f && baseFontSize > 0f)
            {
                tmp.fontSizeMax = baseFontSize;
                tmp.fontSizeMin = baseFontSize > 8f
                    ? Mathf.Max(8f, Mathf.Round(baseFontSize * 0.45f))
                    : Mathf.Max(0.1f, Mathf.Round(baseFontSize * 45f) * 0.01f);
            }
        }

        public void RefreshFont(LanguageCode language)
        {
            if (TargetText == null) return;
            if (originalFont == null)
            {
                originalFont = TargetText.font;
            }
            EnsureAutoFitBounds();
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
