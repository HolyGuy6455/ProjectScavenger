using System;
using System.Collections.Generic;

namespace Scavenger.Network.DTOs
{
    [Serializable]
    public class UserCharacterDto
    {
        public int charId;
        public int level;
        public List<int> unlockedSkills = new List<int>();
    }

    [Serializable]
    public class UserCharacterListResponse
    {
        public List<UserCharacterDto> characters = new List<UserCharacterDto>();
    }
}
