using Scavenger.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Runtime.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// 설정 화면(SettingPanel) UI 표시와 사용자 입력을 처리하는 뷰 컴포넌트입니다.
    /// 실제 설정 데이터의 로드/저장/시스템 반영은 <see cref="SettingManager"/>가 담당합니다.
    /// </summary>
    public class SettingPanel : MonoBehaviour
    {
        private static readonly Color ApplyActiveFillColor = new Color(0.329f, 0.455f, 0.361f, 1f);
        private static readonly Color ApplyActiveStrokeColor = new Color(0.247f, 0.349f, 0.275f, 1f);
        private static readonly Color ApplyActiveTextColor = new Color(1.000f, 0.992f, 0.973f, 1f);

        private static readonly Color ApplyIdleFillColor = new Color(0.918f, 0.898f, 0.859f, 1f);
        private static readonly Color ApplyIdleStrokeColor = new Color(0.831f, 0.796f, 0.733f, 1f);
        private static readonly Color ApplyIdleTextColor = new Color(0.608f, 0.576f, 0.525f, 1f);

        [Header("Navigation")]
        [SerializeField] private Button backButton;

        [Header("Setting Rows")]
        [SerializeField] private SettingRowSelector languageRow;

        [Header("Bottom Actions")]
        [SerializeField] private Button resetDefaultButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private FigmaImage applyButtonImage;
        [SerializeField] private TMP_Text applyButtonText;

        private bool _listenersBound;

        private void Awake()
        {
            BindListeners();
        }

        private void OnEnable()
        {
            BindListeners();
            SyncFromManager();
        }

        private void OnDestroy()
        {
            if (languageRow != null)
            {
                languageRow.OnValueChanged -= HandleRowValueChanged;
            }
        }

        private void BindListeners()
        {
            if (_listenersBound) return;
            _listenersBound = true;

            if (backButton != null && backButton.GetComponent<UI_NavigateBackButton>() == null)
            {
                backButton.onClick.AddListener(OnClickBack);
            }

            if (resetDefaultButton != null)
            {
                resetDefaultButton.onClick.AddListener(OnClickResetDefault);
            }

            if (applyButton != null)
            {
                applyButton.onClick.AddListener(OnClickApply);
            }

            if (languageRow != null)
            {
                languageRow.OnValueChanged -= HandleRowValueChanged;
                languageRow.OnValueChanged += HandleRowValueChanged;
            }
        }

        public void SyncFromManager()
        {
            SettingManager.EnsureInitialized();

            if (languageRow != null)
            {
                int currentIdx = SettingManager.GetLanguageIndex(SettingManager.Language);
                int defaultIdx = SettingManager.GetLanguageIndex(SettingManager.DefaultLanguage);
                languageRow.Configure(SettingManager.GetLanguageDisplayNames(), currentIdx, defaultIdx, false);
            }

            RefreshActionButtonsState();
        }

        private void HandleRowValueChanged(SettingRow row)
        {
            RefreshActionButtonsState();
        }

        public bool HasPendingChanges()
        {
            return languageRow != null && languageRow.IsDirty;
        }

        public void OnClickResetDefault()
        {
            if (languageRow != null)
            {
                languageRow.ResetToDefault();
            }

            RefreshActionButtonsState();
        }

        public void OnClickApply()
        {
            if (languageRow != null)
            {
                LanguageCode selectedLang = SettingManager.GetLanguageByIndex(languageRow.SelectedIndex);
                SettingManager.SetLanguage(selectedLang, true);
                languageRow.Apply();
            }

            RefreshActionButtonsState();
        }

        public void OnClickBack()
        {
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.GoBack();
            }
        }

        private void RefreshActionButtonsState()
        {
            bool isDirty = HasPendingChanges();

            if (applyButton != null)
            {
                applyButton.interactable = isDirty;
            }

            if (applyButtonImage != null)
            {
                applyButtonImage.FillColor = isDirty ? ApplyActiveFillColor : ApplyIdleFillColor;
                applyButtonImage.StrokeColor = isDirty ? ApplyActiveStrokeColor : ApplyIdleStrokeColor;
                applyButtonImage.SetAllDirty();
            }

            if (applyButtonText != null)
            {
                applyButtonText.color = isDirty ? ApplyActiveTextColor : ApplyIdleTextColor;
            }
        }
    }
}

