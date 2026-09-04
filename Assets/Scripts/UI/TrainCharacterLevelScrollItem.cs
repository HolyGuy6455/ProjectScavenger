using UnityEngine;
using TMPro;

public class TrainCharacterLevelScrollItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelText;
    
    public int Level { get; private set; }

    public void Setup(LevelData levelData)
    {
        if (levelData != null)
        {
            Level = levelData.level;
            if (levelText != null)
            {
                levelText.text = levelData.level.ToString();
            }
        }
    }
}
