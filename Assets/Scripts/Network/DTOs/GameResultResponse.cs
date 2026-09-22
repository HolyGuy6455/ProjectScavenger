using System.Collections.Generic;

namespace Scavenger.Network.DTOs
{
    [System.Serializable]
    public class CharExpData
    {
        public int charId;
        public int prevExp;
        public int currentExp;
        public int prevLevel;
        public int currentLevel;

        public int CharId { get => charId; set => charId = value; }
        public int PrevExp { get => prevExp; set => prevExp = value; }
        public int CurrentExp { get => currentExp; set => currentExp = value; }
        public int PrevLevel { get => prevLevel; set => prevLevel = value; }
        public int CurrentLevel { get => currentLevel; set => currentLevel = value; }
    }

    [System.Serializable]
    public class GameResultResponse
    {
        public string message;
        public List<GameItemDto> addedItems;
        
        public int userPrevExp;
        public int userCurrentExp;
        public int userPrevLevel;
        public int userCurrentLevel;

        public List<CharExpData> characterExpData;

        public string Message { get => message; set => message = value; }
        public List<GameItemDto> AddedItems { get => addedItems; set => addedItems = value; }
        public int UserPrevExp { get => userPrevExp; set => userPrevExp = value; }
        public int UserCurrentExp { get => userCurrentExp; set => userCurrentExp = value; }
        public int UserPrevLevel { get => userPrevLevel; set => userPrevLevel = value; }
        public int UserCurrentLevel { get => userCurrentLevel; set => userCurrentLevel = value; }
        public List<CharExpData> CharacterExpData { get => characterExpData; set => characterExpData = value; }
    }
}
