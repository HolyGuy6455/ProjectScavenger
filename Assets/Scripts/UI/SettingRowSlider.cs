using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityFigmaBridge.Runtime.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// 슬라이더 형태의 설정 행 컴포넌트 (예: 카메라 감도, 밝기, 볼륨 등).
    /// </summary>
    public class SettingRowSlider : SettingRow, IPointerDownHandler, IDragHandler
    {
        private static readonly Color ActiveAccentColor = new Color(0.329f, 0.455f, 0.361f, 1f);
        private static readonly Color ActiveHandleFillColor = new Color(1.000f, 0.992f, 0.973f, 1f);
        private static readonly Color ActiveValueBoxColor = new Color(0.957f, 0.929f, 0.878f, 1f);

        private static readonly Color DisabledAccentColor = new Color(0.667f, 0.639f, 0.592f, 1f);
        private static readonly Color DisabledSurfaceColor = new Color(0.918f, 0.898f, 0.859f, 1f);

        [Header("Slider UI References")]
        [SerializeField] private RectTransform touchAreaRect;
        [SerializeField] private RectTransform trackRect;
        [SerializeField] private RectTransform fillRect;
        [SerializeField] private FigmaImage fillImage;
        [SerializeField] private RectTransform handleRect;
        [SerializeField] private FigmaImage handleImage;
        [SerializeField] private FigmaImage valueBoxImage;
        [SerializeField] private TextMeshProUGUI valueText;

        [Header("Slider Settings")]
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 100f;
        [SerializeField] private bool wholeNumbers = true;
        [SerializeField] private float defaultValue = 65f;
        [SerializeField] private float currentValue = 65f;

        private float _savedValue = 65f;
        private bool _initialized;

        public event Action<float> OnSliderValueChanged;

        public float MinValue => minValue;
        public float MaxValue => maxValue;
        public float DefaultValue => defaultValue;

        public float Value
        {
            get => currentValue;
            set => SetValue(value, true);
        }

        public int IntValue => Mathf.RoundToInt(currentValue);

        public override bool IsDirty => !Mathf.Approximately(currentValue, _savedValue);

        private void Awake()
        {
            EnsureInitialized();
            RefreshVisualState();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (trackRect != null)
            {
                RefreshVisualState();
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            currentValue = ClampValue(currentValue);
            _savedValue = currentValue;
        }

        public void Configure(float min, float max, float initialValue, float defaultVal, bool useWholeNumbers = true)
        {
            minValue = min;
            maxValue = Mathf.Max(min, max);
            wholeNumbers = useWholeNumbers;
            defaultValue = ClampValue(defaultVal);
            currentValue = ClampValue(initialValue);
            _savedValue = currentValue;
            _initialized = true;
            RefreshVisualState();
        }

        public void SetValue(float value, bool notify = true)
        {
            EnsureInitialized();
            float clamped = ClampValue(value);
            if (Mathf.Approximately(currentValue, clamped))
            {
                RefreshVisualState();
                return;
            }

            currentValue = clamped;
            RefreshVisualState();

            if (notify)
            {
                OnSliderValueChanged?.Invoke(currentValue);
                NotifyValueChanged();
            }
        }

        public override void ResetToDefault()
        {
            SetValue(defaultValue, true);
        }

        public override void Apply()
        {
            EnsureInitialized();
            _savedValue = currentValue;
        }

        public override void Revert()
        {
            EnsureInitialized();
            SetValue(_savedValue, true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable) return;
            UpdateValueFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!interactable) return;
            UpdateValueFromPointer(eventData);
        }

        private void UpdateValueFromPointer(PointerEventData eventData)
        {
            RectTransform refRect = trackRect != null ? trackRect : touchAreaRect;
            if (refRect == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    refRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                Rect rect = refRect.rect;
                if (rect.width <= 0f) return;

                float normalized = Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width);
                float newValue = Mathf.Lerp(minValue, maxValue, normalized);
                SetValue(newValue, true);
            }
        }

        public override void RefreshVisualState()
        {
            currentValue = ClampValue(currentValue);
            float normalized = maxValue > minValue
                ? Mathf.Clamp01((currentValue - minValue) / (maxValue - minValue))
                : 0f;

            if (trackRect != null)
            {
                float trackWidth = trackRect.rect.width;
                if (trackWidth <= 0f)
                {
                    trackWidth = trackRect.sizeDelta.x > 0f ? trackRect.sizeDelta.x : 476f;
                }

                float trackLeft = trackRect.offsetMin.x;
                float fillWidth = trackWidth * normalized;
                float trackHeight = trackRect.sizeDelta.y > 0f ? trackRect.sizeDelta.y : 12f;

                if (fillRect != null)
                {
                    fillRect.anchorMin = new Vector2(0f, 0.5f);
                    fillRect.anchorMax = new Vector2(0f, 0.5f);
                    fillRect.pivot = new Vector2(0f, 0.5f);
                    fillRect.anchoredPosition = new Vector2(trackLeft, 0f);
                    fillRect.sizeDelta = new Vector2(fillWidth, trackHeight);
                }

                if (handleRect != null)
                {
                    handleRect.anchorMin = new Vector2(0f, 0.5f);
                    handleRect.anchorMax = new Vector2(0f, 0.5f);
                    handleRect.pivot = new Vector2(0.5f, 0.5f);
                    handleRect.anchoredPosition = new Vector2(trackLeft + fillWidth, 0f);
                }
            }

            if (fillImage != null)
            {
                fillImage.FillColor = interactable ? ActiveAccentColor : DisabledAccentColor;
                fillImage.SetAllDirty();
            }

            if (handleImage != null)
            {
                handleImage.FillColor = interactable ? ActiveHandleFillColor : DisabledSurfaceColor;
                handleImage.StrokeColor = interactable ? ActiveAccentColor : DisabledAccentColor;
                handleImage.SetAllDirty();
            }

            if (valueBoxImage != null)
            {
                valueBoxImage.FillColor = interactable ? ActiveValueBoxColor : DisabledSurfaceColor;
                valueBoxImage.SetAllDirty();
            }

            if (valueText != null)
            {
                valueText.text = wholeNumbers
                    ? Mathf.RoundToInt(currentValue).ToString()
                    : currentValue.ToString("0.#");
                valueText.color = interactable ? ActiveAccentColor : DisabledAccentColor;
            }
        }

        private float ClampValue(float val)
        {
            float clamped = Mathf.Clamp(val, minValue, maxValue);
            return wholeNumbers ? Mathf.Round(clamped) : clamped;
        }
    }
}

