using UnityEngine;

namespace Scavenger.UI
{
    public class UI_QuestBoardTab : MonoBehaviour
    {
        [SerializeField] private GameObject selectedBackground;

        public void SetSelected(bool isSelected)
        {
            if (selectedBackground != null)
            {
                selectedBackground.SetActive(isSelected);
            }
        }
    }
}
