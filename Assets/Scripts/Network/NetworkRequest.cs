using System;
using UnityEngine;

namespace Scavenger.Network
{
    public enum DuplicateRequestPolicy { Allow, Reject }
    public enum NetworkRequestStatus { Pending, Succeeded, Failed, Cancelled, AlreadyInProgress }
    public enum NetworkErrorKind { Connection, Timeout, Http, Server, InvalidResponse, ResponseProcessing }

    public sealed class NetworkRequestOptions
    {
        public string Name;
        // 같은 작업을 식별하는 키입니다. 전송마다 달라지는 요청 ID와 구분합니다.
        public string DuplicateKey;
        public DuplicateRequestPolicy DuplicatePolicy = DuplicateRequestPolicy.Allow;
        public bool ShowLoading = true;
        public bool ShowErrorToast = true;
        // 0이면 매니저의 기본값을 사용합니다.
        public int TimeoutSeconds;
        // 화면 전용 조회에만 사용합니다. 서버 상태 변경 요청은 기본적으로 끝까지 처리합니다.
        public Behaviour Owner;
        public bool CancelWhenOwnerUnavailable;

        internal NetworkRequestOptions Snapshot() => (NetworkRequestOptions)MemberwiseClone();
    }

    public sealed class NetworkError
    {
        public NetworkErrorKind Kind { get; internal set; }
        public long HttpStatusCode { get; internal set; }
        public int? ServerCode { get; internal set; }
        public string ServerMessage { get; internal set; }

        public string DefaultMessage
        {
            get
            {
                if (Kind == NetworkErrorKind.Timeout) return LocalizationManager.Get("UI_NET_ERR_TIMEOUT");
                if (Kind == NetworkErrorKind.Connection) return LocalizationManager.Get("UI_NET_ERR_CONNECTION");
                if (HttpStatusCode == 401 || HttpStatusCode == 403) return LocalizationManager.Get("UI_NET_ERR_AUTH");
                if ((Kind == NetworkErrorKind.Http || Kind == NetworkErrorKind.Server)
                    && HttpStatusCode < 500 && !string.IsNullOrWhiteSpace(ServerMessage)) return ServerMessage;
                if (Kind == NetworkErrorKind.InvalidResponse || Kind == NetworkErrorKind.ResponseProcessing)
                    return LocalizationManager.Get("UI_NET_ERR_INVALID_RESPONSE");
                return LocalizationManager.Get("UI_NET_ERR_DEFAULT");
            }
        }
    }

    // yield return으로 기다릴 수도 있습니다. 화면 코루틴이 중단되어도 실제 요청은 매니저가 유지합니다.
    public sealed class NetworkRequestHandle : CustomYieldInstruction
    {
        private Action _cancel;
        public long Id { get; internal set; }
        public string Name { get; internal set; }
        public string DuplicateKey { get; internal set; }
        public double StartedAt { get; internal set; }
        public NetworkRequestStatus Status { get; internal set; }
        public NetworkError Error { get; internal set; }
        public bool IsDone => Status != NetworkRequestStatus.Pending;
        public override bool keepWaiting => !IsDone;

        public void Cancel() => _cancel?.Invoke();
        internal void SetCancel(Action cancel) => _cancel = cancel;
    }
}
