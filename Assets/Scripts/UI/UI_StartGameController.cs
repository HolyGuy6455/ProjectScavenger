using Scavenger.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class UI_StartGameController : MonoBehaviour
    {
        [SerializeField] private Button startGameButton;

        private void Awake()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
            }
        }

        private void OnEnable()
        {
            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnGameStartBroadcasted += HandleGameStart;
                NetworkPartyManager.Instance.OnHostGameStartBroadcasted += HandleHostGameStart;
            }

            _isReady = false;
            if (NetworkPartyManager.Instance != null &&
                NetworkPartyManager.Instance.IsMultiplay &&
                NetworkPartyManager.Instance.PartyMembers != null &&
                NetworkAuthManager.Instance != null)
            {
                var myInfo = NetworkPartyManager.Instance.PartyMembers.Find(m => m.username == NetworkAuthManager.Instance.Username);
                if (myInfo != null) _isReady = myInfo.isReady;
            }

            GetComponentInParent<StageInfoPanel>()?.SetMyReadyState(_isReady);

            LocalizationManager.OnLanguageChanged += RefreshButtonText;
            RefreshButtonText();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshButtonText;

            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnGameStartBroadcasted -= HandleGameStart;
                NetworkPartyManager.Instance.OnHostGameStartBroadcasted -= HandleHostGameStart;
            }
        }

        private bool _isReady = false;

        private void OnStartGameClicked()
        {
            if (NetworkPartyManager.Instance != null)
            {
                _isReady = !_isReady;
                GetComponentInParent<StageInfoPanel>()?.SetMyReadyState(_isReady);
                NetworkPartyManager.Instance.SendReadyState(_isReady);
                RefreshButtonText();
                Debug.Log($"[Match] 레디 상태 변경됨: {_isReady}");
            }
        }

        private void RefreshButtonText()
        {
            if (startGameButton == null) return;

            string key = _isReady ? "UI_STAGEINFO_BTN_CANCEL_READY" : "UI_STAGEINFO_BTN_DEPLOY";
            var textComponent = startGameButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (textComponent != null)
            {
                var loc = textComponent.GetComponent<LocalizedText>();
                if (loc != null)
                {
                    loc.SetKey(key);
                }
                else
                {
                    LocalizedText.ApplyFont(textComponent);
                    textComponent.text = LocalizationManager.Get(key);
                }
            }
            else
            {
                var textMesh = startGameButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (textMesh != null)
                {
                    textMesh.text = LocalizationManager.Get(key);
                }
            }
        }

        private void HandleGameStart(string ip, int port, string matchToken)
        {
            Debug.Log($"[DEBUG-TRACE] [UI_StartGameController] 서버로부터 HandleGameStart 수신! ip:{ip} port:{port} matchToken: {matchToken}");
            
            // TODO: ip와 port 정보를 저장해두고 인게임 씬에서 Netcode(NGO)로 접속 시도해야 함
            PlayerPrefs.SetString("TargetServerIP", ip);
            PlayerPrefs.SetInt("TargetServerPort", port);
            PlayerPrefs.SetString("MatchToken", matchToken);
            PlayerPrefs.SetString("SelectedMap", GetSelectedMapSceneName());
            PlayerPrefs.SetInt("IsTestMode", 0);
            PlayerPrefs.Save();

            // 로딩 씬으로 넘어감 (인텐트 지정)
            Scavenger.Core.SceneTransitionManager.LoadSceneWithIntent(Scavenger.Core.LoadingIntent.EnterInGame);
        }

        private void HandleHostGameStart(string matchToken)
        {
            Debug.Log($"[DEBUG-TRACE] [UI_StartGameController] 서버로부터 HandleHostGameStart 수신! matchToken: {matchToken}");
            
            PlayerPrefs.SetString("MatchToken", matchToken);
            PlayerPrefs.SetString("SelectedMap", GetSelectedMapSceneName());
            PlayerPrefs.SetInt("IsTestMode", 0);
            PlayerPrefs.Save();

            // 로컬 호스트 모드로 로딩 씬 진입
            Scavenger.Core.SceneTransitionManager.LoadSceneWithIntent(Scavenger.Core.LoadingIntent.StartSingleplayerHost);
        }

        private string GetSelectedMapSceneName()
        {
            int stageId = OutGameUIManager.Instance != null ? OutGameUIManager.Instance.SelectedStageId : 1;
            if (DataManager.Instance != null)
            {
                StageData stageData = DataManager.Instance.GetData<StageData>("Stage", stageId);
                if (stageData != null && !string.IsNullOrWhiteSpace(stageData.sceneName))
                {
                    return stageData.sceneName.Trim();
                }
            }
            return "Stage002";
        }
    }
}
