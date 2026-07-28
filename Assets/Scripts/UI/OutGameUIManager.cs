using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.UI
{
    public enum OutGamePanelType
    {
        MainLobby,
        ReadyToBattle,
        FriendParty,
        CharacterSelect
    }

    public class OutGameUIManager : MonoBehaviour
    {
        public static OutGameUIManager Instance { get; private set; }

        [Header("Canvas Settings")]
        [SerializeField] private Transform canvasTransform;

        [Header("Panel Prefabs")]
        [SerializeField] private GameObject mainLobbyPanelPrefab;
        [SerializeField] private GameObject readyToBattlePanelPrefab;
        [SerializeField] private GameObject friendPartyPanelPrefab;
        [SerializeField] private GameObject characterSelectPanelPrefab;

        // 현재 떠 있는 패널 객체
        private GameObject currentPanelInstance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 아웃게임 씬이 시작되면 기본적으로 메인 로비 화면을 띄웁니다.
            ShowPanel(OutGamePanelType.MainLobby);
        }

        public void ShowPanel(OutGamePanelType panelType)
        {
            // 기존 패널이 있다면 파괴하여 메모리 비우기 (고급 방식)
            if (currentPanelInstance != null)
            {
                Destroy(currentPanelInstance);
            }

            GameObject prefabToLoad = null;

            switch (panelType)
            {
                case OutGamePanelType.MainLobby:
                    prefabToLoad = mainLobbyPanelPrefab;
                    break;
                case OutGamePanelType.ReadyToBattle:
                    prefabToLoad = readyToBattlePanelPrefab;
                    break;
                case OutGamePanelType.FriendParty:
                    prefabToLoad = friendPartyPanelPrefab;
                    break;
                case OutGamePanelType.CharacterSelect:
                    prefabToLoad = characterSelectPanelPrefab;
                    break;
            }

            if (prefabToLoad != null && canvasTransform != null)
            {
                // 프리팹을 캔버스의 자식으로 생성
                currentPanelInstance = Instantiate(prefabToLoad, canvasTransform);
                
                // 생성된 패널의 RectTransform을 화면 꽉 차게 덮어주기
                RectTransform rect = currentPanelInstance.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.localScale = Vector3.one;
                }
            }
            else
            {
                Debug.LogError($"[OutGameUIManager] 프리팹 또는 캔버스 연결이 누락되었습니다! (요청 패널: {panelType})");
            }
        }

        // 버튼 클릭 이벤트에서 사용하기 편하도록 오버로딩 함수 제공
        public void OnClickGoToLobby() => ShowPanel(OutGamePanelType.MainLobby);
        public void OnClickGoToReadyToBattle() => ShowPanel(OutGamePanelType.ReadyToBattle);
        public void OnClickGoToParty() => ShowPanel(OutGamePanelType.FriendParty);
        public void OnClickGoToCharacterSelect() => ShowPanel(OutGamePanelType.CharacterSelect);
    }
}
