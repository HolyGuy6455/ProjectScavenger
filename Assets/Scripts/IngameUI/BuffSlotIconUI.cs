using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단일 버프 표시용 UI 컴포넌트입니다.
/// CharSlot 옆의 작은 정사각형 아이콘(nameText 미할당)과
/// 화면 하단의 가로형 버프 태그(iconImage + nameText 할당) 양쪽에서 공용으로 사용됩니다.
/// </summary>
[DisallowMultipleComponent]
public class BuffSlotIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Expiring Blink Settings")]
    [SerializeField] private float blinkThresholdSeconds = 2.0f;
    [SerializeField] private float blinkSpeed = 5.0f;
    [SerializeField] private float minBlinkAlpha = 0.25f;

    private Buff _boundBuff;
    public Buff BoundBuff => _boundBuff;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    public void Bind(Buff buff)
    {
        if (buff == null)
        {
            Clear();
            return;
        }

        _boundBuff = buff;

        if (iconImage != null)
        {
            Sprite icon = buff.GetIconSprite();
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
        {
            nameText.text = buff.GetDisplayName();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        UpdateBlinkVisual();
    }

    public void Clear()
    {
        _boundBuff = null;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        UpdateBlinkVisual();
    }

    private void UpdateBlinkVisual()
    {
        if (_boundBuff == null || canvasGroup == null) return;

        bool shouldBlink = _boundBuff.maxLifeTime > blinkThresholdSeconds
            && _boundBuff.lifeTime > 0f
            && _boundBuff.lifeTime <= blinkThresholdSeconds;

        if (shouldBlink)
        {
            float range = Mathf.Max(0.05f, 1f - minBlinkAlpha);
            canvasGroup.alpha = minBlinkAlpha + Mathf.PingPong(Time.unscaledTime * blinkSpeed, range);
        }
        else
        {
            canvasGroup.alpha = 1f;
        }
    }
}
