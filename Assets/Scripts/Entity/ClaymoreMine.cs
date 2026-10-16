using Unity.Netcode;
using UnityEngine;

/// <summary>서버에서 전방의 적을 감지하고, 설치 방향으로 한 번 사격한 뒤 제거됩니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Shooter))]
public sealed class ClaymoreMine : NetworkBehaviour
{
    private Shooter shooter;
    private float nextScanTime;

    private void Awake() => shooter = GetComponent<Shooter>();

    // 설치한 스킬 레벨에 해당하는 AmmoData로, 네트워크 스폰 직후 호출합니다.
    public void Arm(AmmoData ammo)
    {
        if (!IsServer || !IsSpawned) return;
        if (ammo == null || ammo.SearchRadius <= 0f || ammo.SearchAngle <= 0f)
        {
            Debug.LogError("[ClaymoreMine] Valid AmmoData with a positive detection radius/angle is required.", this);
            return;
        }
        shooter.Target = BulletTarget.Enemy;
        shooter.currentShotType = 0;
        shooter.EquipGun(ammo, 1);
        nextScanTime = 0f;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || Time.time < nextScanTime) return;
        var ammo = shooter.CurrentAmmoData;
        if (ammo == null || shooter.CurrentAmmo == 0) return;
        nextScanTime = Time.time + .1f;

        Vector3 origin = shooter.FirePoint.position;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        foreach (var candidate in Physics.OverlapSphere(origin, ammo.SearchRadius,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!Bullet.TryGetTarget(candidate.transform, out var actor) || actor != BulletTarget.Enemy) continue;
            var health = candidate.GetComponentInParent<Health>();
            if (health == null || health.IsDead || health.CurrentHealth <= 0f) continue;

            Vector3 offset = candidate.bounds.center - origin;
            Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (horizontal.sqrMagnitude > ammo.SearchRadius * ammo.SearchRadius
                || Vector3.Angle(forward, horizontal) > ammo.SearchAngle * .5f) continue;
            if (!HasLineOfSight(origin, offset, health.transform)) continue;

            // 적을 추적 조준하지 않고 설치된 방향의 부채꼴로 파편을 발사합니다.
            if (shooter.TryShoot(forward)) NetworkObject.Despawn();
            return;
        }
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 offset, Transform target)
    {
        foreach (var hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var hitTransform = hit.collider.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(target)) continue;
            // 아군이 앞에 서 있어도 감지와 파편을 막지 않습니다.
            if (Bullet.TryGetTarget(hitTransform, out var actor) && actor == BulletTarget.Player) continue;
            return false;
        }
        return true;
    }
}
