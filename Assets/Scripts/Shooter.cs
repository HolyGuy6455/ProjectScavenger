using System;
using UnityEngine;

/// <summary>
/// 총기 발사 결과 상태를 나타내는 열거형
/// </summary>
public enum FireResult
{
    Success,        // 발사 성공
    NoAmmo,         // 잔탄 없음
    OnCooldown,     // 연사 쿨타임 중
    Reloading       // 재장전 중
}

/// <summary>
/// 총알을 발사하는 기능을 담당하는 컴포넌트입니다.
/// GunData를 기반으로 쿨타임, 잔탄, 연사 기능을 모두 관리합니다.
/// </summary>
public class Shooter : MonoBehaviour
{
    [Header("Shooter Settings")]
    [Tooltip("발사할 총알의 프리팹")]
    [SerializeField] private Bullet bulletPrefab;
    
    [Tooltip("총알이 생성될 위치 (지정하지 않으면 이 게임 오브젝트의 위치를 사용합니다)")]
    [SerializeField] private Transform firePoint;

    [Header("Current Status (Debug)")]
    [SerializeField] private GunData currentGunData;
    public GunData CurrentGunData => currentGunData;
    
    [SerializeField] private int currentAmmo;
    public int CurrentAmmo 
    { 
        get => currentAmmo; 
        set 
        { 
            currentAmmo = value; 
            if (currentGunData != null)
            {
                OnAmmoChanged?.Invoke(currentAmmo, currentGunData.maxAmmo);
            }
        } 
    }
    
    public event Action<int, int> OnAmmoChanged;
    public event Action<GunData> OnWeaponChanged;
    
    [SerializeField] private bool isReloading;

    private float lastFireTime;
    private bool isTriggerPulled;
    private Vector3 currentAimDirection;
    private Vector3 currentTargetPosition;
    private float currentSpread = 0f;

    private void Update()
    {
        // 연사 모드일 때, 방아쇠가 당겨져 있다면 계속 발사 시도
        if (isTriggerPulled)
        {
            FireResult fireResult = TryShoot(currentAimDirection, currentTargetPosition);
            if (fireResult == FireResult.OnCooldown) {
                float remainingTime = lastFireTime + currentGunData.fireRate - Time.time;
                Debug.Log("발사 실패: 쿨타임중. 남은시간 : " + remainingTime);
            }
            else if (fireResult == FireResult.NoAmmo) {
                Debug.Log("발사 실패: 탄창이 비었습니다.");
            }
        }
        else if (currentSpread > 0f)
        {
            float recoveryRate = currentGunData != null ? currentGunData.spreadRecoveryRate : 1f;
            currentSpread -= recoveryRate * Time.deltaTime;
            if (currentSpread < 0f) currentSpread = 0f;
        }
    }

    public void EquipGun(GunData newGunData)
    {
        currentGunData = newGunData;
        CurrentAmmo = currentGunData != null ? currentGunData.maxAmmo : 0;
        isReloading = false;
        isTriggerPulled = false;
        lastFireTime = 0f;
        currentSpread = 0f;
        
        OnWeaponChanged?.Invoke(currentGunData);
    }

    /*
    /// <summary>
    /// 인벤토리에서 탄창을 선택했을 때 장착합니다.
    /// </summary>
    public void EquipMagazine(InventoryItem magazine, int slotIndex)
    {
        // currentMagazine = magazine;
        // magazineSlotIndex = slotIndex;
        // Debug.Log($"{gameObject.name}: 탄창 장착됨. 잔탄: {currentMagazine.Quantity}");
    }

    /// <summary>
    /// 탄창 장착을 해제합니다.
    /// </summary>
    public void UnequipMagazine()
    {
        // currentMagazine = null;
        // magazineSlotIndex = -1;
        // Debug.Log($"{gameObject.name}: 탄창 장착 해제됨.");
    }
    */

    /// <summary>
    /// 단발 사격을 시도하거나 수동으로 한 발씩 발사할 때 호출합니다.
    /// </summary>
    /// <param name="direction">총알이 날아갈 방향</param>
    /// <returns>발사 결과 상태 (UI나 사운드 재생 시 활용)</returns>
    public FireResult TryShoot(Vector3 direction, Vector3 targetPosition = default)
    {
        if (currentGunData == null)
        {
            Debug.LogWarning("무기 데이터(GunData)가 할당되지 않았습니다. EquipGun()을 먼저 호출해주세요.");
            return FireResult.NoAmmo;
        }

        if (isReloading) return FireResult.Reloading;
        if (currentAmmo <= 0) return FireResult.NoAmmo;

        if (Time.time - lastFireTime < currentGunData.fireRate) return FireResult.OnCooldown;

        // 조건이 모두 맞으면 실제 발사 수행
        PerformShoot(direction, targetPosition);
        return FireResult.Success;
    }

    /// <summary>
    /// 연사(Full-Auto) 기능을 위해 방아쇠를 당기거나 놓습니다.
    /// PlayerMovement나 Input 시스템에서 호출하기 좋습니다.
    /// </summary>
    /// <param name="isPulled">true면 방아쇠 당김(발사 시작), false면 방아쇠 놓음(발사 중지)</param>
    /// <param name="direction">조준하는 방향</param>
    public void SetTrigger(bool isPulled, Vector3 direction = default, Vector3 targetPosition = default)
    {
        isTriggerPulled = isPulled;
        if (isPulled)
        {
            currentAimDirection = direction;
            currentTargetPosition = targetPosition;
            // 방아쇠를 당기는 즉시 한 발 발사 시도
            TryShoot(currentAimDirection, currentTargetPosition);
        }
    }

    /// <summary>
    /// 연사 중 마우스 커서나 조이스틱 방향이 바뀔 때 조준 방향을 갱신해줍니다.
    /// </summary>
    public void UpdateAimDirection(Vector3 direction, Vector3 targetPosition = default)
    {
        currentAimDirection = direction;
        currentTargetPosition = targetPosition;
    }

    /// <summary>
    /// 실제 총알을 생성하고 잔탄/시간 상태를 갱신하는 내부 메서드
    /// </summary>
    private void PerformShoot(Vector3 direction, Vector3 targetPosition)
    {
        CurrentAmmo--;
        
        lastFireTime = Time.time;

        if (bulletPrefab == null)
        {
            Debug.LogWarning($"{gameObject.name}의 Shooter에 Bullet Prefab이 할당되지 않았습니다!");
            return;
        }

        direction.Normalize();
        if (direction == Vector3.zero) direction = transform.forward;

        // 탄퍼짐 적용 (현재 currentSpread를 기반으로 랜덤 각도 추가)
        float randomSpread = UnityEngine.Random.Range(-currentSpread * 0.5f, currentSpread * 0.5f);
        direction = Quaternion.Euler(0, randomSpread, 0) * direction;

        // 발사 후 탄퍼짐 증가
        currentSpread += currentGunData.spreadPerShot;
        if (currentSpread > currentGunData.maxSpread)
        {
            currentSpread = currentGunData.maxSpread;
        }

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        
        int shots = currentGunData.bulletsPerShot > 0 ? currentGunData.bulletsPerShot : 1;
        float degree = currentGunData.bulletDegree;

        if (shots == 1)
        {
            Quaternion spawnRotation = Quaternion.LookRotation(direction);
            Bullet newBullet = Instantiate(bulletPrefab, spawnPosition, spawnRotation);
            
            if (currentGunData.shotType == 2)
            {
                newBullet.InitializeParabola(targetPosition, gameObject, currentGunData.ExplosionRadius);
            }
            else
            {
                newBullet.Initialize(direction, gameObject);
            }
        }
        else
        {
            // 여러 발 발사 (산탄총)
            float startAngle = -degree / 2f;
            float angleStep = degree / (shots - 1);

            for (int i = 0; i < shots; i++)
            {
                float currentAngle = startAngle + (angleStep * i);
                
                // 기준 방향을 Y축 기준으로 회전시킵니다.
                Vector3 rotatedDir = Quaternion.Euler(0, currentAngle, 0) * direction;
                
                Quaternion spawnRotation = Quaternion.LookRotation(rotatedDir);
                Bullet newBullet = Instantiate(bulletPrefab, spawnPosition, spawnRotation);
                
                if (currentGunData.shotType == 2)
                {
                    newBullet.InitializeParabola(targetPosition, gameObject, currentGunData.ExplosionRadius);
                }
                else
                {
                    newBullet.Initialize(rotatedDir, gameObject);
                }
            }
        }
    }

    /// <summary>
    /// 재장전을 수행합니다.
    /// </summary>
    public void Reload()
    {
        if (currentGunData == null || isReloading) return;
        
        CurrentAmmo = currentGunData.maxAmmo;
        Debug.Log($"{gameObject.name}: 재장전 완료. 잔탄: {currentAmmo}");
    }

    /// <summary>
    /// 지정된 수량만큼 재장전합니다. (샷건 등 1발씩 장전용)
    /// </summary>
    public void ReloadAmount(int amount)
    {
        if (currentGunData == null || isReloading) return;
        
        CurrentAmmo = Mathf.Min(CurrentAmmo + amount, currentGunData.maxAmmo);
        Debug.Log($"{gameObject.name}: {amount}발 재장전 완료. 잔탄: {currentAmmo}");
    }
}
