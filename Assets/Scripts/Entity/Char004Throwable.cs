using Unity.Netcode;
using UnityEngine;

public enum Char004ThrowKind
{
    Smoke = 0,
    Bottle = 1,
    Molotov = 2
}

/// <summary>서버가 충돌을 판정하고 착탄 지대를 생성하는 char004 전용 투척물입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class Char004Throwable : NetworkBehaviour
{
    [SerializeField] private Char004ThrowKind kind;
    [SerializeField] private GameObject landingAreaPrefab;
    [SerializeField, Min(.1f)] private float speed = 12f;
    [SerializeField, Min(0f)] private float arcHeight = 2f;
    [SerializeField, Min(.001f)] private float collisionRadius = .06f;
    [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;

    private readonly NetworkVariable<Vector3> startPosition = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<Vector3> targetPosition = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<double> launchTime = new NetworkVariable<double>();
    private readonly NetworkVariable<float> flightDuration = new NetworkVariable<float>();

    private GameObject owner;
    private SkillData skillData;
    private bool initialized;
    private bool landed;
    private float previousProgress;

    public Char004ThrowKind Kind => kind;

    /// <summary>서버에서 Instantiate 직후, NetworkObject.Spawn 이전에 호출합니다.</summary>
    public bool Initialize(Vector3 target, GameObject thrower, SkillData data)
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || IsSpawned || !IsFinite(target)
            || thrower == null || !HasValidSkillData(data, kind)
            || landingAreaPrefab == null || landingAreaPrefab.GetComponent<Char004ThrowArea>() == null
            || landingAreaPrefab.GetComponent<NetworkObject>() == null)
            return false;

        owner = thrower;
        skillData = data;
        // 사거리는 조준 확정 시 서버가 검증합니다. 투척 동작 중 이동해도 확정 지점은 유지합니다.
        startPosition.Value = transform.position;
        targetPosition.Value = target;
        launchTime.Value = manager.ServerTime.Time;
        flightDuration.Value = Mathf.Clamp(Vector3.Distance(transform.position, target) / Mathf.Max(.1f, speed), .15f, 5f);
        previousProgress = 0f;
        landed = false;
        initialized = true;
        return true;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && !initialized)
        {
            Debug.LogError("[Char004Throwable] Initialize must succeed before Spawn.", this);
            NetworkObject.Despawn();
            return;
        }
        UpdatePresentation(GetProgress());
    }

    private void Update()
    {
        if (!IsSpawned || landed) return;
        float progress = GetProgress();
        if (!IsServer)
        {
            UpdatePresentation(progress);
            return;
        }

        // 긴 프레임에도 포물선을 여러 짧은 구간으로 나누어 얇은 벽을 놓치지 않습니다.
        int steps = Mathf.Max(1, Mathf.CeilToInt((progress - previousProgress) * flightDuration.Value / .04f));
        float fromProgress = previousProgress;
        for (int i = 1; i <= steps; i++)
        {
            float toProgress = Mathf.Lerp(previousProgress, progress, (float)i / steps);
            Vector3 from = EvaluatePosition(fromProgress);
            Vector3 to = EvaluatePosition(toProgress);
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance > .00001f)
            {
                RaycastHit nearest = default;
                float nearestDistance = float.PositiveInfinity;
                foreach (var hit in Physics.SphereCastAll(from, collisionRadius, delta / distance, distance,
                             collisionLayers, QueryTriggerInteraction.Ignore))
                {
                    if (ShouldIgnore(hit.collider) || hit.distance >= nearestDistance) continue;
                    nearest = hit;
                    nearestDistance = hit.distance;
                }
                if (nearest.collider != null)
                {
                    // 초기 겹침은 SphereCast의 hit.point가 원점일 수 있어 현재 위치를 사용합니다.
                    Land(nearest.distance > .00001f ? nearest.point : from, nearest.normal);
                    return;
                }
            }
            fromProgress = toProgress;
        }

        previousProgress = progress;
        UpdatePresentation(progress);
        if (progress >= 1f) Land(targetPosition.Value, Vector3.up);
    }

    private float GetProgress()
    {
        if (flightDuration.Value <= 0f || NetworkManager == null) return 0f;
        return Mathf.Clamp01((float)((NetworkManager.ServerTime.Time - launchTime.Value) / flightDuration.Value));
    }

    private Vector3 EvaluatePosition(float progress)
    {
        Vector3 position = Vector3.Lerp(startPosition.Value, targetPosition.Value, progress);
        position.y += 4f * arcHeight * progress * (1f - progress);
        return position;
    }

    private void UpdatePresentation(float progress)
    {
        transform.position = EvaluatePosition(progress);
        Vector3 tangent = targetPosition.Value - startPosition.Value;
        tangent.y += 4f * arcHeight * (1f - 2f * progress);
        if (tangent.sqrMagnitude > .00001f) transform.rotation = Quaternion.LookRotation(tangent);
    }

    private bool ShouldIgnore(Collider other)
    {
        return other == null || other.isTrigger || other.transform.IsChildOf(transform)
            || (owner != null && other.transform.IsChildOf(owner.transform));
    }

    private void Land(Vector3 position, Vector3 surfaceNormal)
    {
        if (!IsServer || landed) return;
        landed = true;
        position = FindGroundBelow(position, surfaceNormal);
        transform.position = position;
        GameObject instance = Instantiate(landingAreaPrefab, position, Quaternion.identity);
        var area = instance.GetComponent<Char004ThrowArea>();
        if (area.Initialize(owner, skillData, kind))
            instance.GetComponent<NetworkObject>().Spawn();
        else
            Destroy(instance);
        NetworkObject.Despawn();
    }

    private Vector3 FindGroundBelow(Vector3 position, Vector3 surfaceNormal)
    {
        // 벽면 중간에 맞으면 그 바로 아래 바닥에 지대를 놓습니다. 캐릭터 몸체는 바닥이 아닙니다.
        Vector3 origin = position + surfaceNormal * .1f + Vector3.up * .25f;
        float nearestDistance = float.PositiveInfinity;
        Vector3 ground = position;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 100f,
                     collisionLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance >= nearestDistance || hit.normal.y < .5f || ShouldIgnore(hit.collider)
                || Bullet.TryGetTarget(hit.collider.transform, out _)
                || hit.collider.GetComponentInParent<Char004ThrowArea>() != null) continue;
            ground = hit.point;
            nearestDistance = hit.distance;
        }
        return ground;
    }

    public static bool HasValidSkillData(SkillData data, Char004ThrowKind throwKind)
    {
        if (throwKind < Char004ThrowKind.Smoke || throwKind > Char004ThrowKind.Molotov) return false;
        int requiredValues = throwKind == Char004ThrowKind.Bottle ? 2 : 3;
        if (data == null || data.charId != 4 || data.funcValue == null || data.funcValue.Length < requiredValues) return false;
        if ((throwKind == Char004ThrowKind.Smoke && data.func != 8)
            || (throwKind == Char004ThrowKind.Bottle && data.func != 9)
            || (throwKind == Char004ThrowKind.Molotov && data.func != 1)) return false;
        if (!IsFinite(data.funcValue[0]) || data.funcValue[0] <= 0f
            || !IsFinite(data.funcValue[1]) || data.funcValue[1] <= 0f) return false;
        return throwKind == Char004ThrowKind.Bottle
            || (IsFinite(data.funcValue[2]) && data.funcValue[2] > 0f);
    }

    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
