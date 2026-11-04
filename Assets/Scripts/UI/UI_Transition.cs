using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public enum UITransitionType
    {
        Panel,
        Popup
    }

    /// <summary>
    /// 패널 및 팝업의 등장(Fade-In)과 퇴장(Fade-Out) 연출을 담당하는 컴포넌트입니다.
    /// - Panel: 배경(backgroundCanvasGroups)과 UI 요소(uiCanvasGroup)를 분리해 순차/지연 페이드 처리
    /// - Popup: 팝업 전체(uiCanvasGroup)를 단일 페이드 처리
    /// </summary>
    [DisallowMultipleComponent]
    public class UI_Transition : MonoBehaviour
    {
        [Header("Transition Type")]
        [SerializeField] private UITransitionType transitionType = UITransitionType.Panel;

        [Header("Canvas Group References")]
        [Tooltip("패널의 UI 요소 전체(또는 팝업 전체)의 알파를 제어할 루트 CanvasGroup입니다. 비워두면 현재 오브젝트의 CanvasGroup을 자동으로 사용합니다.")]
        [SerializeField] private CanvasGroup uiCanvasGroup;

        [Tooltip("패널 전용: 배경 요소를 제어할 CanvasGroup 목록입니다. (ignoreParentGroups = true가 자동 적용됩니다)")]
        [SerializeField] private List<CanvasGroup> backgroundCanvasGroups = new List<CanvasGroup>();

        private Coroutine _transitionCoroutine;
        private bool _initialized;

        public UITransitionType TransitionType
        {
            get => transitionType;
            set => transitionType = value;
        }

        public bool IsTransitioning => _transitionCoroutine != null;

        private void Awake()
        {
            EnsureInitialized(transitionType);
        }

        private void OnDisable()
        {
            _transitionCoroutine = null;
        }

        public void EnsureInitialized(UITransitionType defaultType)
        {
            if (_initialized) return;
            _initialized = true;
            transitionType = defaultType;

            if (uiCanvasGroup == null)
            {
                uiCanvasGroup = GetComponent<CanvasGroup>();
                if (uiCanvasGroup == null)
                {
                    uiCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            uiCanvasGroup.ignoreParentGroups = false;

            if (transitionType == UITransitionType.Panel)
            {
                backgroundCanvasGroups.RemoveAll(g => g == null || g == uiCanvasGroup);

                if (backgroundCanvasGroups.Count == 0)
                {
                    AutoDetectBackgroundCanvasGroups();
                }

                for (int i = 0; i < backgroundCanvasGroups.Count; i++)
                {
                    if (backgroundCanvasGroups[i] != null)
                    {
                        backgroundCanvasGroups[i].ignoreParentGroups = true;
                    }
                }
            }
        }

        public void BindExternalBackground(CanvasGroup externalBgGroup)
        {
            if (externalBgGroup == null || externalBgGroup == uiCanvasGroup) return;

            externalBgGroup.ignoreParentGroups = true;
            if (!backgroundCanvasGroups.Contains(externalBgGroup))
            {
                backgroundCanvasGroups.Add(externalBgGroup);
            }
        }

        private void AutoDetectBackgroundCanvasGroups()
        {
            // 대표 배경 오브젝트 이름을 우선 탐색합니다.
            string[] candidateNames =
            {
                "BackgroundImage",
                "기존 책상 배경",
                "Background",
                "outgame-quests-quest-board/BackgroundImage",
                "outgame-quests-quest-board/foregroundImage",
                "desk-canvas/BackgroundImage"
            };

            foreach (string path in candidateNames)
            {
                Transform found = transform.Find(path);
                if (found != null && found.gameObject.activeInHierarchy)
                {
                    CanvasGroup cg = found.GetComponent<CanvasGroup>();
                    if (cg == null) cg = found.gameObject.AddComponent<CanvasGroup>();
                    cg.ignoreParentGroups = true;
                    if (!backgroundCanvasGroups.Contains(cg))
                    {
                        backgroundCanvasGroups.Add(cg);
                    }
                }
            }

            // 이름 매칭이 없다면 0번 자식이 단일 배경 이미지인지 확인합니다.
            if (backgroundCanvasGroups.Count == 0 && transform.childCount > 0)
            {
                Transform firstChild = transform.GetChild(0);
                if (firstChild.childCount == 0 &&
                    firstChild.GetComponent<Graphic>() != null &&
                    firstChild.GetComponent<Selectable>() == null)
                {
                    CanvasGroup cg = firstChild.GetComponent<CanvasGroup>();
                    if (cg == null) cg = firstChild.gameObject.AddComponent<CanvasGroup>();
                    cg.ignoreParentGroups = true;
                    backgroundCanvasGroups.Add(cg);
                }
            }
        }

        public void StopTransition()
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
        }

        /// <summary>
        /// 애니메이션 없이 즉시 완전 불투명 상태로 표시합니다.
        /// (뒤로 가기 시 밑에 깔리는 이전 패널 등에 사용)
        /// </summary>
        public void ShowImmediate(bool interactable = true)
        {
            EnsureInitialized(transitionType);
            StopTransition();

            SetBackgroundAlpha(1f);
            SetUiAlpha(1f);
            SetInteractable(interactable);
            SetBlocksRaycasts(true);
        }

        /// <summary>
        /// 애니메이션 없이 즉시 완전 투명 상태로 만듭니다.
        /// </summary>
        public void HideImmediate()
        {
            EnsureInitialized(transitionType);
            StopTransition();

            SetBackgroundAlpha(0f);
            SetUiAlpha(0f);
            SetInteractable(false);
            SetBlocksRaycasts(false);
        }

        public void PlayFadeIn(Action onComplete = null)
        {
            EnsureInitialized(transitionType);
            StopTransition();

            // 활성화 직후 첫 프레임 깜빡임을 방지하기 위해 즉시 알파 0으로 초기화합니다.
            SetBackgroundAlpha(0f);
            SetUiAlpha(0f);
            SetInteractable(false);
            SetBlocksRaycasts(true);

            if (!isActiveAndEnabled)
            {
                ShowImmediate(true);
                onComplete?.Invoke();
                return;
            }

            if (transitionType == UITransitionType.Popup)
            {
                _transitionCoroutine = StartCoroutine(PopupFadeInRoutine(onComplete));
            }
            else
            {
                _transitionCoroutine = StartCoroutine(PanelFadeInRoutine(onComplete));
            }
        }

        public void PlayFadeOut(Action onComplete = null)
        {
            EnsureInitialized(transitionType);
            StopTransition();

            SetInteractable(false);
            SetBlocksRaycasts(true);

            if (!isActiveAndEnabled)
            {
                HideImmediate();
                onComplete?.Invoke();
                return;
            }

            if (transitionType == UITransitionType.Popup)
            {
                _transitionCoroutine = StartCoroutine(PopupFadeOutRoutine(onComplete));
            }
            else
            {
                _transitionCoroutine = StartCoroutine(PanelFadeOutRoutine(onComplete));
            }
        }

        private IEnumerator PanelFadeInRoutine(Action onComplete)
        {
            OutGameUIManager manager = OutGameUIManager.Instance;
            float bgDuration = manager != null ? manager.PanelBgFadeInDuration : OutGameUIManager.DEFAULT_PANEL_BG_FADE_IN_DURATION;
            float uiDelay = manager != null ? manager.PanelUiFadeInDelay : OutGameUIManager.DEFAULT_PANEL_UI_FADE_IN_DELAY;
            float uiDuration = manager != null ? manager.PanelUiFadeInDuration : OutGameUIManager.DEFAULT_PANEL_UI_FADE_IN_DURATION;

            bool hasBackground = HasValidBackgroundGroup();
            if (!hasBackground)
            {
                // 별도 배경이 없는 패널은 딜레이 없이 UI만 페이드인합니다.
                bgDuration = 0f;
            }

            float totalDuration = Mathf.Max(bgDuration, uiDelay + uiDuration);
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float bgAlpha = bgDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / bgDuration);
                float uiAlpha;
                if (elapsed < uiDelay)
                {
                    uiAlpha = 0f;
                }
                else if (uiDuration <= 0f)
                {
                    uiAlpha = 1f;
                }
                else
                {
                    uiAlpha = Mathf.Clamp01((elapsed - uiDelay) / uiDuration);
                }

                SetBackgroundAlpha(bgAlpha);
                SetUiAlpha(uiAlpha);
                yield return null;
            }

            _transitionCoroutine = null;
            SetBackgroundAlpha(1f);
            SetUiAlpha(1f);
            SetInteractable(true);
            SetBlocksRaycasts(true);
            onComplete?.Invoke();
        }

        private IEnumerator PanelFadeOutRoutine(Action onComplete)
        {
            OutGameUIManager manager = OutGameUIManager.Instance;
            float uiDuration = manager != null ? manager.PanelUiFadeOutDuration : OutGameUIManager.DEFAULT_PANEL_UI_FADE_OUT_DURATION;
            float bgDelay = manager != null ? manager.PanelBgFadeOutDelay : OutGameUIManager.DEFAULT_PANEL_BG_FADE_OUT_DELAY;
            float bgDuration = manager != null ? manager.PanelBgFadeOutDuration : OutGameUIManager.DEFAULT_PANEL_BG_FADE_OUT_DURATION;

            bool hasBackground = HasValidBackgroundGroup();
            if (!hasBackground)
            {
                bgDelay = 0f;
                bgDuration = 0f;
            }

            SetBackgroundAlpha(1f);
            SetUiAlpha(1f);

            float totalDuration = Mathf.Max(uiDuration, bgDelay + bgDuration);
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float uiAlpha = uiDuration <= 0f ? 0f : 1f - Mathf.Clamp01(elapsed / uiDuration);
                float bgAlpha;
                if (elapsed < bgDelay)
                {
                    bgAlpha = 1f;
                }
                else if (bgDuration <= 0f)
                {
                    bgAlpha = 0f;
                }
                else
                {
                    bgAlpha = 1f - Mathf.Clamp01((elapsed - bgDelay) / bgDuration);
                }

                SetUiAlpha(uiAlpha);
                SetBackgroundAlpha(bgAlpha);
                yield return null;
            }

            _transitionCoroutine = null;
            SetUiAlpha(0f);
            SetBackgroundAlpha(0f);
            SetBlocksRaycasts(false);
            onComplete?.Invoke();
        }

        private IEnumerator PopupFadeInRoutine(Action onComplete)
        {
            OutGameUIManager manager = OutGameUIManager.Instance;
            float duration = manager != null ? manager.PopupFadeInDuration : OutGameUIManager.DEFAULT_POPUP_FADE_IN_DURATION;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                SetUiAlpha(alpha);
                yield return null;
            }

            _transitionCoroutine = null;
            SetUiAlpha(1f);
            SetInteractable(true);
            SetBlocksRaycasts(true);
            onComplete?.Invoke();
        }

        private IEnumerator PopupFadeOutRoutine(Action onComplete)
        {
            OutGameUIManager manager = OutGameUIManager.Instance;
            float duration = manager != null ? manager.PopupFadeOutDuration : OutGameUIManager.DEFAULT_POPUP_FADE_OUT_DURATION;

            float startAlpha = uiCanvasGroup != null ? uiCanvasGroup.alpha : 1f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                SetUiAlpha(Mathf.Lerp(startAlpha, 0f, t));
                yield return null;
            }

            _transitionCoroutine = null;
            SetUiAlpha(0f);
            SetBlocksRaycasts(false);
            onComplete?.Invoke();
        }

        private bool HasValidBackgroundGroup()
        {
            for (int i = 0; i < backgroundCanvasGroups.Count; i++)
            {
                if (backgroundCanvasGroups[i] != null) return true;
            }
            return false;
        }

        public void SetInteractable(bool interactable)
        {
            if (uiCanvasGroup != null)
            {
                uiCanvasGroup.interactable = interactable;
            }

            for (int i = 0; i < backgroundCanvasGroups.Count; i++)
            {
                if (backgroundCanvasGroups[i] != null)
                {
                    backgroundCanvasGroups[i].interactable = interactable;
                }
            }
        }

        public void SetBlocksRaycasts(bool blocksRaycasts)
        {
            if (uiCanvasGroup != null)
            {
                uiCanvasGroup.blocksRaycasts = blocksRaycasts;
            }

            for (int i = 0; i < backgroundCanvasGroups.Count; i++)
            {
                if (backgroundCanvasGroups[i] != null)
                {
                    backgroundCanvasGroups[i].blocksRaycasts = blocksRaycasts;
                }
            }
        }

        private void SetUiAlpha(float alpha)
        {
            if (uiCanvasGroup != null)
            {
                uiCanvasGroup.alpha = alpha;
            }
        }

        private void SetBackgroundAlpha(float alpha)
        {
            for (int i = 0; i < backgroundCanvasGroups.Count; i++)
            {
                if (backgroundCanvasGroups[i] != null)
                {
                    backgroundCanvasGroups[i].alpha = alpha;
                }
            }
        }
    }
}

