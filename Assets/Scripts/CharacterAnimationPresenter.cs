using System;
using System.Collections;
using UnityEngine;

/// <summary>Owns the character Animator. Gameplay systems request poses without writing its layers or state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class CharacterAnimationPresenter : MonoBehaviour
{
    [SerializeField] private bool enableDamageAnimations;
    [SerializeField] private string deathStateName = "Death";
    [SerializeField] private string hitStateName = "Armature|hit";

    private Animator animator;
    private CharacterExpressionController expression;
    private int generation;
    private int fullBodyPlayback;
    private string fullBodyState;
    private bool resetPoseOnEnable;

    public bool HandlesDamageAnimations => enableDamageAnimations;
    public bool IsDeathPresented { get; private set; }
    public bool IsFullBodyPlaying => fullBodyPlayback != 0;
    public event Action<CharacterAnimationPresenter> PresentationDisabled;

    public static CharacterAnimationPresenter GetOrCreate(GameObject model)
    {
        if (model == null) return null;
        var existing = model.GetComponentInChildren<CharacterAnimationPresenter>(true);
        if (existing != null) return existing;
        var target = model.GetComponentInChildren<Animator>(true);
        return target != null ? target.gameObject.AddComponent<CharacterAnimationPresenter>() : null;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        expression = GetComponent<CharacterExpressionController>();
    }

    private void OnEnable()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (expression == null) expression = GetComponent<CharacterExpressionController>();
        if (resetPoseOnEnable && !IsDeathPresented) RestoreMovement();
        resetPoseOnEnable = false;
    }

    private void OnDisable()
    {
        resetPoseOnEnable = true;
        PresentationDisabled?.Invoke(this);
        InvalidatePlayback();
    }

    public void SetMovement(Vector2 movement)
    {
        if (animator == null) return;
        if (IsDeathPresented) movement = Vector2.zero;
        animator.SetFloat("InputX", movement.x);
        animator.SetFloat("InputY", movement.y);
    }

    public void PlayShoot() => PlayUpperBody("Armature|shoot");
    public void PlayReload() => PlayUpperBody("Armature|reloading");

    public void PlayHit(bool gameplayReloadInProgress)
    {
        if (!enableDamageAnimations || gameplayReloadInProgress || IsReloadPoseActive()) return;
        PlayUpperBody(hitStateName);
    }

    private bool IsReloadPoseActive()
    {
        if (animator == null || animator.layerCount < 2) return false;
        return animator.GetCurrentAnimatorStateInfo(1).IsName("Armature|reloading")
            || (animator.IsInTransition(1) && animator.GetNextAnimatorStateInfo(1).IsName("Armature|reloading"));
    }

    private void PlayUpperBody(string stateName)
    {
        if (!isActiveAndEnabled || IsDeathPresented || IsFullBodyPlaying || !HasState(1, stateName)) return;
        // Each upper-body action returns to idle through its Animator exit-time transition.
        animator.Play(stateName, 1, 0f);
    }

    public int PlayFullBody(string stateName)
    {
        if (!isActiveAndEnabled || IsDeathPresented || !HasState(0, stateName)) return 0;
        fullBodyPlayback = ++generation;
        fullBodyState = stateName;
        animator.speed = 1f;
        if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);
        animator.Play(stateName, 0, 0f);
        return fullBodyPlayback;
    }

    public int PlayRoll(float duration)
    {
        int token = PlayFullBody("Roll");
        if (token == 0) return token;
        animator.Update(0f);
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (duration > 0f && state.IsName("Roll")) animator.speed = state.length / duration;
        return token;
    }

    public IEnumerator WaitForDuration(int token, Action<float> resolved)
    {
        float deadline = Time.realtimeSinceStartup + .5f;
        while (IsCurrentPlayback(token))
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(fullBodyState) && state.length > 0f
                && !float.IsNaN(state.length) && !float.IsInfinity(state.length))
            {
                resolved(state.length);
                yield break;
            }
            if (Time.realtimeSinceStartup >= deadline) yield break;
            yield return null;
        }
    }

    public bool IsCurrentPlayback(int token) => token != 0 && token == fullBodyPlayback
        && isActiveAndEnabled && !IsDeathPresented;

    public void FinishFullBody(int token, bool restoreMovement)
    {
        if (!IsCurrentPlayback(token)) return;
        if (restoreMovement)
        {
            fullBodyPlayback = 0;
            fullBodyState = null;
            RestoreMovement();
        }
        // Exit holds its final pose and upper-body mask while the result UI is shown.
    }

    public void CancelFullBody(int token, bool restoreMovement = false)
    {
        if (token == 0 || token != fullBodyPlayback) return;
        InvalidatePlayback();
        if (restoreMovement && isActiveAndEnabled && !IsDeathPresented) RestoreMovement();
    }

    public void PlayDeath()
    {
        if (!enableDamageAnimations || IsDeathPresented) return;
        InvalidatePlayback();
        IsDeathPresented = true;
        animator.speed = 1f;
        SetMovement(Vector2.zero);
        if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);
        if (HasState(0, deathStateName)) animator.Play(deathStateName, 0, 0f);
    }

    public void StopPlayback()
    {
        InvalidatePlayback();
    }

    private void InvalidatePlayback()
    {
        ++generation;
        fullBodyPlayback = 0;
        fullBodyState = null;
        if (animator != null) animator.speed = 1f;
    }

    private void RestoreMovement()
    {
        if (animator == null || IsDeathPresented) return;
        animator.speed = 1f;
        if (HasState(0, "Movement")) animator.Play("Movement", 0, 0f);
        if (animator.layerCount > 1)
        {
            animator.SetLayerWeight(1, 1f);
            if (HasState(1, "Armature|idle")) animator.Play("Armature|idle", 1, 0f);
        }
        if (expression != null) expression.ResetExpression();
    }

    private bool HasState(int layer, string name)
    {
        return animator != null && animator.runtimeAnimatorController != null && layer < animator.layerCount
            && !string.IsNullOrEmpty(name)
            && animator.HasState(layer, Animator.StringToHash(animator.GetLayerName(layer) + "." + name));
    }
}
