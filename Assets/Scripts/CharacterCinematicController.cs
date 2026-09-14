using UnityEngine;
using Cinemachine;

public class CharacterCinematicController : MonoBehaviour
{
    [Header("Cinematic Camera Settings")]
    [Tooltip("이 캐릭터에 부착된 시네마틱 연출 전용 시네마머신 카메라 (예: CameraPosition 뼈대 내의 VCam)")]
    [SerializeField] private CinemachineVirtualCamera cinematicCamera;

    public CinemachineVirtualCamera CinematicCamera => cinematicCamera;

    private void Awake()
    {
        // 시작 시 카메라 우선순위를 낮춰줍니다.
        if (cinematicCamera != null)
        {
            cinematicCamera.Priority = 0;
        }
    }
}
