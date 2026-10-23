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
        _deadline = EditorApplication.timeSinceStartup + 60;
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
        var first = Send("/hold-first");
        var second = Send("/hold-second");
        var background = Send("/slow", new NetworkRequestOptions { ShowLoading = false });
        Check(first.Id != second.Id, "Unique request IDs");
        Check(_manager.ActiveRequestCount == 3 && _manager.BlockingRequestCount == 2, "Concurrent and background counts");
        Check(NetworkRequestManager.IsInputBlocked && !events.sendNavigationEvents, "Input immediately blocked");
        var loading = Object.FindFirstObjectByType<NetworkLoadingPopup>();
        Check(loading != null, "Loading prefab instantiated");
        var loadingGroup = loading.GetComponent<CanvasGroup>();
        Check(loadingGroup.alpha == 0f && loadingGroup.blocksRaycasts, "Fade starts transparent with immediate input blocking");
        var spinner = loading.transform.Find("Root/Spinner");
        float angle = spinner.localEulerAngles.z;
        int frame = Time.frameCount;
        while (Time.frameCount < frame + 3) yield return null;
        Check(Mathf.Abs(Mathf.DeltaAngle(angle, spinner.localEulerAngles.z)) > 0.1f, "Spinner moves at timeScale zero");
        Check(loadingGroup.alpha > 0f, "Fade-in progresses at timeScale zero");
        var hits = new System.Collections.Generic.List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) }, hits);
        Check(hits.Count > 0 && hits[0].gameObject == loading.gameObject, "Overlay blocks underlying button");
        _server.ReleaseFirst = true;
        while (!first.IsDone || !background.IsDone) yield return null;
        Check(first.Status == NetworkRequestStatus.Succeeded, "Valid JSON succeeds");
        Check(!second.IsDone && loading.gameObject.activeSelf, "One response does not hide another request's loader");
        while (loadingGroup.alpha < 1f) yield return null;
        _server.ReleaseSecond = true;
        while (!second.IsDone) yield return null;
        Check(_manager.ActiveRequestCount == 0 && !loadingGroup.blocksRaycasts, "Last response immediately releases pointer blocking");
        Check(loading.gameObject.activeSelf && loadingGroup.alpha > 0f, "Last response fades out instead of disappearing instantly");
        Check(events.sendNavigationEvents && !NetworkRequestManager.IsInputBlocked, "Navigation restored");
        while (loading.gameObject.activeSelf) yield return null;
        Check(loadingGroup.alpha == 0f, "Fade-out completes at timeScale zero");

        // 짧은 요청은 완전히 불투명해질 때까지 기다리지 않고 현재 투명도에서 사라집니다.
        first = Send("/ok");
        float peakAlpha = loadingGroup.alpha;
        while (!first.IsDone) { peakAlpha = Mathf.Max(peakAlpha, loadingGroup.alpha); yield return null; }
        Check(peakAlpha < 1f && loadingGroup.alpha < 1f, "Quick response never flashes to full opacity");
        while (loading.gameObject.activeSelf) yield return null;

        // 에디터의 프레임 간격과 서버 응답 시간이 달라도 중간 페이드 상태를 확실히 관찰합니다.
        SetField(loading, "fadeOutDuration", 10f);
        _server.ReleaseFirst = false;
        _server.ReleaseSecond = false;
        first = Send("/hold-first");
        while (loadingGroup.alpha < 1f) yield return null;
        _server.ReleaseFirst = true;
        while (!first.IsDone) yield return null;
        while (loadingGroup.alpha >= 0.99f) yield return null;
        float fadeOutAlpha = loadingGroup.alpha;
        second = Send("/hold-second");
        Check(fadeOutAlpha > 0f && Mathf.Approximately(loadingGroup.alpha, fadeOutAlpha),
            "New request preserves current fade-out opacity: " + fadeOutAlpha + " -> " + loadingGroup.alpha);
        Check(loadingGroup.blocksRaycasts && NetworkRequestManager.IsInputBlocked, "New request immediately restores input blocking");
        while (!second.IsDone && loadingGroup.alpha <= fadeOutAlpha) yield return null;
        Check(loadingGroup.alpha > fadeOutAlpha, "Interrupted fade-out reverses into fade-in");
        SetField(loading, "fadeOutDuration", 0.2f);
        _server.ReleaseSecond = true;
        while (!second.IsDone) yield return null;
        while (loading.gameObject.activeSelf) yield return null;

        SetField(loading, "fadeInDuration", 0f);
        SetField(loading, "fadeOutDuration", 0f);
        int beforeZeroFade = _server.Hits;
        first = Send("/slow");
        Check(loadingGroup.alpha == 1f, "Zero fade-in duration shows immediately");
        while (_server.Hits == beforeZeroFade) yield return null;
        first.Cancel();
        Check(!loading.gameObject.activeSelf && loadingGroup.alpha == 0f, "Zero fade-out duration hides immediately");
        SetField(loading, "fadeInDuration", 0.2f);
        SetField(loading, "fadeOutDuration", 0.2f);

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

        toast.Hide();
        var toastCases = ToastCases();
        while (toastCases.MoveNext()) yield return toastCases.Current;
        var fadeCases = ToastFadeCases();
        while (fadeCases.MoveNext()) yield return fadeCases.Current;
        var earlyExitCases = ToastEarlyExitCases();
        while (earlyExitCases.MoveNext()) yield return earlyExitCases.Current;

        first = Send("/slow");
        toast.Show("Shutdown active", 10f);
        toast.Show("Shutdown pending", 10f);
        _manager.gameObject.SetActive(false);
        Check(first.Status == NetworkRequestStatus.Cancelled && _manager.ActiveRequestCount == 0, "Manager shutdown cancels and releases requests");
        _manager.gameObject.SetActive(true);
        Check(!toast.gameObject.activeSelf, "Re-enabling manager does not revive stale toast");
        toast.Show("Restarted", 0.1f);
        double restartDeadline = Time.realtimeSinceStartupAsDouble + 2;
        while (toast.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < restartDeadline) yield return null;
        Check(!toast.gameObject.activeSelf, "Manager shutdown also clears the shared toast queue");
    }

    private static IEnumerator ToastCases()
    {
        var uiObject = CreateIntegrationObject("RegressionOutGameUI");
        uiObject.SetActive(false); // 로비 패널을 생성하는 Start는 실행하지 않습니다.
        var ui = uiObject.AddComponent<OutGameUIManager>();
        var toast = ui.ShowToast("First", 0.3f);
        var label = toast.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        Check(label.text == "First" && toast.isActiveAndEnabled, "Idle toast displays immediately through UI manager");
        Check(_manager.ShowToast("First", 10f) == toast, "UI and network share one cached toast");
        ui.ShowToast("Second", 0.3f);
        _manager.ShowToast("Second", 10f);
        _manager.ShowToast("Third", 0.3f);
        Check(label.text == "First", "Queued messages do not replace the current toast");
        double deadline = Time.realtimeSinceStartupAsDouble + 5;
        while (label.text == "First" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(label.text == "Second", "Active duplicate does not extend display duration; FIFO advances at timeScale zero");
        while (label.text == "Second" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(label.text == "Third", "Pending duplicate neither evicts a message nor replaces its duration");
        while (toast.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(!toast.gameObject.activeSelf, "Toast hides when the queue is empty");

        toast.Show("Burst start", 0.3f);
        for (int i = 0; i < 100; i++) _manager.ShowToast("Burst " + i, 0.3f);
        Check(label.text == "Burst start", "Burst preserves the currently visible message");
        deadline = Time.realtimeSinceStartupAsDouble + 5;
        while (label.text == "Burst start" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(label.text == "Burst 98", "Only the newest two messages remain pending after 100 requests");
        while (label.text == "Burst 98" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(label.text == "Burst 99", "Newest pending message follows in order");
        while (toast.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(!toast.gameObject.activeSelf, "Burst drains without a long backlog");

        // 긴 페이드아웃으로 대기 시간을 확보합니다. 새 요청도 페이드아웃을 단축하면 안 됩니다.
        SetField(toast, "fadeOutDuration", 5.5f);
        double started = Time.realtimeSinceStartupAsDouble;
        toast.Show("Long fade-out", 10f);
        toast.Show("Expired pending", 10f);
        while (Time.realtimeSinceStartupAsDouble - started < 3) yield return null;
        toast.Show("Expired pending", 10f);
        while (Time.realtimeSinceStartupAsDouble - started < 4.5) yield return null;
        toast.Show("Fresh pending", 0.3f);
        while (label.text == "Long fade-out" && Time.realtimeSinceStartupAsDouble - started < 10) yield return null;
        Check(label.text == "Fresh pending", "Pending messages older than five seconds expire despite duplicate requests");
        SetField(toast, "fadeOutDuration", 0.2f);
        while (toast.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble - started < 10) yield return null;
        Check(!toast.gameObject.activeSelf, "Fresh pending message still displays after stale message is discarded");

        toast.Show("Dismiss", 10f);
        toast.Show("Discard with dismissal", 10f);
        toast.Hide();
        toast.Show("After dismissal", 0.1f);
        deadline = Time.realtimeSinceStartupAsDouble + 2;
        while (toast.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(!toast.gameObject.activeSelf && label.text == "After dismissal", "Explicit hide clears old pending messages");
        _manager.ShowToast("  ");
        Check(!toast.gameObject.activeSelf, "Empty messages do not activate the overlay");

        _server.ReleaseFirst = false;
        var remaining = Send("/hold-first");
        var failed = Send("/failure", new NetworkRequestOptions());
        while (!failed.IsDone) yield return null;
        var loading = Object.FindFirstObjectByType<NetworkLoadingPopup>();
        Check(!remaining.IsDone && NetworkRequestManager.IsInputBlocked && loading.gameObject.activeSelf,
            "One failed request keeps another request's loading overlay active");
        Check(label.text == "재료가 부족합니다." && toast.isActiveAndEnabled,
            "Failure toast displays before the remaining request completes");
        Check(toast.transform.parent == loading.transform.parent
            && toast.transform.GetSiblingIndex() > loading.transform.GetSiblingIndex(), "Toast renders above the loading popup");
        Check(!toast.GetComponent<CanvasGroup>().blocksRaycasts, "Toast does not intercept overlay input blocking");
        Check(ui.ShowToast("UI during loading", 0.3f) == toast, "UI notification joins the same queue during loading");
        deadline = Time.realtimeSinceStartupAsDouble + 4;
        while (label.text != "UI during loading" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(label.text == "UI during loading" && NetworkRequestManager.IsInputBlocked,
            "Queued UI notification displays while another request is still pending");
        Check(Object.FindObjectsByType<ToastPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "All notifications reuse one toast instance");
        _server.ReleaseFirst = true;
        while (!remaining.IsDone) yield return null;
        toast.Hide();
    }

    private static IEnumerator ToastFadeCases()
    {
        var toast = _manager.ShowToast("Fade A", 0.3f);
        var label = toast.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        var group = toast.GetComponent<CanvasGroup>();
        Check(group.alpha == 0f, "Toast starts fully transparent");
        toast.Show("Fade B", 0.3f);
        float previousAlpha = group.alpha;
        bool sawFadeIn = false, sawFadeOut = false;
        double deadline = Time.realtimeSinceStartupAsDouble + 6;
        while (label.text == "Fade A" && Time.realtimeSinceStartupAsDouble < deadline)
        {
            if (group.alpha > previousAlpha && group.alpha < 1f) sawFadeIn = true;
            if (group.alpha < previousAlpha && group.alpha > 0f) sawFadeOut = true;
            previousAlpha = group.alpha;
            yield return null;
        }
        Check(sawFadeIn && sawFadeOut, "Toast smoothly fades in and out at timeScale zero");
        Check(label.text == "Fade B" && group.alpha == 0f,
            "Queued toast replaces the previous message only at full transparency");

        bool queuedDuringFadeOut = false;
        previousAlpha = group.alpha;
        while (label.text == "Fade B" && Time.realtimeSinceStartupAsDouble < deadline)
        {
            if (!queuedDuringFadeOut && group.alpha < previousAlpha && group.alpha > 0f)
            {
                float beforeEnqueue = group.alpha;
                toast.Show("Fade C", 0.3f);
                Check(label.text == "Fade B" && group.alpha == beforeEnqueue,
                    "New request during fade-out does not replace or restart the visible toast");
                queuedDuringFadeOut = true;
            }
            previousAlpha = group.alpha;
            yield return null;
        }
        Check(queuedDuringFadeOut && label.text == "Fade C" && group.alpha == 0f,
            "Request received during fade-out begins its own fade-in after the previous toast disappears");
        while (toast.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(!toast.gameObject.activeSelf && group.alpha == 0f, "Last toast deactivates fully transparent");

        SetField(toast, "fadeInDuration", 0f);
        SetField(toast, "fadeOutDuration", 0f);
        toast.Show("Zero fade", 0.3f);
        deadline = Time.realtimeSinceStartupAsDouble + 2;
        while (group.alpha < 1f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(group.alpha == 1f, "Zero fade duration reaches full opacity");
        while (toast.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(!toast.gameObject.activeSelf && group.alpha == 0f, "Zero fade duration still completes and clears opacity");
        SetField(toast, "fadeInDuration", 0.2f);
        SetField(toast, "fadeOutDuration", 0.2f);
    }

    private static IEnumerator ToastEarlyExitCases()
    {
        var toast = _manager.ShowToast("Early A", 5f);
        var label = toast.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        var group = toast.GetComponent<CanvasGroup>();
        double deadline = Time.realtimeSinceStartupAsDouble + 3;
        while (group.alpha < 1f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(group.alpha == 1f, "Toast reaches its fully visible hold phase");

        // 같은 문구와 빈 문구는 현재 메시지의 유지 시간을 조기 종료하지 않습니다.
        toast.Show("Early A", 10f);
        toast.Show("  ");
        double holdStarted = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - holdStarted < 0.3) yield return null;
        Check(label.text == "Early A" && group.alpha == 1f, "Duplicate and empty messages do not interrupt the current hold");

        double requestedAt = Time.realtimeSinceStartupAsDouble;
        toast.Show("Early B", 0.5f);
        Check(label.text == "Early A" && group.alpha == 1f, "New message does not instantly replace or hide the current toast");
        while (label.text == "Early A" && Time.realtimeSinceStartupAsDouble - requestedAt < 2) yield return null;
        double transitionTime = Time.realtimeSinceStartupAsDouble - requestedAt;
        Check(label.text == "Early B" && transitionTime >= 0.18 && transitionTime < 2,
            "New request skips the five-second hold but preserves the full 0.2-second fade-out: " + transitionTime);
        Check(group.alpha == 0f, "Replacement starts its own fade-in from zero alpha");

        deadline = Time.realtimeSinceStartupAsDouble + 3;
        while (group.alpha < 1f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        double nextHoldStarted = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - nextHoldStarted < 0.2) yield return null;
        Check(label.text == "Early B" && group.alpha == 1f, "Replacement receives its own hold duration");
        while (toast.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(!toast.gameObject.activeSelf && group.alpha == 0f, "Replacement fades out normally when no new requests arrive");
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
        public volatile bool ReleaseFirst;
        public volatile bool ReleaseSecond;
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
                while ((path == "/hold-first" && !ReleaseFirst) || (path == "/hold-second" && !ReleaseSecond))
                    await Task.Delay(10, _stop.Token).ConfigureAwait(false);
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
