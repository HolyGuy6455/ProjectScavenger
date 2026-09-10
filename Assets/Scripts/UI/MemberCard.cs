using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class MemberCard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image[] deckCharacterImages; // Size 4
        [SerializeField] private GameObject readyStampImage;

        /// <summary>
        /// 4명의 캐릭터 덱 정보를 받아서 resourceSlot 이미지를 표시합니다.
        /// 빈 슬롯(0 이하)인 경우 이미지를 비활성화합니다.
        /// </summary>
        public void SetDeck(int[] charIds)
        {
            if (deckCharacterImages == null || charIds == null) return;

            for (int i = 0; i < 4; i++)
            {
                if (deckCharacterImages.Length > i && deckCharacterImages[i] != null)
                {
                    int charId = charIds.Length > i ? charIds[i] : 0;
                    if (charId > 0 && DataManager.Instance != null)
                    {
                        var charData = DataManager.Instance.GetData<CharData>("Char", charId);
                        if (charData != null && !string.IsNullOrEmpty(charData.resourceSlot))
                        {
                            string spritePath = charData.resourceSlot.Replace(".png", "");
                            if (!spritePath.StartsWith("char/")) spritePath = "char/" + spritePath;
                            
                            Sprite icon = Resources.Load<Sprite>(spritePath);
                            deckCharacterImages[i].sprite = icon;
                            deckCharacterImages[i].enabled = icon != null;
                        }
                        else
                        {
                            deckCharacterImages[i].enabled = false;
                        }
                    }
                    else
                    {
                        deckCharacterImages[i].enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// 파티 레디 상태에 따라 레디 도장(또는 텍스트) 이미지를 켜고 끕니다.
        /// </summary>
        public void SetReady(bool isReady)
        {
            if (readyStampImage != null)
            {
                readyStampImage.SetActive(isReady);
            }
        }
    }
}
