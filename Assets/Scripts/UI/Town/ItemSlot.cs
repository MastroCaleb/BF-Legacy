using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour
{
    public int stackKey;
    public string itemId;   // resolved from the stack each UpdateView — kept as a field since other UI code reads it directly (tooltips, click handlers, etc.)
    public ItemData itemData;
    public Button button;
    public Image slotImage;
    public Image itemThumbnail;
    public TextMeshProUGUI countText;

    public Sprite materialSlotSprite;
    public Sprite consumableSlotSprite;
    public Sprite sphereSlotSprite;
    public Sprite lsSphereSlotSprite;
    public Sprite evoMatSlotSprite;
    public Sprite raidMatSlotSprite;
    public Sprite boosterSlotSprite;


    private Image _image;
    private Button _button;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _button = GetComponent<Button>();
    }

    public void SetVisible(bool visible)
    {
        if (_image != null) _image.enabled = visible;
        if (_button != null) _button.enabled = visible;
    }

    public void UpdateView()
    {
        ItemStack stack = PlayerItemInventoryDatabase.GetStack(stackKey);
        if (stack == null)
        {
            // The stack this slot pointed at was fully removed (sold, consumed, etc.).
            gameObject.SetActive(false);
            return;
        }

        itemId = stack.itemId;
        itemData = ItemDatabase.GetItem(itemId);
        if (itemData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (slotImage != null)
            slotImage.sprite = GetSpriteForItemType();

        if (itemThumbnail != null)
            itemThumbnail.sprite = itemData.thumbnailSprite;

        // This stack's count, not the item's total across every stack — use
        // PlayerItemInventoryDatabase.GetItemCount(itemId) instead if a
        // display ever needs the grand total.
        if (countText != null)
            countText.text = "x" + stack.count.ToString();
    }

    public Sprite GetSpriteForItemType()
    {
        return itemData.itemType switch
        {
            ItemType.Material => materialSlotSprite,
            ItemType.Consumable when itemData.raid => raidMatSlotSprite,
            ItemType.Sphere => sphereSlotSprite,
            ItemType.LsSphere => lsSphereSlotSprite,
            ItemType.EvoMat => evoMatSlotSprite,
            ItemType.Consumable => consumableSlotSprite,
            ItemType.Unknown => boosterSlotSprite,
            _ => null
        };
    }

    public void OnClick()
    {
        Debug.Log($"Clicked item: {itemId} (stack {stackKey}, count={PlayerItemInventoryDatabase.GetStack(stackKey)?.count ?? 0})");

        if (ItemInventoryRenderer.selectionMode == ItemInventorySelectionMode.ItemSell)
        {
            // Item sell selection logic can be expanded here as needed —
            // operate on stackKey (this specific stack) via
            // PlayerItemInventoryDatabase.RemoveStack, or on itemId via
            // RemoveItem/SellItems if the intent is "sell N of this item"
            // rather than "sell this stack".
        }
    }
}