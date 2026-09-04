using System.Collections.Generic;
using UnityEngine;

public class MenuTabController : MonoBehaviour
{
    [Header("Menu Tab Buttons")]
    [Tooltip("자식으로 가지고 있는 메뉴 탭 버튼(MenuTabButton)들을 리스트에 넣어주세요.")]
    [SerializeField] private List<MenuTabButton> tabButtons = new List<MenuTabButton>();

    private void Start()
    {
        // 리스트에 등록된 모든 탭 버튼들을 초기화합니다.
        for (int i = 0; i < tabButtons.Count; i++)
        {
            int index = i; // 람다 클로저 문제 방지
            
            if (tabButtons[index] != null)
            {
                // 각 버튼을 누를 때 자신의 인덱스를 ShowTab 함수에 전달하도록 연결
                tabButtons[index].Initialize(() => ShowTab(index));
            }
        }

        // 스크립트가 시작될 때 맨 처음 등록된 탭(0번째)을 기본 화면으로 띄워줍니다.
        if (tabButtons.Count > 0)
        {
            ShowTab(0);
        }
    }

    /// <summary>
    /// 컨트롤러가 모든 탭 버튼에게 선택 상태를 갱신하라고 명령합니다.
    /// </summary>
    public void ShowTab(int tabIndex)
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] != null)
            {
                // 클릭한 탭의 인덱스와 일치하면 true(선택됨), 다르면 false(선택 해제)
                tabButtons[i].SetSelected(i == tabIndex);
            }
        }
    }
}
