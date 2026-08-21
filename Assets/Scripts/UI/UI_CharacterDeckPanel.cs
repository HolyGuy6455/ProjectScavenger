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
            for (int i = 0; i < 4; i++)
            {
                _selectedCharIds[i] = i + 1;
                
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
                var window = OutGameUIManager.Instance.ShowWindow<UI_CharacterSelectWindow>(OutGameWindowType.CharacterSelect);
                if (window != null)
                {
                    window.Open((selectedData) =>
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
            List<int> selectedList = new List<int>(_selectedCharIds);
            if (Scavenger.Network.NetworkPartyManager.Instance != null)
            {
                Scavenger.Network.NetworkPartyManager.Instance.SendSelectedCharacters(selectedList);
            }
            else
            {
                Debug.LogWarning("[UI_CharacterDeckPanel] NetworkPartyManager 인스턴스가 없어 전송할 수 없습니다.");
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
    }
}