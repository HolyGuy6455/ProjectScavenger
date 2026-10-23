using System;
using System.Collections.Generic;
using System.Text;
using Scavenger.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Scavenger.Network
{
    // Unity 메인 스레드에서 호출합니다. Send에 넘긴 UnityWebRequest의 소유권은 매니저로 이전됩니다.
    public sealed class NetworkRequestManager : MonoBehaviour
    {
        private static NetworkRequestManager _instance;
        private static bool _quitting;
        public static NetworkRequestManager Instance
        {
            get
            {
                if (_instance == null && !_quitting)
                    _instance = new GameObject(nameof(NetworkRequestManager)).AddComponent<NetworkRequestManager>();
                return _instance;
            }
        }

        public static bool IsInputBlocked => _instance != null && _instance.BlockingRequestCount > 0;
        [SerializeField, Min(1)] private int defaultTimeoutSeconds = 15;
        private readonly Dictionary<long, PendingRequest> _pending = new Dictionary<long, PendingRequest>();
        private readonly Dictionary<EventSystem, bool> _navigationStates = new Dictionary<EventSystem, bool>();
        private long _nextId;
        private bool _stopping;
        private Canvas _canvas;
        private NetworkLoadingPopup _loadingPopup;
        private ToastPopup _toast;
        public int ActiveRequestCount => _pending.Count;
        public int BlockingRequestCount { get; private set; }

        private sealed class PendingRequest
        {
            public NetworkRequestHandle Handle;
            public NetworkRequestOptions Options;
            public UnityWebRequest Request;
            public int TimeoutSeconds;
            public Action<string> ApplyResponse;
            public Action<NetworkError> OnFailure;
            public Action<NetworkRequestHandle> OnFinished;
            public bool Finishing;
        }

        [Serializable]
        private sealed class ErrorEnvelope
        {
            public int errorCode;
            public string message;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _quitting = false; }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // 인증 정보는 전송 시점에 고정합니다.
        public static UnityWebRequest CreateJsonRequest(string url, string method, string json = null, string authToken = null)
        {
            var request = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer() };
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(authToken)) request.SetRequestHeader("Authorization", "Bearer " + authToken);
            return request;
        }

        public NetworkRequestHandle[] GetActiveRequests()
        {
            var result = new NetworkRequestHandle[_pending.Count];
            int index = 0;
            foreach (var entry in _pending.Values) result[index++] = entry.Handle;
            return result;
        }

        // onSuccess에서는 영구 데이터를 먼저 반영하고 화면의 유효성을 확인한 뒤 UI를 갱신합니다.
        // onFinished는 로딩/중복 키 해제 후 모든 결과(중복 차단/취소 포함)에 정확히 한 번 호출됩니다.
        // 전송한 request를 호출자 쪽 using/Dispose로 해제하지 마세요.
        public NetworkRequestHandle Send<T>(UnityWebRequest request, Action<T> onSuccess,
            NetworkRequestOptions options = null, Action<NetworkError> onFailure = null,
            Func<T, bool> validateResponse = null, Action<NetworkRequestHandle> onFinished = null) where T : class
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            options = (options ?? new NetworkRequestOptions()).Snapshot();
            var handle = new NetworkRequestHandle
            {
                Id = ++_nextId, Name = options.Name ?? typeof(T).Name,
                DuplicateKey = options.DuplicateKey, StartedAt = Time.realtimeSinceStartupAsDouble
            };
            if (options.DuplicatePolicy == DuplicateRequestPolicy.Reject && string.IsNullOrEmpty(options.DuplicateKey))
            {
                request.Dispose();
                throw new ArgumentException("Reject 정책에는 DuplicateKey가 필요합니다.", nameof(options));
            }
            if (options.DuplicatePolicy == DuplicateRequestPolicy.Reject)
            {
                foreach (var current in _pending.Values)
                {
                    if (current.Handle.DuplicateKey != options.DuplicateKey) continue;
                    handle.Status = NetworkRequestStatus.AlreadyInProgress;
                    request.Dispose();
                    InvokeSafely(() => onFinished?.Invoke(handle));
                    return handle;
                }
            }
            if (_stopping || !isActiveAndEnabled || OwnerUnavailable(options))
            {
                handle.Status = NetworkRequestStatus.Cancelled;
                request.Dispose();
                InvokeSafely(() => onFinished?.Invoke(handle));
                return handle;
            }

            var pending = new PendingRequest
            {
                Handle = handle, Options = options, Request = request,
                TimeoutSeconds = options.TimeoutSeconds > 0 ? options.TimeoutSeconds : Mathf.Max(1, defaultTimeoutSeconds),
                OnFailure = onFailure, OnFinished = onFinished,
                ApplyResponse = body =>
                {
                    T response;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith("{"))
                            throw new FormatException("JSON object expected");
                        response = JsonUtility.FromJson<T>(body);
                        if (response == null || (validateResponse != null && !validateResponse(response)))
                            throw new FormatException("Invalid response fields");
                    }
                    catch (Exception ex) { throw new InvalidResponseException(ex); }
                    onSuccess?.Invoke(response);
                }
            };
            _pending.Add(handle.Id, pending);
            handle.SetCancel(() => Finish(pending, NetworkRequestStatus.Cancelled));
            if (options.ShowLoading) BlockingRequestCount++;
            try
            {
                RefreshLoading();
                request.timeout = pending.TimeoutSeconds;
                request.SendWebRequest();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(pending, NetworkRequestStatus.Failed, new NetworkError { Kind = NetworkErrorKind.Connection });
            }
            return handle;
        }

        private sealed class InvalidResponseException : Exception
        {
            public InvalidResponseException(Exception inner) : base("Invalid network response", inner) { }
        }

        private static bool OwnerUnavailable(NetworkRequestOptions options) => options.CancelWhenOwnerUnavailable
            && (options.Owner == null || !options.Owner.isActiveAndEnabled);

        private void Update()
        {
            if (_pending.Count == 0) return;
            // 콜백에서 새 요청을 시작하거나 다른 요청을 취소할 수 있습니다.
            var snapshot = new List<PendingRequest>(_pending.Values);
            foreach (var pending in snapshot)
            {
                if (pending.Handle.IsDone || pending.Finishing) continue;
                if (OwnerUnavailable(pending.Options)) { Finish(pending, NetworkRequestStatus.Cancelled); continue; }
                if (pending.Request.isDone)
                {
                    CompleteResponse(pending);
                }
                else if (Time.realtimeSinceStartupAsDouble - pending.Handle.StartedAt >= pending.TimeoutSeconds)
                {
                    Finish(pending, NetworkRequestStatus.Failed, new NetworkError { Kind = NetworkErrorKind.Timeout });
                }
            }
            UpdateNavigationBlock();
        }

        private void CompleteResponse(PendingRequest pending)
        {
            var request = pending.Request;
            string body = request.downloadHandler?.text;
            ErrorEnvelope envelope = null;
            try { if (!string.IsNullOrWhiteSpace(body)) envelope = JsonUtility.FromJson<ErrorEnvelope>(body); }
            catch (ArgumentException) { }
            if (request.result != UnityWebRequest.Result.Success || (envelope != null && envelope.errorCode != 0))
            {
                NetworkErrorKind kind = NetworkErrorKind.Http;
                if (request.result == UnityWebRequest.Result.ConnectionError)
                    kind = request.error != null && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                        ? NetworkErrorKind.Timeout : NetworkErrorKind.Connection;
                else if (request.result == UnityWebRequest.Result.DataProcessingError) kind = NetworkErrorKind.InvalidResponse;
                else if (request.result == UnityWebRequest.Result.Success) kind = NetworkErrorKind.Server;
                Finish(pending, NetworkRequestStatus.Failed, new NetworkError
                {
                    Kind = kind, HttpStatusCode = request.responseCode,
                    ServerCode = envelope == null ? (int?)null : envelope.errorCode, ServerMessage = envelope?.message
                });
                return;
            }
            Finish(pending, NetworkRequestStatus.Succeeded, applyResponse: () => pending.ApplyResponse(body));
        }

        private void Finish(PendingRequest pending, NetworkRequestStatus status, NetworkError error = null, Action applyResponse = null)
        {
            if (pending.Finishing || pending.Handle.IsDone) return;
            pending.Finishing = true;
            try
            {
                if (!pending.Request.isDone) pending.Request.Abort();
                try { applyResponse?.Invoke(); }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    status = NetworkRequestStatus.Failed;
                    error = new NetworkError { HttpStatusCode = pending.Request.responseCode,
                        Kind = ex is InvalidResponseException ? NetworkErrorKind.InvalidResponse : NetworkErrorKind.ResponseProcessing };
                }
                pending.Handle.Status = status;
                pending.Handle.Error = error;
                if (error != null)
                {
                    Debug.LogWarning($"[Network] #{pending.Handle.Id} {pending.Handle.Name}: {error.Kind} (HTTP {error.HttpStatusCode}, code {error.ServerCode})");
                    InvokeSafely(() => pending.OnFailure?.Invoke(error));
                    if (pending.Options.ShowErrorToast) InvokeSafely(() => ShowToast(error.DefaultMessage));
                }
            }
            finally
            {
                pending.Handle.SetCancel(null);
                _pending.Remove(pending.Handle.Id);
                if (pending.Options.ShowLoading) BlockingRequestCount--;
                pending.Request.Dispose();
                RefreshLoading();
            }
            InvokeSafely(() => pending.OnFinished?.Invoke(pending.Handle));
        }

        private static void InvokeSafely(Action callback)
        {
            try { callback(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        // 일반 UI와 네트워크 오류가 같은 토스트 인스턴스/대기열을 사용합니다.
        public ToastPopup ShowToast(string message, float? duration = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return _toast;
            EnsureCanvas();
            if (_toast == null)
            {
                var prefab = Resources.Load<ToastPopup>("UIPrefabs/Popups/ToastPopup");
                if (prefab == null) { Debug.LogWarning("[Network] ToastPopup 프리팹이 없습니다."); return null; }
                _toast = Instantiate(prefab, _canvas.transform, false);
            }
            _toast.Show(message, duration);
            return _toast;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("NetworkOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32760;
            var scaler = go.GetComponent<CanvasScaler>();
            // 기존 씬 Canvas와 같은 배율로 프리팹에 지정한 UI 크기를 유지합니다.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
        }

        private void RefreshLoading()
        {
            if (BlockingRequestCount > 0 && _loadingPopup == null)
            {
                EnsureCanvas();
                var prefab = Resources.Load<NetworkLoadingPopup>("UIPrefabs/Popups/NetworkLoadingPopup");
                if (prefab != null) _loadingPopup = Instantiate(prefab, _canvas.transform, false);
                else Debug.LogError("[Network] NetworkLoadingPopup 프리팹이 없습니다.");
            }
            if (_loadingPopup != null)
            {
                if (BlockingRequestCount > 0) _loadingPopup.Show();
                else _loadingPopup.Hide();
            }
            if (_toast != null) _toast.transform.SetAsLastSibling();
            UpdateNavigationBlock();
        }

        private void UpdateNavigationBlock()
        {
            if (BlockingRequestCount > 0)
            {
                var events = EventSystem.current;
                if (events != null)
                {
                    if (!_navigationStates.ContainsKey(events)) _navigationStates.Add(events, events.sendNavigationEvents);
                    events.sendNavigationEvents = false;
                }
            }
            else
            {
                foreach (var pair in _navigationStates)
                    if (pair.Key != null) pair.Key.sendNavigationEvents = pair.Value;
                _navigationStates.Clear();
            }
        }

        private void OnApplicationQuit() { _quitting = true; }
        private void OnDisable()
        {
            _stopping = true;
            foreach (var pending in new List<PendingRequest>(_pending.Values)) Finish(pending, NetworkRequestStatus.Cancelled);
            if (_loadingPopup != null) _loadingPopup.gameObject.SetActive(false);
            if (_toast != null) _toast.Hide();
            _stopping = false;
        }
        private void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
