using System;
using System.Collections;
using UnityEngine;

/// <summary>Owns the character Animator. Gameplay systems request poses without writing its layers or state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class CharacterAnimationPresenter : MonoBehaviour
{
    private const string ReloadingParameter = "IsReloading";
    private const string ReloadSpeedParameter = "ReloadSpeed";
    private const string SkillSpeedParameter = "SkillSpeed";

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
    public bool IsDeathAnimationPlaying
    {
        get
        {
            if (!IsDeathPresented || !isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled
                || !HasState(0, deathStateName)) return false;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            // Animator.Play is evaluated on the next animation update.
            return !state.IsName(deathStateName) || state.normalizedTime < 1f;
        }
    }
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
    public bool PlayUpperBodySkill() => PlayUpperBody("Skill");

    public bool PlayUpperBodyAim() => PlayUpperBody("ThrowAim");

    public bool PlayUpperBodySkill(float duration, float normalizedStart = 0f)
    {
        if (!isActiveAndEnabled || IsDeathPresented || IsFullBodyPlaying || !HasState(1, "Skill")) return false;
        var clip = Array.Find(animator.runtimeAnimatorController.animationClips,
            item => item.name.EndsWith("_upperbody_skill", StringComparison.Ordinal));
        if (clip == null || !IsPositiveFinite(duration) || normalizedStart < 0f || normalizedStart >= 1f
            || !HasParameter("UpperSkillSpeed", AnimatorControllerParameterType.Float)) return false;
        animator.SetFloat("UpperSkillSpeed", clip.length * (1f - normalizedStart) / duration);
        animator.Play("Skill", 1, normalizedStart);
        return true;
    }

    public void CancelUpperBodySkill()
    {
        if (animator == null) return;
        if (HasParameter("UpperSkillSpeed", AnimatorControllerParameterType.Float)) animator.SetFloat("UpperSkillSpeed", 1f);
        if (!IsDeathPresented && !IsFullBodyPlaying) PlayUpperBody("Armature|idle");
    }

    public bool PlayReload(float duration, bool firstRound)
    {
        if (!isActiveAndEnabled || IsDeathPresented || IsFullBodyPlaying || animator == null) return false;
        if (!IsPositiveFinite(duration))
        {
            Debug.LogError($"[CharacterAnimationPresenter] Reload duration must be finite and positive: {duration}.", this);
            return false;
        }

        if (!HasState(1, "ReloadStart") || !HasState(1, "ReloadLoop") || !HasState(1, "ReloadEnd"))
        {
            if (!HasState(1, "Armature|reloading")) return false;
            PlayReload();
            return true;
        }

        if (!HasParameter(ReloadingParameter, AnimatorControllerParameterType.Bool)
            || !HasParameter(ReloadSpeedParameter, AnimatorControllerParameterType.Float)
            || !TryGetReloadLengths(out float startLength, out float loopLength))
        {
            Debug.LogError("[CharacterAnimationPresenter] Phased reload requires IsReloading, ReloadSpeed and unique upper-body reload clips.", this);
            return false;
        }

        float insertionDuration = duration - (firstRound ? startLength : 0f);
        float speed = loopLength / insertionDuration;
        if (!IsPositiveFinite(insertionDuration) || !IsPositiveFinite(speed))
        {
            Debug.LogError($"[CharacterAnimationPresenter] Reload duration {duration} must exceed the opening clip duration {startLength} on the first round.", this);
            return false;
        }

        // Only the insertion state changes speed; locomotion and opening/closing keep their authored timing.
        animator.SetFloat(ReloadSpeedParameter, speed);
        animator.SetBool(ReloadingParameter, true);
        animator.Play(firstRound ? "ReloadStart" : "ReloadLoop", 1, 0f);
        return true;
    }

    public void FinishReload()
    {
        if (HasParameter(ReloadingParameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(ReloadingParameter, false);
    }

    public void CancelReload()
    {
        FinishReload();
        if (HasParameter(ReloadSpeedParameter, AnimatorControllerParameterType.Float))
            animator.SetFloat(ReloadSpeedParameter, 1f);
        if (isActiveAndEnabled && !IsDeathPresented && !IsFullBodyPlaying
            && HasState(1, "Armature|idle"))
            animator.Play("Armature|idle", 1, 0f);
    }

    private bool TryGetReloadLengths(out float startLength, out float loopLength)
    {
        startLength = loopLength = 0f;
        AnimationClip start = null;
        AnimationClip loop = null;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name.EndsWith("_upperbody_reloading_start", StringComparison.Ordinal))
            {
                if (start != null && start != clip) return false;
                start = clip;
            }
            else if (clip.name.EndsWith("_upperbody_reloading_loop", StringComparison.Ordinal)
                || clip.name.EndsWith("_upperbody_reloading", StringComparison.Ordinal))
            {
                if (loop != null && loop != clip) return false;
                loop = clip;
            }
        }
        if (start == null || loop == null) return false;
        startLength = start.length;
        loopLength = loop.length;
        return IsPositiveFinite(startLength) && IsPositiveFinite(loopLength);
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    public void PlayHit(bool gameplayReloadInProgress)
    {
        if (!enableDamageAnimations || gameplayReloadInProgress || IsReloadPoseActive()) return;
        if (GetComponent<Char004ThrowAnimation>()?.IsPlaying == true
            || GetComponentInParent<SkillAimController>()?.IsPrepared == true) return;
        PlayUpperBody(hitStateName);
    }

    public bool IsReloadPoseActive()
    {
        if (animator == null || animator.layerCount < 2) return false;
        return IsReloadState(animator.GetCurrentAnimatorStateInfo(1))
            || (animator.IsInTransition(1) && IsReloadState(animator.GetNextAnimatorStateInfo(1)));
    }

    private static bool IsReloadState(AnimatorStateInfo state) => state.IsTag("Reload")
        || state.IsName("Armature|reloading") || state.IsName("ReloadStart")
        || state.IsName("ReloadLoop") || state.IsName("ReloadEnd");

    private bool PlayUpperBody(string stateName)
    {
        if (!isActiveAndEnabled || IsDeathPresented || IsFullBodyPlaying || !HasState(1, stateName)) return false;
        // Each upper-body action returns to idle through its Animator exit-time transition.
        animator.Play(stateName, 1, 0f);
        return true;
    }

    public int PlayFullBody(string stateName)
    {
        if (!isActiveAndEnabled || IsDeathPresented || !HasState(0, stateName)) return 0;
        CancelReload();
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

    public int PlaySkill(float duration)
    {
        if (!isActiveAndEnabled || IsDeathPresented || animator == null || !HasState(0, "Skill")) return 0;
        var clip = Array.Find(animator.runtimeAnimatorController.animationClips,
            item => item.name.EndsWith("_skill", StringComparison.Ordinal)
                && !item.name.EndsWith("_upperbody_skill", StringComparison.Ordinal));
        if (!IsPositiveFinite(duration) || clip == null || !IsPositiveFinite(clip.length / duration)
            || !HasParameter(SkillSpeedParameter, AnimatorControllerParameterType.Float))
        {
            Debug.LogError("[CharacterAnimationPresenter] Skill requires a positive duration, a full-body skill clip and SkillSpeed.", this);
            return 0;
        }

        animator.SetFloat(SkillSpeedParameter, clip.length / duration);
        return PlayFullBody("Skill");
    }

    public void FinishSkill(int token)
    {
        if (!IsCurrentPlayback(token)) return;
        fullBodyPlayback = 0;
        fullBodyState = null;
        if (animator.layerCount > 1) animator.SetLayerWeight(1, 1f);
    }

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

    public void ResetDeathPresentation()
    {
        if (!IsDeathPresented) return;
        IsDeathPresented = false;
        RestoreMovement();
    }

    private void InvalidatePlayback()
    {
        CancelReload();
        ++generation;
        fullBodyPlayback = 0;
        fullBodyState = null;
        if (animator != null) animator.speed = 1f;
    }

    private void RestoreMovement()
    {
        if (animator == null || IsDeathPresented) return;
        CancelReload();
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

    private bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == name && parameter.type == type) return true;
        return false;
    }
}
