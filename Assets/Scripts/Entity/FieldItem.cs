using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class FieldItem : NetworkBehaviour
{
    [Header("Item Info")]
    public NetworkVariable<int> itemId = new NetworkVariable<int>(0);
    public NetworkVariable<int> quantity = new NetworkVariable<int>(1);

    private Plane _dragPlane;
    private Vector3 _offset;
    private Vector3 _dragStartPosition;
    private bool _isDragging = false;
    private bool _pickupPending = false;
    private float _pickupPendingUntil = 0f;
    
    // 짧은 탭(클릭) 판정을 위한 변수
    private float _pressTime;
    private Vector2 _pressPosition;
    private const float SHORT_CLICK_TIME = 0.25f; // 0.25초 이내
    private const float SHORT_CLICK_DIST = 10f; // 10 픽셀 이내 이동
    
    // 현재 마우스가 올라가 있는 슬롯을 기억하기 위한 변수
    private InventorySlot _currentlyHoveredSlot;
    
    private SpriteRenderer _spriteRenderer;

    public override void OnNetworkSpawn()
    {
        _pickupPending = false;
        _pickupPendingUntil = 0f;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        UpdateSprite();
        
        // 아이템 ID/수량 변경 시 클라이언트 상태 갱신
        itemId.OnValueChanged += OnItemIdChanged;
        quantity.OnValueChanged += OnQuantityChanged;
    }

    public override void OnNetworkDespawn()
    {
        itemId.OnValueChanged -= OnItemIdChanged;
        quantity.OnValueChanged -= OnQuantityChanged;
        _pickupPending = false;
        _pickupPendingUntil = 0f;
        base.OnNetworkDespawn();
    }

    private void OnItemIdChanged(int oldValue, int newValue)
    {
        UpdateSprite();
    }

    private void OnQuantityChanged(int oldValue, int newValue)
    {
        _pickupPending = false;
        _pickupPendingUntil = 0f;
    }

    public void Setup(int newId, int newQuantity)
    {
        if (!IsServer) return; // 호스트(서버)만 값을 세팅할 수 있음
        itemId.Value = newId;
        quantity.Value = newQuantity;
    }

    private void UpdateSprite()
    {
        if (itemId.Value <= 0) return;

        ItemData data = DataManager.Instance.GetData<ItemData>("Item", itemId.Value);
        if (data != null && !string.IsNullOrEmpty(data.uiResource))
        {
            Sprite loadedSprite = SpriteManager.Instance.LoadSprite(data.uiResource);
            
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

    public bool TryPickup(int targetSlotIndex = -1)
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null) return false;

        if (_pickupPending && Time.unscaledTime >= _pickupPendingUntil)
        {
            _pickupPending = false;
        }
        if (_pickupPending) return false;

        int currentItemId = itemId.Value;
        int currentQuantity = quantity.Value;
        if (currentItemId <= 0 || currentQuantity <= 0) return false;

        int addable = inventory.GetAddableQuantity(currentItemId, currentQuantity);
        if (addable <= 0)
        {
            Debug.Log("[FieldItem] 인벤토리가 가득 차 아이템을 주울 수 없습니다.");
            return false;
        }

        // 오프라인(비네트워크) 테스트 환경 폴백
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !IsSpawned)
        {
            int added = inventory.TryAddItem(currentItemId, addable, targetSlotIndex);
            if (added <= 0) return false;
            if (added >= currentQuantity)
            {
                Destroy(gameObject);
            }
            return true;
        }

        _pickupPending = true;
        _pickupPendingUntil = Time.unscaledTime + 1.0f;
        RequestPickupServerRpc(addable, targetSlotIndex);
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(int requestedQuantity, int targetSlotIndex, ServerRpcParams rpcParams = default)
    {
        if (requestedQuantity <= 0 || NetworkObject == null || !NetworkObject.IsSpawned) return;
        if (itemId.Value <= 0 || quantity.Value <= 0) return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (NetworkManager.Singleton == null
            || !NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var senderClient)
            || senderClient.PlayerObject == null)
        {
            return;
        }

        PlayerInventory playerInventory = senderClient.PlayerObject.GetComponent<PlayerInventory>();
        if (playerInventory == null) return;

        int grantedItemId = itemId.Value;
        int grantedAmount = Mathf.Min(requestedQuantity, quantity.Value);
        int remaining = quantity.Value - grantedAmount;
        quantity.Value = remaining;

        var targetRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } }
        };
        playerInventory.GrantPickupClientRpc(grantedItemId, grantedAmount, targetSlotIndex, targetRpcParams);

        if (remaining <= 0)
        {
            NetworkObject.Despawn(true);
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
                transform.position = _dragStartPosition;
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
            // 화면에서 광선을 쏴서 이 오브젝트의 콜라이더에 맞았는지 확인합니다. (RaycastAll을 사용하여 지붕 등을 관통)
            RaycastHit[] hits = Physics.RaycastAll(ray);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject)
                {
                    _isDragging = true;
                    _dragStartPosition = transform.position;
                    _pressTime = Time.time;
                    _pressPosition = mousePos;
                    _dragPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
                    
                    if (_dragPlane.Raycast(ray, out float distance))
                    {
                        Vector3 hitPoint = ray.GetPoint(distance);
                        _offset = transform.position - hitPoint;
                    }
                    break;
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
                transform.position = _dragStartPosition;

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
