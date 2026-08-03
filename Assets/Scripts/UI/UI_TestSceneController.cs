using UnityEngine;
using UnityEngine.UI;
using Scavenger.Core;
using TMPro;

namespace Scavenger.UI
{
    public class UI_TestSceneController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Dropdown mapDropdown; // 나중을 위해 추가

        private void Start()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }
            
            if (mapDropdown != null)
            {
                // 기본 맵 초기화
                mapDropdown.options.Clear();
                mapDropdown.options.Add(new TMP_Dropdown.OptionData("Stage001"));
                // 나중에 맵이 추가되면 여기에 추가할 수 있습니다.
            }
        }

        private void OnStartClicked()
        {
            string selectedMap = "Stage001";
            if (mapDropdown != null && mapDropdown.options.Count > 0)
            {
                selectedMap = mapDropdown.options[mapDropdown.value].text;
            }

            // 맵 이름을 PlayerPrefs에 저장하여 UI_LoadingScreen에서 사용할 수 있도록 함
            PlayerPrefs.SetString("SelectedMap", selectedMap);
            PlayerPrefs.Save();

            Debug.Log($"[TestScene] 싱글플레이어 호스트로 시작합니다. 선택된 맵: {selectedMap}");

            // 인텐트를 StartSingleplayerHost로 지정하고 로딩 씬으로 이동
            SceneTransitionManager.LoadSceneWithIntent(LoadingIntent.StartSingleplayerHost);
        }
    }
}
