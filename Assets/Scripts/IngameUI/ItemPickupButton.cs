using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemPickupButton : MonoBehaviour
{
    [SerializeField] private Button pickupButton;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemText;
    
    [Header("Settings")]
    [SerializeField] private float detectionRadius = 3f;
    [SerializeField] private LayerMask itemLayer = Physics.AllLayers;
    [SerializeField] private float updateInterval = 0.1f; // 매 프레임 Physics 연산을 방지하기 위한 간격

    private FieldItem _closestItem;
    private GameObject _player;
    private float _nextUpdateTime;

    private void Start()
    {
        if (pickupButton == null)
            pickupButton = GetComponent<Button>();

        if (pickupButton != null)
        {
            pickupButton.onClick.AddListener(OnPickupClicked);
            pickupButton.gameObject.SetActive(false); // 초기엔 숨김
        }
    }

    private void Update()
    {
        // 0.1초마다 주변 아이템 탐색
        if (Time.time >= _nextUpdateTime)
        {
            _nextUpdateTime = Time.time + updateInterval;
            FindClosestItem();
            UpdateUI();
        }
    }

    private void FindClosestItem()
    {
        if (PlayerController.LocalInstance == null)
        {
            _closestItem = null;
            return;
        }

        _player = PlayerController.LocalInstance.gameObject;

        _closestItem = null;
        float minDistance = detectionRadius;

        // 플레이어 주변 반경 내 콜라이더 검색
        Collider[] colliders = Physics.OverlapSphere(_player.transform.position, detectionRadius, itemLayer);
        
        foreach (Collider col in colliders)
        {
            FieldItem item = col.GetComponent<FieldItem>();
            if (item == null) item = col.GetComponentInParent<FieldItem>();

            // 스폰되어 있는 아이템인지 확인
            if (item != null && item.NetworkObject != null && item.NetworkObject.IsSpawned)
            {
                float dist = Vector3.Distance(_player.transform.position, item.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    _closestItem = item;
                }
            }
        }
    }

    private void UpdateUI()
    {
        if (_closestItem != null)
        {
            if (pickupButton != null && !pickupButton.gameObject.activeSelf)
                pickupButton.gameObject.SetActive(true);

            if (DataManager.Instance != null)
            {
                ItemData data = DataManager.Instance.GetData<ItemData>("Item", _closestItem.itemId.Value);
                if (data != null)
                {
                    // 텍스트 설정
                    if (itemText != null)
                    {
                        itemText.text = $"{data.name} 줍기";
                    }

                    // 아이콘 설정
                    if (itemIcon != null && !string.IsNullOrEmpty(data.uiResource))
                    {
                        string path = data.uiResource.Replace(".png", "");
                        Sprite sprite = Resources.Load<Sprite>($"Items/{path}");
                        if (sprite == null) sprite = Resources.Load<Sprite>(path);
                        
                        itemIcon.sprite = sprite;
                        itemIcon.enabled = sprite != null;
                    }
                }
            }
        }
        else
        {
            if (pickupButton != null && pickupButton.gameObject.activeSelf)
                pickupButton.gameObject.SetActive(false);
        }
    }

    private void OnPickupClicked()
    {
        if (_closestItem != null)
        {
            _closestItem.TryPickup();
            
            // 줍기 후 즉시 상태 리셋
            _closestItem = null;
            UpdateUI();
        }
    }
}
