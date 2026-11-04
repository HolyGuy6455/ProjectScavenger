using System;
using TMPro;
using UnityEngine;

namespace Scavenger.UI
{
    /// <summary>
    /// 설정 패널의 개별 설정 행(Row) 베이스 클래스.
    /// 좌측 안내 문구(제목/설명)와 값 변경/초기화/적용 인터페이스를 공통으로 관리합니다.
    /// </summary>
    public abstract class SettingRow : MonoBehaviour
    {
        [Header("Row Info UI")]
        [SerializeField] protected TextMeshProUGUI titleText;
        [SerializeField] protected TextMeshProUGUI descriptionText;
        [SerializeField] protected RectTransform controlContainer;

        [Header("State")]
        [SerializeField] protected bool interactable = true;

        public event Action<SettingRow> OnValueChanged;

        public TextMeshProUGUI TitleText => titleText;
        public TextMeshProUGUI DescriptionText => descriptionText;
        public RectTransform ControlContainer => controlContainer;

        public bool Interactable
        {
            get => interactable;
            set
            {
                if (interactable == value) return;
                interactable = value;
                RefreshVisualState();
            }
        }

        /// <summary>
        /// 저장(적용)된 값과 현재 조작 중인 값이 다른지 여부.
        /// </summary>
        public abstract bool IsDirty { get; }

        /// <summary>
        /// 좌측 제목 및 부가 설명 텍스트를 설정합니다.
        /// 설명 텍스트가 비어 있으면 설명 오브젝트를 비활성화하여 제목이 수직 중앙에 오도록 합니다.
        /// </summary>
        public virtual void SetInfo(string title, string description = null)
        {
            if (titleText != null && title != null)
            {
                titleText.text = title;
            }

            if (descriptionText != null)
            {
                bool hasDescription = !string.IsNullOrEmpty(description);
                descriptionText.text = hasDescription ? description : string.Empty;
                if (descriptionText.gameObject.activeSelf != hasDescription)
                {
                    descriptionText.gameObject.SetActive(hasDescription);
                }
            }
        }

        /// <summary>
        /// 기본값으로 되돌립니다 (적용 전 단계).
        /// </summary>
        public abstract void ResetToDefault();

        /// <summary>
        /// 현재 변경된 값을 확정(저장) 상태로 반영합니다.
        /// </summary>
        public abstract void Apply();

        /// <summary>
        /// 확정(저장)되어 있던 값으로 되돌립니다.
        /// </summary>
        public abstract void Revert();

        /// <summary>
        /// 현재 값과 활성 상태에 맞춰 UI 그래픽을 갱신합니다.
        /// </summary>
        public abstract void RefreshVisualState();

        protected void NotifyValueChanged()
        {
            OnValueChanged?.Invoke(this);
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (descriptionText != null)
            {
                bool hasDescription = !string.IsNullOrEmpty(descriptionText.text);
                if (descriptionText.gameObject.activeSelf != hasDescription)
                {
                    descriptionText.gameObject.SetActive(hasDescription);
                }
            }
            RefreshVisualState();
        }
#endif
    }
}

