using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

public static class UpgradeItemMenuPatch
{
  public static Vector2 vanillaValue = new(0, 0);
  public static Vector2 craftingPos = new(0, 0);

  [HarmonyPatch(typeof(UpgradeItemMenu), nameof(UpgradeItemMenu.open))]
  [HarmonyPrefix]
  public static void UpgradeMenuOpened(UpgradeItemMenu __instance, InvItemClass invItemClass)
  {
    PositionMe positionMe = __instance.gameObject.GetComponent<PositionMe>();
    if (vanillaValue.x == 0 && vanillaValue.y == 0)
    {
      vanillaValue.x = positionMe.offset.x;
      vanillaValue.y = positionMe.offset.y;
    }
    if (Plugin.WorkbenchInventoryModification.Value)
    {
      float extraSlots = Plugin.CraftingRightSlots.Value - 5f;
      float xPosition = 119f + 31f * extraSlots;
      positionMe.offset = new Vector2(craftingPos.x + xPosition, craftingPos.y);
    }
    else
    {
      positionMe.offset = new Vector2(vanillaValue.x, vanillaValue.y);
    }
  }
}