using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FieldItem : MonoBehaviour
{
    [Header("Item Info")]
    public int itemId;
    public int quantity = 1;

    private Plane _dragPlane;
    private Vector3 _offset;
    private bool _isDragging = false;
    
    // 짧은 탭(클릭) 판정을 위한 변수
    private float _pressTime;
    private Vector2 _pressPosition;
    private const float SHORT_CLICK_TIME = 0.25f; // 0.25초 이내
    private const float SHORT_CLICK_DIST = 10f; // 10 픽셀 이내 이동
    
    // 현재 마우스가 올라가 있는 슬롯을 기억하기 위한 변수
    private InventorySlot _currentlyHoveredSlot;
    
    private SpriteRenderer _spriteRenderer;

    private void Start()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        UpdateSprite();
    }

    public void Setup(int newId, int newQuantity)
    {
        itemId = newId;
        quantity = newQuantity;
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (itemId <= 0) return;

        ItemData data = DataManager.Instance.GetData<ItemData>("Item", itemId);
        if (data != null && !string.IsNullOrEmpty(data.uiResource))
        {
            // InventoryUI와 동일하게 이름에서 확장자를 제거하고 로드합니다.
            string nameWithoutExt = data.uiResource.Replace(".png", "").Replace(".jpg", "");
            Sprite loadedSprite = Resources.Load<Sprite>(nameWithoutExt);
            
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_spriteRenderer != null && loadedSprite != null)
            {
                _spriteRenderer.sprite = loadedSprite;
            }
        }
    }

    private void TryPickup(int targetSlotIndex = -1)
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory != null)
        {
            if (targetSlotIndex >= 0)
            {
                // 특정 슬롯을 향해 드롭한 경우 해당 슬롯 우선 처리
                inventory.AddItemToSlot(itemId, quantity, targetSlotIndex);
            }
            else
            {
                // 인벤토리에 아이템 추가 (기본)
                inventory.AddItem(itemId, quantity);
            }
            
            // 인벤토리가 가득 차서 남은 수량이 버려지더라도 맵에서는 파괴합니다.
            // TODO(seongmin.shin) : 수량이 남으면 맵에서 파괴되지 않게끔 변경해야 함
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (Camera.main == null) return;
        
        // Input System (New)를 사용할 경우 Mouse.current를 통해 입력을 받습니다.
        if (UnityEngine.InputSystem.Mouse.current == null) return;

        // 인벤토리가 켜져 있을 때만 필드 아이템과 상호작용(드래그) 할 수 있게 합니다.
        if (InventoryOrSkillPanelToggleManager.Instance != null && !InventoryOrSkillPanelToggleManager.Instance.IsInventoryActive)
        {
            // 드래그 중이었다면 강제 종료
            if (_isDragging)
            {
                _isDragging = false;
                if (_currentlyHoveredSlot != null)
                {
                    _currentlyHoveredSlot.SetHighlight(false);
                    _currentlyHoveredSlot = null;
                }
            }
            return;
        }

        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        // 마우스 클릭이 시작된 프레임
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 화면에서 광선을 쏴서 이 오브젝트의 콜라이더에 맞았는지 확인합니다.
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    _isDragging = true;
                    _pressTime = Time.time;
                    _pressPosition = mousePos;
                    _dragPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
                    
                    if (_dragPlane.Raycast(ray, out float distance))
                    {
                        Vector3 hitPoint = ray.GetPoint(distance);
                        _offset = transform.position - hitPoint;
                    }
                }
            }
        }
        // 마우스를 누른 채로 이동 중일 때
        else if (UnityEngine.InputSystem.Mouse.current.leftButton.isPressed && _isDragging)
        {
            if (_dragPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                transform.position = hitPoint + _offset;
            }

            // 호버(Hover) 연출 업데이트
            UpdateHoverEffect(mousePos);
        }
        // 마우스 클릭을 해제했을 때
        else if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (_isDragging)
            {
                _isDragging = false;

                // 드래그 종료 시 호버 효과 지우기
                if (_currentlyHoveredSlot != null)
                {
                    _currentlyHoveredSlot.SetHighlight(false);
                    _currentlyHoveredSlot = null;
                }

                // 짧은 클릭(탭)인지 검사합니다.
                bool isShortClick = (Time.time - _pressTime) <= SHORT_CLICK_TIME && 
                                  Vector2.Distance(mousePos, _pressPosition) <= SHORT_CLICK_DIST;

                if (isShortClick)
                {
                    TryPickup(-1); // 자동 인벤토리 습득
                    return;
                }

                // 마우스를 놓은 위치에 UI 요소(InventorySlot)가 있는지 확인합니다.
                if (EventSystem.current != null)
                {
                    PointerEventData pointerData = new PointerEventData(EventSystem.current)
                    {
                        position = mousePos
                    };

                    List<RaycastResult> results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointerData, results);

                    foreach (RaycastResult result in results)
                    {
                        // UI 요소나 그 부모에 InventorySlot 컴포넌트가 있는지 검사
                        InventorySlot slot = result.gameObject.GetComponentInParent<InventorySlot>();
                        if (slot != null)
                        {
                            // 인벤토리 특정 슬롯 위에 드롭되었으므로 획득 처리
                            TryPickup(slot.SlotIndex);
                            break;
                        }
                    }
                }
                
                _currentlyHoveredSlot = null;
            }
        }
    }

    private void UpdateHoverEffect(Vector2 mousePos)
    {
        if (EventSystem.current == null) return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = mousePos
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        InventorySlot hoveredSlot = null;
        foreach (RaycastResult result in results)
        {
            hoveredSlot = result.gameObject.GetComponentInParent<InventorySlot>();
            if (hoveredSlot != null) break;
        }

        // 이전 슬롯과 다르다면, 이전 슬롯 하이라이트 끄고 새 슬롯 하이라이트 켜기
        if (_currentlyHoveredSlot != hoveredSlot)
        {
            if (_currentlyHoveredSlot != null)
            {
                _currentlyHoveredSlot.SetHighlight(false);
            }

            _currentlyHoveredSlot = hoveredSlot;

            if (_currentlyHoveredSlot != null)
            {
                _currentlyHoveredSlot.SetHighlight(true);
            }
        }
    }
}
