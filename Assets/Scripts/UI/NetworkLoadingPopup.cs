using UnityEngine;

namespace Scavenger.UI
{
    [DisallowMultipleComponent]
    public class NetworkLoadingPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform spinner;
        [Tooltip("스피너의 초당 시계 방향 회전 각도")]
        [SerializeField, Min(0f)] private float rotationSpeed = 360f;

        private void OnEnable()
        {
            transform.SetAsLastSibling();
            if (spinner != null) spinner.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (spinner == null) return;

            // 게임이 일시 정지되어도 네트워크 로딩 표시는 계속 회전합니다.
            spinner.Rotate(0f, 0f, -rotationSpeed * Time.unscaledDeltaTime);
        }
    }
}
