using System;
using Scavenger.Audio;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 총알을 발사하는 기능을 담당하는 컴포넌트입니다.
/// AmmoData를 기반으로 쿨타임, 잔탄, 연사 기능을 모두 관리합니다.
/// 멀티플레이어(NGO) 환경에 맞춰 발사와 잔탄 계산은 서버가 전담합니다.
/// </summary>
public struct AmmoGroup : INetworkSerializable, IEquatable<AmmoGroup>
{
    public int ammoId;
    public int count;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ammoId);
        serializer.SerializeValue(ref count);
    }

    public bool Equals(AmmoGroup other)
    {
        return ammoId == other.ammoId && count == other.count;
    }
}

public class Shooter : NetworkBehaviour
{
    [Header("Shooter Settings")]
    [Tooltip("발사할 총알의 프리팹")]
    [SerializeField] private Bullet bulletPrefab;
    
    [Tooltip("총알이 생성될 위치 (지정하지 않으면 이 게임 오브젝트의 위치를 사용합니다)")]
    [SerializeField] private Transform firePoint;
    public Transform FirePoint => firePoint != null ? firePoint : transform;
    public BulletTarget Target { get; set; } = BulletTarget.Enemy;

    [Header("Current Status (Debug)")]
    [SerializeField] private AmmoData currentAmmoData;
    public AmmoData CurrentAmmoData => currentAmmoData;
    public int currentShotType;
    
    public NetworkList<AmmoGroup> chamberAmmoQueue = new NetworkList<AmmoGroup>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public int CurrentAmmo 
    { 
        get 
        {
            int total = 0;
            foreach (var group in chamberAmmoQueue) total += group.count;
            return total;
        }
    }
    
    public event Action<int, int> OnAmmoChanged;
    public event Action<AmmoData> OnWeaponChanged;
    
    [SerializeField] private bool isReloading;
    public bool IsReloading => isReloading;

    private float lastFireTime;
    private bool isTriggerPulled;
    private Vector3 currentAimDirection;
    private Vector3 currentTargetPosition;
    private float currentSpread = 0f;

    private IShooterAnimationReceiver animReceiver;
    private PlayerController playerController;

    private void Awake()
    {
        animReceiver = GetComponentInParent<IShooterAnimationReceiver>();
        playerController = GetComponentInParent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && currentAmmoData != null)
        {
            chamberAmmoQueue.Clear();
            InternalLoadAmmo(currentAmmoData.maxAmmo, currentAmmoData.id);
        }

        chamberAmmoQueue.OnListChanged += HandleAmmoListChanged;
        NotifyAmmoChanged();
    }

    public override void OnNetworkDespawn()
    {
        chamberAmmoQueue.OnListChanged -= HandleAmmoListChanged;
        isReloading = false;
        isTriggerPulled = false;
    }

    private void HandleAmmoListChanged(NetworkListEvent<AmmoGroup> changeEvent)
    {
        NotifyAmmoChanged();
    }

    private void NotifyAmmoChanged()
    {
        if (currentAmmoData != null)
            OnAmmoChanged?.Invoke(CurrentAmmo, currentAmmoData.maxAmmo);
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
            float recoveryRate = currentAmmoData != null ? currentAmmoData.spreadRecoveryRate : 1f;
            currentSpread -= recoveryRate * Time.deltaTime;
            if (currentSpread < 0f) currentSpread = 0f;
        }
    }

    public void EquipGun(AmmoData newAmmoData, int restoredAmmo = -1)
    {
        if (newAmmoData == null) return;

        ApplyAmmoData(newAmmoData, restoredAmmo);

        // 클라이언트에서 호출한 경우 서버에도 장착 사실을 알림
        if (!IsServer && IsOwner)
        {
            CmdEquipGunServerRpc(newAmmoData.id, restoredAmmo);
        }
    }

    [ServerRpc]
    private void CmdEquipGunServerRpc(int gunId, int restoredAmmo)
    {
        AmmoData serverAmmoData = DataManager.Instance.GetData<AmmoData>("Ammo", gunId);
        
        if (serverAmmoData != null)
        {
            ApplyAmmoData(serverAmmoData, restoredAmmo);
        }
    }

    private void ApplyAmmoData(AmmoData newAmmoData, int restoredAmmo = -1)
    {
        currentAmmoData = newAmmoData;
        
        if (IsServer) 
        {
            chamberAmmoQueue.Clear();
            int targetAmmo = restoredAmmo >= 0 ? restoredAmmo : currentAmmoData.maxAmmo;
            if (targetAmmo > 0)
            {
                InternalLoadAmmo(targetAmmo, currentAmmoData.id);
            }
        }

        isReloading = false;
        isTriggerPulled = false;
        clientLastFireTime = 0f;
        serverLastFireTime = 0f;
        currentSpread = 0f;
        
        OnWeaponChanged?.Invoke(currentAmmoData);
        NotifyAmmoChanged();
    }

    public void EquipGunWithChamber(AmmoData newAmmoData, AmmoGroup[] savedChamber)
    {
        if (newAmmoData == null) return;

        currentAmmoData = newAmmoData;
        isReloading = false;
        isTriggerPulled = false;
        clientLastFireTime = 0f;
        serverLastFireTime = 0f;
        currentSpread = 0f;
        
        if (!IsServer && IsOwner)
        {
            CmdEquipGunWithChamberServerRpc(newAmmoData.id, savedChamber);
        }
        else if (IsServer)
        {
            chamberAmmoQueue.Clear();
            if (savedChamber != null && savedChamber.Length > 0)
            {
                foreach (var group in savedChamber) chamberAmmoQueue.Add(group);
            }
            else
            {
                InternalLoadAmmo(currentAmmoData.maxAmmo, currentAmmoData.id);
            }
        }

        OnWeaponChanged?.Invoke(currentAmmoData);
        NotifyAmmoChanged();
    }

    [ServerRpc]
    private void CmdEquipGunWithChamberServerRpc(int gunId, AmmoGroup[] savedChamber)
    {
        AmmoData serverAmmoData = DataManager.Instance.GetData<AmmoData>("Ammo", gunId);
        
        if (serverAmmoData != null)
        {
            currentAmmoData = serverAmmoData;
            chamberAmmoQueue.Clear();
            if (savedChamber != null && savedChamber.Length > 0)
            {
                foreach (var group in savedChamber) chamberAmmoQueue.Add(group);
            }
            else
            {
                InternalLoadAmmo(currentAmmoData.maxAmmo, currentAmmoData.id);
            }
            isReloading = false;
            isTriggerPulled = false;
            serverLastFireTime = 0f;
            currentSpread = 0f;
            OnWeaponChanged?.Invoke(currentAmmoData);
            NotifyAmmoChanged();
        }
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
    /// 서버에서는 발사 성공 여부, 클라이언트에서는 발사 요청 수락 여부를 반환합니다.
    /// </summary>
    public bool TryShoot(Vector3 direction, Vector3 targetPosition = default)
    {
        if (!IsSpawned || !IsOwner) return false; // 발사 요청은 소유자(Owner)만 할 수 있습니다.
        if (playerController != null && playerController.IsInputLocked) return false;

        if (currentAmmoData == null || isReloading || CurrentAmmo <= 0) return false;
        if (Time.time - clientLastFireTime < currentAmmoData.fireRate) return false;
        if (IsServer && Time.time - serverLastFireTime < currentAmmoData.fireRate) return false;

        // 로컬 쿨타임 갱신 (클라이언트 측에서 쿨타임을 관리하여 연속 요청 방지)
        clientLastFireTime = Time.time;

        // 사격 애니메이션 재생 트리거
        animReceiver?.PlayShootAnimation();

        // 서버 소유 AI는 즉시 처리하고, 클라이언트 소유자는 서버에 요청한다.
        if (IsServer)
            PerformShoot(direction, targetPosition);
        else
            CmdShootServerRpc(direction, targetPosition);
        return true;
    }

    [ServerRpc]
    private void CmdShootServerRpc(Vector3 direction, Vector3 targetPosition)
    {
        // 연출 시작 직전에 전송된 발사 요청도 서버에서 차단합니다.
        if (playerController != null && playerController.IsInputLocked) return;
        // 서버 측에서 한 번 더 검증 (해킹 방지)
        if (currentAmmoData == null || isReloading || CurrentAmmo <= 0 || Time.time - serverLastFireTime < currentAmmoData.fireRate)
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

            if (currentAmmoData != null && currentShotType == 2)
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
        if (chamberAmmoQueue.Count == 0) return;

        int lastIndex = chamberAmmoQueue.Count - 1;
        var lastGroup = chamberAmmoQueue[lastIndex];
        int ammoId = lastGroup.ammoId;
        
        lastGroup.count--;
        if (lastGroup.count <= 0)
        {
            chamberAmmoQueue.RemoveAt(lastIndex);
        }
        else
        {
            chamberAmmoQueue[lastIndex] = lastGroup;
        }

        serverLastFireTime = Time.time;

        if (bulletPrefab == null) return;

        direction.Normalize();
        if (direction == Vector3.zero) direction = transform.forward;

        AmmoData shotData = DataManager.Instance.GetData<AmmoData>("Ammo", ammoId);
        if (shotData == null) shotData = currentAmmoData;

        float randomSpread = UnityEngine.Random.Range(-currentSpread * 0.5f, currentSpread * 0.5f);
        direction = Quaternion.Euler(0, randomSpread, 0) * direction;

        currentSpread += shotData.spreadPerShot;
        if (currentSpread > shotData.maxSpread)
        {
            currentSpread = shotData.maxSpread;
        }

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        int shots = shotData.bulletsPerShot > 0 ? shotData.bulletsPerShot : 1;
        float degree = shotData.bulletDegree;

        if (shots == 1)
        {
            Quaternion spawnRotation = Quaternion.LookRotation(direction);
            Bullet newBullet = Instantiate(bulletPrefab, spawnPosition, spawnRotation);
            
            // 네트워크 오브젝트 스폰
            newBullet.GetComponent<NetworkObject>()?.Spawn();

            if (currentShotType == 2)
            {
                newBullet.InitializeParabola(targetPosition, gameObject, shotData.ExplosionRadius, Target, shotData);
            }
            else
            {
                newBullet.Initialize(direction, gameObject, Target, shotData);
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

                if (currentShotType == 2)
                {
                    newBullet.InitializeParabola(targetPosition, gameObject, shotData.ExplosionRadius, Target, shotData);
                }
                else
                {
                    newBullet.Initialize(rotatedDir, gameObject, Target, shotData);
                }
            }
        }

        // A shot can create several pellets, but its firing sound is emitted only once.
        // Use the consumed ammo and copy the position before a mine can despawn itself.
        if (shotData.fireSoundKey != SoundKey.None)
            PlayFireSoundClientRpc(shotData.fireSoundKey, spawnPosition);
    }

    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void PlayFireSoundClientRpc(SoundKey key, Vector3 position)
    {
        // The host also hears this through the RPC; other clients must not be filtered by IsOwner.
        SoundManager.PlayAt(key, position);
    }

    /// <summary>
    /// 서버 소유 AI의 재장전 대기를 시작하고, 완료 또는 취소 전까지 사격을 막습니다.
    /// </summary>
    public bool BeginReload()
    {
        if (!IsSpawned || !IsServer || !IsOwner || currentAmmoData == null || isReloading)
            return false;

        SetTrigger(false);
        isReloading = true;
        return true;
    }

    public void CancelReload()
    {
        isReloading = false;
    }

    public void CompleteReload()
    {
        if (!isReloading) return;
        isReloading = false;
        if (IsSpawned && IsServer && IsOwner)
            Reload();
    }

    /// <summary>
    /// 재장전을 수행합니다.
    /// </summary>
    public void Reload(int amount = -1, int ammoId = -1)
    {
        if (!IsOwner) return;
        if (currentAmmoData == null || isReloading) return;

        if (IsServer)
            PerformReload(amount, ammoId);
        else
            CmdReloadServerRpc(amount, ammoId);
    }

    [ServerRpc]
    private void CmdReloadServerRpc(int amount, int ammoId)
    {
        PerformReload(amount, ammoId);
    }

    private void PerformReload(int amount, int ammoId)
    {
        if (!IsServer || currentAmmoData == null) return;

        chamberAmmoQueue.Clear();
        if (ammoId == -1) ammoId = currentAmmoData.id;
        int loadAmount = (amount <= 0) ? currentAmmoData.maxAmmo : amount;
        InternalLoadAmmo(loadAmount, ammoId);
    }

    /// <summary>
    /// 특수 탄환 또는 일반 탄환을 지정된 수량만큼 장전합니다.
    /// </summary>
    public void LoadAmmo(int amount, int ammoId = -1)
    {
        if (!IsOwner) return;
        if (currentAmmoData == null || isReloading) return;

        CmdLoadAmmoServerRpc(amount, ammoId);
    }

    [ServerRpc]
    private void CmdLoadAmmoServerRpc(int amount, int ammoId)
    {
        InternalLoadAmmo(amount, ammoId);
    }

    private void InternalLoadAmmo(int amount, int ammoId)
    {
        if (!IsServer) return;
        if (currentAmmoData == null) return;
        
        if (ammoId == -1) ammoId = currentAmmoData.id;
        
        int currentTotal = CurrentAmmo;
        int spaceLeft = currentAmmoData.maxAmmo - currentTotal;
        if (spaceLeft <= 0) return;
        
        int loadCount = Mathf.Min(amount, spaceLeft);
        if (loadCount <= 0) return;
        
        if (chamberAmmoQueue.Count > 0)
        {
            var lastGroup = chamberAmmoQueue[chamberAmmoQueue.Count - 1];
            if (lastGroup.ammoId == ammoId)
            {
                lastGroup.count += loadCount;
                chamberAmmoQueue[chamberAmmoQueue.Count - 1] = lastGroup;
                return;
            }
        }
        
        chamberAmmoQueue.Add(new AmmoGroup { ammoId = ammoId, count = loadCount });
    }

    /// <summary>
    /// 재장전 애니메이션만 강제로 트리거합니다. (SkillInstance 등에서 선입력 재생용)
    /// </summary>
    public void TriggerReloadAnimation()
    {
        animReceiver?.PlayReloadAnimation();
    }

    public bool TriggerReloadAnimation(float duration, bool firstRound)
    {
        return playerController != null && playerController.PlayReloadAnimation(duration, firstRound);
    }

    public void FinishReloadAnimation() => playerController?.FinishReloadAnimation();
    public void CancelReloadAnimation() => playerController?.CancelReloadAnimation();
}
