using System.Collections.Generic;

namespace Scavenger.Network.DTOs
{
    [System.Serializable]
    public class GameItemDto
    {
        public int itemId;
        public int amount;

        public int ItemId { get => itemId; set => itemId = value; }
        public int Amount { get => amount; set => amount = value; }
    }

    [System.Serializable]
    public class KilledMonsterDto
    {
        public int MonsterId;
        public int Amount;
    }

    [System.Serializable]
    public class GameResultRequest
    {
        public string ResultStatus; // "Clear", "Fail", "Exit"
        public int StageId;
        public List<GameItemDto> CollectedItems;
        public List<KilledMonsterDto> KilledMonsters;
    }
}
