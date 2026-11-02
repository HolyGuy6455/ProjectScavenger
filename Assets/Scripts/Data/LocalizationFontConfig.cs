using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalizationFontConfig", menuName = "Scavenger/Localization Font Config")]
public class LocalizationFontConfig : ScriptableObject
{
    public const string ResourceLoadPath = "LocalizationFontConfig";

    [Serializable]
    public class FontMappingRule
    {
        [Tooltip("UI(TMP)에 원래 지정되어 있던 원본 폰트 에셋")]
        public TMP_FontAsset sourceFont;

        [Tooltip("용도 및 스타일 메모 (예: 본문 고딕 Bold, 숫자 전용 등)")]
        public string memo;

        [Tooltip("한국어(KO)일 때 대체할 폰트 (비워두면 sourceFont 그대로 사용)")]
        public TMP_FontAsset koFont;

        [Tooltip("영어(EN)일 때 대체할 폰트 (비워두면 sourceFont 그대로 사용)")]
        public TMP_FontAsset enFont;

        [Tooltip("일본어(JA)일 때 대체할 폰트 (비워두면 글로벌 JA 기본 폰트 또는 sourceFont 사용)")]
        public TMP_FontAsset jaFont;

        public TMP_FontAsset GetOverrideForLanguage(LanguageCode language)
        {
            return language switch
            {
                LanguageCode.KO => koFont,
                LanguageCode.EN => enFont,
                LanguageCode.JA => jaFont,
                _ => null
            };
        }
    }

    [Header("언어별 전역 기본 대체 폰트 (매핑 규칙에 개별 지정이 없을 때 선택적 사용)")]
    [Tooltip("일본어(JA)에서 개별 매핑이 비어있을 때 일괄 적용할 기본 일본어 폰트 (비워두면 원본 폰트 유지)")]
    public TMP_FontAsset defaultJaFallbackFont;

    [Header("원본 폰트 1:1 언어별 매핑 테이블")]
    public List<FontMappingRule> rules = new List<FontMappingRule>();

    public TMP_FontAsset ResolveFont(TMP_FontAsset originalFont, LanguageCode language)
    {
        if (originalFont == null) return null;

        if (rules != null)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule != null && rule.sourceFont == originalFont)
                {
                    TMP_FontAsset mapped = rule.GetOverrideForLanguage(language);
                    if (mapped != null)
                    {
                        return mapped;
                    }
                    break;
                }
            }
        }

        if (language == LanguageCode.JA && defaultJaFallbackFont != null)
        {
            return defaultJaFallbackFont;
        }

        return originalFont;
    }
}

