using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class DialogueWindowPatch
{
  [HarmonyPatch(typeof(DialogueWindow), nameof(DialogueWindow.openTrade))]
  [HarmonyPostfix]
  public static void InventoryBackgrounds(DialogueWindow instance)
  {
    var traderInventory = instance.npc.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();
    var exchangeTrader = instance.exchangeTrader.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();
    var exchangePlayer = instance.exchangePlayer.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();

    var closeButton = instance.inventoryBackground.Find("Close");
    var closeButtonBackground = instance.inventoryBackground.Find("Background");
    var acceptButton = instance.exchangeTrader.transform.Find("AcceptTradeBtn");

    var closeOffset = new Vector2(0, 0);
    if (Plugin.TraderSlots.Value)
    {
      traderInventory.offset = new Vector2(traderInventory.offset.x + Plugin.TraderInventoryXOffset.Value, traderInventory.offset.y + Plugin.TraderInventoryZOffset.Value);
      exchangeTrader.offset = new Vector2(exchangeTrader.offset.x + Plugin.TraderBuyXOffset.Value, exchangeTrader.offset.y + Plugin.TraderBuyZOffset.Value);
      exchangePlayer.offset = new Vector2(exchangePlayer.offset.x + Plugin.TraderSellXOffset.Value, exchangePlayer.offset.y + Plugin.TraderSellZOffset.Value);
      closeOffset = new Vector2(Plugin.TraderCloseXOffset.Value, Plugin.TraderCloseZOffset.Value);
    }
    
    var extraSlots = Plugin.InventoryRightSlots.Value - 2f;
    if (extraSlots > 0)
    {
      var inventoryBackground = instance.inventoryBackground.GetComponent<PositionMe>();
      var inventoryBackgroundSprite = instance.inventoryBackground.GetComponent<tk2dSprite>();
      inventoryBackgroundSprite.scale = new Vector3(2f + 0.65f * extraSlots, inventoryBackgroundSprite.scale.y, inventoryBackgroundSprite.scale.z);
      inventoryBackground.offset.x += 31f * extraSlots;
      traderInventory.offset.x += 31f * extraSlots;
      exchangeTrader.offset.x += 31f * extraSlots;
      exchangePlayer.offset.x += 31f * extraSlots;
      inventoryBackground.init();
    }
    
    // we want to have the accept button and close button at the same spots, why is it separated like this, and why is AcceptTrade in "trader" ;-;
    // - 50 because it should be below accept and bg is -1 on y pos
    closeButton.position = new Vector3(acceptButton.position.x + closeOffset.x, acceptButton.position.y, acceptButton.position.z - 50 + closeOffset.y);
    closeButtonBackground.position = new Vector3(acceptButton.position.x + closeOffset.x, acceptButton.position.y - 1, acceptButton.position.z - 50 + closeOffset.y);
    
    traderInventory.init();
    exchangePlayer.init();
    exchangeTrader.init();
  }
}