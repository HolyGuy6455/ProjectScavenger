using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class UI_CharacterSelectPopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform contentParent; 
        [SerializeField] private GameObject characterSelectNodePrefab; 
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private List<UI_CharacterSelectNode> _nodes = new List<UI_CharacterSelectNode>();
        private CharData _selectedCharData;
        private Action<CharData> _onConfirmCallback;

        public void Open(Action<CharData> onConfirm)
        {
            _onConfirmCallback = onConfirm;
            _selectedCharData = null;

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmButtonClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
            }

            PopulateNodes();
        }

        private void PopulateNodes()
        {
            if (contentParent != null)
            {
                foreach (Transform child in contentParent)
                {
                    Destroy(child.gameObject);
                }
            }
            _nodes.Clear();

            if (DataManager.Instance != null)
            {
                List<CharData> charDatas = DataManager.Instance.GetAllData<CharData>("Char");
                if (charDatas != null)
                {
                    foreach (var data in charDatas)
                    {
                        if (characterSelectNodePrefab != null && contentParent != null)
                        {
                            GameObject obj = Instantiate(characterSelectNodePrefab, contentParent);
                            UI_CharacterSelectNode node = obj.GetComponent<UI_CharacterSelectNode>();
                            if (node != null)
                            {
                                node.Initialize(data, OnNodeSelected);
                                _nodes.Add(node);
                            }
                        }
                    }
                }
            }
        }

        private void OnNodeSelected(UI_CharacterSelectNode selectedNode, CharData charData)
        {
            _selectedCharData = charData;

            foreach (var node in _nodes)
            {
                if (node != null)
                {
                    node.SetSelected(node == selectedNode);
                }
            }
        }

        private void OnConfirmButtonClicked()
        {
            if (_selectedCharData != null)
            {
                _onConfirmCallback?.Invoke(_selectedCharData);
            }
            
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.ClosePopup(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnCancelButtonClicked()
        {
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.ClosePopup(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}