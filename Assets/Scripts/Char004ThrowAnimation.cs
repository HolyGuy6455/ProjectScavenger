using UnityEngine;

/// <summary>왼손의 조준용 소품과 이동 가능한 상반신 투척 동작을 표시합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationPresenter))]
public sealed class Char004ThrowAnimation : MonoBehaviour
{
    [SerializeField] private Transform releasePoint;

    private GameObject carriedVisual;
    public bool IsPlaying { get; private set; }
    public bool CanThrow => isActiveAndEnabled && !IsPlaying && carriedVisual != null;
    public Transform ReleasePoint => releasePoint;
    private CharacterAnimationPresenter Presenter => GetComponent<CharacterAnimationPresenter>();
    private Char004ThrowSkill ThrowSkill => GetComponentInParent<Char004ThrowSkill>();

    public bool ShowAim(GameObject projectilePrefab)
    {
        var visual = projectilePrefab != null ? projectilePrefab.transform.Find("Visual") : null;
        if (!isActiveAndEnabled || IsPlaying || releasePoint == null || visual == null)
        {
            Debug.LogError("[Char004ThrowAnimation] Active model, left-hand release point and projectile Visual are required.", this);
            return false;
        }
        if (!Presenter.PlayUpperBodyAim()) return false;
        RemoveVisual();
        carriedVisual = Instantiate(visual.gameObject, releasePoint, false);
        carriedVisual.name = "HeldThrowable";
        carriedVisual.SetActive(true);
        // The model's scale applies once through the hand hierarchy; prefab Visual uses native units.
        carriedVisual.transform.localPosition = visual.localPosition;
        carriedVisual.transform.localRotation = visual.localRotation;
        carriedVisual.transform.localScale = visual.localScale;
        return true;
    }

    public bool TryPlay(float duration)
    {
        if (!CanThrow) return false;
        // AimHold is authored at frame 19; continue from that pose rather than repeating the windup.
        if (!Presenter.PlayUpperBodySkill(duration, 18f / 53f)) return false;
        IsPlaying = true;
        return true;
    }

    // AnimationEvent on the upper-body Skill take at the release frame.
    public void ReleaseThrowable()
    {
        if (!IsPlaying) return;
        RemoveVisual();
        ThrowSkill?.OnReleaseAnimationEvent();
    }

    // The Animator's Skill state calls this after its transition back to upper-body idle.
    public void OnThrowStateExit()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        RemoveVisual();
        ThrowSkill?.FinishThrow();
    }

    public void CancelAimVisual()
    {
        IsPlaying = false;
        RemoveVisual();
        Presenter.CancelUpperBodySkill();
    }

    private void RemoveVisual()
    {
        if (carriedVisual == null) return;
        carriedVisual.SetActive(false);
        Destroy(carriedVisual);
        carriedVisual = null;
    }

    private void OnDisable()
    {
        IsPlaying = false;
        RemoveVisual();
    }
}
