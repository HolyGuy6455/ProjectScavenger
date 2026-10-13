using UnityEngine;

/// <summary>
/// Shooter의 현재 잔탄과 탄창 크기를 AIBrain의 숫자 Blackboard 값에 전달합니다.
/// </summary>
[RequireComponent(typeof(Shooter), typeof(AIBrain))]
public class AIBrainShooterLinker : MonoBehaviour
{
    [Header("Blackboard Keys")]
    public string currentAmmoKey = "Ammo/Current";
    public string maxAmmoKey = "Ammo/Max";

    private Shooter _shooter;
    private AIBrain _brain;

    private void Awake()
    {
        _shooter = GetComponent<Shooter>();
        _brain = GetComponent<AIBrain>();
    }

    private void OnEnable()
    {
        if (_shooter == null) return;
        _shooter.OnAmmoChanged += HandleAmmoChanged;
        _shooter.OnWeaponChanged += HandleWeaponChanged;
        UpdateSnapshot();
    }

    private void OnDisable()
    {
        if (_shooter == null) return;
        _shooter.OnAmmoChanged -= HandleAmmoChanged;
        _shooter.OnWeaponChanged -= HandleWeaponChanged;
    }

    private void HandleWeaponChanged(AmmoData ammoData)
    {
        UpdateSnapshot();
    }

    private void UpdateSnapshot()
    {
        int maxAmmo = _shooter.CurrentAmmoData != null ? _shooter.CurrentAmmoData.maxAmmo : 0;
        HandleAmmoChanged(_shooter.CurrentAmmo, maxAmmo);
    }

    private void HandleAmmoChanged(int currentAmmo, int maxAmmo)
    {
        if (_brain == null) return;

        var currentAmmoProperty = _brain.GetVariable<BTSharedNumeric>(currentAmmoKey);
        if (currentAmmoProperty != null) currentAmmoProperty.value = currentAmmo;

        var maxAmmoProperty = _brain.GetVariable<BTSharedNumeric>(maxAmmoKey);
        if (maxAmmoProperty != null) maxAmmoProperty.value = maxAmmo;
    }
}
