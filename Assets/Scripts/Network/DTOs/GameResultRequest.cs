using System.Collections.Generic;

namespace Scavenger.Network.DTOs
{
    [System.Serializable]
    public class GameItemDto
    {
        public int ItemId;
        public int Amount;
    }

    [System.Serializable]
    public class GameResultRequest
    {
        public string ResultStatus; // "Clear", "Fail", "Exit"
        public List<GameItemDto> CollectedItems;
    }
}
