using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public ItemData Data { get; private set; }
    public int Quantity { get; set; }

    public InventoryItem(ItemData data, int quantity = 1)
    {
        Data = data;
        Quantity = quantity;
    }

    public bool IsEmpty => Data == null;
}
