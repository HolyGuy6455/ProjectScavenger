using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    [RequireComponent(typeof(Button))]
    public class UI_NavigateBackButton : MonoBehaviour
    {
        private void Awake()
        {
            Button btn = GetComponent<Button>();
            btn.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.GoBack();
            }
            else
            {
                Debug.LogWarning("OutGameUIManager 인스턴스를 찾을 수 없습니다.");
            }
        }
    }
}
