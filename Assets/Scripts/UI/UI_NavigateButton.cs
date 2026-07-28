using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    [RequireComponent(typeof(Button))]
    public class UI_NavigateButton : MonoBehaviour
    {
        [Tooltip("이 버튼을 누르면 이동할 패널을 선택하세요.")]
        public OutGamePanelType targetPanel;

        private void Awake()
        {
            Button btn = GetComponent<Button>();
            btn.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.ShowPanel(targetPanel);
            }
            else
            {
                Debug.LogWarning("OutGameUIManager 인스턴스를 찾을 수 없습니다.");
            }
        }
    }
}
