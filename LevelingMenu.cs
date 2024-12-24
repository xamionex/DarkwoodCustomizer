using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class LevelingMenuPatch
{
  [HarmonyPatch(typeof(LevelingMenu), "positionInventory")]
  [HarmonyPostfix]
  public static void LevelingMenuInventory(LevelingMenu __instance)
  {
    var extraRightInventorySlots = 0;
    var extraDownInventorySlots = 0;
    if (Plugin.InventorySlots.Value)
    {
      extraRightInventorySlots = Plugin.InventoryRightSlots.Value - 2;
      extraDownInventorySlots = Plugin.InventoryDownSlots.Value - 9; // Assuming max upgrades since by default the layout doesn't move depending on upgrades
    }

    // 611 2008.436 680 InventoryBackground
    // 611 2014.436 320 InventoryBackground/Close (diff 2.125 Z)
    var inventoryPanel = __instance.InventoryBackground.GetComponent<PositionMe>();
    var closeButton = __instance.InventoryBackground.Find("Close");
    var inventoryPanelSprite = __instance.InventoryBackground.GetComponent<tk2dSprite>();
    inventoryPanelSprite.scale = new Vector3(2f + 0.65f * extraRightInventorySlots, 2f + 0.65f * extraDownInventorySlots, inventoryPanelSprite.scale.z);
    inventoryPanel.offset.x += 31f * extraRightInventorySlots;
    closeButton.position = new Vector3(closeButton.position.x, closeButton.position.y, __instance.InventoryBackground.position.z / 2.125f - 31f * extraDownInventorySlots);
    inventoryPanel.init();
  }
}