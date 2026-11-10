using UnityEngine;
using System.Collections.Generic;

namespace Scavenger.UI
{
    public class StageInfoPanel : MonoBehaviour
    {
        [Header("Member Cards")]
        [SerializeField] private MemberCard myMemberCard;
        [SerializeField] private List<MemberCard> partyMemberCards = new List<MemberCard>();

        [Header("UI Elements")]
        [SerializeField] private GameObject supportButtonObject;
        [SerializeField] private TMPro.TextMeshProUGUI stageTitleText;
        [SerializeField] private TMPro.TextMeshProUGUI stageDescriptionText;
        [SerializeField] private UnityEngine.UI.Image backgroundImage;
        [SerializeField] private Transform enemiesRowContainer;
        [SerializeField] private Transform rewardsRowContainer;
        [SerializeField] private ItemSlotWrapper slotPrefab;

        private const float SLOT_SIZE = 88f;

        private int _currentStageId = 1;
        private readonly List<ItemSlotWrapper> _enemySlots = new List<ItemSlotWrapper>();
        private readonly List<ItemSlotWrapper> _rewardSlots = new List<ItemSlotWrapper>();

        public void SetStageForNextOpen(int stageId)
        {
            if (stageId > 0)
            {
                _currentStageId = stageId;
            }
        }

        private void Awake()
        {
            if (supportButtonObject != null)
            {
                var btn = supportButtonObject.GetComponentInChildren<UnityEngine.UI.Button>(true);
                if (btn != null)
                {
                    btn.onClick.RemoveListener(OnClickSupportButton);
                    btn.onClick.AddListener(OnClickSupportButton);
                }
            }
        }

        private void OnClickSupportButton()
        {
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.ShowPopup<InviteFriendPopup>(OutGamePopupType.InviteFriend);
            }
        }

        private void OnEnable()
        {
            if (OutGameUIManager.Instance != null && OutGameUIManager.Instance.SelectedStageId > 0)
            {
                _currentStageId = OutGameUIManager.Instance.SelectedStageId;
            }

            LocalizationManager.OnLanguageChanged += RefreshUI;

            if (Scavenger.Network.NetworkPartyManager.Instance == null)
            {
                var partySessionObj = new GameObject("PartySession");
                var partyManager = partySessionObj.AddComponent<Scavenger.Network.NetworkPartyManager>();
                partyManager.Connect();
            }

            if (Scavenger.Network.NetworkPartyManager.Instance != null)
            {
                Scavenger.Network.NetworkPartyManager.Instance.OnPartyMembersUpdated += RefreshUI;
            }

            RefreshUI();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshUI;

            if (Scavenger.Network.NetworkPartyManager.Instance != null)
            {
                Scavenger.Network.NetworkPartyManager.Instance.OnPartyMembersUpdated -= RefreshUI;
            }
        }

        public void RefreshUI()
        {
            RefreshStageDetails();

            bool isMultiplay = Scavenger.Network.NetworkPartyManager.Instance != null && Scavenger.Network.NetworkPartyManager.Instance.IsMultiplay;

            // 1. 로컬 유저 본인의 덱 정보 갱신
            if (myMemberCard != null && UserDataManager.Instance != null)
            {
                // UserDataManager에서 저장된 4인 덱 정보를 가져와 카드에 세팅
                myMemberCard.SetDeck(UserDataManager.Instance.SelectedDeck);

                // 로컬 유저의 레디 상태 갱신 (싱글플레이 시 항상 준비 완료 표시)
                bool isMyReady = !isMultiplay;
                if (isMultiplay && Scavenger.Network.NetworkPartyManager.Instance.PartyMembers != null)
                {
                    var myInfo = Scavenger.Network.NetworkPartyManager.Instance.PartyMembers.Find(m => m.username == Scavenger.Network.NetworkAuthManager.Instance.Username);
                    if (myInfo != null) isMyReady = myInfo.isReady;
                }
                myMemberCard.SetReady(isMyReady);
            }

            if (supportButtonObject != null)
            {
                supportButtonObject.SetActive(!isMultiplay);
            }

            // 2. 파티 멤버들의 덱 정보 갱신
            if (partyMemberCards != null)
            {
                for (int i = 0; i < partyMemberCards.Count; i++)
                {
                    if (partyMemberCards[i] == null) continue;

                    // 멀티플레이가 아니면 파티원 카드를 모두 끕니다.
                    if (!isMultiplay)
                    {
                        partyMemberCards[i].gameObject.SetActive(false);
                        continue;
                    }

                    var partyMembers = Scavenger.Network.NetworkPartyManager.Instance.PartyMembers;
                    if (partyMembers != null)
                    {
                        // 본인을 제외한 파티원 리스트 생성
                        var otherMembers = new List<Scavenger.Network.UserInfo>();
                        foreach (var m in partyMembers)
                        {
                            if (m.username != Scavenger.Network.NetworkAuthManager.Instance.Username)
                            {
                                otherMembers.Add(m);
                            }
                        }

                        if (i < otherMembers.Count)
                        {
                            var member = otherMembers[i];
                            partyMemberCards[i].gameObject.SetActive(true);
                            partyMemberCards[i].SetMemberName(member.username);
                            partyMemberCards[i].SetReady(member.isReady);
                            
                            if (member.selectedCharIds != null)
                            {
                                partyMemberCards[i].SetDeck(member.selectedCharIds.ToArray());
                            }
                        }
                        else
                        {
                            partyMemberCards[i].gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        partyMemberCards[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void RefreshStageDetails()
        {
            if (DataManager.Instance == null) return;

            StageData stageData = DataManager.Instance.GetData<StageData>("Stage", _currentStageId);
            if (stageData == null) return;

            if (stageTitleText != null)
            {
                LocalizedText.ApplyFont(stageTitleText);
                stageTitleText.text = stageData.GetLocalizedName();
            }

            if (stageDescriptionText != null)
            {
                LocalizedText.ApplyFont(stageDescriptionText);
                stageDescriptionText.text = stageData.GetLocalizedDesc();
            }

            if (backgroundImage != null && !string.IsNullOrEmpty(stageData.bgIllustration))
            {
                Sprite sp = SpriteManager.Instance.LoadSprite(stageData.bgIllustration);
                if (sp != null)
                {
                    backgroundImage.sprite = sp;
                    var fitter = backgroundImage.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                    if (fitter != null && sp.rect.height > 0f)
                    {
                        fitter.aspectRatio = sp.rect.width / sp.rect.height;
                    }
                }
            }

            RefreshEnemySlots(stageData);
            RefreshRewardSlots(stageData);
        }

        private void RefreshEnemySlots(StageData stageData)
        {
            if (enemiesRowContainer == null) return;

            CacheExistingSlotsIfNeeded(enemiesRowContainer, _enemySlots);

            int count = stageData.mainEnemies != null ? stageData.mainEnemies.Length : 0;
            int validIndex = 0;

            for (int i = 0; i < count; i++)
            {
                int enemyId = stageData.mainEnemies[i];
                if (enemyId <= 0) continue;

                EnemyData enemyData = DataManager.Instance.GetData<EnemyData>("Enemy", enemyId);
                if (enemyData == null) continue;

                Sprite icon = !string.IsNullOrEmpty(enemyData.icon)
                    ? SpriteManager.Instance.LoadSprite(enemyData.icon)
                    : null;

                ItemSlotWrapper slot = GetOrCreateSlot(enemiesRowContainer, _enemySlots, validIndex);
                if (slot != null)
                {
                    slot.gameObject.SetActive(true);
                    slot.SetIconOnlyData(icon, 0);
                    validIndex++;
                }
            }

            for (int i = validIndex; i < _enemySlots.Count; i++)
            {
                if (_enemySlots[i] != null)
                {
                    _enemySlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void RefreshRewardSlots(StageData stageData)
        {
            if (rewardsRowContainer == null) return;

            CacheExistingSlotsIfNeeded(rewardsRowContainer, _rewardSlots);

            int count = stageData.mainRewards != null ? stageData.mainRewards.Length : 0;
            int validIndex = 0;

            for (int i = 0; i < count; i++)
            {
                int itemId = stageData.mainRewards[i];
                if (itemId <= 0) continue;

                ItemData itemData = DataManager.Instance.GetData<ItemData>("Item", itemId);
                if (itemData == null) continue;

                Sprite icon = !string.IsNullOrEmpty(itemData.uiResource)
                    ? SpriteManager.Instance.LoadSprite(itemData.uiResource)
                    : null;

                ItemSlotWrapper slot = GetOrCreateSlot(rewardsRowContainer, _rewardSlots, validIndex);
                if (slot != null)
                {
                    slot.gameObject.SetActive(true);
                    slot.SetIconOnlyData(icon, itemData.id);
                    validIndex++;
                }
            }

            for (int i = validIndex; i < _rewardSlots.Count; i++)
            {
                if (_rewardSlots[i] != null)
                {
                    _rewardSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void CacheExistingSlotsIfNeeded(Transform container, List<ItemSlotWrapper> pool)
        {
            pool.RemoveAll(s => s == null);
            if (pool.Count > 0 || container == null) return;

            for (int i = 0; i < container.childCount; i++)
            {
                var slot = container.GetChild(i).GetComponent<ItemSlotWrapper>();
                if (slot != null)
                {
                    ConfigureSlotSize(slot);
                    pool.Add(slot);
                }
            }
        }

        private ItemSlotWrapper GetOrCreateSlot(Transform container, List<ItemSlotWrapper> pool, int index)
        {
            if (index < pool.Count && pool[index] != null)
            {
                return pool[index];
            }

            if (slotPrefab == null)
            {
                slotPrefab = Resources.Load<ItemSlotWrapper>("UIPrefabs/Node/ItemSlotWrapper");
                if (slotPrefab == null) return null;
            }

            ItemSlotWrapper inst = Instantiate(slotPrefab, container);
            inst.name = $"ItemSlotWrapper ({index})";
            ConfigureSlotSize(inst);
            pool.Add(inst);
            return inst;
        }

        private static void ConfigureSlotSize(ItemSlotWrapper slot)
        {
            if (slot == null) return;

            if (slot.transform is RectTransform rt)
            {
                rt.localScale = Vector3.one;
                rt.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
            }

            var le = slot.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le != null)
            {
                le.preferredWidth = SLOT_SIZE;
                le.preferredHeight = SLOT_SIZE;
                le.minWidth = SLOT_SIZE;
                le.minHeight = SLOT_SIZE;
            }
        }
    }
}
