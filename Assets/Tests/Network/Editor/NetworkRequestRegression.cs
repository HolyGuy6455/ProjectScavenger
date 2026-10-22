using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Scavenger.Network;
using Scavenger.Network.DTOs;
using Scavenger.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 빈 씬의 Play 모드에서 실행합니다. 실제 게임 서버 대신 프로세스 내부 loopback 서버만 사용합니다.
public static class NetworkRequestRegression
{
    [Serializable] public class Response { public int value; }
    private static IEnumerator _suite;
    private static LocalServer _server;
    private static NetworkRequestManager _manager;
    private static GameObject _events, _canvas, _owner;
    private static readonly System.Collections.Generic.List<GameObject> _integrationObjects = new System.Collections.Generic.List<GameObject>();
    private static bool _oldBackground;
    private static float _oldTimeScale;
    private static int _checks;
    private static double _deadline;
    private static string _unexpectedLog;
    public static string Result { get; private set; }

    [MenuItem("Tools/Tests/Network Requests (Empty Scene Play Mode)")]
    public static void Run()
    {
        if (_suite != null) throw new InvalidOperationException("Already running");
        if (!Application.isPlaying || Object.FindFirstObjectByType<NetworkRequestManager>() != null
            || Object.FindFirstObjectByType<NetworkAuthManager>() != null)
            throw new InvalidOperationException("Enter Play mode in an empty scene first.");
        _oldBackground = Application.runInBackground;
        _oldTimeScale = Time.timeScale;
        Application.runInBackground = true;
        _checks = 0;
        _unexpectedLog = null;
        Application.logMessageReceived += CheckLog;
        _deadline = EditorApplication.timeSinceStartup + 30;
        _server = new LocalServer();
        _manager = NetworkRequestManager.Instance;
        Result = "RUNNING";
        _suite = Cases();
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup > _deadline)
                throw new Exception("Suite timed out or Play mode stopped");
            if (_suite.MoveNext()) return;
            Check(_unexpectedLog == null, "Unexpected Unity error: " + _unexpectedLog);
            Result = "PASS " + _checks + " checks";
            Debug.Log("[NetworkRequestRegression] " + Result);
        }
        catch (Exception ex)
        {
            Result = "FAIL after " + _checks + ": " + ex;
            Debug.LogError("[NetworkRequestRegression] " + Result);
        }
        Cleanup();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static void CheckLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message.Contains("EXPECTED regression") || message.Contains("JSON object expected")
            || message.Contains("Invalid response fields")) return;
        _unexpectedLog = message;
    }

    private static NetworkRequestHandle Send(string path, NetworkRequestOptions options = null,
        Action<Response> success = null, Action<NetworkError> failure = null, Action<NetworkRequestHandle> finished = null)
    {
        return _manager.Send(NetworkRequestManager.CreateJsonRequest(_server.Url + path, "GET"), success ?? (_ => { }),
            options ?? new NetworkRequestOptions { ShowErrorToast = false }, failure,
            response => response.value == 7, finished);
    }

    private static IEnumerator Cases()
    {
        _events = new GameObject("RegressionEvents", typeof(EventSystem));
        var events = _events.GetComponent<EventSystem>();
        _canvas = new GameObject("UnderlyingCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var button = new GameObject("UnderlyingButton", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(_canvas.transform, false);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;

        Time.timeScale = 0;
        var first = Send("/slow");
        var second = Send("/slower");
        var background = Send("/slow", new NetworkRequestOptions { ShowLoading = false });
        Check(first.Id != second.Id, "Unique request IDs");
        Check(_manager.ActiveRequestCount == 3 && _manager.BlockingRequestCount == 2, "Concurrent and background counts");
        Check(NetworkRequestManager.IsInputBlocked && !events.sendNavigationEvents, "Input immediately blocked");
        var loading = Object.FindFirstObjectByType<NetworkLoadingPopup>();
        Check(loading != null, "Loading prefab instantiated");
        var spinner = loading.transform.Find("Root/Spinner");
        float angle = spinner.localEulerAngles.z;
        int frame = Time.frameCount;
        while (Time.frameCount < frame + 3) yield return null;
        Check(Mathf.Abs(Mathf.DeltaAngle(angle, spinner.localEulerAngles.z)) > 0.1f, "Spinner moves at timeScale zero");
        var hits = new System.Collections.Generic.List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) }, hits);
        Check(hits.Count > 0 && hits[0].gameObject == loading.gameObject, "Overlay blocks underlying button");
        while (!first.IsDone || !background.IsDone) yield return null;
        Check(first.Status == NetworkRequestStatus.Succeeded, "Valid JSON succeeds");
        Check(!second.IsDone && loading.gameObject.activeSelf, "One response does not hide another request's loader");
        while (!second.IsDone) yield return null;
        Check(_manager.ActiveRequestCount == 0 && !loading.gameObject.activeSelf, "Last request hides loader");
        Check(events.sendNavigationEvents && !NetworkRequestManager.IsInputBlocked, "Navigation restored");

        int before = _server.Hits, finished = 0, failures = 0;
        var options = new NetworkRequestOptions { DuplicateKey = "same", DuplicatePolicy = DuplicateRequestPolicy.Reject };
        first = Send("/slow", options);
        var duplicate = Send("/slow", options, failure: _ => failures++, finished: _ => finished++);
        Check(duplicate.Status == NetworkRequestStatus.AlreadyInProgress && finished == 1 && failures == 0, "Duplicate rejected without failure callback");
        Check(_manager.ActiveRequestCount == 1, "Rejected request does not count");
        second = Send("/slow", new NetworkRequestOptions { DuplicateKey = "same", DuplicatePolicy = DuplicateRequestPolicy.Allow });
        Check(_manager.ActiveRequestCount == 2, "Allow sends same key concurrently");
        while (!first.IsDone || !second.IsDone) yield return null;
        Check(_server.Hits - before == 2, "Rejected duplicate never reached server");
        first = Send("/ok", options);
        while (!first.IsDone) yield return null;
        Check(first.Status == NetworkRequestStatus.Succeeded, "Key reusable after completion");

        NetworkError received = null;
        first = Send("/failure", new NetworkRequestOptions(), failure: error => received = error);
        while (!first.IsDone) yield return null;
        Check(first.Status == NetworkRequestStatus.Failed && received.HttpStatusCode == 400 && received.ServerCode == 5, "HTTP and server error preserved");
        var toast = Object.FindFirstObjectByType<ToastPopup>();
        Check(toast != null && toast.gameObject.activeSelf, "Default error toast");
        Check(toast.GetComponentInChildren<TMPro.TextMeshProUGUI>().text == "재료가 부족합니다.", "Server error message shown");
        toast.Hide();
        received = null;
        first = Send("/failure", new NetworkRequestOptions { ShowErrorToast = false }, failure: error => received = error);
        while (!first.IsDone) yield return null;
        Check(received != null && !toast.gameObject.activeSelf, "Custom handling suppresses default toast");
        first = Send("/business", failure: error => received = error);
        while (!first.IsDone) yield return null;
        Check(received.Kind == NetworkErrorKind.Server && received.ServerCode == 5, "Business failure on HTTP 200");
        first = Send("/invalid");
        while (!first.IsDone) yield return null;
        Check(first.Error.Kind == NetworkErrorKind.InvalidResponse, "Malformed response fails and releases loader");
        first = Send("/invalid-fields");
        while (!first.IsDone) yield return null;
        Check(first.Error.Kind == NetworkErrorKind.InvalidResponse, "Missing required response fields fail");

        first = Send("/timeout", new NetworkRequestOptions { TimeoutSeconds = 1, ShowErrorToast = false });
        while (!first.IsDone) yield return null;
        Check(first.Error.Kind == NetworkErrorKind.Timeout && _manager.ActiveRequestCount == 0, "Timeout releases loader at timeScale zero");
        finished = 0;
        first = Send("/slow", finished: _ => finished++);
        first.Cancel(); first.Cancel();
        Check(first.Status == NetworkRequestStatus.Cancelled && finished == 1 && _manager.ActiveRequestCount == 0, "Cancel is idempotent");

        _owner = new GameObject("RequestOwner", typeof(CanvasGroup));
        var owner = _owner.GetComponent<CanvasGroup>();
        bool applied = false;
        first = Send("/slow", new NetworkRequestOptions { Owner = owner }, success: _ => applied = true);
        _owner.SetActive(false);
        while (!first.IsDone) yield return null;
        Check(applied && first.Status == NetworkRequestStatus.Succeeded, "Mutation survives inactive owner");
        _owner.SetActive(true);
        first = Send("/slow", new NetworkRequestOptions { Owner = owner, CancelWhenOwnerUnavailable = true });
        _owner.SetActive(false);
        while (!first.IsDone) yield return null;
        Check(first.Status == NetworkRequestStatus.Cancelled, "View-only request cancels on inactive owner");
        _owner.SetActive(true);
        first = Send("/slow", new NetworkRequestOptions { Owner = owner, CancelWhenOwnerUnavailable = true });
        Object.Destroy(_owner);
        while (!first.IsDone) yield return null;
        Check(first.Status == NetworkRequestStatus.Cancelled, "Destroyed owner cancels view-only request");

        bool followupApplied = false;
        NetworkRequestHandle followup = null;
        first = Send("/ok", success: _ => followup = Send("/slow", success: __ => followupApplied = true));
        while (!first.IsDone) yield return null;
        Check(followup != null && _manager.ActiveRequestCount == 1 && NetworkRequestManager.IsInputBlocked, "Follow-up request keeps overlay active");
        while (!followup.IsDone) yield return null;
        Check(followupApplied, "Follow-up callback runs");

        first = Send("/ok", success: _ => throw new Exception("EXPECTED regression callback failure"));
        while (!first.IsDone) yield return null;
        Check(first.Error.Kind == NetworkErrorKind.ResponseProcessing && _manager.ActiveRequestCount == 0, "Success callback exception releases request");
        first = Send("/failure", failure: _ => throw new Exception("EXPECTED regression failure callback failure"),
            finished: _ => throw new Exception("EXPECTED regression finished callback failure"));
        while (!first.IsDone) yield return null;
        Check(_manager.ActiveRequestCount == 0 && !NetworkRequestManager.IsInputBlocked, "Failure and finished callback exceptions release request");

        var otherScene = SceneManager.CreateScene("NetworkRequestRegressionOtherScene");
        var sceneOwner = new GameObject("SceneRequestOwner", typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(sceneOwner, otherScene);
        first = Send("/slow");
        second = Send("/slow", new NetworkRequestOptions { Owner = sceneOwner.GetComponent<CanvasGroup>(), CancelWhenOwnerUnavailable = true });
        SceneManager.SetActiveScene(otherScene);
        var unload = SceneManager.UnloadSceneAsync(otherScene);
        while (!first.IsDone) yield return null;
        Check(_manager != null && first.Status == NetworkRequestStatus.Succeeded, "Manager survives scene change");
        while (!unload.isDone) yield return null;
        Check(second.Status == NetworkRequestStatus.Cancelled, "Scene unload cancels view-only owner");

        // 실제 UI 호출부도 요청 당시의 캐릭터와 데이터를 유지하는지 검증합니다.
        var authObject = CreateIntegrationObject("RegressionAuth");
        var auth = authObject.AddComponent<NetworkAuthManager>();
        auth.enabled = false; // 자동 로그인 Start를 실행하지 않습니다.
        SetField(auth, "<ServerUrl>k__BackingField", _server.Url);
        SetField(auth, "<AuthToken>k__BackingField", "regression-token");
        SetField(auth, "<Username>k__BackingField", "regression-user");
        var dataObject = CreateIntegrationObject("RegressionUserData");
        var data = dataObject.AddComponent<UserDataManager>();
        data.enabled = false;
        data.OwnedCharacters.Add(new UserCharacterDto { charId = 42, level = 1 });
        data.OwnedCharacters.Add(new UserCharacterDto { charId = 43, level = 1 });
        var skillObject = CreateIntegrationObject("RegressionSkillPopup");
        var skill = skillObject.AddComponent<SkillLevelUpPopup>();
        SetField(skill, "_charId", 42); SetField(skill, "_targetSkillId", 101);
        InvokePrivate(skill, "OnLevelUpClicked");
        Object.Destroy(skillObject);
        while (_manager.ActiveRequestCount > 0) yield return null;
        Check(data.GetCharacter(42).unlockedSkills.Contains(101), "Skill result applies after popup destruction");
        Check(!data.GetCharacter(43).unlockedSkills.Contains(101), "Skill result does not change another character");
        Check(data.GetGameItemAmount(1) == 7, "Skill material balance applied");
        Check(_server.LastSkillBody.Contains("\"charId\":42") && _server.LastSkillBody.Contains("\"targetSkillId\":101"), "Skill sends captured request body");

        var trainObject = CreateIntegrationObject("RegressionTrainingPanel");
        trainObject.SetActive(false);
        var train = trainObject.AddComponent<TrainCharacterPanel>();
        var rack = new GameObject("CharacterRack");
        rack.transform.SetParent(trainObject.transform, false);
        SetField(train, "leftCharacterRack", rack.transform);
        trainObject.SetActive(true);
        SetField(train, "_selectedCharId", 42); SetField(train, "_currentTargetLevel", 15);
        train.OnClickLevelUp();
        trainObject.SetActive(false);
        SetField(train, "_selectedCharId", 43);
        while (_manager.ActiveRequestCount > 0) yield return null;
        Check(data.GetCharacter(42).level == 15 && data.GetCharacter(43).level == 1, "Training applies to original character after panel closes");
        Check(data.GetGameItemAmount(1) == 2, "Training material balance applied");

        _server.FailSkill = true;
        skillObject = CreateIntegrationObject("RegressionFailedSkillPopup");
        skill = skillObject.AddComponent<SkillLevelUpPopup>();
        SetField(skill, "_charId", 43); SetField(skill, "_targetSkillId", 200);
        InvokePrivate(skill, "OnLevelUpClicked");
        while (_manager.ActiveRequestCount > 0) yield return null;
        Check(toast.GetComponentInChildren<TMPro.TextMeshProUGUI>().text == "스킬 레벨업에 필요한 재료가 부족합니다.", "Skill-specific failure toast retained");

        first = Send("/slow");
        _manager.gameObject.SetActive(false);
        Check(first.Status == NetworkRequestStatus.Cancelled && _manager.ActiveRequestCount == 0, "Manager shutdown cancels and releases requests");
    }

    private static GameObject CreateIntegrationObject(string name)
    {
        var go = new GameObject(name);
        _integrationObjects.Add(go);
        return go;
    }
    private static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void InvokePrivate(object target, string name) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private static void Cleanup()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CheckLog;
        _suite = null;
        _server?.Dispose();
        if (_manager != null) Object.Destroy(_manager.gameObject);
        if (_owner != null) Object.Destroy(_owner);
        if (_canvas != null) Object.Destroy(_canvas);
        if (_events != null) Object.Destroy(_events);
        foreach (var go in _integrationObjects) if (go != null) Object.Destroy(go);
        _integrationObjects.Clear();
        Time.timeScale = _oldTimeScale;
        Application.runInBackground = _oldBackground;
    }

    // 외부 서비스/계정 상태를 변경하지 않는 최소 HTTP fixture.
    private sealed class LocalServer : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private int _hits;
        public volatile bool FailSkill;
        public volatile string LastSkillBody;
        public int Hits => Volatile.Read(ref _hits);
        public string Url { get; }
        public LocalServer()
        {
            _listener.Start();
            Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Accept();
        }
        private async Task Accept()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Serve(client);
                }
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }
        private async Task Serve(TcpClient client)
        {
            using (client)
            try
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                string line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null) return;
                string path = line.Split(' ')[1];
                int length = 0;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line.Substring(15).Trim());
                var requestBody = new char[length];
                for (int read = 0; read < length;)
                {
                    int count = await reader.ReadAsync(requestBody, read, length - read).ConfigureAwait(false);
                    if (count == 0) break;
                    read += count;
                }
                if (path == "/api/game/levelup-skill") LastSkillBody = new string(requestBody);
                Interlocked.Increment(ref _hits);
                int delay = path == "/slow" || path.StartsWith("/api/game/levelup-") ? 350 : path == "/slower" ? 1000 : path == "/timeout" ? 2500 : 20;
                await Task.Delay(delay, _stop.Token).ConfigureAwait(false);
                string body = path == "/invalid" ? "<html>oops</html>" : path == "/invalid-fields" ? "{}"
                    : path == "/failure" || path == "/business" ? "{\"errorCode\":5,\"message\":\"재료가 부족합니다.\"}" : "{\"value\":7}";
                if (path == "/api/game/levelup-skill") body = FailSkill ? "{\"errorCode\":5,\"message\":\"Insufficient items\"}" : "{\"errorCode\":0,\"unlockedSkills\":[101],\"equippedSkills\":[101,0,0,0],\"updatedGameItems\":[{\"itemId\":1,\"amount\":7}]}";
                if (path == "/api/game/levelup-character") body = "{\"level\":15,\"updatedGameItems\":[{\"itemId\":1,\"amount\":2}]}";
                if (path == "/api/game/character/list") body = "{\"characters\":[{\"charId\":42,\"level\":1,\"unlockedSkills\":[101]},{\"charId\":43,\"level\":1,\"unlockedSkills\":[]}],\"selectedDeck\":[42,43,0,0]}";
                if (path == "/api/game/item/list") body = "{\"gameItems\":[{\"itemId\":1,\"amount\":2}],\"userCurrencys\":[]}";
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                string status = path == "/failure" || (path == "/api/game/levelup-skill" && FailSkill) ? "400 Bad Request" : "200 OK";
                byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }
        public void Dispose() { _stop.Cancel(); _listener.Stop(); }
    }
}
