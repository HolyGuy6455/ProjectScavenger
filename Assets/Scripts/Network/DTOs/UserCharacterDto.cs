using System;
using System.Collections.Generic;

namespace Scavenger.Network.DTOs
{
    [Serializable]
    public class UserCharacterDto
    {
        public int charId;
        public int level;
        public int exp;
        public int[] equippedSkills = new int[4];
        public List<int> unlockedSkills = new List<int>();
    }

    [Serializable]
    public class UserCharacterListResponse
    {
        public List<UserCharacterDto> characters = new List<UserCharacterDto>();
        public int[] selectedDeck = new int[4];
    }

    [Serializable]
    public class EquipSkillRequest
    {
        public int charId;
        public int skillId;
        public int slotIndex;
    }

    [Serializable]
    public class EquipSkillResponse
    {
        public string message;
        public int[] equippedSkills;
    }

    [Serializable]
    public class LevelUpSkillRequest
    {
        public int charId;
        public int targetSkillId;
    }

    // 서버 LevelUpSkillErrorCode와 숫자를 맞춰 유지합니다.
    public enum LevelUpSkillErrorCode
    {
        None = 0,
        InvalidToken = 1,
        CharacterNotFound = 2,
        SkillNotFound = 3,
        AlreadyUnlocked = 4,
        InsufficientItems = 5
    }

    [Serializable]
    public class LevelUpSkillResponse
    {
        public LevelUpSkillErrorCode errorCode;
        public string message;
        public List<int> unlockedSkills;
        public List<UserCurrencyDto> updatedGameItems;
        public int[] equippedSkills;
    }

    [Serializable]
    public class LevelUpCharacterRequest
    {
        public int charId;
        public int targetLevel;
    }

    [Serializable]
    public class LevelUpCharacterResponse
    {
        public string message;
        public int level;
        public List<UserCurrencyDto> updatedGameItems;
    }

    [Serializable]
    public class UserCurrencyDto
    {
        public int itemId;
        public int amount;
    }

    [Serializable]
    public class UserCurrencyListResponse
    {
        public List<UserCurrencyDto> userCurrencys;
        public List<UserCurrencyDto> gameItems;
        public int stamina;
        public int maxStamina;
        public int nextStaminaRecoverySeconds;
    }

    [Serializable]
    public class SaveDeckRequest
    {
        public int[] selectedCharIds = new int[4];
    }

    [Serializable]
    public class SaveDeckResponse
    {
        public string message;
        public int[] selectedDeck;
    }
}
