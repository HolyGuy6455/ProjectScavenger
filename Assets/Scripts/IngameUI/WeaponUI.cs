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
    
    [Tooltip("현재 무기의 실루엣을 표시할 이미지")]
    [SerializeField] private Image weaponImage;
    
    [Tooltip("재장전 중임을 표시할 아이콘 이미지")]
    [SerializeField] private Image reloadIcon;

    [Header("Settings")]
    [Tooltip("실루엣 리소스가 들어있는 Resources 하위 폴더 경로")]
    [SerializeField] private string resourceFolderPath = "Silhouettes";

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

        // GunData에서 resourceSilhouette 필드가 삭제되었으므로 이미지 로드 주석 처리
        /*
        if (weaponImage != null && !string.IsNullOrEmpty(gunData.resourceSilhouette))
        {
            // 확장자(.png 등) 제거 후 이름만 추출
            string spriteName = gunData.resourceSilhouette;
            int dotIndex = spriteName.LastIndexOf('.');
            if (dotIndex > 0)
            {
                spriteName = spriteName.Substring(0, dotIndex);
            }

            // Resources 폴더에서 Sprite 로드
            string loadPath = string.IsNullOrEmpty(resourceFolderPath) ? spriteName : $"{resourceFolderPath}/{spriteName}";
            Sprite loadedSprite = Resources.Load<Sprite>(loadPath);
            
            if (loadedSprite != null)
            {
                weaponImage.sprite = loadedSprite;
                weaponImage.enabled = true; // 만약 기존에 꺼져있었다면 켜기
            }
            else
            {
                Debug.LogWarning($"[WeaponUI] '{loadPath}' 경로에서 Sprite를 찾을 수 없습니다.");
            }
        }
        */
    }
}
