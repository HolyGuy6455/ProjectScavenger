using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button slotButton;
    [SerializeField] private Animator animator;
    [SerializeField] private Image bgImage;

    public int SlotIndex { get; private set; }

    private void Awake()
    {
        // 컴포넌트 누락 여부 검사
        if (animator == null || bgImage == null || iconImage == null || quantityText == null || slotButton == null)
        {
            Debug.LogWarning($"[InventorySlot] {gameObject.name}의 Inspector에 할당되지 않은 컴포넌트가 있습니다!");
        }
        
    }

    public void Setup(int index)
    {
        SlotIndex = index;
        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClicked);
        }
    }

    private void OnSlotClicked()
    {
        Debug.Log($"Inventory Slot {SlotIndex} clicked!");
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.UseItem(SlotIndex);
        }
    }

    public void UpdateSlot(InventoryItem item, Sprite sprite, bool isSelected)
    {
        if (item == null || item.IsEmpty)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            if (quantityText != null)
            {
                quantityText.text = "";
            }
            SetSelected(false);
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        if (quantityText != null)
        {
            UpdateQuantityText(item);
        }
        
        SetSelected(isSelected);
    }

    private void SetSelected(bool isSelected)
    {
        if (animator != null)
        {
            animator.SetBool("IsSelected", isSelected);
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (animator != null)
        {
            animator.SetBool("IsHighlighted", isHighlighted);
        }
    }

    private void UpdateQuantityText(InventoryItem item)
    {
        QuantityShowType showType = item.Data.QuantityShowType;

        if (showType == QuantityShowType.None)
        {
            quantityText.text = "";
        }
        else if (showType == QuantityShowType.Integer)
        {
            quantityText.text = item.Quantity.ToString();
            quantityText.color = Color.white;
        }
        else if (showType == QuantityShowType.Fraction)
        {
            int quantity = item.Quantity;
            int max = item.Data.maxQuantity;
            
            quantityText.text = $"{quantity} / {max}";

            if (max > 0)
            {
                float ratio = (float)quantity / max;
                if (ratio <= 0.1f)
                {
                    quantityText.color = new Color(1f, 0.4f, 0.4f); // 밝은 빨간색
                }
                else if (ratio <= 0.333f)
                {
                    quantityText.color = new Color(1f, 1f, 0.6f); // 옅은 노란색
                }
                else
                {
                    quantityText.color = Color.white;
                }
            }
            else
            {
                quantityText.color = Color.white;
            }
        }
    }

    private GameObject _dragGhost;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (PlayerInventory.Instance == null) return;
        InventoryItem item = PlayerInventory.Instance.Items[SlotIndex];
        if (item == null || item.IsEmpty || iconImage.sprite == null) return;

        // 고스트 UI 생성
        _dragGhost = new GameObject("DragGhost");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            _dragGhost.transform.SetParent(canvas.transform, false);
        }
        else
        {
            _dragGhost.transform.SetParent(transform.root, false);
        }
        
        Image ghostImage = _dragGhost.AddComponent<Image>();
        ghostImage.sprite = iconImage.sprite;
        ghostImage.raycastTarget = false; // 이벤트 블로킹 방지

        RectTransform ghostRect = _dragGhost.GetComponent<RectTransform>();
        // sizeDelta 대신 rect.size를 사용하여 늘리기(Stretch) 앵커 상태에서도 실제 크기를 가져옵니다.
        ghostRect.sizeDelta = iconImage.rectTransform.rect.size;
        ghostRect.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
        {
            _dragGhost.transform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost = null;

            // UI 바깥(월드)에 드롭되었는지 확인
            if (eventData.pointerCurrentRaycast.gameObject == null) 
            {
                if (Camera.main != null && PlayerInventory.Instance != null)
                {
                    Ray ray = Camera.main.ScreenPointToRay(eventData.position);
                    // 플레이어의 Y 높이를 기준으로 바닥 평면 생성
                    float spawnY = PlayerInventory.Instance.transform.position.y;
                    Plane groundPlane = new Plane(Vector3.up, new Vector3(0, spawnY, 0));
                    
                    if (groundPlane.Raycast(ray, out float distance))
                    {
                        Vector3 dropPoint = ray.GetPoint(distance);
                        PlayerInventory.Instance.DropItem(SlotIndex, dropPoint);
                    }
                }
            }
            else
            {
                // UI 요소 위에 드롭됨 (다른 슬롯인지 확인)
                InventorySlot targetSlot = eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlot>();
                if (targetSlot != null && targetSlot.SlotIndex != this.SlotIndex)
                {
                    PlayerInventory.Instance.MoveItem(this.SlotIndex, targetSlot.SlotIndex);
                }
            }
        }
    }
}
