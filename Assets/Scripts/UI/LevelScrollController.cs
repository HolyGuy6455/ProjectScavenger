using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System;

public class LevelScrollController : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform centerPoint; // Viewport의 중앙을 가리키는 빈 RectTransform 등을 연결

    [Header("Settings")]
    [SerializeField] private float snapSpeed = 10f;
    [SerializeField] private float minScale = 1.0f;
    [SerializeField] private float maxScale = 1.5f;
    [SerializeField] private float scaleDistance = 200f; // 중심으로부터 이 거리 이내일 때 점점 커짐

    public event Action<int> OnLevelSelected;

    private List<TrainCharacterLevelScrollItem> _items = new List<TrainCharacterLevelScrollItem>();
    private bool _isDragging = false;
    private TrainCharacterLevelScrollItem _targetItem = null;
    private int _currentClosestLevel = -1;

    public void Initialize(List<TrainCharacterLevelScrollItem> items, int currentLevel)
    {
        _items = items;
        _isDragging = false;
        
        TrainCharacterLevelScrollItem initialItem = _items.Find(x => x.Level == currentLevel);
        if (initialItem != null)
        {
            // 한 프레임 쉬고 스크롤 포지션을 잡아주기 위해 LayoutRebuilder가 동작한 뒤에 처리하는 것이 좋으나, 
            // 여기서는 즉시 이동을 시도합니다. 만약 위치가 어긋나면 Canvas.ForceUpdateCanvases() 를 호출할 수 있습니다.
            Canvas.ForceUpdateCanvases();
            SnapToItem(initialItem, instant: true);
        }
    }

    private void Update()
    {
        if (_items == null || _items.Count == 0) return;

        UpdateScales();

        TrainCharacterLevelScrollItem closest = FindClosestItem();
        if (closest != null && closest.Level != _currentClosestLevel)
        {
            _currentClosestLevel = closest.Level;
            OnLevelSelected?.Invoke(closest.Level);
        }

        if (!_isDragging)
        {
            if (scrollRect.velocity.magnitude < 50f)
            {
                if (scrollRect.velocity.magnitude > 0)
                    scrollRect.velocity = Vector2.zero;

                if (_targetItem == null || _targetItem.Level != _currentClosestLevel)
                {
                    _targetItem = closest;
                }

                if (_targetItem != null)
                {
                    Vector3 currentPos = content.localPosition;
                    Vector3 targetPos = GetSnapPosition(_targetItem);
                    content.localPosition = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * snapSpeed);
                }
            }
            else
            {
                _targetItem = null; // Let inertia handle scrolling until it slows down
            }
        }
    }

    private void UpdateScales()
    {
        Vector3 centerWorldPos = centerPoint.position;
        
        foreach (var item in _items)
        {
            float dist = Mathf.Abs(item.transform.position.x - centerWorldPos.x);
            float t = Mathf.Clamp01(dist / scaleDistance);
            float currentScale = Mathf.Lerp(maxScale, minScale, t);
            item.transform.localScale = new Vector3(currentScale, currentScale, 1f);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        _targetItem = null;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
    }

    private TrainCharacterLevelScrollItem FindClosestItem()
    {
        TrainCharacterLevelScrollItem closest = null;
        float minDistance = float.MaxValue;
        Vector3 centerWorldPos = centerPoint.position;

        foreach (var item in _items)
        {
            float dist = Mathf.Abs(item.transform.position.x - centerWorldPos.x);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = item;
            }
        }

        return closest;
    }

    private Vector3 GetSnapPosition(TrainCharacterLevelScrollItem item)
    {
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
        
        Vector3 itemLocalInViewport = viewport.InverseTransformPoint(item.transform.position);
        Vector3 centerLocalInViewport = viewport.InverseTransformPoint(centerPoint.position);
        
        float diffX = centerLocalInViewport.x - itemLocalInViewport.x;
        
        return new Vector3(content.localPosition.x + diffX, content.localPosition.y, content.localPosition.z);
    }
    
    private void SnapToItem(TrainCharacterLevelScrollItem item, bool instant = false)
    {
        _targetItem = item;
        Vector3 snapPos = GetSnapPosition(item);
        
        if (instant)
        {
            content.localPosition = snapPos;
            scrollRect.velocity = Vector2.zero;
            _currentClosestLevel = item.Level;
            OnLevelSelected?.Invoke(item.Level);
        }
    }
}
