using UnityEngine;
using TMPro;

namespace Scavenger.UI
{
    public class UserCurrencyBadge : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("스테미너용 뱃지라면 체크하세요.")]
        [SerializeField] private bool isStaminaBadge = false;
        
        [Tooltip("스테미너가 아니라면 표시할 재화 ID를 입력하세요. (예: 1=골드, 2=다이아)")]
        [SerializeField] private int currencyId = 1;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI amountText;

        private void Awake()
        {
            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnCurrencyDataUpdated += SyncValue;
            }
        }

        private void Start()
        {
            SyncValue(); // 초기값 세팅
        }

        private void OnDestroy()
        {
            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnCurrencyDataUpdated -= SyncValue;
            }
        }

        private int _lastDisplayedStamina = -1;

        private void Update()
        {
            if (isStaminaBadge && UserDataManager.Instance != null)
            {
                int current = UserDataManager.Instance.GetCurrentStamina();
                if (current != _lastDisplayedStamina)
                {
                    _lastDisplayedStamina = current;
                    SyncValue();
                }
            }
        }

        public void SyncValue()
        {
            if (amountText == null) return;
            
            if (UserDataManager.Instance == null)
            {
                amountText.text = "0";
                return;
            }

            if (isStaminaBadge)
            {
                int current = UserDataManager.Instance.GetCurrentStamina();
                int max = UserDataManager.Instance.GetMaxStamina();
                amountText.text = $"{current}/{max}";
            }
            else
            {
                int amount = UserDataManager.Instance.GetUserCurrencyAmount(currencyId);
                amountText.text = amount.ToString("N0"); // 천단위 콤마
            }
        }
    }
}
