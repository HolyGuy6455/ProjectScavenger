using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Runtime.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// 체크박스(토글) 형태의 설정 행 컴포넌트 (예: 튜토리얼, 피해 수치, 그림자, 음소거 등).
    /// </summary>
    public class SettingRowCheckBox : SettingRow
    {
        private static readonly Color ActiveTextColor = new Color(0.169f, 0.149f, 0.137f, 1f);
        private static readonly Color DisabledTextColor = new Color(0.667f, 0.639f, 0.592f, 1f);

        private static readonly Color UncheckedFillColor = new Color(1.000f, 0.992f, 0.973f, 1f);
        private static readonly Color UncheckedStrokeColor = new Color(0.478f, 0.431f, 0.388f, 1f);
        private static readonly Color DisabledUncheckedFillColor = new Color(0.918f, 0.898f, 0.859f, 1f);

        [Header("CheckBox UI References")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private Image checkedIconImage;
        [SerializeField] private Sprite activeCheckedSprite;
        [SerializeField] private Sprite disabledCheckedSprite;
        [SerializeField] private GameObject uncheckedBoxObject;
        [SerializeField] private FigmaImage uncheckedBoxImage;
        [SerializeField] private TextMeshProUGUI stateText;

        [Header("CheckBox Settings")]
        [SerializeField] private bool defaultValue = true;
        [SerializeField] private bool isChecked = true;
        [SerializeField] private string checkedLabel = "사용";
        [SerializeField] private string uncheckedLabel = "사용 안 함";

        private bool _savedValue = true;
        private bool _initialized;

        public event Action<bool> OnCheckChanged;

        public bool IsChecked
        {
            get => isChecked;
            set => SetChecked(value, true);
        }

        public override bool IsDirty => isChecked != _savedValue;

        private void Awake()
        {
            EnsureInitialized();
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(OnClickToggle);
            }
            RefreshVisualState();
        }

        private void OnDestroy()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(OnClickToggle);
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _savedValue = isChecked;
        }

        public void Configure(bool initialChecked, bool defaultChecked, string onLabel = null, string offLabel = null)
        {
            if (onLabel != null) checkedLabel = onLabel;
            if (offLabel != null) uncheckedLabel = offLabel;
            defaultValue = defaultChecked;
            isChecked = initialChecked;
            _savedValue = initialChecked;
            _initialized = true;
            RefreshVisualState();
        }

        public void SetChecked(bool value, bool notify = true)
        {
            EnsureInitialized();
            if (isChecked == value)
            {
                RefreshVisualState();
                return;
            }

            isChecked = value;
            RefreshVisualState();

            if (notify)
            {
                OnCheckChanged?.Invoke(isChecked);
                NotifyValueChanged();
            }
        }

        private void OnClickToggle()
        {
            if (!interactable) return;
            SetChecked(!isChecked, true);
        }

        public override void ResetToDefault()
        {
            SetChecked(defaultValue, true);
        }

        public override void Apply()
        {
            EnsureInitialized();
            _savedValue = isChecked;
        }

        public override void Revert()
        {
            EnsureInitialized();
            SetChecked(_savedValue, true);
        }

        public override void RefreshVisualState()
        {
            if (toggleButton != null)
            {
                toggleButton.interactable = interactable;
            }

            if (checkedIconImage != null)
            {
                checkedIconImage.enabled = isChecked;
                if (isChecked)
                {
                    Sprite targetSprite = interactable
                        ? activeCheckedSprite
                        : (disabledCheckedSprite != null ? disabledCheckedSprite : activeCheckedSprite);
                    if (targetSprite != null)
                    {
                        checkedIconImage.sprite = targetSprite;
                    }
                }
            }

            if (uncheckedBoxObject != null)
            {
                uncheckedBoxObject.SetActive(!isChecked);
            }

            if (uncheckedBoxImage != null && !isChecked)
            {
                uncheckedBoxImage.FillColor = interactable ? UncheckedFillColor : DisabledUncheckedFillColor;
                uncheckedBoxImage.StrokeColor = interactable ? UncheckedStrokeColor : DisabledTextColor;
                uncheckedBoxImage.SetAllDirty();
            }

            if (stateText != null)
            {
                string label = isChecked ? checkedLabel : uncheckedLabel;
                if (!string.IsNullOrEmpty(label))
                {
                    stateText.text = label;
                }
                stateText.color = interactable ? ActiveTextColor : DisabledTextColor;
            }
        }
    }
}

