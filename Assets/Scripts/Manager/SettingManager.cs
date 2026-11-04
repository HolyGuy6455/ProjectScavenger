using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Manager
{
    /// <summary>
    /// 게임 전역 설정(언어, 사운드 등)을 관리하는 매니저 클래스입니다.
    /// SettingPanel이 열리지 않은 상태에서도 게임 시작 즉시 기존 설정을 불러와 유지 및 적용합니다.
    /// </summary>
    public static class SettingManager
    {
        // TODO: 추후 계정 연동(서버 업로드/동기화) 기능 추가 필요함.
        // 현재는 플레이어 기기 귀속(PlayerPrefs) 기반으로 설정을 읽고 저장합니다.

        private const string LanguagePrefKey = "Scavenger_Language";

        public const LanguageCode DefaultLanguage = LanguageCode.KO;

        private static readonly LanguageCode[] SupportedLanguages =
        {
            LanguageCode.KO,
            LanguageCode.EN,
            LanguageCode.JA
        };

        private static readonly string[] LanguageDisplayNames =
        {
            "한국어",
            "English",
            "日本語"
        };

        private static bool _initialized;
        private static LanguageCode _language = DefaultLanguage;

        public static event Action OnSettingsChanged;

        public static LanguageCode Language
        {
            get
            {
                EnsureInitialized();
                return _language;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            _initialized = false;
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            LoadFromDevice();
            ApplyToSystems();
        }

        public static IList<string> GetLanguageDisplayNames()
        {
            return LanguageDisplayNames;
        }

        public static int GetLanguageIndex(LanguageCode language)
        {
            for (int i = 0; i < SupportedLanguages.Length; i++)
            {
                if (SupportedLanguages[i] == language)
                {
                    return i;
                }
            }
            return 0;
        }

        public static LanguageCode GetLanguageByIndex(int index)
        {
            if (index < 0 || index >= SupportedLanguages.Length)
            {
                return DefaultLanguage;
            }
            return SupportedLanguages[index];
        }

        public static void SetLanguage(LanguageCode language, bool saveImmediately = true)
        {
            EnsureInitialized();
            _language = language;

            if (saveImmediately)
            {
                SaveToDevice();
            }

            ApplyToSystems();
            OnSettingsChanged?.Invoke();
        }

        public static void ResetToDefaults(bool saveImmediately = true)
        {
            EnsureInitialized();
            _language = DefaultLanguage;

            if (saveImmediately)
            {
                SaveToDevice();
            }

            ApplyToSystems();
            OnSettingsChanged?.Invoke();
        }

        public static void LoadFromDevice()
        {
            // TODO: 추후 계정 연동 기능 추가 시 로그인 후 서버 설정값과 동기화 처리 필요함
            int savedLang = PlayerPrefs.GetInt(LanguagePrefKey, (int)DefaultLanguage);
            if (Enum.IsDefined(typeof(LanguageCode), savedLang))
            {
                _language = (LanguageCode)savedLang;
            }
            else
            {
                _language = DefaultLanguage;
            }
        }

        public static void SaveToDevice()
        {
            // TODO: 추후 계정 연동 기능 추가 시 서버에 설정 변경 사항 업로드 처리 필요함
            PlayerPrefs.SetInt(LanguagePrefKey, (int)_language);
            PlayerPrefs.Save();
        }

        private static void ApplyToSystems()
        {
            if (LocalizationManager.CurrentLanguage != _language)
            {
                LocalizationManager.SetLanguage(_language);
            }
        }
    }
}