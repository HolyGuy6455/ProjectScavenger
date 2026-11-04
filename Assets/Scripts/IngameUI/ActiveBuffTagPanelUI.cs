using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 하단에 현재 조작 중인 캐릭터의 활성 버프(공용 + 개별, 최대 4개)를
/// [아이콘 + 버프 이름] 가로형 태그로 나열하는 UI 패널입니다.
/// </summary>
[DisallowMultipleComponent]
public class ActiveBuffTagPanelUI : MonoBehaviour
{
    [SerializeField] private List<BuffSlotIconUI> tagItems = new List<BuffSlotIconUI>();

    private BuffController _buffController;
    private int _overrideSlotIndex = -1;
    private readonly List<Buff> _displayBuffBuffer = new List<Buff>(BuffController.MaxDisplayedBuffs);

    private void Start()
    {
        ClearAllTags();
        StartCoroutine(WaitAndBindLocalPlayerRoutine());
    }

    private void OnEnable()
    {
        if (CharacterStateManager.Instance != null)
        {
            CharacterStateManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
            CharacterStateManager.Instance.OnCharacterChanged += HandleCharacterChanged;
        }

        if (_buffController != null)
        {
            _buffController.OnBuffsChanged -= RefreshTags;
            _buffController.OnBuffsChanged += RefreshTags;
            RefreshTags();
        }
    }

    private void OnDisable()
    {
        if (CharacterStateManager.Instance != null)
        {
            CharacterStateManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
        }

        if (_buffController != null)
        {
            _buffController.OnBuffsChanged -= RefreshTags;
        }
    }

    private IEnumerator WaitAndBindLocalPlayerRoutine()
    {
        while (PlayerController.LocalInstance == null)
        {
            yield return null;
        }

        BindBuffController(PlayerController.LocalInstance.GetComponent<BuffController>());
    }

    public void BindBuffController(BuffController buffController, int overrideSlotIndex = -1)
    {
        if (_buffController != null)
        {
            _buffController.OnBuffsChanged -= RefreshTags;
        }

        _buffController = buffController;
        _overrideSlotIndex = overrideSlotIndex;

        if (_buffController != null && isActiveAndEnabled)
        {
            _buffController.OnBuffsChanged += RefreshTags;
        }

        RefreshTags();
    }

    private void HandleCharacterChanged(CharData charData)
    {
        RefreshTags();
    }

    public void RefreshTags()
    {
        if (tagItems == null || tagItems.Count == 0) return;

        if (_buffController == null)
        {
            ClearAllTags();
            return;
        }

        int activeSlot = ResolveActiveSlotIndex();
        _buffController.GetDisplayBuffs(activeSlot, _displayBuffBuffer, Mathf.Min(BuffController.MaxDisplayedBuffs, tagItems.Count));

        for (int i = 0; i < tagItems.Count; i++)
        {
            if (tagItems[i] == null) continue;

            if (i < _displayBuffBuffer.Count)
            {
                tagItems[i].Bind(_displayBuffBuffer[i]);
                RectTransform itemRect = tagItems[i].transform as RectTransform;
                if (itemRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
                }
            }
            else
            {
                tagItems[i].Clear();
            }
        }

        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }
    }

    public void ClearAllTags()
    {
        if (tagItems == null) return;
        for (int i = 0; i < tagItems.Count; i++)
        {
            if (tagItems[i] != null)
            {
                tagItems[i].Clear();
            }
        }
    }

    private int ResolveActiveSlotIndex()
    {
        if (_overrideSlotIndex >= 0 && _overrideSlotIndex < 4)
        {
            return _overrideSlotIndex;
        }

        if (PlayerController.LocalInstance != null)
        {
            if (CharacterStateManager.Instance != null && CharacterStateManager.Instance.CurrentCharId > 0)
            {
                var deck = PlayerController.LocalInstance.GetLocalDeck();
                for (int i = 0; i < 4; i++)
                {
                    if (deck.GetCharId(i) == CharacterStateManager.Instance.CurrentCharId)
                    {
                        return i;
                    }
                }
            }
            return PlayerController.LocalInstance.ActiveSlotIndex;
        }

        return 0;
    }
}
