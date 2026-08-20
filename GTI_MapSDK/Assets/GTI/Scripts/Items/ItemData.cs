using UnityEngine;

/// <summary>
/// Defines a single item type. Create one asset per item via the
/// Assets > Create > GTI > Item Data menu. Assign the asset to the
/// Item component on the matching prefab.
///
/// For 50+ items, keep all assets in Assets/Data/Items/ and name them
/// clearly (e.g. "TV_ItemData", "Laptop_ItemData") so the Project window
/// stays navigable.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "GTI/Item Data")]
public class ItemData : ScriptableObject
{
    [Tooltip("Display name shown in the HUD and price canvas.")]
    public string itemName = "Unknown Item";

    [Tooltip("Base sell value in dollars.")]
    public int baseValue = 10;

    [Tooltip("Determines pickup behaviour and slot category.")]
    public ItemSize size = ItemSize.Pocket;

    [Tooltip("Icon shown in the pocket hotbar. Can be left null for carry/haul items.")]
    public Sprite icon;

    [Tooltip("Utility items are never sold at round end and persist across scene transitions. Use for tools the player keeps between heists.")]
    public bool isUtility;

    [Tooltip("Pocket items with this flag are held VISIBLY in front of the player while their hotbar slot is selected, instead of being invisible. Left-click then uses/deploys the item (taser fire, smoke throw, rope/door deploy). Selecting such an item also forces the player to drop any carried or hauled item.")]
    public bool heldInFront;
}