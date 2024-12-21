using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class DialogueWindowPatch
{
  [HarmonyPatch(typeof(DialogueWindow), nameof(DialogueWindow.openTrade))]
  [HarmonyPostfix]
  public static void InventoryBackgrounds(DialogueWindow __instance)
  {
    if (!Plugin.TraderSlots.Value) return;
    PositionMe traderInventory = __instance.npc.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();
    PositionMe exchangeTrader = __instance.exchangeTrader.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();
    PositionMe exchangePlayer = __instance.exchangePlayer.GetComponent<Inventory>().thisUI.GetComponent<PositionMe>();

    var closeButton = __instance.inventoryBackground.Find("Close");
    var closeButtonBackground = __instance.inventoryBackground.Find("Background");

    traderInventory.offset = new Vector2(traderInventory.offset.x + Plugin.TraderXOffset.Value, traderInventory.offset.y + Plugin.TraderZOffset.Value);
    exchangeTrader.offset = new Vector2(exchangeTrader.offset.x + Plugin.TraderXOffset.Value, exchangeTrader.offset.y + Plugin.TraderZOffset.Value);
    exchangePlayer.offset = new Vector2(exchangePlayer.offset.x + Plugin.TraderXOffset.Value, exchangePlayer.offset.y + Plugin.TraderZOffset.Value);
    
    float extraSlots = Plugin.InventoryRightSlots.Value - 2f;
    if (extraSlots > 0)
    {
      PositionMe inventoryBackground = __instance.inventoryBackground.GetComponent<PositionMe>();
      tk2dSprite inventoryBackgroundSprite = __instance.inventoryBackground.GetComponent<tk2dSprite>();
      inventoryBackgroundSprite.scale = new Vector3(2f + 0.65f * extraSlots, inventoryBackgroundSprite.scale.y, inventoryBackgroundSprite.scale.z);
      inventoryBackground.offset.x += 31f * extraSlots;
      inventoryBackground.init();
    }
    
    //diff from 209 2045 660
    //to 440 2075 539
    closeButton.position = new Vector3(__instance.inventoryBackground.position.x * 2.1052631578947368421052631578947f - 31f * extraSlots + Plugin.TraderXOffset.Value, __instance.inventoryBackground.position.y + 30, __instance.inventoryBackground.position.z - 121 + Plugin.TraderZOffset.Value);
    closeButtonBackground.position = new Vector3(__instance.inventoryBackground.position.x * 2.1052631578947368421052631578947f - 31f * extraSlots + Plugin.TraderXOffset.Value, __instance.inventoryBackground.position.y + 30, __instance.inventoryBackground.position.z - 121 + Plugin.TraderZOffset.Value);
    
    traderInventory.init();
    exchangePlayer.init();
    exchangeTrader.init();
  }
}