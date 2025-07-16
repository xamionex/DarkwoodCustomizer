using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class DialogueWindowPatch
{
  private static Vector3? _cachedCloseButtonOriginal;
  private static Vector3? _cachedCloseButtonBackgroundOriginal;
  
  [HarmonyPatch(typeof(DialogueWindow), nameof(DialogueWindow.openTrade))]
  [HarmonyPostfix]
  public static void InventoryBackgrounds(DialogueWindow __instance)
  {
    var traderInventory = __instance.npc.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();
    var exchangeTrader = __instance.exchangeTrader.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();
    var exchangePlayer = __instance.exchangePlayer.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();

    var closeButton = __instance.inventoryBackground.Find("Close");
    var closeButtonBackground = __instance.inventoryBackground.Find("Background");
    // Cache original positions if not already cached
    if (!_cachedCloseButtonOriginal.HasValue)
    {
      _cachedCloseButtonOriginal = closeButton.position;
      _cachedCloseButtonBackgroundOriginal = closeButtonBackground.position;
    }
    
    if (Plugin.TraderSlots.Value)
    {
      traderInventory.offset = new Vector2(traderInventory.offset.x + Plugin.TraderInventoryXOffset.Value, traderInventory.offset.y + Plugin.TraderInventoryZOffset.Value);
      exchangeTrader.offset = new Vector2(exchangeTrader.offset.x + Plugin.TraderBuyXOffset.Value, exchangeTrader.offset.y + Plugin.TraderBuyZOffset.Value);
      exchangePlayer.offset = new Vector2(exchangePlayer.offset.x + Plugin.TraderSellXOffset.Value, exchangePlayer.offset.y + Plugin.TraderSellZOffset.Value);
    }
    
    var extraSlots = Plugin.InventoryRightSlots.Value - 2f;
    var extraSlotsOffset = 0f;
    if (extraSlots > 0)
    {
      var inventoryBackground = __instance.inventoryBackground.GetComponent<PositionMe>();
      var inventoryBackgroundSprite = __instance.inventoryBackground.GetComponent<tk2dSprite>();
      inventoryBackgroundSprite.scale = new Vector3(2f + 0.65f * extraSlots, inventoryBackgroundSprite.scale.y, inventoryBackgroundSprite.scale.z);
      extraSlotsOffset = 31f * extraSlots;
      inventoryBackground.offset.x += extraSlotsOffset;
      traderInventory.offset.x += extraSlotsOffset;
      exchangeTrader.offset.x += extraSlotsOffset;
      exchangePlayer.offset.x += extraSlotsOffset;
      inventoryBackground.init();
    }
    
    // we want to have the accept button and close button at the same spots, why is it separated like this, and why is AcceptTrade in "trader" ;-;
    // - 50 because it should be below accept and bg is -1 on y pos
    closeButton.position = new Vector3(
      _cachedCloseButtonOriginal.Value.x + extraSlotsOffset + Plugin.TraderCloseXOffset.Value,
      _cachedCloseButtonOriginal.Value.y,
      _cachedCloseButtonOriginal.Value.z + Plugin.TraderCloseZOffset.Value
    );
        
    closeButtonBackground.position = new Vector3(
      _cachedCloseButtonBackgroundOriginal.Value.x + extraSlotsOffset + Plugin.TraderCloseXOffset.Value,
      _cachedCloseButtonBackgroundOriginal.Value.y,
      _cachedCloseButtonBackgroundOriginal.Value.z + Plugin.TraderCloseZOffset.Value
    );
    
    traderInventory.init();
    exchangePlayer.init();
    exchangeTrader.init();
  }
}