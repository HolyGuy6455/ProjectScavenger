using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(Button))]
public class MenuTabButton : MonoBehaviour
{
    [Header("Tab Settings")]
    [Tooltip("이 버튼을 눌렀을 때 화면에 보여질 UI 패널")]
    [SerializeField] private GameObject targetPanel;
    
    [Tooltip("버튼이 선택되었을 때 켜질 하이라이트/배경 이미지 (추후 Animator 제어로 변경할 예정)")]
    [SerializeField] private Image highlightImage;

    private Button _button;

    /// <summary>
    /// MenuTabController에서 이 버튼을 초기화할 때 호출합니다.
    /// </summary>
    public void Initialize(UnityAction onClickAction)
    {
        _button = GetComponent<Button>();
        
        // 기존 리스너 초기화 후 새로운 클릭 이벤트 연결
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(onClickAction);
    }

    /// <summary>
    /// 버튼이 선택되었는지 여부에 따라 패널과 이미지를 업데이트합니다.
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        // 1. 연결된 패널 활성/비활성 제어
        if (targetPanel != null)
        {
            targetPanel.SetActive(isSelected);
        }

        // 2. 선택 표시용 하이라이트 이미지 제어 (현재는 단순 visible 토글)
        if (highlightImage != null)
        {
            highlightImage.enabled = isSelected;
        }

        // TODO: 나중에 애니메이터를 사용할 때는 이 부분에 애니메이터 파라미터 제어 코드를 넣으면 됩니다.
        // 예: _animator.SetBool("IsSelected", isSelected);
    }
}
