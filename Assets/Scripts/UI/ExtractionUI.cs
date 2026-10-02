using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scavenger.Environment;

namespace Scavenger.UI
{
    public class ExtractionUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject exitProgressBarGroup;
        public Image progressBarFill;
        public TextMeshProUGUI exitText;

        private ExtractionZone currentZone;

        private void Start()
        {
            if (exitProgressBarGroup != null)
            {
                exitProgressBarGroup.SetActive(false);
            }
            
            if (Manager.GameFlowManager.Instance != null)
            {
                Manager.GameFlowManager.Instance.OnExtractionStateChanged += HandleExtractionStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (Manager.GameFlowManager.Instance != null)
            {
                Manager.GameFlowManager.Instance.OnExtractionStateChanged -= HandleExtractionStateChanged;
            }
        }

        private void HandleExtractionStateChanged(ExtractionZone zone, bool isExtracting)
        {
            if (isExtracting)
            {
                currentZone = zone;
            }
            else if (currentZone == zone)
            {
                currentZone = null;
            }
        }

        private void Update()
        {

            if (currentZone != null && currentZone.IsExtracting)
            {
                if (exitProgressBarGroup != null && !exitProgressBarGroup.activeSelf)
                {
                    exitProgressBarGroup.SetActive(true);
                }

                float timer = currentZone.CurrentExtractionTimer;
                float totalTime = currentZone.extractionTime;
                
                if (progressBarFill != null)
                {
                    progressBarFill.fillAmount = timer / totalTime;
                }

                if (exitText != null)
                {
                    float remainingTime = Mathf.Max(0, totalTime - timer);
                    exitText.text = $"탈출 중... {remainingTime:F1}s";
                }
            }
            else
            {
                if (exitProgressBarGroup != null && exitProgressBarGroup.activeSelf)
                {
                    exitProgressBarGroup.SetActive(false);
                }
            }
        }
    }
}
