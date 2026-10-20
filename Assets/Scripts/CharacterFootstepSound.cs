using Scavenger.Audio;
using UnityEngine;

/// <summary>Plays locally from the movement animations already evaluated on every client.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class CharacterFootstepSound : MonoBehaviour
{
    [SerializeField] private SoundKey soundKey = SoundKey.Footstep;

    private Animator animator;
    private PlayerController player;
    private CharacterAnimationPresenter presenter;
    private Vector3 previousPosition;
    private float lastMovementTime;
    private float lastStepTime;
    private readonly int[] lastFootCycles = { int.MinValue, int.MinValue };

    private void OnEnable()
    {
        animator = GetComponent<Animator>();
        presenter = GetComponent<CharacterAnimationPresenter>();
        player = GetComponentInParent<PlayerController>();
        previousPosition = player != null ? player.transform.position : transform.position;
        lastMovementTime = lastStepTime = float.NegativeInfinity;
        ResetStepCycles();
    }

    private void LateUpdate()
    {
        if (player == null) return;
        SampleMovement();
        if (!CanWalk()) ResetStepCycles();
    }

    private void SampleMovement()
    {
        Vector3 position = player.transform.position;
        Vector3 delta = position - previousPosition;
        delta.y = 0f;
        previousPosition = position;
        // Ignore spawn/teleport jumps; a short grace period accommodates network interpolation.
        if (delta.sqrMagnitude > 4f)
            lastMovementTime = float.NegativeInfinity;
        else if (delta.sqrMagnitude > .000001f)
            lastMovementTime = Time.time;
    }

    private bool CanWalk()
    {
        if (!isActiveAndEnabled || player == null || !player.IsSpawned || !player.IsClient
            || player.IsInputLocked || player.IsRolling || animator == null || !animator.isActiveAndEnabled
            || (presenter != null && (presenter.IsDeathPresented || presenter.IsFullBodyPlaying)))
            return false;

        GameObject activeModel = player.GetActiveCharacterModel();
        if (activeModel == null || !transform.IsChildOf(activeModel.transform)) return false;
        float x = animator.GetFloat("InputX");
        float y = animator.GetFloat("InputY");
        return x * x + y * y > .001f;
    }

    // intParameter identifies the foot (0 or 1); all directional clips share contact phases.
    public void OnFootstep(AnimationEvent animationEvent)
    {
        if (!CanWalk() || !SoundManager.IsWorldListenerReady || soundKey == SoundKey.None
            || animationEvent == null || !animationEvent.isFiredByAnimator) return;
        if (!animationEvent.animatorStateInfo.IsName("Movement")) return;

        SampleMovement();
        if (Time.time - lastMovementTime > .12f || Time.time - lastStepTime < .08f) return;

        int foot = animationEvent.intParameter;
        if (foot < 0 || foot >= lastFootCycles.Length) return;
        int cycle = Mathf.FloorToInt(animationEvent.animatorStateInfo.normalizedTime);
        // Blended walk/run/directional clips can emit the same contact several times.
        if (lastFootCycles[foot] == cycle) return;
        lastFootCycles[foot] = cycle;
        lastStepTime = Time.time;
        SoundManager.PlayAt(soundKey, player.transform.position);
    }

    private void ResetStepCycles()
    {
        lastFootCycles[0] = lastFootCycles[1] = int.MinValue;
    }
}
