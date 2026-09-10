using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 총알을 발사하는 기능을 담당하는 컴포넌트입니다.
/// GunData를 기반으로 쿨타임, 잔탄, 연사 기능을 모두 관리합니다.
/// 멀티플레이어(NGO) 환경에 맞춰 발사와 잔탄 계산은 서버가 전담합니다.
/// </summary>
public class Shooter : NetworkBehaviour
{
    [Header("Shooter Settings")]
    [Tooltip("발사할 총알의 프리팹")]
    [SerializeField] private Bullet bulletPrefab;
    
    [Tooltip("총알이 생성될 위치 (지정하지 않으면 이 게임 오브젝트의 위치를 사용합니다)")]
    [SerializeField] private Transform firePoint;

    [Header("Current Status (Debug)")]
    [SerializeField] private GunData currentGunData;
    public GunData CurrentGunData => currentGunData;
    
    // 잔탄 수는 서버가 관리하며 클라이언트에게 동기화됩니다.
    public NetworkVariable<int> NetworkCurrentAmmo = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public int CurrentAmmo 
    { 
        get => NetworkCurrentAmmo.Value; 
        set 
        { 
            if (IsServer) NetworkCurrentAmmo.Value = value; 
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

    private IShooterAnimationReceiver animReceiver;

    private void Awake()
    {
        animReceiver = GetComponentInParent<IShooterAnimationReceiver>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && currentGunData != null)
        {
            CurrentAmmo = currentGunData.maxAmmo;
        }

        NetworkCurrentAmmo.OnValueChanged += (oldValue, newValue) =>
        {
            if (currentGunData != null)
            {
                OnAmmoChanged?.Invoke(newValue, currentGunData.maxAmmo);
            }
        };
    }

    private void Update()
    {
        if (!IsOwner) return; // 로컬 입력은 소유자(Owner)만 처리

        // 연사 모드일 때, 방아쇠가 당겨져 있다면 계속 발사 시도
        if (isTriggerPulled)
        {
            TryShoot(currentAimDirection, currentTargetPosition);
        }
        else if (currentSpread > 0f)
        {
            float recoveryRate = currentGunData != null ? currentGunData.spreadRecoveryRate : 1f;
            currentSpread -= recoveryRate * Time.deltaTime;
            if (currentSpread < 0f) currentSpread = 0f;
        }
    }

    public void EquipGun(GunData newGunData, int restoredAmmo = -1)
    {
        if (newGunData == null) return;

        ApplyGunData(newGunData, restoredAmmo);

        // 클라이언트에서 호출한 경우 서버에도 장착 사실을 알림
        if (!IsServer && IsOwner)
        {
            CmdEquipGunServerRpc(newGunData.id, restoredAmmo);
        }
    }

    [ServerRpc]
    private void CmdEquipGunServerRpc(int gunId, int restoredAmmo)
    {
        GunData serverGunData = DataManager.Instance.GetData<GunData>("Gun", gunId);
        
        if (serverGunData != null)
        {
            ApplyGunData(serverGunData, restoredAmmo);
        }
    }

    private void ApplyGunData(GunData newGunData, int restoredAmmo = -1)
    {
        currentGunData = newGunData;
        
        if (IsServer) 
        {
            int targetAmmo = restoredAmmo >= 0 ? restoredAmmo : currentGunData.maxAmmo;
            CurrentAmmo = targetAmmo;
        }

        isReloading = false;
        isTriggerPulled = false;
        clientLastFireTime = 0f;
        serverLastFireTime = 0f;
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

    private float clientLastFireTime;
    private float serverLastFireTime;

    /// <summary>
    /// 단발 사격을 시도하거나 수동으로 한 발씩 발사할 때 호출합니다.
    /// </summary>
    public void TryShoot(Vector3 direction, Vector3 targetPosition = default)
    {
        if (!IsOwner) return; // 발사 요청은 소유자(Owner)만 할 수 있습니다.

        if (currentGunData == null) return;
        if (isReloading) return;
        if (CurrentAmmo <= 0) return;
        if (Time.time - clientLastFireTime < currentGunData.fireRate) return;

        // 로컬 쿨타임 갱신 (클라이언트 측에서 쿨타임을 관리하여 연속 요청 방지)
        clientLastFireTime = Time.time;

        // 사격 애니메이션 재생 트리거
        animReceiver?.PlayShootAnimation();

        // 서버에 실제 발사(탄환 생성 및 물리 처리)를 요청
        CmdShootServerRpc(direction, targetPosition);
    }

    [ServerRpc]
    private void CmdShootServerRpc(Vector3 direction, Vector3 targetPosition)
    {
        // 서버 측에서 한 번 더 검증 (해킹 방지)
        if (currentGunData == null || isReloading || CurrentAmmo <= 0 || Time.time - serverLastFireTime < currentGunData.fireRate)
            return;

        PerformShoot(direction, targetPosition);
    }

    /// <summary>
    /// 연사(Full-Auto) 기능을 위해 방아쇠를 당기거나 놓습니다.
    /// PlayerController나 Input 시스템에서 호출하기 좋습니다.
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

            if (currentGunData != null && currentGunData.shotType == 2)
            {
                // 포물선 무기(수류탄 등)는 조준할 시간을 벌어주기 위해 첫 발사에 딜레이(fireRate)를 줍니다.
                // clientLastFireTime을 현재 시간으로 덮어씌워서, 쿨타임이 한 바퀴 돌아야 쏴지도록 속입니다.
                clientLastFireTime = Time.time;
            }
            else
            {
                // 방아쇠를 당기는 즉시 한 발 발사 시도 (직사형 등)
                TryShoot(currentAimDirection, currentTargetPosition);
            }
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
    /// 실제 총알을 생성하고 잔탄/시간 상태를 갱신하는 내부 메서드 (서버에서만 실행됨)
    /// </summary>
    private void PerformShoot(Vector3 direction, Vector3 targetPosition)
    {
        if (!IsServer) return;

        CurrentAmmo--;
        serverLastFireTime = Time.time;

        if (bulletPrefab == null) return;

        direction.Normalize();
        if (direction == Vector3.zero) direction = transform.forward;

        float randomSpread = UnityEngine.Random.Range(-currentSpread * 0.5f, currentSpread * 0.5f);
        direction = Quaternion.Euler(0, randomSpread, 0) * direction;

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
            
            // 네트워크 오브젝트 스폰
            newBullet.GetComponent<NetworkObject>()?.Spawn();

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
            float startAngle = -degree / 2f;
            float angleStep = degree / (shots - 1);

            for (int i = 0; i < shots; i++)
            {
                float currentAngle = startAngle + (angleStep * i);
                Vector3 rotatedDir = Quaternion.Euler(0, currentAngle, 0) * direction;
                
                Quaternion spawnRotation = Quaternion.LookRotation(rotatedDir);
                Bullet newBullet = Instantiate(bulletPrefab, spawnPosition, spawnRotation);
                
                // 네트워크 오브젝트 스폰
                newBullet.GetComponent<NetworkObject>()?.Spawn();

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
        if (!IsOwner) return;
        if (currentGunData == null || isReloading) return;

        CmdReloadServerRpc();
    }

    [ServerRpc]
    private void CmdReloadServerRpc()
    {
        if (currentGunData != null)
        {
            CurrentAmmo = currentGunData.maxAmmo;
        }
    }

    /// <summary>
    /// 지정된 수량만큼 재장전합니다. (샷건 등 1발씩 장전용)
    /// </summary>
    public void ReloadAmount(int amount)
    {
        if (!IsOwner) return;
        if (currentGunData == null || isReloading) return;

        CmdReloadAmountServerRpc(amount);
    }

    [ServerRpc]
    private void CmdReloadAmountServerRpc(int amount)
    {
        CurrentAmmo = Mathf.Min(CurrentAmmo + amount, currentGunData.maxAmmo);
    }

    /// <summary>
    /// 재장전 애니메이션만 강제로 트리거합니다. (SkillInstance 등에서 선입력 재생용)
    /// </summary>
    public void TriggerReloadAnimation()
    {
        animReceiver?.PlayReloadAnimation();
    }
}
