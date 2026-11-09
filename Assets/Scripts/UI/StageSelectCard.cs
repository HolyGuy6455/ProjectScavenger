using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    [ExecuteAlways]
    public class StageSelectCard : MonoBehaviour
    {
        public const float DEFAULT_CARD_ASPECT_RATIO = 0.48f;

        [Header("UI References")]
        [SerializeField] private Button cardButton;
        [SerializeField] private Image previewImage;
        [SerializeField] private TextMeshProUGUI recordNumberText;
        [SerializeField] private GameObject recommendBadgeObject;
        [SerializeField] private TextMeshProUGUI stageNameText;
        [SerializeField] private TextMeshProUGUI stageDescText;
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private float cardAspectRatio = DEFAULT_CARD_ASPECT_RATIO;

        private StageData _stageData;
        private Action<StageData> _onClickCallback;

        private void Awake()
        {
            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
            }

            if (!Application.isPlaying) return;

            if (cardButton == null)
            {
                cardButton = GetComponentInChildren<Button>(true);
            }

            if (cardButton != null)
            {
                cardButton.onClick.RemoveListener(HandleClick);
                cardButton.onClick.AddListener(HandleClick);
            }
        }

        private void OnEnable()
        {
            UpdatePreferredWidthFromHeight();
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdatePreferredWidthFromHeight();
        }

        public void UpdatePreferredWidthFromHeight()
        {
            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
                if (layoutElement == null) return;
            }

            RectTransform rt = transform as RectTransform;
            if (rt == null) return;

            float height = rt.rect.height;
            if (height <= 1f && rt.parent is RectTransform parentRt)
            {
                height = parentRt.rect.height;
            }

            if (height > 1f)
            {
                float ratio = cardAspectRatio > 0.05f ? cardAspectRatio : DEFAULT_CARD_ASPECT_RATIO;
                float targetWidth = Mathf.Round(height * ratio * 10f) / 10f;
                if (Mathf.Abs(layoutElement.preferredWidth - targetWidth) > 0.5f)
                {
                    layoutElement.preferredWidth = targetWidth;
                    layoutElement.minWidth = targetWidth;
                }
            }
        }

        public void Setup(StageData stageData, int displayIndex, Action<StageData> onClickCallback)
        {
            _stageData = stageData;
            _onClickCallback = onClickCallback;

            if (stageData == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (recordNumberText != null)
            {
                LocalizedText.ApplyFont(recordNumberText);
                recordNumberText.text = $"FIELD / {displayIndex:D2}";
            }

            // if (recommendBadgeObject != null)
            // {
            //     recommendBadgeObject.SetActive(displayIndex == 1);
            // }

            if (stageNameText != null)
            {
                LocalizedText.ApplyFont(stageNameText);
                string localizedName = stageData.GetLocalizedName();
                stageNameText.text = string.IsNullOrEmpty(localizedName) ? stageData.name : localizedName;
            }

            if (stageDescText != null)
            {
                LocalizedText.ApplyFont(stageDescText);
                string localizedDesc = stageData.GetLocalizedDesc();
                stageDescText.text = string.IsNullOrEmpty(localizedDesc) ? stageData.desc : localizedDesc;
            }

            if (previewImage != null && !string.IsNullOrEmpty(stageData.previewImage))
            {
                Sprite sp = SpriteManager.Instance.LoadSprite(stageData.previewImage);
                if (sp != null)
                {
                    previewImage.sprite = sp;
                    var fitter = previewImage.GetComponent<AspectRatioFitter>();
                    if (fitter != null && sp.rect.height > 0f)
                    {
                        fitter.aspectRatio = sp.rect.width / sp.rect.height;
                    }
                }
            }

            UpdatePreferredWidthFromHeight();
        }

        private void HandleClick()
        {
            if (_stageData != null)
            {
                _onClickCallback?.Invoke(_stageData);
            }
        }
    }
}
