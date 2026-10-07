using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace Scavenger.UI
{
    public class UI_StoragePanel : MonoBehaviour
    {
        [Header("Grid Setup")]
        [SerializeField] private Transform contentParent; // inventory-slots-grid
        [SerializeField] private GameObject rowPrefab;    // StorageRow 프리팹

        [Header("Dossier Setup (우측 상세정보 영역)")]
        [SerializeField] private GameObject dossierPanel; // selected-item-dossier
        [SerializeField] private Image dossierIconImage;
        [SerializeField] private TextMeshProUGUI dossierNameText;
        [SerializeField] private TextMeshProUGUI dossierDescText;
        [SerializeField] private TextMeshProUGUI dossierAmountText;

        [Header("Tabs Setup")]
        [SerializeField] private UI_StorageTab[] tabs;

        [Header("Category & Count")]
        [SerializeField] private TextMeshProUGUI categoryCountText;
        private int maxCapacity = 500;

        private List<GameObject> activeRows = new List<GameObject>();
        private List<UI_StorageItemSlot> allSlots = new List<UI_StorageItemSlot>();
        
        private ItemCategory currentCategory = ItemCategory.All;
        private int currentSelectedItemId = 0;

        private void OnEnable()
        {
            currentSelectedItemId = 0; // 패널을 처음 열 때 무조건 첫번째 아이템이 선택되도록 초기화
            if (tabs != null)
            {
                foreach (var tab in tabs)
                {
                    if (tab != null) tab.Setup(OnTabSelected);
                }
            }

            OnTabSelected(ItemCategory.All); // 처음 열릴 때는 전체 탭 선택 및 초기화
            
            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnItemDataUpdated += RefreshUI;
                UserDataManager.Instance.FetchItemList(); // 최신 아이템 데이터 로드
            }
        }

        private void OnDisable()
        {
            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnItemDataUpdated -= RefreshUI;
            }
        }

        public void RefreshUI()
        {
            if (UserDataManager.Instance == null) return;

            // 1. 기존 리스트 초기화
            activeRows.Clear();
            allSlots.Clear();

            // 2. 부모 오브젝트(contentParent)의 모든 자식 삭제 (에디터에 남아있던 더미 삭제)
            if (contentParent != null)
            {
                foreach (Transform child in contentParent)
                {
                    Destroy(child.gameObject);
                }
            }

            // 3. 보유량이 1 이상이고, 현재 선택된 탭 카테고리에 맞는 아이템 리스트 추출
            var items = UserDataManager.Instance.OwnedGameItems
                .Where(kvp => kvp.Value > 0)
                .Where(kvp => 
                {
                    if (currentCategory == ItemCategory.All) return true;
                    ItemData data = DataManager.Instance.GetData<ItemData>("Item", kvp.Key);
                    return data != null && data.Category == currentCategory;
                })
                .ToList();

            if (categoryCountText != null)
            {
                categoryCountText.text = $"{GetCategoryName(currentCategory)} ({items.Count} / {maxCapacity})";
            }

            int slotsPerRow = 6;
            int rowCount = Mathf.CeilToInt((float)items.Count / slotsPerRow);
            
            // 최소 1줄 이상 빈 칸이라도 보이게 하고 싶다면 rowCount를 조정 (예: rowCount = Mathf.Max(rowCount, 4))

            for (int i = 0; i < rowCount; i++)
            {
                if (rowPrefab == null || contentParent == null) break;

                GameObject rowObj = Instantiate(rowPrefab, contentParent);
                activeRows.Add(rowObj);

                // Row 프리팹 내부에 있는 5개의 슬롯 스크립트를 가져옵니다.
                UI_StorageItemSlot[] slotsInRow = rowObj.GetComponentsInChildren<UI_StorageItemSlot>(true);

                for (int j = 0; j < slotsPerRow; j++)
                {
                    if (j >= slotsInRow.Length) break;

                    int itemIndex = (i * slotsPerRow) + j;
                    if (itemIndex < items.Count)
                    {
                        slotsInRow[j].Setup(items[itemIndex].Key, items[itemIndex].Value, SelectSlot);
                    }
                    else
                    {
                        // 아이템이 꽉 차지 않은 나머지 슬롯은 빈 상태로 처리
                        slotsInRow[j].Setup(0, 0, null);
                    }
                    allSlots.Add(slotsInRow[j]);
                }
            }

            // 아이템이 있다면 첫번째 아이템 자동 선택 (또는 기존 선택 유지)
            if (items.Count > 0)
            {
                if (currentSelectedItemId == 0 || !items.Any(x => x.Key == currentSelectedItemId))
                {
                    SelectSlot(items[0].Key);
                }
                else
                {
                    SelectSlot(currentSelectedItemId);
                }
            }
            else
            {
                SelectSlot(0); // 표시할 아이템이 없으면 빈 화면
            }
        }

        private void SelectSlot(int itemId)
        {
            currentSelectedItemId = itemId;

            // 모든 슬롯의 선택 상태 갱신
            foreach (var slot in allSlots)
            {
                if (slot != null)
                {
                    slot.SetSelected(itemId != 0 && slot.CurrentItemId == itemId);
                }
            }

            // 빈 슬롯 클릭 시
            if (itemId == 0)
            {
                if (dossierPanel != null) dossierPanel.SetActive(false);
                return;
            }

            ItemData data = DataManager.Instance.GetData<ItemData>("Item", itemId);
            if (data == null) return;

            if (dossierPanel != null) dossierPanel.SetActive(true);

            if (dossierNameText != null) dossierNameText.text = data.name;
            
            if (dossierDescText != null) dossierDescText.text = data.Desc;

            if (dossierAmountText != null)
            {
                int amount = UserDataManager.Instance.GetGameItemAmount(itemId);
                dossierAmountText.text = $"x{amount}";
            }

            if (dossierIconImage != null && !string.IsNullOrEmpty(data.uiResource))
            {
                string path = data.uiResource.Replace(".png", "");
                Sprite sprite = Resources.Load<Sprite>($"Items/{path}");
                if (sprite == null) sprite = Resources.Load<Sprite>(path);
                
                if (sprite != null)
                {
                    dossierIconImage.sprite = sprite;
                    dossierIconImage.enabled = true;
                }
                else
                {
                    dossierIconImage.enabled = false;
                }
            }
        }

        private void OnTabSelected(ItemCategory category)
        {
            currentCategory = category;
            currentSelectedItemId = 0; // 탭을 바꿀 때도 첫번째 아이템이 선택되도록 초기화
            
            if (tabs != null)
            {
                foreach (var tab in tabs)
                {
                    if (tab != null) tab.SetSelected(tab.TabCategory == category);
                }
            }

            RefreshUI();
        }

        public void SetMaxCapacity(int capacity)
        {
            maxCapacity = capacity;
            RefreshUI();
        }

        private string GetCategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Important: return "중요";
                case ItemCategory.Food: return "음식";
                case ItemCategory.Tool: return "도구";
                case ItemCategory.Consumable: return "소모품";
                case ItemCategory.All:
                default: return "전체";
            }
        }
    }
}
