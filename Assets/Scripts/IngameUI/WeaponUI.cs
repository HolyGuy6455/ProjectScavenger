using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("총기와 탄약 정보를 받아올 Shooter 컴포넌트")]
    [SerializeField] private Shooter shooter;
    
    [Tooltip("현재 잔탄 / 최대 잔탄을 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI ammoText;
    
    [Tooltip("재장전 중임을 표시할 아이콘 이미지")]
    [SerializeField] private Image reloadIcon;

    private void OnEnable()
    {
        // 동적 스폰 대응: 로컬 플레이어 찾기
        if (shooter == null && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            if (Unity.Netcode.NetworkManager.Singleton.LocalClient != null && Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                shooter = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Shooter>();
            }
        }

        if (shooter != null)
        {
            shooter.OnAmmoChanged += HandleAmmoChanged;
            shooter.OnWeaponChanged += HandleWeaponChanged;
            
            // 현재 장착중인 무기가 있다면 UI 즉시 갱신
            if (shooter.CurrentGunData != null)
            {
                HandleWeaponChanged(shooter.CurrentGunData);
                HandleAmmoChanged(shooter.CurrentAmmo, shooter.CurrentGunData.maxAmmo);
            }
        }
    }

    private void OnDisable()
    {
        if (shooter != null)
        {
            shooter.OnAmmoChanged -= HandleAmmoChanged;
            shooter.OnWeaponChanged -= HandleWeaponChanged;
        }
    }

    public void SetShooter(Shooter newShooter)
    {
        if (shooter != null)
        {
            shooter.OnAmmoChanged -= HandleAmmoChanged;
            shooter.OnWeaponChanged -= HandleWeaponChanged;
        }

        shooter = newShooter;

        if (shooter != null)
        {
            shooter.OnAmmoChanged += HandleAmmoChanged;
            shooter.OnWeaponChanged += HandleWeaponChanged;
            
            if (shooter.CurrentGunData != null)
            {
                HandleWeaponChanged(shooter.CurrentGunData);
                HandleAmmoChanged(shooter.CurrentAmmo, shooter.CurrentGunData.maxAmmo);
            }
        }
    }

    private void Update()
    {
        if (reloadIcon != null && PlayerSkillController.Instance != null)
        {
            bool isReloading = PlayerSkillController.Instance.IsReloading();
            if (reloadIcon.gameObject.activeSelf != isReloading)
            {
                reloadIcon.gameObject.SetActive(isReloading);
            }
        }
    }

    private void HandleAmmoChanged(int currentAmmo, int maxAmmo)
    {
        if (ammoText != null)
        {
            ammoText.text = $"{currentAmmo} / {maxAmmo}";
        }
    }

    private void HandleWeaponChanged(GunData gunData)
    {
        if (gunData == null) return;
    }
}

