using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Runtime.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// 좌우 화살표 선택 형태의 설정 행 컴포넌트 (예: 언어, 그래픽 품질, 프레임 제한 등).
    /// </summary>
    public class SettingRowSelector : SettingRow
    {
        private static readonly Color ActiveTextColor = new Color(0.169f, 0.149f, 0.137f, 1f);
        private static readonly Color DisabledTextColor = new Color(0.667f, 0.639f, 0.592f, 1f);
        private static readonly Color ActiveBoxFillColor = new Color(1.000f, 0.992f, 0.973f, 1f);
        private static readonly Color DisabledBoxFillColor = new Color(0.918f, 0.898f, 0.859f, 1f);

        [Header("Selector UI References")]
        [SerializeField] private FigmaImage boxImage;
        [SerializeField] private Button prevButton;
        [SerializeField] private Image prevIconImage;
        [SerializeField] private Sprite prevActiveSprite;
        [SerializeField] private Sprite prevDisabledSprite;
        [SerializeField] private Button nextButton;
        [SerializeField] private Image nextIconImage;
        [SerializeField] private Sprite nextActiveSprite;
        [SerializeField] private Sprite nextDisabledSprite;
        [SerializeField] private TextMeshProUGUI valueText;

        [Header("Selector Settings")]
        [SerializeField] private List<string> options = new List<string> { "한국어", "English", "日本語" };
        [SerializeField] private int defaultIndex = 0;
        [SerializeField] private int selectedIndex = 0;
        [SerializeField] private bool loop = false;

        private int _savedIndex = 0;
        private bool _initialized;

        public event Action<int> OnSelectionChanged;

        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetSelectedIndex(value, true);
        }

        public string SelectedOption =>
            (options != null && selectedIndex >= 0 && selectedIndex < options.Count)
                ? options[selectedIndex]
                : string.Empty;

        public IReadOnlyList<string> Options => options;

        public override bool IsDirty => selectedIndex != _savedIndex;

        private void Awake()
        {
            EnsureInitialized();
            if (prevButton != null) prevButton.onClick.AddListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(SelectNext);
            RefreshVisualState();
        }

        private void OnDestroy()
        {
            if (prevButton != null) prevButton.onClick.RemoveListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(SelectNext);
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            selectedIndex = ClampIndex(selectedIndex);
            _savedIndex = selectedIndex;
        }

        public void Configure(IList<string> optionList, int initialIndex, int defaultIdx = 0, bool allowLoop = false)
        {
            options.Clear();
            if (optionList != null)
            {
                options.AddRange(optionList);
            }

            loop = allowLoop;
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

        public void SelectPrevious()
        {
            if (!interactable || options == null || options.Count <= 1) return;

            int nextIdx = selectedIndex - 1;
            if (nextIdx < 0)
            {
                if (!loop) return;
                nextIdx = options.Count - 1;
            }

            SetSelectedIndex(nextIdx, true);
        }

        public void SelectNext()
        {
            if (!interactable || options == null || options.Count <= 1) return;

            int nextIdx = selectedIndex + 1;
            if (nextIdx >= options.Count)
            {
                if (!loop) return;
                nextIdx = 0;
            }

            SetSelectedIndex(nextIdx, true);
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
            int count = options != null ? options.Count : 0;
            selectedIndex = ClampIndex(selectedIndex);

            bool canPrev = interactable && count > 1 && (loop || selectedIndex > 0);
            bool canNext = interactable && count > 1 && (loop || selectedIndex < count - 1);

            if (prevButton != null) prevButton.interactable = canPrev;
            if (nextButton != null) nextButton.interactable = canNext;

            if (prevIconImage != null)
            {
                Sprite sp = canPrev ? prevActiveSprite : (prevDisabledSprite != null ? prevDisabledSprite : prevActiveSprite);
                if (sp != null) prevIconImage.sprite = sp;
            }

            if (nextIconImage != null)
            {
                Sprite sp = canNext ? nextActiveSprite : (nextDisabledSprite != null ? nextDisabledSprite : nextActiveSprite);
                if (sp != null) nextIconImage.sprite = sp;
            }

            if (boxImage != null)
            {
                boxImage.FillColor = interactable ? ActiveBoxFillColor : DisabledBoxFillColor;
                boxImage.SetAllDirty();
            }

            if (valueText != null)
            {
                if (count > 0 && selectedIndex >= 0 && selectedIndex < count)
                {
                    valueText.text = options[selectedIndex];
                }
                valueText.color = interactable ? ActiveTextColor : DisabledTextColor;
            }
        }

        private int ClampIndex(int idx)
        {
            if (options == null || options.Count == 0) return 0;
            return Mathf.Clamp(idx, 0, options.Count - 1);
        }
    }
}

