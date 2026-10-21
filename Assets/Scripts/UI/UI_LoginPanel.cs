using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Scavenger.Audio;
using Scavenger.Network;
using Scavenger.Core;
using System.Threading.Tasks;

namespace Scavenger.UI
{
    public class UI_LoginPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button startButton;

        [Header("Content Patch")]
        [Tooltip("Optional additional catalog URL. Leave empty to use the built-in Addressables catalog.")]
        [SerializeField] private string additionalCatalogUrl;
        [Tooltip("Addressables labels or addresses to download before entering the lobby.")]
        [SerializeField] private string[] startupDownloadKeys = System.Array.Empty<string>();

        private bool loginSucceeded;
        private bool loginFinished;
        private bool soundInitializationFinished;
        private bool sceneTransitionStarted;
        private NetworkAuthManager authManager;
        private bool contentReady;
        private bool patchFailed;
        private DefaultConfirmPopup patchPopup;
        private TaskCompletionSource<bool> pendingConfirmation;
        public StartupContentPatcher ContentPatch { get; private set; }

        private bool IsReadyToEnterLobby => contentReady && loginSucceeded && soundInitializationFinished
            && DataManager.Instance != null && DataManager.Instance.IsDataLoaded
            && UserDataManager.Instance != null && UserDataManager.Instance.IsInitialDataLoaded;

        private async void Start()
        {
            statusText.text = "Login...";
            startButton.interactable = false;
            
            // 로그인 씬의 준비가 끝난 후 버튼 클릭으로 로비 씬에 진입합니다.
            startButton.onClick.AddListener(OnClickStart);

            authManager = NetworkAuthManager.Instance;
            if (authManager == null || DataManager.Instance == null || UserDataManager.Instance == null)
            {
                statusText.text = "Startup managers are missing.";
                Debug.LogError("[LoginScene] AuthManager, DataManager and UserDataManager are required.");
                return;
            }

            authManager.OnLoginComplete += HandleLoginComplete;
            if (!string.IsNullOrEmpty(authManager.AuthToken))
                HandleLoginComplete(true);

            StartCoroutine(WaitForStartupData());

            await PrepareContentAsync();
        }

        private async Task PrepareContentAsync()
        {
            patchFailed = false;
            contentReady = false;
            ContentPatch ??= new StartupContentPatcher();
            RefreshStartState();

            // 같은 실행 중 로그인 씬에 재진입하면 이미 사용 중인 에셋의 카탈로그를 바꾸지 않습니다.
            if (!DataManager.Instance.IsDataLoaded)
            {
                while (true)
                {
                    bool success = await ContentPatch.RunAsync(additionalCatalogUrl, startupDownloadKeys,
                        ConfirmDownloadAsync);
                    if (this == null || !isActiveAndEnabled) return;
                    if (success) break;

                    patchFailed = true;
                    RefreshStartState();
                    if (ContentPatch.WasCancelled) return;

                    bool retry = await ShowPatchConfirmationAsync(
                        "데이터 업데이트에 실패했습니다.\n네트워크 연결을 확인한 뒤 다시 시도해 주세요.",
                        "데이터 업데이트", "재시도");
                    if (this == null || !isActiveAndEnabled || !retry) return;
                    patchFailed = false;
                    RefreshStartState();
                }
            }
            contentReady = true;
            DataManager.Instance.BeginInitialDataLoad();

            // 사운드도 카탈로그 갱신과 다운로드가 끝난 뒤 로드합니다.
            try
            {
                if (SoundManager.IsAudioEnabled)
                    await SoundManager.InitializeAsync();
                // 초기화 실패는 SoundManager가 기록하며, 로그인 자체는 무음으로 계속 진행합니다.
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }

            if (this == null)
                return;

            soundInitializationFinished = true;
            RefreshStartState();
        }

        private void OnDestroy()
        {
            CompleteConfirmation(false);
            if (patchPopup != null) Destroy(patchPopup.gameObject);
            if (startButton != null)
                startButton.onClick.RemoveListener(OnClickStart);
            if (authManager != null)
            {
                authManager.OnLoginComplete -= HandleLoginComplete;
            }
        }

        private void OnDisable()
        {
            CompleteConfirmation(false);
        }

        private void Update()
        {
            if (pendingConfirmation == null) return;
            // 외부에서 팝업을 닫거나 씬을 나가도 응답 대기가 남지 않게 합니다.
            if (patchPopup == null || !patchPopup.isActiveAndEnabled)
                CompleteConfirmation(false);
            else if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                patchPopup.Cancel();
        }

        private Task<bool> ConfirmDownloadAsync(long bytes)
        {
            string size = bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):0.##} GB"
                : bytes >= 1024L * 1024 ? $"{bytes / (1024d * 1024):0.##} MB"
                : bytes >= 1024 ? $"{bytes / 1024d:0.##} KB" : $"{bytes} B";
            return ShowPatchConfirmationAsync($"추가 데이터 {size}를 다운로드할까요?",
                "게임 실행에 필요한 데이터가 있습니다.", "다운로드");
        }

        private Task<bool> ShowPatchConfirmationAsync(string message, string description, string confirmLabel)
        {
            if (this == null || !isActiveAndEnabled) return Task.FromResult(false);
            if (pendingConfirmation != null) return pendingConfirmation.Task;

            pendingConfirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = pendingConfirmation.Task;
            if (patchPopup == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                var prefab = Resources.Load<GameObject>("UIPrefabs/Popups/DefaultConfirmPopup");
                var template = prefab == null ? null : prefab.GetComponent<DefaultConfirmPopup>();
                if (canvas != null && template != null)
                {
                    patchPopup = Instantiate(template, canvas.rootCanvas.transform, false);
                    if (patchPopup.transform is RectTransform rect)
                    {
                        rect.anchoredPosition = Vector2.zero;
                        rect.localScale = Vector3.one;
                    }
                }
                else
                {
                    Debug.LogError("[LoginScene] 확인 팝업 프리팹 또는 Canvas가 없습니다.");
                }
            }
            if (patchPopup != null)
            {
                patchPopup.Open(message, () => CompleteConfirmation(true), () => CompleteConfirmation(false),
                    description, confirmLabel, "취소");
                patchPopup.transform.SetAsLastSibling();
            }
            if (patchPopup == null) CompleteConfirmation(false);
            RefreshStartState();
            return response;
        }

        private void CompleteConfirmation(bool confirmed)
        {
            var response = pendingConfirmation;
            pendingConfirmation = null;
            if (patchPopup != null) patchPopup.Close();
            response?.TrySetResult(confirmed);
        }

        private void HandleLoginComplete(bool success)
        {
            loginSucceeded = success;
            loginFinished = true;

            RefreshStartState();
        }

        private void RefreshStartState()
        {
            startButton.interactable = pendingConfirmation == null && (patchFailed || IsReadyToEnterLobby)
                && !sceneTransitionStarted;
            if (sceneTransitionStarted)
                statusText.text = "Loading...";
            else if (patchFailed)
                statusText.text = ContentPatch?.WasCancelled == true
                    ? "Download cancelled. Tap to retry." : "Content update failed. Tap to retry.";
            else if (!contentReady)
                statusText.text = ContentPatch?.Status ?? "Checking for updates...";
            else if (!loginFinished)
                statusText.text = "Login...";
            else if (!loginSucceeded)
                statusText.text = "Login Failed";
            else if (UserDataManager.Instance != null && !string.IsNullOrEmpty(UserDataManager.Instance.InitialDataError))
                statusText.text = "Failed to load player data.";
            else
                statusText.text = IsReadyToEnterLobby ? "Touch to Start" : "Loading...";
        }

        private System.Collections.IEnumerator WaitForStartupData()
        {
            while (!sceneTransitionStarted)
            {
                RefreshStartState();
                if (IsReadyToEnterLobby) yield break;
                yield return null;
            }
        }

        private void OnClickStart()
        {
            if (pendingConfirmation != null) return;
            if (patchFailed)
            {
                _ = PrepareContentAsync();
                return;
            }
            if (!IsReadyToEnterLobby || sceneTransitionStarted)
                return;
            sceneTransitionStarted = true;
            RefreshStartState();
            SceneManager.LoadSceneAsync("OutgameScene");
        }
    }
}
