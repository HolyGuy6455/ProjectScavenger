using System;
using System.Collections.Generic;

namespace Scavenger.Network.DTOs
{
    [Serializable]
    public class UserQuestDto
    {
        public int questId;
        public int progressValue;
        public bool isCompleted;
        public bool isRewarded;
    }

    [Serializable]
    public class QuestListResponse
    {
        public List<UserQuestDto> quests;
    }

    [Serializable]
    public class QuestRewardRequest
    {
        public int questId;
    }

    [Serializable]
    public class QuestRewardResponse
    {
        public string message;
        public List<UserCurrencyDto> addedItems;
        public List<UserCurrencyDto> addedGameItems;
    }

    public enum QuestCycleType
    {
        DAILY = 0,
        WEEKLY = 1,
        MAIN = 2
    }
}
