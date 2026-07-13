using UnityEngine;

[System.Serializable]
public class CharacterInventory
{
    public int CharacterId { get; private set; }
    public InventoryItem[] Items { get; private set; }
    public int SelectedSlotIndex { get; set; }

    public CharacterInventory(int characterId, int maxSlots)
    {
        CharacterId = characterId;
        Items = new InventoryItem[maxSlots];
        SelectedSlotIndex = -1;
    }
}
