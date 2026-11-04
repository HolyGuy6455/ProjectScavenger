using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Runtime.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// 라디오 버튼(단일 선택 그룹) 형태의 설정 행 컴포넌트 (예: 조작 방식, 최적화 기준, 출력 모드 등).
    /// </summary>
    public class SettingRowRadioButton : SettingRow
    {
        private static readonly Color SelectedBoxFill = new Color(0.902f, 0.925f, 0.882f, 1f);
        private static readonly Color SelectedAccentColor = new Color(0.329f, 0.455f, 0.361f, 1f);

        private static readonly Color UnselectedBoxFill = new Color(1.000f, 0.992f, 0.973f, 1f);
        private static readonly Color UnselectedBoxStroke = new Color(0.871f, 0.839f, 0.780f, 1f);
        private static readonly Color UnselectedCircleStroke = new Color(0.478f, 0.431f, 0.388f, 1f);
        private static readonly Color UnselectedTextColor = new Color(0.169f, 0.149f, 0.137f, 1f);

        private static readonly Color DisabledBoxFill = new Color(0.918f, 0.898f, 0.859f, 1f);
        private static readonly Color DisabledMutedColor = new Color(0.667f, 0.639f, 0.592f, 1f);

        [Serializable]
        public class RadioOptionItem
        {
            public Button button;
            public FigmaImage boxImage;
            public FigmaImage outerCircleImage;
            public GameObject innerDotObject;
            public FigmaImage innerDotImage;
            public TextMeshProUGUI labelText;
        }

        [Header("Radio Options UI")]
        [SerializeField] private List<RadioOptionItem> options = new List<RadioOptionItem>();

        [Header("Radio Settings")]
        [SerializeField] private int defaultIndex = 0;
        [SerializeField] private int selectedIndex = 0;

        private int _savedIndex = 0;
        private bool _initialized;

        public event Action<int> OnSelectionChanged;

        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetSelectedIndex(value, true);
        }

        public int OptionCount => options != null ? options.Count : 0;

        public override bool IsDirty => selectedIndex != _savedIndex;

        private void Awake()
        {
            EnsureInitialized();
            BindButtons();
            RefreshVisualState();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            selectedIndex = ClampIndex(selectedIndex);
            _savedIndex = selectedIndex;
        }

        private void BindButtons()
        {
            if (options == null) return;
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var opt = options[i];
                if (opt?.button != null)
                {
                    opt.button.onClick.AddListener(() => OnClickOption(index));
                }
            }
        }

        private void UnbindButtons()
        {
            if (options == null) return;
            foreach (var opt in options)
            {
                if (opt?.button != null)
                {
                    opt.button.onClick.RemoveAllListeners();
                }
            }
        }

        public void Configure(IList<string> optionLabels, int initialIndex, int defaultIdx = 0)
        {
            if (optionLabels != null && options != null)
            {
                int count = Mathf.Min(optionLabels.Count, options.Count);
                for (int i = 0; i < count; i++)
                {
                    if (options[i]?.labelText != null && optionLabels[i] != null)
                    {
                        options[i].labelText.text = optionLabels[i];
                    }
                }
            }

            defaultIndex = ClampIndex(defaultIdx);
            selectedIndex = ClampIndex(initialIndex);
            _savedIndex = selectedIndex;
            _initialized = true;
            RefreshVisualState();
        }

        public void SetSelectedIndex(int index, bool notify = true)
        {
            EnsureInitialized();
            int clamped = ClampIndex(index);
            if (selectedIndex == clamped)
            {
                RefreshVisualState();
                return;
            }

            selectedIndex = clamped;
            RefreshVisualState();

            if (notify)
            {
                OnSelectionChanged?.Invoke(selectedIndex);
                NotifyValueChanged();
            }
        }

        private void OnClickOption(int index)
        {
            if (!interactable) return;
            SetSelectedIndex(index, true);
        }

        public override void ResetToDefault()
        {
            SetSelectedIndex(defaultIndex, true);
        }

        public override void Apply()
        {
            EnsureInitialized();
            _savedIndex = selectedIndex;
        }

        public override void Revert()
        {
            EnsureInitialized();
            SetSelectedIndex(_savedIndex, true);
        }

        public override void RefreshVisualState()
        {
            if (options == null || options.Count == 0) return;
            selectedIndex = ClampIndex(selectedIndex);

            for (int i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                if (opt == null) continue;

                bool isSelected = (i == selectedIndex);

                if (opt.button != null)
                {
                    opt.button.interactable = interactable;
                }

                if (opt.boxImage != null)
                {
                    if (!interactable)
                    {
                        opt.boxImage.FillColor = DisabledBoxFill;
                        opt.boxImage.StrokeColor = UnselectedBoxStroke;
                        opt.boxImage.StrokeWidth = 1f;
                    }
                    else if (isSelected)
                    {
                        opt.boxImage.FillColor = SelectedBoxFill;
                        opt.boxImage.StrokeColor = SelectedAccentColor;
                        opt.boxImage.StrokeWidth = 2f;
                    }
                    else
                    {
                        opt.boxImage.FillColor = UnselectedBoxFill;
                        opt.boxImage.StrokeColor = UnselectedBoxStroke;
                        opt.boxImage.StrokeWidth = 1f;
                    }
                    opt.boxImage.SetAllDirty();
                }

                if (opt.outerCircleImage != null)
                {
                    if (!interactable)
                    {
                        opt.outerCircleImage.FillColor = DisabledBoxFill;
                        opt.outerCircleImage.StrokeColor = DisabledMutedColor;
                    }
                    else if (isSelected)
                    {
                        opt.outerCircleImage.FillColor = UnselectedBoxFill;
                        opt.outerCircleImage.StrokeColor = SelectedAccentColor;
                    }
                    else
                    {
                        opt.outerCircleImage.FillColor = UnselectedBoxFill;
                        opt.outerCircleImage.StrokeColor = UnselectedCircleStroke;
                    }
                    opt.outerCircleImage.StrokeWidth = 2f;
                    opt.outerCircleImage.SetAllDirty();
                }

                if (opt.innerDotObject != null)
                {
                    opt.innerDotObject.SetActive(isSelected);
                }

                if (opt.innerDotImage != null && isSelected)
                {
                    opt.innerDotImage.FillColor = interactable ? SelectedAccentColor : DisabledMutedColor;
                    opt.innerDotImage.SetAllDirty();
                }

                if (opt.labelText != null)
                {
                    if (!interactable)
                    {
                        opt.labelText.color = DisabledMutedColor;
                    }
                    else
                    {
                        opt.labelText.color = isSelected ? SelectedAccentColor : UnselectedTextColor;
                    }
                }
            }
        }

        private int ClampIndex(int idx)
        {
            if (options == null || options.Count == 0) return 0;
            return Mathf.Clamp(idx, 0, options.Count - 1);
        }
    }
}

