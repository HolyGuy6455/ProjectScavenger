using UnityEngine;
using Cinemachine;

public class CharacterCinematicController : MonoBehaviour
{
    [Header("Cinematic Camera Settings")]
    [Tooltip("이 캐릭터에 부착된 시네마틱 연출 전용 시네마머신 카메라 (예: CameraPosition 뼈대 내의 VCam)")]
    [SerializeField] private CinemachineVirtualCamera cinematicCamera;

    [Tooltip("Entering 연출이 끝나기 몇 초 전에 게임 카메라로 복귀할지 설정합니다.")]
    [SerializeField, Min(0f)] private float cameraReturnLeadSeconds = 1f;

    [SerializeField] private bool overrideFieldOfView;
    [SerializeField, Range(1f, 179f)] private float enteringExitFieldOfView = 70f;
    [SerializeField, Range(1f, 179f)] private float ultimateFieldOfView = 58f;

    private int nextCinematicToken;
    private int activeCinematicToken;
    private Transform followBeforeReturn;
    private Transform lookAtBeforeReturn;
    private Transform returnCameraTarget;

    public CinemachineVirtualCamera CinematicCamera => cinematicCamera;
    public float CameraReturnLeadSeconds => Mathf.Max(0f, cameraReturnLeadSeconds);
    public bool IsCinematicActive => activeCinematicToken != 0;

    public void ConfigureCamera(float enteringExitFov, float ultimateFov, float returnLeadSeconds)
    {
        overrideFieldOfView = true;
        enteringExitFieldOfView = Mathf.Clamp(enteringExitFov, 1f, 179f);
        ultimateFieldOfView = Mathf.Clamp(ultimateFov, 1f, 179f);
        cameraReturnLeadSeconds = Mathf.Max(0f, returnLeadSeconds);
    }

    public void PrepareForCinematic(string stateName)
    {
        if (cinematicCamera == null || !overrideFieldOfView) return;
        cinematicCamera.m_Lens.FieldOfView = stateName == "Ultimate"
            ? ultimateFieldOfView
            : enteringExitFieldOfView;
    }

    public int BeginCinematic(string stateName, bool isOwner)
    {
        CancelCinematic();
        if (!isActiveAndEnabled) return 0;

        unchecked { nextCinematicToken++; }
        if (nextCinematicToken == 0) nextCinematicToken++;
        activeCinematicToken = nextCinematicToken;
        PrepareForCinematic(stateName);
        if (isOwner && cinematicCamera != null) cinematicCamera.Priority = 20;
        return activeCinematicToken;
    }

    public void EndCinematic(int token, bool releaseCamera = true)
    {
        if (token == 0 || token != activeCinematicToken) return;
        activeCinematicToken = 0;
        if (releaseCamera && cinematicCamera != null)
        {
            HoldReturnCamera();
            cinematicCamera.Priority = 0;
        }
    }

    public void CancelCinematic()
    {
        activeCinematicToken = 0;
        ReleaseReturnCamera();
        // Also releases a completed Exit view retained by EndCinematic(token, false).
        if (cinematicCamera != null) cinematicCamera.Priority = 0;
    }

    private void HoldReturnCamera()
    {
        if (returnCameraTarget != null || !CinemachineCore.Instance.IsLive(cinematicCamera)
            || (cinematicCamera.Follow == null && cinematicCamera.LookAt == null)) return;

        // The outgoing camera remains live during the blend. Idle and player movement
        // must not move either its position target or its aim target during that time.
        followBeforeReturn = cinematicCamera.Follow;
        lookAtBeforeReturn = cinematicCamera.LookAt;
        returnCameraTarget = new GameObject(name + " Camera Return").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(returnCameraTarget.gameObject, gameObject.scene);
        if (followBeforeReturn != null)
        {
            returnCameraTarget.SetPositionAndRotation(followBeforeReturn.position, followBeforeReturn.rotation);
            cinematicCamera.Follow = returnCameraTarget;
        }
        if (lookAtBeforeReturn != null)
        {
            var lookAt = new GameObject("LookAt").transform;
            lookAt.SetParent(returnCameraTarget, false);
            lookAt.SetPositionAndRotation(lookAtBeforeReturn.position, lookAtBeforeReturn.rotation);
            cinematicCamera.LookAt = lookAt;
        }
    }

    private void LateUpdate()
    {
        if (returnCameraTarget != null
            && (cinematicCamera == null || !CinemachineCore.Instance.IsLive(cinematicCamera)))
            ReleaseReturnCamera();
    }

    private void ReleaseReturnCamera()
    {
        if (returnCameraTarget == null) return;
        if (cinematicCamera != null)
        {
            cinematicCamera.Follow = followBeforeReturn;
            cinematicCamera.LookAt = lookAtBeforeReturn;
        }
        Destroy(returnCameraTarget.gameObject);
        returnCameraTarget = null;
        followBeforeReturn = null;
        lookAtBeforeReturn = null;
    }

    private void Awake()
    {
        CancelCinematic();
    }

    private void OnDisable()
    {
        CancelCinematic();
    }
}
