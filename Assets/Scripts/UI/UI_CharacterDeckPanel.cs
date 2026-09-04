using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class UI_CharacterDeckPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private UI_CharacterDeckNode[] deckNodes = new UI_CharacterDeckNode[4];
        [SerializeField] private Button confirmButton;

        private int[] _selectedCharIds = new int[4];

        private void Start()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmButtonClicked);
            }

            InitializeDeck();
        }

        private void InitializeDeck()
        {
            if (UserDataManager.Instance != null && UserDataManager.Instance.SelectedDeck != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    _selectedCharIds[i] = UserDataManager.Instance.SelectedDeck[i] > 0 ? UserDataManager.Instance.SelectedDeck[i] : i + 1;
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    _selectedCharIds[i] = i + 1;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (deckNodes != null && i < deckNodes.Length && deckNodes[i] != null)
                {
                    deckNodes[i].Initialize(i, this);
                    UpdateDeckNodeUI(i);
                }
            }
        }

        private void UpdateDeckNodeUI(int slotIndex)
        {
            if (DataManager.Instance != null)
            {
                int charId = _selectedCharIds[slotIndex];
                CharData data = DataManager.Instance.GetData<CharData>("Char", charId);
                
                if (deckNodes[slotIndex] != null)
                {
                    deckNodes[slotIndex].SetCharacter(data);
                }
            }
        }

        public void OnDeckNodeChangeRequested(int slotIndex)
        {
            if (OutGameUIManager.Instance != null)
            {
                var popup = OutGameUIManager.Instance.ShowPopup<UI_CharacterSelectPopup>(OutGamePopupType.CharacterSelect);
                if (popup != null)
                {
                    popup.Open((selectedData) =>
                    {
                        if (selectedData != null)
                        {
                            _selectedCharIds[slotIndex] = selectedData.id;
                            UpdateDeckNodeUI(slotIndex);
                        }
                    });
                }
            }
            else
            {
                Debug.LogError("[UI_CharacterDeckPanel] OutGameUIManager 인스턴스가 없어 윈도우를 띄울 수 없습니다.");
            }
        }

        private void OnConfirmButtonClicked()
        {
            if (confirmButton != null) confirmButton.interactable = false;

            List<int> selectedList = new List<int>(_selectedCharIds);
            
            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.SaveSelectedDeck(_selectedCharIds, (success) =>
                {
                    if (confirmButton != null) confirmButton.interactable = true;
                    
                    if (success)
                    {
                        if (Scavenger.Network.NetworkPartyManager.Instance != null)
                        {
                            Scavenger.Network.NetworkPartyManager.Instance.SendSelectedCharacters(selectedList);
                        }
                        
                        if (OutGameUIManager.Instance != null)
                        {
                            OutGameUIManager.Instance.GoBack();
                        }
                        else
                        {
                            gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        Debug.LogError("[UI_CharacterDeckPanel] 덱 저장에 실패하여 다음 단계로 넘어가지 못했습니다.");
                    }
                });
            }
            else
            {
                if (confirmButton != null) confirmButton.interactable = true;
            }
        }
    }
}