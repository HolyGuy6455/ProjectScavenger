using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Scavenger.UI;
using System;

public class ClearResultPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("화면 전체를 덮는 투명 버튼 (클릭 시 팝업 호출)")]
    [SerializeField] private Button invisibleScreenButton;
    [Tooltip("연출이 끝나면 나타나는 '화면을 터치하세요' 텍스트/UI")]
    [SerializeField] private GameObject continueTextObj;
    [Tooltip("팝업 배경으로 깔리는 어두운 이미지 오브젝트 (자식 오브젝트)")]
    [SerializeField] private GameObject backgroundDimObj;

    [Header("Popups")]
    [SerializeField] private StageClearPopup stageClearPopup;
    [SerializeField] private CharacterLevelUpCelebrationPopup levelUpPopup;

    private Queue<CharData> _leveledUpCharsQueue = new Queue<CharData>();

    private void Start()
    {
        if (invisibleScreenButton != null)
        {
            invisibleScreenButton.onClick.AddListener(OnScreenClicked);
            // 데이터를 받기 전까지는 상호작용 불가능하게 막아둡니다.
            invisibleScreenButton.interactable = false;
        }

        if (backgroundDimObj != null) backgroundDimObj.SetActive(false);
        if (invisibleScreenButton != null) invisibleScreenButton.gameObject.SetActive(true);
        if (continueTextObj != null) continueTextObj.SetActive(false);

        if (stageClearPopup != null) stageClearPopup.gameObject.SetActive(false);
        if (levelUpPopup != null) levelUpPopup.gameObject.SetActive(false);
    }

    public void SetupResultFlow(List<LootInfo> loots, List<CharExpResult> expResults, PlayerExpResult playerExpResult)
    {
        _leveledUpCharsQueue.Clear();
        if (expResults != null)
        {
            foreach (var res in expResults)
            {
                if (res.newLevel > res.oldLevel)
                {
                    _leveledUpCharsQueue.Enqueue(res.charData);
                }
            }
        }

        if (stageClearPopup != null)
        {
            // 팝업 준비 (초기화)
            stageClearPopup.Initialize(loots, expResults, playerExpResult, OnStageClearPopupClosed);
        }
        
        if (invisibleScreenButton != null) 
        {
            invisibleScreenButton.gameObject.SetActive(true);
            // 이제 데이터를 받았으므로 터치 가능
            invisibleScreenButton.interactable = true;
        }
        if (backgroundDimObj != null) backgroundDimObj.SetActive(false);
    }

    public void OnCinematicFinished()
    {
        if (invisibleScreenButton != null && invisibleScreenButton.gameObject.activeSelf)
        {
            if (continueTextObj != null) continueTextObj.SetActive(true);
        }
    }

    private void OnScreenClicked()
    {
        if (invisibleScreenButton != null) invisibleScreenButton.gameObject.SetActive(false);
        if (continueTextObj != null) continueTextObj.SetActive(false);
        
        // 팝업이 뜰 때 비로소 배경 이미지를 켭니다. (스르륵 켜지도록 코루틴 적용)
        if (backgroundDimObj != null)
        {
            backgroundDimObj.SetActive(true);
            CanvasGroup dimCg = backgroundDimObj.GetComponent<CanvasGroup>();
            if (dimCg == null) dimCg = backgroundDimObj.AddComponent<CanvasGroup>();
            
            dimCg.alpha = 0f;
            StartCoroutine(FadeInDim(dimCg, 0.5f));
        }

        if (stageClearPopup != null)
        {
            // 투명 버튼을 누르면 전리품 팝업 애니메이션 시작
            stageClearPopup.PlayShowAnimation(OnStageClearAnimationFinished);
        }
    }

    private System.Collections.IEnumerator FadeInDim(CanvasGroup cg, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(time / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private void OnStageClearAnimationFinished()
    {
        // 전리품 팝업 내부의 연출(이후 추가될)이 끝난 후 레벨업 팝업 큐 시작
        ProcessNextLevelUpPopup();
    }

    private void ProcessNextLevelUpPopup()
    {
        if (_leveledUpCharsQueue.Count > 0)
        {
            CharData nextChar = _leveledUpCharsQueue.Dequeue();
            
            if (levelUpPopup != null)
            {
                // TODO: 1을 실제 레벨업 수치로 교체 필요
                levelUpPopup.Open(1, nextChar, ProcessNextLevelUpPopup);
            }
            else
            {
                ProcessNextLevelUpPopup();
            }
        }
        else
        {
            // 모든 축하 팝업을 다 봤으면 StageClearPopup의 확인 버튼 활성화
            if (stageClearPopup != null)
            {
                stageClearPopup.SetConfirmButtonInteractable(true);
            }
        }
    }

    private void OnStageClearPopupClosed()
    {
        var uiSystem = FindObjectOfType<Scavenger.UI.UI_SystemActions>();
        if (uiSystem != null)
        {
            uiSystem.DoReturnToMainMenu();
        }
    }

    private void OnDestroy()
    {
        if (invisibleScreenButton != null)
        {
            invisibleScreenButton.onClick.RemoveListener(OnScreenClicked);
        }
    }
}
