using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Scavenger.UI
{
    public class StageClearCharNode : MonoBehaviour
    {
        [Header("Backgrounds")]
        [SerializeField] private GameObject backgroundNormal;
        [SerializeField] private GameObject backgroundLevelUp;

        [Header("Avatar & Tag")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private GameObject levelUpTag;

        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI charNameText;
        [SerializeField] private TextMeshProUGUI charLevelText;
        [SerializeField] private TextMeshProUGUI charLevelUpText; // 레벨업 시 텍스트 변경용 (예: "LV.24 -> 25")
        [SerializeField] private TextMeshProUGUI expText;

        [Header("Exp Bar")]
        [SerializeField] private Image expBarFill;

        public void Setup(CharData charData, int oldLevel, int newLevel, int oldExp, int newExp, int maxExpForOldLevel, int maxExpForNewLevel, int addedExp)
        {
            if (charData == null) return;

            bool isLevelUp = newLevel > oldLevel;

            // 배경 및 태그 초기화
            if (backgroundNormal != null) backgroundNormal.SetActive(!isLevelUp);
            if (backgroundLevelUp != null) backgroundLevelUp.SetActive(isLevelUp);
            if (levelUpTag != null) levelUpTag.SetActive(isLevelUp);

            // 캐릭터 이름 및 초상화 설정
            if (charNameText != null)
            {
                // 실제 이름 매핑
                charNameText.text = charData.name; 
            }

            if (avatarImage != null)
            {
                string standPath = System.IO.Path.GetFileNameWithoutExtension(charData.resourceSlot);
                Sprite loadedSprite = Resources.Load<Sprite>($"char/{standPath}");
                if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>(standPath);
                
                if (loadedSprite != null)
                {
                    avatarImage.sprite = loadedSprite;
                    avatarImage.enabled = true;
                }
            }

            // 획득 경험치 텍스트 설정 (초기화)
            if (expText != null) expText.text = "+0 EXP";

            // 레벨업 텍스트 설정
            if (isLevelUp)
            {
                if (charLevelText != null) charLevelText.gameObject.SetActive(false);
                if (charLevelUpText != null)
                {
                    charLevelUpText.gameObject.SetActive(true);
                    charLevelUpText.text = $"LV.{oldLevel} <color=#FFD700>-> {newLevel}</color>";
                }
            }
            else
            {
                if (charLevelText != null)
                {
                    charLevelText.gameObject.SetActive(true);
                    charLevelText.text = $"LV.{oldLevel}";
                }
                if (charLevelUpText != null) charLevelUpText.gameObject.SetActive(false);
            }

            // 경험치 바 채우기 애니메이션 시작
            StartCoroutine(ExpBarAnimation(oldLevel, newLevel, oldExp, newExp, maxExpForOldLevel, maxExpForNewLevel, addedExp));
        }

        private IEnumerator ExpBarAnimation(int oldLevel, int newLevel, int oldExp, int newExp, int maxExpForOldLevel, int maxExpForNewLevel, int addedExp)
        {
            if (expBarFill == null) yield break;

            float duration = 1.0f; // 애니메이션 총 지속 시간
            float time = 0f;
            
            bool isLevelUp = newLevel > oldLevel;

            if (isLevelUp)
            {
                // 이전 레벨의 경험치 바를 끝까지 채움
                float startFill = (float)oldExp / maxExpForOldLevel;
                float halfDuration = duration / 2f;
                
                while (time < halfDuration)
                {
                    time += Time.deltaTime;
                    float progress = time / halfDuration;
                    expBarFill.fillAmount = Mathf.Lerp(startFill, 1f, progress);
                    if (expText != null) expText.text = $"+{Mathf.RoundToInt(Mathf.Lerp(0, addedExp / 2f, progress))} EXP";
                    yield return null;
                }

                // 새로운 레벨의 목표 경험치까지 채움
                time = 0f;
                float targetFill = (float)newExp / maxExpForNewLevel;

                while (time < halfDuration)
                {
                    time += Time.deltaTime;
                    float progress = time / halfDuration;
                    expBarFill.fillAmount = Mathf.Lerp(0f, targetFill, progress);
                    if (expText != null) expText.text = $"+{Mathf.RoundToInt(Mathf.Lerp(addedExp / 2f, addedExp, progress))} EXP";
                    yield return null;
                }
                
                expBarFill.fillAmount = targetFill;
                if (expText != null) expText.text = $"+{addedExp} EXP";
            }
            else
            {
                // 레벨업이 없는 경우
                float startFill = (float)oldExp / maxExpForOldLevel;
                float targetFill = (float)newExp / maxExpForNewLevel;

                while (time < duration)
                {
                    time += Time.deltaTime;
                    float progress = time / duration;
                    expBarFill.fillAmount = Mathf.Lerp(startFill, targetFill, progress);
                    if (expText != null) expText.text = $"+{Mathf.RoundToInt(Mathf.Lerp(0, addedExp, progress))} EXP";
                    yield return null;
                }
                
                expBarFill.fillAmount = targetFill;
                if (expText != null) expText.text = $"+{addedExp} EXP";
            }
        }
    }
}
