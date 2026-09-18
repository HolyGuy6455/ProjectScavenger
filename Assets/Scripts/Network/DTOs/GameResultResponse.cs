using System.Collections.Generic;

namespace Scavenger.Network.DTOs
{
    [System.Serializable]
    public class CharExpData
    {
        public int CharId;
        public int PrevExp;
        public int CurrentExp;
        public int PrevLevel;
        public int CurrentLevel;
    }

    [System.Serializable]
    public class GameResultResponse
    {
        public string Message;
        public List<GameItemDto> AddedItems;
        
        public int UserPrevExp;
        public int UserCurrentExp;
        public int UserPrevLevel;
        public int UserCurrentLevel;

        public List<CharExpData> CharacterExpData;
    }
}
