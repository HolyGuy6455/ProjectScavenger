using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>투척물과 별개로 스폰되어 착탄 위치의 효과와 수명을 서버에서 관리합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class Char004ThrowArea : NetworkBehaviour
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField, Min(.01f)] private float visualRadius = 1f;
    [SerializeField] private SphereCollider sightBlocker;
    [SerializeField, Min(0f)] private float fireDamagePerSecond = 5f;

    private readonly NetworkVariable<Char004ThrowKind> throwKind = new NetworkVariable<Char004ThrowKind>();
    private readonly NetworkVariable<float> radius = new NetworkVariable<float>();
    private readonly NetworkVariable<double> startTime = new NetworkVariable<double>();
    private readonly NetworkVariable<double> endTime = new NetworkVariable<double>();
    private double nextFireTick;
    private bool initialized;

    public Char004ThrowKind Kind => throwKind.Value;
    public float Radius => radius.Value;

    /// <summary>서버에서 Instantiate 직후, NetworkObject.Spawn 이전에 호출합니다.</summary>
    public bool Initialize(GameObject owner, SkillData data, Char004ThrowKind kind)
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || IsSpawned || !Char004Throwable.HasValidSkillData(data, kind))
            return false;

        throwKind.Value = kind;
        radius.Value = data.funcValue[1];
        startTime.Value = manager.ServerTime.Time;
        nextFireTick = startTime.Value + 1d;
        // 유리병은 착탄 순간 한 번만 소리를 전달합니다. 짧은 수명은 스폰 전달을 위한 표시 시간입니다.
        endTime.Value = startTime.Value + (kind == Char004ThrowKind.Bottle ? .25f : data.funcValue[2]);
        initialized = true;
        return true;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && !initialized)
        {
            Debug.LogError("[Char004ThrowArea] Initialize must succeed before Spawn.", this);
            NetworkObject.Despawn();
            return;
        }

        ConfigureVisuals();
        if (sightBlocker != null)
        {
            sightBlocker.radius = Radius;
            sightBlocker.center = Vector3.up;
            sightBlocker.isTrigger = true;
            sightBlocker.enabled = Kind == Char004ThrowKind.Smoke;
        }
        if (!IsServer) return;
        if (Kind == Char004ThrowKind.Bottle) ReportBottleNoise();
    }

    public override void OnNetworkDespawn()
    {
        if (sightBlocker != null) sightBlocker.enabled = false;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;
        double now = NetworkManager.ServerTime.Time;
        if (Kind == Char004ThrowKind.Molotov)
        {
            double tickThrough = now < endTime.Value ? now : endTime.Value;
            // 착탄 즉시 중복 피해 없이 1초 뒤 첫 틱, 만료 시점의 마지막 틱까지 처리합니다.
            while (nextFireTick <= tickThrough)
            {
                ApplyFireDamage();
                nextFireTick += 1d;
            }
        }
        if (now >= endTime.Value) NetworkObject.Despawn();
    }

    private void ApplyFireDamage()
    {
        if (fireDamagePerSecond <= 0f) return;
        var damaged = new HashSet<Health>();
        foreach (var candidate in Physics.OverlapSphere(transform.position, Radius,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!Bullet.TryGetTarget(candidate.transform, out var actor) || actor != BulletTarget.Enemy) continue;
            var health = candidate.GetComponentInParent<Health>();
            if (health == null || health.IsDead || health.CurrentHealth <= 0f || !damaged.Add(health)) continue;

            // AI가 숨은 투척자의 위치 대신 실제 화염 지대를 피격 원인으로 기억합니다.
            health.TakeDamage(new DamageInfo(fireDamagePerSecond, gameObject, candidate.ClosestPoint(transform.position),
                Vector3.up, sourcePosition: transform.position));
        }
    }

    private void ConfigureVisuals()
    {
        if (visualRoot == null) return;
        Vector3 scale = visualRoot.transform.localScale;
        float ratio = Radius / Mathf.Max(.01f, visualRadius);
        visualRoot.transform.localScale = new Vector3(scale.x * ratio, scale.y, scale.z * ratio);

        float age = Mathf.Max(0f, (float)(NetworkManager.ServerTime.Time - startTime.Value));
        foreach (var particle in visualRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.stopAction = ParticleSystemStopAction.None;
            if (Kind != Char004ThrowKind.Bottle) main.loop = true;
            // 늦게 접속한 클라이언트도 이미 지난 지대 수명을 다시 시작하지 않습니다.
            particle.Simulate(age, false, true, true);
            particle.Play(false);
        }
    }

    private void ReportBottleNoise()
    {
        var notified = new HashSet<AIBrainStimulusLinker>();
        foreach (var candidate in Physics.OverlapSphere(transform.position, Radius,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!Bullet.TryGetTarget(candidate.transform, out var target) || target != BulletTarget.Enemy) continue;
            var linker = candidate.GetComponentInParent<AIBrainStimulusLinker>();
            if (linker == null || !notified.Add(linker)) continue;
            var brain = linker.GetComponent<AIBrain>();
            // 공격자 오브젝트만 처리하는 구형 AI의 피격 기억을 빈 소리 자극으로 덮어쓰지 않습니다.
            if (brain == null || brain.GetVariable<BTSharedNumeric>(linker.typeKey) == null
                || brain.GetVariable<BTSharedVector3>(linker.positionKey) == null) continue;
            linker.ReportStimulus(AIStimulusType.Noise, transform.position);
        }
    }

}
